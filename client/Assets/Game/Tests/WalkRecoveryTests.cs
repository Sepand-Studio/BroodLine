using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Broodline.Api;
using Broodline.Game.Shell;
using Broodline.Model;
using Broodline.Net;
using Broodline.UI;
using Broodline.UI.Screens;
using Broodline.UI.Shell;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Broodline.Game.Tests
{
    /// **No exit from the walk may leave the player with no live control.**
    ///
    /// THE DEFECT THIS SUITE WATCHES, in the words of the device walk that
    /// found it: the deploy screen came back with "a Start button that no
    /// longer resumed anything", and relaunching was the only way out.
    /// `FtueDirector`'s walk ended on a bare `return` from any of twenty-odd
    /// places, `ScreenFlow` presents with `after: null` so the screen that
    /// was up stayed up, and `BootController` caught nothing and did nothing.
    /// The sentence explaining it went to a toast that holds for four
    /// seconds.
    ///
    /// EVERY CASE BELOW WOULD HAVE FAILED BEFORE THE FIX, AND FAILED IN THE
    /// SAME TWO WAYS: `Running.IsCompleted` was true (the walk was over) and
    /// `Presented` was either the stranded screen or nothing at all. Neither
    /// is a threshold, so there is nothing here to be generous with - see the
    /// task report for what the suite printed on the unfixed code.
    ///
    /// WHAT MADE THIS SUITE POSSIBLE, since `FtueDirectorTests`' class
    /// comment says the walk cannot be driven from EditMode and was right
    /// about its own contents:
    ///
    ///   1. **The director's dependencies are all seams already.**
    ///      `BroodlineApiClient` takes a plain `HttpClient`
    ///      (`OutboxClientTests`' pattern), `ScreenHost` takes bare
    ///      `VisualElement`s (`ScreenHostTests`' pattern), and the played
    ///      wave is a delegate precisely so a test can supply one. Nothing
    ///      test-only was added to production code to make this run.
    ///   2. **A click needs no `Panel` after all.** `ScaffoldTests
    ///      .RaiseClicked` reaches `Clickable`'s `clicked` backing field by
    ///      reflection, which runs exactly the subscriber list the screen
    ///      registered. `RaiseClicked` below is that helper, in this
    ///      assembly because the other one is private to a suite in
    ///      `Broodline.UI.Tests`. This is what reaches `FightAsync`'s exits,
    ///      which all lie past the deploy screen's Start button.
    ///   3. **`EditModeRunner` suppresses the synchronization context** for
    ///      the whole case (its own "THE FIX"), so a continuation resumed by
    ///      `TaskCompletionSource.TrySetResult` runs INLINE on this thread
    ///      rather than queueing for a main thread that is not pumping.
    ///      Everything below therefore completes synchronously inside the
    ///      call that provoked it, and `RunAsync()` is never awaited - it is
    ///      started and then INSPECTED, because a walk that is still running
    ///      is the pass condition.
    ///
    /// THE ONE THING THAT WOULD BREAK IT, said so nobody debugs it twice: a
    /// real `Task.Delay`. `Retry`'s backoff resumes on a pool thread, and the
    /// director touches `VisualElement`s after the await, so a case that
    /// provoked a RETRIED failure would throw a main-thread error instead of
    /// asserting. Every stubbed failure below is therefore one
    /// `Retry.IsTransient` answers false to, or on a call that is not
    /// retried at all (`/v1/roster` is not).
    ///
    /// WHAT IS NOT REACHED FROM HERE is listed in the task report rather than
    /// implied by silence: `ResyncAsync`'s exit and `CampaignAsync`'s three
    /// post-pick exits need a completed beat or a pick on a list of option
    /// rows, and `SpliceAsync`'s screen-side exits need beat 6's chamber
    /// answered. They are the same exit CLASS as cases covered here - a
    /// `false` returned into the walk - and the fix does not read the class.
    public class WalkRecoveryTests
    {
        string _outboxPath;

        /// Every walk this case started, so `TearDown` can OBSERVE them.
        readonly List<Walk> _started = new List<Walk>();

        [SetUp]
        public void SetUp()
        {
            _started.Clear();
            _outboxPath = Path.Combine(Path.GetTempPath(), "walk-recovery-" + Guid.NewGuid().ToString("N") + ".bin");
        }

        [TearDown]
        public void TearDown()
        {
            // READING `Exception` IS WHAT OBSERVES THE TASK, and an unobserved
            // one is why this loop exists. `RunAsync()` is started and never
            // awaited here - a walk that is still running is the pass
            // condition - so a faulted one would otherwise sit until
            // finalization and surface as a `TaskScheduler
            // .UnobservedTaskException` inside whatever unrelated case
            // happened to be running then. Pristine output is a gate in this
            // project, and a failure attributed to the wrong test is worse
            // than a loud one attributed to the right test.
            //
            // ASSERTED RATHER THAN SWALLOWED. Nothing in these cases can
            // legitimately fault: `RunAsync` catches everything `WalkAsync`
            // throws, and only `AnotherTryAsync` can throw past it. So a
            // fault here is a real defect and says so.
            foreach (var walk in _started)
            {
                var fault = walk.Running == null ? null : walk.Running.Exception;
                Assert.IsNull(fault, "the walk faulted out of RunAsync, which nothing is supposed to be able to do: " + fault);
            }

            if (File.Exists(_outboxPath)) File.Delete(_outboxPath);
            var temp = _outboxPath + ".tmp";
            if (File.Exists(temp)) File.Delete(temp);
        }

        // ---------------------------------------------------------------
        // The exits that never get as far as a screen
        // ---------------------------------------------------------------

        [Test]
        public void AColdStartThatProducedNoSnapshot_LeavesALiveControlOnScreen()
        {
            // `WalkAsync`'s first exit, and the only one a player can reach
            // before anything at all has been drawn. It used to leave the
            // shell empty: no screen, no button, one toast.
            var walk = Run(new Walk { Snapshot = null });

            Assert.IsFalse(walk.Running.IsCompleted,
                "the walk finished - which is the defect: nothing is going to put a screen up now");
            Assert.IsNotNull(walk.Interrupted,
                "the walk stopped without presenting anything with a control on it");

            // WIRED, NOT MERELY ENABLED. This line used to read
            // `Assert.IsTrue(walk.Retry.enabledSelf)`, which nothing in
            // `InterruptedView` can make false - a could-not-fail assertion,
            // which is a repeat failure class in this phase. `Subscribed`
            // reads the handler list off the button's `Clickable`, so
            // deleting the `clicked +=` in that screen's constructor reddens
            // this and every case that follows it.
            Assert.IsNotNull(Subscribed(walk.Retry),
                "the recovery screen's only control has nothing behind it");

            // NOT `LoadRoster`, WHICH IS WHAT THIS EXIT SAID UNTIL FIX ROUND
            // 1: nothing has been asked of `/v1/roster` by this point, and
            // this task turned every stop's sentence from four seconds of
            // toast into the persistent explanation on the screen.
            Assert.AreEqual(FtueNotice.ColdStartEmpty, walk.Reason,
                "the screen is not showing the sentence the walk actually stopped on");
        }

        [Test]
        public void ARosterThatWillNotLoad_ShowsTheSERVERsOwnSentenceOnTheRecoveryScreen()
        {
            // 503 rather than a 500 with no body, because the point of this
            // case is that the SERVER's sentence survives to the screen -
            // `ServerError` is where that translation lives and this is the
            // only place that proves it reaches a player after a stop.
            const string refusal = "The roster service is having a moment.";
            var walk = Run(new Walk { Snapshot = Fresh() }
                .Answering("/v1/roster", HttpStatusCode.ServiceUnavailable, ErrorJson("internal", refusal)));

            Assert.IsFalse(walk.Running.IsCompleted);
            Assert.IsNotNull(walk.Interrupted);
            Assert.AreEqual(refusal, walk.Reason);

            // AND THE CONTROL ON THIS PARTICULAR SCREEN RECOVERS, which is
            // more than "a button exists": the roster starts answering, the
            // tap re-enters the walk, and the beat the player was owed
            // arrives. A recovery screen whose button re-presented itself
            // forever would satisfy every other assertion in this file.
            walk.Answering("/v1/roster", HttpStatusCode.OK, RosterJson(Creature(), Creature()));
            RaiseClicked(walk.Retry);

            Assert.IsInstanceOf<DeployView>(walk.Presented,
                "the tap did not get the player back to the beat they were owed");
            Assert.IsFalse(walk.Running.IsCompleted);
        }

        [Test]
        public void ARosterWithNothingDeployableInIt_ShowsTheDEPLOYMENTsOwnSentence()
        {
            // `FightAsync`'s `CanDeploy` refusal - the first of its four
            // exits and the only one reachable before the deploy screen is
            // built. Its own comment already knew the shape of this defect
            // ("a player staring at a greyed button forever") and chose to
            // stop instead; stopping is what this suite now makes survivable.
            var walk = Run(new Walk { Snapshot = Fresh() }
                .Answering("/v1/roster", HttpStatusCode.OK, RosterJson()));

            var expected = DeployScreen.Build(1, new RosterScreen(), new List<Guid>()).Blocker;
            Assert.IsNotEmpty(expected, "the fixture no longer produces an undeployable model");

            Assert.IsFalse(walk.Running.IsCompleted);
            Assert.IsNotNull(walk.Interrupted);
            Assert.AreEqual(expected, walk.Reason);
        }

        [Test]
        public void ACampaignSelectWithNoWavesInTheBundle_NoLongerStopsSilently()
        {
            // Both of `CampaignAsync`'s opening guards used to `return` with
            // nothing said at all, which is why they were made to speak in
            // the same change: the recovery screen shows the LAST sentence,
            // and a silent stop would have shown the PREVIOUS stop's reason.
            var done = Fresh();
            done.HighestWaveCleared = Ftue.SessionTwoWave;
            done.Ftue.Splices = 1;
            done.Waves = new List<WaveSummary>();

            var walk = Run(new Walk { Snapshot = done }
                .Answering("/v1/roster", HttpStatusCode.OK, RosterJson(Creature())));

            Assert.IsFalse(walk.Running.IsCompleted);
            Assert.IsNotNull(walk.Interrupted);
            Assert.AreEqual(FtueNotice.NoWavesAuthored, walk.Reason);
        }

        [Test]
        public void ACampaignSelectWhoseSnapshotWentAway_NoLongerStopsSilentlyEither()
        {
            // The OTHER of `CampaignAsync`'s two opening guards. Both were
            // silent and both now speak, and they speak DIFFERENTLY - one
            // means nothing synced, the other means the sync landed and
            // authored no waves. A player cannot act on the distinction;
            // `BootController.OnNotice` logs every sentence, and a developer
            // reading a device log can.
            var done = Fresh();
            done.HighestWaveCleared = Ftue.SessionTwoWave;
            done.Ftue.Splices = 1;

            // Present for `WalkAsync`'s own read at the top of the pass, gone
            // by the time `CampaignAsync` asks for it - which is a snapshot
            // store being replaced under a running walk, not a contrivance.
            var reads = 0;
            var walk = Run(new Walk { Snapshots = () => ++reads == 1 ? done : null }
                .Answering("/v1/roster", HttpStatusCode.OK, RosterJson(Creature())));

            Assert.IsFalse(walk.Running.IsCompleted);
            Assert.IsNotNull(walk.Interrupted);
            Assert.AreEqual(FtueNotice.CampaignUnreadable, walk.Reason);
            Assert.AreNotEqual(FtueNotice.NoWavesAuthored, walk.Reason,
                "the two guards are saying the same thing, so the log cannot tell them apart");
        }

        [Test]
        public void AThrowOutOfTheWalk_LandsOnTheSameScreen_NotInALogNobodyOnADeviceCanRead()
        {
            // This is the class that used to reach `BootController`'s
            // `Debug.LogError` and stop there. A `Func<PlayerSnapshot>` that
            // throws is the cheapest way to produce one; the director does
            // not care where it came from.
            var walk = Run(new Walk { Snapshots = () => throw new InvalidOperationException("the snapshot source broke") });

            Assert.IsFalse(walk.Running.IsCompleted);
            Assert.IsNotNull(walk.Interrupted);

            // NOT the exception's own message. See `FtueNotice.WalkThrew`:
            // a throw out of the walk is a defect rather than a refusal, and
            // `ServerError.From` would have handed the player the .NET text.
            Assert.AreEqual(FtueNotice.WalkThrew, walk.Reason);
            StringAssert.DoesNotContain("snapshot source broke", walk.Reason);
        }

        // ---------------------------------------------------------------
        // The exits that lie past the deploy screen's Start button
        // ---------------------------------------------------------------

        [Test]
        public void AWalkWaitingOnARealScreenIsNotInterrupted()
        {
            // THE CONTROL, AND IT IS WHAT MAKES THE REST OF THIS FILE MEAN
            // ANYTHING. A recovery screen that went up whenever the walk
            // stopped AWAITING would pass every other case here while
            // destroying the game, so the healthy cold open is asserted to
            // reach the deploy screen and stay there.
            var walk = Healthy();

            Assert.IsFalse(walk.Running.IsCompleted);
            Assert.IsInstanceOf<DeployView>(walk.Presented,
                "the cold open no longer reaches the deploy screen, so the cases below prove nothing");
            Assert.IsNull(walk.Interrupted);
            CollectionAssert.IsEmpty(walk.Notices, "a healthy walk said something was wrong");
        }

        [Test]
        public void AWaveTheServerRefuses_ReplacesTheDeadStartButton_WithoutReOfferingIt()
        {
            // `FightAsync`'s `StartAsync` catch, DEFINITIVE arm.
            // `StartLeavesTheWalkAlive` is deliberately not touched by this
            // task: a transient failure still goes straight back to a live
            // Start button and never reaches the recovery screen. A verdict
            // does, and its own comment says why it must not simply re-offer
            // the same screen - "a loop, not a remedy".
            const string verdict = "That wave is locked.";
            var walk = Healthy().Answering("/v1/wave/start", HttpStatusCode.Conflict, ErrorJson("wave_locked", verdict));

            RaiseClicked(walk.Presented.Q<Button>("start"));

            Assert.IsFalse(walk.Running.IsCompleted);
            Assert.IsNotNull(walk.Interrupted, "the player is still on the deploy screen its Start button just died on");
            Assert.AreEqual(verdict, walk.Reason);

            // AND IT DOES NOT SPIN, which is the other half of the ruling.
            // One tap produced exactly one refusal and then stopped asking.
            Assert.AreEqual(1, walk.Server.CallsTo("/v1/wave/start"),
                "the walk re-offered a refusal it had already been given");
        }

        [Test]
        public void AWaveThatNeverFinished_IsSurvivable_WhichIsTheBACKGROUNDEDAppTheWalkMet()
        {
            // `FightAsync`'s `_play` catch, and it is not a hypothetical one:
            // `WaveHost.CompletionTimeoutSeconds` is 120 seconds of WALL
            // CLOCK, so a player who backgrounds the app mid-wave for two
            // minutes comes back to a wave the host has abandoned. That is
            // exactly how Task 21f reproduced the dead screen on an iPhone
            // 17, and the message below is the one its console printed.
            //
            // THE TIMEOUT ITSELF IS STILL A DEFECT and is deliberately out of
            // this task's scope - a suspended app is not a hung one. What
            // changed is what the player meets afterwards.
            const string hung = "Wave did not finish within 120s of wall clock.";
            var walk = Healthy(play: (wave, specs, seed, input) => throw new InvalidOperationException(hung));
            walk.Answering("/v1/wave/start", HttpStatusCode.OK, StartJson());

            RaiseClicked(walk.Presented.Q<Button>("start"));

            Assert.IsFalse(walk.Running.IsCompleted);
            Assert.IsNotNull(walk.Interrupted);
            Assert.AreEqual(hung, walk.Reason);

            // AND THE RETRY LANDS SOMEWHERE CORRECT, NOT MERELY SOMEWHERE
            // ALIVE. `wave/start` committed the deployment server-side before
            // the wave was abandoned, so the roster comes back locked - which
            // is why the route is re-stubbed rather than left alone. The tap
            // must therefore reach `AbandonedWaveSheet`, Phase 9 design
            // §2.1's designed way out, rather than a deploy screen the server
            // would refuse. This was reasoned about in the first report and
            // is asserted here instead.
            walk.Answering("/v1/roster", HttpStatusCode.OK, RosterJson(Creature(committedTo: Guid.NewGuid())));
            RaiseClicked(walk.Retry);

            Assert.IsNotNull(walk.Sheets.Q<Button>("forfeit"),
                "a player who backgrounded the app mid-wave is not being offered the forfeit that frees their roster");
            Assert.IsFalse(walk.Running.IsCompleted);
        }

        [Test]
        public void ASubmissionTheOutboxCouldNotSend_StopsOnAScreenThatSaysSo()
        {
            // `FightAsync`'s fourth exit, and the one Task 21f's own device
            // run actually took: the wave was played, the submit did not
            // reach the server, and the walk stopped because the SERVER's
            // verdict is the only one that counts. The sentence is the
            // outbox's, threaded through `FtueNotice.For`.
            var walk = Healthy();
            walk.Answering("/v1/wave/start", HttpStatusCode.OK, StartJson())
                .Answering("/v1/wave/submit", HttpStatusCode.ServiceUnavailable, ErrorJson("internal", "later"));

            RaiseClicked(walk.Presented.Q<Button>("start"));

            Assert.IsFalse(walk.Running.IsCompleted);
            Assert.IsNotNull(walk.Interrupted);
            Assert.AreEqual(FtueNotice.For(OutboxOutcome.Queued, FtueNotice.SubmitWave), walk.Reason);
        }

        [Test]
        public void AForfeitThatDidNotFreeTheRoster_ReplacesTheScreenTheSheetWasOver()
        {
            // `ForfeitIfLockedAsync`, which is the only exit whose player was
            // looking at a SHEET rather than a screen. `ScreenFlow` hides the
            // sheet as the turn ends, so without this fix the player was left
            // on whatever was underneath it - which on a cold start is
            // nothing at all.
            const string refused = "That wave cannot be abandoned right now.";
            var walk = Run(new Walk { Snapshot = Fresh() }
                .Answering("/v1/roster", HttpStatusCode.OK, RosterJson(Creature(committedTo: Guid.NewGuid())))
                .Answering("/v1/wave/abandon", HttpStatusCode.Conflict, ErrorJson("wave_locked", refused)));

            var forfeit = walk.Sheets.Q<Button>("forfeit");
            Assert.IsNotNull(forfeit, "the abandoned-wave sheet is not up, so this case is not testing what it says");

            RaiseClicked(forfeit);

            Assert.IsFalse(walk.Running.IsCompleted);
            Assert.IsNotNull(walk.Interrupted);
            Assert.AreEqual(refused, walk.Reason);
            Assert.AreEqual(0, walk.Sheets.childCount, "the sheet is still over the recovery screen");
        }

        // ---------------------------------------------------------------
        // The control itself
        // ---------------------------------------------------------------

        [Test]
        public void TheControlRunsTheWalkAgain_OncePerTapAndNotOnItsOwn()
        {
            // THE NON-SPIN ARGUMENT, ASSERTED RATHER THAN REASONED. The walk
            // cannot distinguish a transient exit from a definitive one -
            // a bare `return` carries nothing - so what keeps a refusal from
            // looping is that a human is between every two attempts. Here
            // the walk fails the same way every time, and the count of
            // attempts is exactly the count of taps.
            var walk = Run(new Walk { Snapshot = null });
            Assert.AreEqual(1, walk.Notices.Count, "the first stop said its sentence more than once");

            var first = walk.Interrupted;
            RaiseClicked(walk.Retry);

            Assert.AreEqual(2, walk.Notices.Count, "the tap did not re-enter the walk");
            Assert.IsNotNull(walk.Interrupted);
            Assert.AreNotSame(first, walk.Interrupted, "the same dead screen is still presented");
            Assert.IsFalse(walk.Running.IsCompleted);

            // Nothing ran on its own between the two taps.
            RaiseClicked(walk.Retry);
            Assert.AreEqual(3, walk.Notices.Count);
        }

        [Test]
        public void AScreenBoundWithNoReason_ShowsTheFallback_RatherThanABlankCard()
        {
            // NOTHING IN THE DIRECTOR CAN REACH THIS TODAY - every exit
            // speaks before it returns, which is the property `AnotherTry
            // Async` relies on and which the cases above spot-check exit by
            // exit. It is held by inspection rather than by the compiler, so
            // the screen is asserted to survive the day it stops being true
            // rather than to render an empty card under a live button.
            var bare = new InterruptedView();
            bare.Bind(null, () => { });
            Assert.AreEqual(InterruptedView.UnexplainedReason, bare.Q<Label>("message").text);

            bare.Bind(string.Empty, () => { });
            Assert.AreEqual(InterruptedView.UnexplainedReason, bare.Q<Label>("message").text);

            // AND IT DOES NOT STACK. A re-bound screen carries one sentence,
            // not two - the same hazard the single `clicked` subscription in
            // the constructor avoids.
            bare.Bind("Something specific.", () => { });
            Assert.AreEqual(1, bare.Q<VisualElement>("reason").childCount);
            Assert.AreEqual("Something specific.", bare.Q<Label>("message").text);
        }

        // ---------------------------------------------------------------
        // The harness
        // ---------------------------------------------------------------

        /// One walk under test: the stubbed server, the shell it draws into,
        /// and the running `Task` that is deliberately never awaited.
        sealed class Walk
        {
            public readonly StubServer Server = new StubServer();
            public readonly List<string> Notices = new List<string>();
            public readonly VisualElement ScreenSlot = new VisualElement { name = "screen-host" };
            public readonly VisualElement Sheets = new VisualElement { name = "sheet-layer" };

            public PlayerSnapshot Snapshot;
            public Func<PlayerSnapshot> Snapshots;
            public FtueDirector.PlayWave Play;
            public Task Running;

            public Walk Answering(string path, HttpStatusCode status, string body)
            {
                Server.Routes[path] = new Reply { Status = status, Body = body };
                return this;
            }

            public VisualElement Presented => ScreenSlot.childCount == 0 ? null : ScreenSlot.ElementAt(0);
            public InterruptedView Interrupted => Presented as InterruptedView;
            public Button Retry => Interrupted == null ? null : Interrupted.Q<Button>("retry");

            /// The sentence the recovery screen is showing, read off the
            /// rendered label rather than off the director - the point of
            /// this task is that it reaches a player.
            public string Reason
            {
                get
                {
                    var label = Interrupted == null ? null : Interrupted.Q<Label>("message");
                    return label == null ? null : label.text;
                }
            }
        }

        Walk Run(Walk walk)
        {
            var api = new BroodlineApiClient(new HttpClient(walk.Server)) { BaseUrl = "http://stub.invalid/" };

            // `isOffline: () => false` for `OutboxClientTests`' reason:
            // `Application.internetReachability` is not controllable from an
            // EditMode test, and the outcome this suite reads depends on it.
            var outbox = new OutboxClient(api, new Outbox(), new OutboxStore(_outboxPath), () => false);

            var host = new ScreenHost(walk.ScreenSlot, walk.Sheets, new TabBar());

            var director = new FtueDirector(
                api,
                outbox,
                walk.Play ?? ((wave, specs, seed, input) => Task.FromResult(Won())),
                new ScreenFlow(host),
                walk.Snapshots ?? (() => walk.Snapshot),
                () => Task.CompletedTask,
                notice => walk.Notices.Add(notice));

            // STARTED, NOT AWAITED. A walk that is still running is the pass
            // condition, so awaiting this would hang every case in the file.
            // Recorded so `TearDown` can observe it - see there.
            walk.Running = director.RunAsync();
            _started.Add(walk);
            return walk;
        }

        /// A cold open that reaches the deploy screen and waits there: a
        /// fresh snapshot and two uncommitted creatures.
        Walk Healthy(FtueDirector.PlayWave play = null)
        {
            return Run(new Walk { Snapshot = Fresh(), Play = play }
                .Answering("/v1/roster", HttpStatusCode.OK, RosterJson(Creature(), Creature())));
        }

        // ---------------------------------------------------------------
        // Fixtures
        // ---------------------------------------------------------------

        static PlayerSnapshot Fresh()
        {
            return new PlayerSnapshot
            {
                PlayerId = "p1",
                ServerId = 1,
                Balances = new Dictionary<string, int>(),
                HighestWaveCleared = 0,
                BundleVersion = "0.1.0",
                Tabs = new Dictionary<string, int>(),
                Waves = new List<WaveSummary> { new WaveSummary { Id = 1, RewardCurrency = "shards", RewardAmount = 5 } },
                Traits = new List<TraitSummary>(),
                Ftue = new FtueFacts(),
            };
        }

        static CreatureDto Creature(Guid? committedTo = null)
        {
            return new CreatureDto
            {
                CreatureId = Guid.NewGuid(),
                Species = "Vetch",
                Generation = 1,
                Trait1 = "Taunt", Tier1 = 1,
                Trait2 = "Carapace", Tier2 = 1,
                Instinct = "Vanguard",
                IsFounder = false,
                CommittedTo = committedTo,
            };
        }

        static string RosterJson(params CreatureDto[] creatures)
        {
            var response = new RosterResponse { Cap = 20 };
            foreach (var creature in creatures) response.Creatures.Add(creature);
            return JsonConvert.SerializeObject(response);
        }

        /// An issuance the engine can actually simulate: `SpecsFor` parses
        /// the species, traits and instinct against this build's enums and
        /// `SeedOf` refuses anything that is not a 63-bit unsigned integer,
        /// and both run before `_play` is called.
        static string StartJson()
        {
            var response = new WaveStartResponse
            {
                IssuanceId = Guid.NewGuid(),
                Seed = "12345",
                WaveId = 1,
                ExpiresAt = "2030-01-01T00:00:00Z",
                Deployment = new List<CreatureSpecDto>
                {
                    new CreatureSpecDto
                    {
                        Species = "Vetch", Trait1 = "Taunt", Tier1 = 1,
                        Trait2 = "Carapace", Tier2 = 1, Instinct = "Vanguard", Pocket = 0,
                    },
                },
            };
            return JsonConvert.SerializeObject(response);
        }

        static WaveReport Won()
        {
            return new WaveReport
            {
                Result = "Win",
                Ticks = 10,
                IntegrityRemaining = 5,
                ReplayBytes = new byte[] { 1, 2, 3 },
                Breaches = new List<BreachSummary>(),
            };
        }

        static string ErrorJson(string code, string message)
        {
            return JsonConvert.SerializeObject(new Dictionary<string, string> { { "code", code }, { "message", message } });
        }

        // ---------------------------------------------------------------
        // The two things this suite borrows
        // ---------------------------------------------------------------

        /// A stubbed backend, keyed by path, that answers SYNCHRONOUSLY.
        ///
        /// `Task.FromResult` IS LOAD-BEARING, not a convenience: an
        /// already-completed awaitable never suspends the director's state
        /// machine, so everything a call provokes happens inline on this
        /// thread and the assertions below it can read the result. See this
        /// file's class comment.
        sealed class StubServer : HttpMessageHandler
        {
            public readonly Dictionary<string, Reply> Routes = new Dictionary<string, Reply>();
            readonly List<string> _calls = new List<string>();

            public int CallsTo(string path)
            {
                var seen = 0;
                foreach (var call in _calls) if (call == path) seen++;
                return seen;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var path = request.RequestUri.AbsolutePath;
                _calls.Add(path);

                // AN UNSTUBBED PATH IS A 404 WITH A BODY, not a throw: a
                // route this suite forgot should show up as the wrong
                // SENTENCE on the recovery screen, which names it, rather
                // than as a transport failure that looks like a real one.
                Reply reply;
                if (!Routes.TryGetValue(path, out reply))
                {
                    reply = new Reply
                    {
                        Status = HttpStatusCode.NotFound,
                        Body = ErrorJson("not_found", "No stub answers " + path + "."),
                    };
                }

                return Task.FromResult(new HttpResponseMessage(reply.Status)
                {
                    Content = new StringContent(reply.Body, Encoding.UTF8, "application/json"),
                });
            }
        }

        struct Reply
        {
            public HttpStatusCode Status;
            public string Body;
        }

        /// Raises a `Button`'s `clicked` with no `Panel` attached.
        ///
        /// `ScaffoldTests.RaiseClicked`'s MECHANISM AND ITS REASONING, which
        /// is recorded in full on that method and not repeated here: the
        /// event cannot be raised from outside `Button` (CS0070),
        /// `Clickable.SimulateSingleClick` is `internal`, and
        /// `VisualElement.SendEvent` needs an attached `Panel`. What is
        /// reachable is the `Action` field the compiler generates behind
        /// `Clickable.clicked`, and calling it runs exactly the subscriber
        /// list the screen registered.
        ///
        /// COPIED RATHER THAN SHARED, because the original is private to a
        /// fixture in `Broodline.UI.Tests` and this is `Broodline.Game
        /// .Tests`. The alternative - a test-only helper assembly both can
        /// reference - is more machinery than two dozen lines of reflection
        /// deserve, and this copy throws in the same place for the same
        /// reason, so neither can rot into a silent no-op.
        static void RaiseClicked(Button button)
        {
            var raise = Subscribed(button);
            Assert.IsNotNull(raise, "nothing is subscribed to this button, so the screen wired no handler");
            raise();
        }

        /// What is subscribed to a `Button`'s `clicked`, or null if nothing
        /// is - the falsifiable half of "there is a live control here".
        ///
        /// SPLIT OUT OF `RaiseClicked` IN FIX ROUND 1, because the exit cases
        /// wanted the CHECK without the side effect. Asserting a recovery
        /// button is `enabledSelf` proves nothing - no reachable change to
        /// `InterruptedView` makes it false - whereas this goes red the
        /// moment that screen stops wiring its constructor subscription.
        static Action Subscribed(Button button)
        {
            Assert.IsNotNull(button, "there is no button here at all");

            var clickable = button.clickable;
            Assert.IsNotNull(clickable, "the Button has no Clickable manipulator to raise");

            const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var field = typeof(Clickable).GetField("clicked", Instance);
            if (field == null || field.FieldType != typeof(Action))
            {
                FieldInfo only = null;
                var found = 0;
                foreach (var candidate in typeof(Clickable).GetFields(Instance))
                {
                    if (candidate.FieldType != typeof(Action)) continue;
                    only = candidate;
                    found++;
                }
                field = found == 1 ? only : null;
            }

            if (field == null)
            {
                throw new MissingFieldException(
                    "UnityEngine.UIElements.Clickable no longer backs its `clicked` event with a single "
                    + "Action field, so WalkRecoveryTests cannot drive the walk past a screen. It needs a "
                    + "new hook rather than a weaker assertion.");
            }

            return (Action)field.GetValue(clickable);
        }
    }
}
