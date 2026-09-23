using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Broodline.Api;
using Broodline.Creatures;
using Broodline.Game.Shell;
using Broodline.Model;
using Broodline.Net;
using Broodline.Sim.Combat;
using Broodline.UI;
using Broodline.UI.Components;
using Broodline.UI.Screens;
using Instinct = Broodline.Sim.Combat.Instinct;   // Broodline.Api has an `Instinct` class too - the generated
                                                  // /v1/sync forecast row. This file means the engine's enum.

namespace Broodline.Game
{
    /// What one blocked mutation says to the player.
    ///
    /// `OutboxClient` answers with four outcomes and only one of them is
    /// success, so every director call site has four arms whether it writes
    /// them or not - a walk that assumes `Sent` null-references the first
    /// time a player is offline. The sentences live here rather than inside
    /// the director so they can be tested as sentences, the same convention
    /// `WaveScreens.cs` states for the screens.
    ///
    /// The ASYMMETRY is deliberate and is the outbox's, not this class's:
    /// `wave/submit` and `ftue/splice-stock` queue while offline, so their
    /// blocked sentence promises a retry. `splice/commit`, `node/claim` and
    /// `creature/name` are server-authoritative and never queue, so theirs
    /// says the action is unavailable instead of promising something that
    /// will not happen. Do not smooth these into one line.
    public static class FtueNotice
    {
        public static string For(OutboxOutcome outcome, string action, BroodlineApiException error = null)
        {
            switch (outcome)
            {
                case OutboxOutcome.Sent:
                    return string.Empty;
                case OutboxOutcome.Queued:
                    return action + " has not reached the server yet. It will be sent when you are back online.";
                case OutboxOutcome.Unavailable:
                    return action + " needs a connection. Try again when you are back online.";
                case OutboxOutcome.Rejected:
                    // NO `Diagnostics.Defect` HERE, AND IT IS THE ONE EXEMPTION
                    // AMONG THE NINE READERS OF `PlayerMessage` - the reason the
                    // other count in this file is eight. `error` is a
                    // `BroodlineApiException`, which `ServerError.From` answers
                    // on one of its two API branches and never on the transport
                    // branch this task changed, so the raw `Exception.Message`
                    // this file stopped showing a player was never what reached
                    // here and there is nothing for a log to recover.
                    //
                    // WHAT IS NOT CLAIMED, because fix round 1 claimed it and it
                    // was false: the STATUS is not in `PlayerMessage`.
                    // `PlayerMessage` is the server's `message` alone,
                    // `StatusCode` is a separate property, and nothing on this
                    // path logs it. A refusal that needs its status read is a
                    // reason to log here after all; none has come up.
                    //
                    // It is also `static` and reached from the outbox rather
                    // than from a catch.
                    var why = error == null ? string.Empty : ServerError.From(error).PlayerMessage;
                    return string.IsNullOrEmpty(why)
                        ? action + " was refused."
                        : action + " was refused: " + why;
                default:
                    return action + " did not complete.";
            }
        }

        /// Beat 6 has nothing to splice. `TutorialPair` returns null when
        /// fewer than two eligible creatures are in the roster - every
        /// candidate a Founder, out fighting, or simply absent because the
        /// roster read is behind the grant.
        ///
        /// A SENTENCE, NOT A LITERAL AT THE CALL SITE, for the reason this
        /// class exists: a string inside the director is one no test can tell
        /// from a string inside a view, which is the slip Tasks 15 and 16
        /// each paid for once.
        public const string NoTutorialPair =
            "The tutorial pair is not in your roster. Refresh and try again.";

        /// splice_confirm_spec section 6: "the named Founder is visibly
        /// locked out." This is what the player is told if the pair selection
        /// ever produced one anyway.
        public const string FounderInTutorialPair =
            "The tutorial splice cannot use one of your Founders.";

        public const string SubmitWave = "Your wave result";
        public const string NameFounder = "Naming";
        public const string SpliceStock = "The tutorial stock";
        public const string SpliceCommit = "The splice";
        /// The cold start left no snapshot behind, so the walk has nothing to
        /// derive a beat from.
        ///
        /// IT USED TO BE CALLED `LoadRoster` AND USED TO SAY "Your roster
        /// could not be loaded" - Phase 9 Task 21g, fix round 1. Nothing has
        /// been asked of `/v1/roster` at that point in the walk; the sentence
        /// named a subsystem that had not been touched. It survived as long as
        /// it did because it was four seconds of toast on a screen the player
        /// was about to leave. Task 21g promoted every stop's sentence into
        /// the persistent, authoritative explanation on `InterruptedView` -
        /// and this is the stop an offline launch is MOST likely to reach, so
        /// it became the most-read wrong sentence in the build.
        ///
        /// REPLACED RATHER THAN JOINED BY A NEW ONE, because the old constant
        /// had exactly one call site: the null-snapshot exit below.
        /// `LoadRosterAsync` says the SERVER's own sentence
        /// (`error.PlayerMessage`), never this. Adding a fourth constant would
        /// have left `LoadRoster` behind with no callers and a sentence nobody
        /// had validated, for the next person to reach for.
        ///
        /// IT NAMES THE BUTTON RATHER THAN A RELAUNCH, which is the whole
        /// difference from `ColdStartFailed` below. That one is said by
        /// `BootController`, where there is no button to name; this one is
        /// said where there is one.
        ///
        /// AND IT IS TRUE, WHICH IS MORE THAN "BETTER THAN THE LAST ONE".
        /// `Session.ColdStartAsync` sets `Snapshot` from the CACHE before it
        /// calls anything, so a returning player keeps their stored progress
        /// even with the network down and never reaches this exit at all. A
        /// null snapshot here means the cache held nothing AND the sync did
        /// not land - there really is no progress to read.
        public const string ColdStartEmpty =
            "Your progress could not be read from the server. Try again.";

        /// A human playing the game found this in the first minute: starting
        /// a wave commits the deployed creatures server-side, and quitting
        /// before submitting leaves them committed. `AbandonedWaveSheet` is
        /// the way out; this is what is said when Forfeit does not free the
        /// roster - the server refused, or the call failed.
        public const string ForfeitFailed =
            "Your creatures are still out fighting and the wave could not be forfeited. Try again in a moment.";

        /// Said the MOMENT the first attempt at a call fails, not after the
        /// last one - `Retry.TransientAsync` calls `onRetry` before it waits,
        /// for this sentence's sake.
        ///
        /// THE SILENCE WAS HALF THE DEFECT. A developer pressed Start on the
        /// deploy screen against a cold backend, got one notice and nothing
        /// else, and a second press worked. A tester who presses once
        /// concludes the button is broken. This is what fills the gap while
        /// the retries run, and it says WAKING UP rather than "failed"
        /// because that is what is actually happening: `min_instance_count`
        /// is 0 on both Cloud Run services, so the first call of a session
        /// is waiting on a container to start.
        public const string ServerWakingUp =
            "The server is waking up. Still trying...";

        /// Said when every attempt has been spent and the wave still has no
        /// issuance.
        ///
        /// "NOTHING WAS SPENT" IS A CLAIM THIS FILE IS ENTITLED TO MAKE, and
        /// it is the only reassurance offered because it is the only one
        /// that is true. `POST /v1/wave/start` debits no currency, grants no
        /// reward, and does not count against the replay cap (which counts
        /// `settlement = 'consumed'` rows only). What it MAY have done is
        /// commit the deployment to a live issuance, which is why the
        /// sentence does not say "nothing happened" - tapping Start again is
        /// what resolves that, because the server hands the same issuance
        /// back rather than refusing it (see `DeployScreen.StartAsync`).
        ///
        /// AND THE BUTTON IS LIVE WHEN THEY READ IT. The sentence would be a
        /// lie on a stranded screen: `FightAsync` returns the walk to the
        /// deploy screen for this failure rather than ending it.
        public const string StartWaveUnreached =
            "The server did not answer. Nothing was spent - tap Start again.";

        /// The cold start itself failed, after every retry. `BootController`
        /// used to answer this with a `Debug.LogError` alone, which a tester
        /// holding a device cannot read.
        ///
        /// IT PROMISES A RELAUNCH RATHER THAN A BUTTON, because there is no
        /// button: nothing in this build re-runs the cold start on demand,
        /// and naming a remedy that does not exist is the failure
        /// `ServerError.Remedy.None` exists to avoid.
        public const string ColdStartFailed =
            "Could not reach the server. Check your connection and reopen the app.";

        /// Campaign Select needs a snapshot and there is none.
        ///
        /// SAID AT ALL BECAUSE IT USED TO BE SILENT - Phase 9 Task 21g. Both
        /// of `CampaignAsync`'s opening guards ended the walk with a bare
        /// `return` and no sentence anywhere, which left the player on
        /// whatever screen was up with nothing to read and nothing to tap.
        /// The recovery screen shows the LAST thing the walk said, so an exit
        /// that says nothing is an exit that shows the previous exit's
        /// sentence - a wrong explanation is worse than a vague one.
        public const string CampaignUnreadable =
            "The wave list has not arrived yet. Try again in a moment.";

        /// The snapshot arrived and `config.waves` is empty.
        ///
        /// A SECOND SENTENCE FOR WHAT LOOKS LIKE THE SAME THING, and the
        /// difference is real even though the player's remedy is the same:
        /// one means nothing synced, the other means the sync landed and the
        /// bundle authored no waves. `BootController.OnNotice` logs every
        /// sentence, so this distinction is what a developer reading a device
        /// log has to tell them apart with.
        public const string NoWavesAuthored =
            "There are no waves to fight yet. Try again in a moment.";

        /// Something threw its way out of the whole walk.
        ///
        /// NOT `ServerError.From(error).PlayerMessage`, AND THAT IS THE
        /// DECISION. Every refusal the walk can meet is already caught and
        /// translated where the call was made - `ServerError` is how, and its
        /// sentences are the server's own. What reaches the outermost catch
        /// is therefore not a refusal but a defect, and for a defect
        /// `PlayerMessage` degrades to the raw `Exception.Message`: "Object
        /// reference not set to an instance of an object" is a stack trace
        /// wearing a sentence's clothes. The exception goes to the log where
        /// a developer can use it; the player gets this.
        ///
        /// THE RULING IS NOW INSIDE `PlayerMessage` AND THIS IS DEFINED FROM
        /// IT - Phase 9 Task 21h. `ServerError.From(Exception)`'s transport
        /// branch no longer surfaces a raw `Exception.Message` at all, so the
        /// EIGHT readers of `PlayerMessage` in this file that can reach that
        /// branch get the same treatment this constant gave itself. Eight of
        /// nine, corrected in fix round 2: nine sites read `PlayerMessage`, and
        /// `FtueNotice.For` is not one of the eight because its `error` is a
        /// `BroodlineApiException` and never reaches the transport branch. The
        /// catch this constant is said from is in neither count - it reads
        /// `PlayerMessage` nowhere, which is what 21g did about it.
        ///
        /// A player caught by a defect at the call that made it and a player
        /// caught by one thrown out of the whole walk are in the same situation,
        /// so they get the same sentence, from one definition.
        public const string WalkThrew = ServerError.UnexpectedProblem;

        /// The last resort, in `BootController`, when even the recovery
        /// screen could not be put up.
        ///
        /// IT PROMISES A RELAUNCH RATHER THAN A BUTTON, for `ColdStartFailed`'s
        /// reason and more literally: by the time this is said the thing that
        /// shows buttons is what failed, so there is no button to name.
        public const string WalkUnrecoverable =
            "The game stopped and could not recover. Reopen the app.";
    }

    /// The first hour, walked.
    ///
    /// `Ftue.Derive` says WHICH beat; this says what a beat IS - one hosted
    /// wave, or one screen, and the server call between them. It holds no
    /// progress of its own: every pass round the loop re-syncs and
    /// re-derives, so closing the app mid-beat and reopening resumes from
    /// what the server knows rather than from anything written here.
    ///
    /// THE ONE PIECE OF SESSION STATE, and it is not progress: `_namingOffered`.
    /// Skipping beat 4 does NOT name the creature server-side, so
    /// `ftue.founderNamed` stays false and `Derive` keeps answering
    /// `NameFounder` while wave 2 is uncleared. Bible 3.3 makes the prompt
    /// optional and puts the rename on the Roster, and task-17-brief.md says
    /// the beat is "offered once per session start" - so the latch lives
    /// here, for the length of one walk, and is deliberately not persisted.
    /// Without it the skip button is an infinite loop.
    ///
    /// EVERY MUTATION GOES THROUGH THE OUTBOX and every one of them checks
    /// its outcome. A beat whose mutation did not land does not advance:
    /// either the walk stops with a notice (the wave submit, the stock
    /// grant, the commit - nothing after them is meaningful without them) or
    /// it continues having said so (naming, which bible 3.3 already makes
    /// optional).
    ///
    /// NO `ConfigureAwait(false)` ANYWHERE IN THIS FILE, AND THAT IS LOAD-BEARING.
    /// In .NET library code it is good manners; in Unity UI code it is the bug.
    /// `UnitySynchronizationContext` is the only thing that puts a continuation
    /// back on the main thread, and `ConfigureAwait(false)` is the explicit
    /// instruction not to use it - so the await resumes on a threadpool thread
    /// and the next screen this director builds throws
    /// `VisualElementCreation can only be called from the main thread`.
    ///
    /// ONE IS ENOUGH TO POISON THE WHOLE WALK. A pool thread carries no
    /// synchronization context, so once the first await has moved off the main
    /// thread, every later await captures `null` and stays off it however it is
    /// written. `RunAsync`'s first await was the one that did it.
    ///
    /// AND IT IS INVISIBLE TO EVERY TEST WE HAVE. The flag only diverts a task
    /// that has NOT already completed synchronously. Tests drive this director
    /// against stubs that complete inline, the continuation never leaves the
    /// calling thread, and the suite is green over a first hour that cannot run
    /// at all against a real `api`. It took Task 17 Step 6 - a human, a live
    /// server and a screen - to see it. `Net/` may keep the flag: it touches no
    /// VisualElement, and a method's own `ConfigureAwait` does not follow its
    /// caller home.
    public sealed class FtueDirector
    {
        /// The hosted wave, as a delegate rather than as `WaveHost` itself.
        ///
        /// `WaveHost.RunAsync` loads a Unity scene additively, which no
        /// EditMode test can do - taking the seam here is what lets the
        /// director be constructed and driven without one. `BootController`
        /// passes the real `WaveHost.RunAsync`.
        public delegate Task<WaveReport> PlayWave(int waveId, CreatureSpec[] deployment, ulong seed, bool inputEnabled);

        readonly BroodlineApiClient _api;
        readonly OutboxClient _outbox;
        readonly PlayWave _play;
        readonly ScreenFlow _flow;
        readonly Func<PlayerSnapshot> _snapshot;
        readonly Func<Task> _resync;

        /// The caller's notice, WRAPPED so that everything said through it is
        /// also remembered - see the constructor.
        readonly Action<string> _notice;

        /// The last sentence the walk said, and the thing the recovery screen
        /// reads when the walk stops.
        ///
        /// WHY THE LAST NOTICE IS THE RIGHT ANSWER, and why nothing carries a
        /// reason out of the walk instead. Every `return` that ends the walk
        /// is bare and carries nothing; turning fourteen of them into
        /// something that did would be an audit that the fifteenth silently
        /// fails. What every one of them DOES do is say its sentence through
        /// `_notice` immediately before returning - including
        /// `CampaignAsync`'s two, which were made to as part of this change -
        /// so "the last thing said" and "why it stopped" are the same string
        /// for every exit that exists, and for any exit added later that
        /// follows the file's own habit of speaking before it stops.
        ///
        /// THE FAILURE MODE IS A STALE SENTENCE, NOT A MISSING ONE, and it is
        /// bounded by that property: a notice said mid-beat (`ServerWakingUp`
        /// on the first retry) is overwritten by the sentence of whatever
        /// eventually stops the walk. It would only survive to be shown if a
        /// future exit stopped without speaking.
        string _lastNotice;

        readonly RosterScreen _roster = new RosterScreen();

        /// The portrait studio - Phase 9 design §3.8. Optional, and stored
        /// without a null check: `BootController`'s is the only call site
        /// this constructor has today, and it always passes the real one -
        /// but Tasks 14 and 16 are the first to add the screens that will
        /// read this field, and their tests will want to build a director
        /// without a scene to construct a studio in. Kept nullable now so
        /// that stays possible; until those tasks land it is carried but
        /// never shown.
        readonly PortraitStudio _studio;

        /// The lane stage - Phase 9 design §3.8, the second of the two
        /// off-screen rigs. Optional and nullable for `_studio`'s reasons,
        /// which apply here unchanged: `BootController` is the only
        /// production call site and always passes the real one, and
        /// `WalkRecoveryTests` builds a director with no scene to construct a
        /// camera in. (That sentence named `FtueDirectorTests` until Phase 9
        /// Task 21g and was wrong when it was written: nothing constructed a
        /// director outside `BootController` at all. It is true now, of the
        /// suite that does.) A director without a stage shows the deploy screen with
        /// the lane card on its own fill, which is what every capture in
        /// `implementation/results/screens` shows too.
        readonly LaneStage _stage;

        bool _namingOffered;

        public FtueDirector(
            BroodlineApiClient api,
            OutboxClient outbox,
            PlayWave play,
            ScreenFlow flow,
            Func<PlayerSnapshot> snapshot,
            Func<Task> resync,
            Action<string> notice,
            PortraitStudio studio = null,
            LaneStage stage = null)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
            _play = play ?? throw new ArgumentNullException(nameof(play));
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _resync = resync ?? throw new ArgumentNullException(nameof(resync));
            if (notice == null) throw new ArgumentNullException(nameof(notice));

            // WRAPPED HERE RATHER THAN AT THE FIFTEEN CALL SITES, so that a
            // sentence cannot be said without being remembered. Changing
            // every `_notice(...)` to a recording method would work today and
            // would be one forgotten call away from a recovery screen showing
            // the wrong reason; this way there is nothing to forget.
            //
            // EMPTY IS NOT A SENTENCE. `FtueNotice.For` answers `Sent` with
            // `string.Empty` and `BootController.OnNotice` drops it; letting
            // one through here would blank a real reason.
            _notice = sentence =>
            {
                if (!string.IsNullOrEmpty(sentence)) _lastNotice = sentence;
                notice(sentence);
            };
            _studio = studio;
            _stage = stage;
        }

        // ---------------------------------------------------------------
        // The walk
        // ---------------------------------------------------------------

        /// The walk, and the thing that catches it when it stops.
        ///
        /// **NO EXIT FROM THE WALK MAY LEAVE THE PLAYER WITH NO LIVE
        /// CONTROL**, and this loop is where that is established - Phase 9
        /// Task 21g, off the exit gate's device walks.
        ///
        /// WHAT A PLAYER MET. `WalkAsync` below has nine `return`s,
        /// `CampaignAsync` has five, `FightAsync` has four exits that can end
        /// it and the splice beat has seven across its two halves - and every
        /// one of them ended the whole walk. Counting them is the wrong
        /// answer and is recorded here only to show the size of the wrong
        /// answer.
        ///
        /// `ScreenFlow.ShowAsync` presents with `after: null`, so the
        /// screen that was up stayed up - a deploy screen with a Start button
        /// that no longer resumed anything, or a campaign list whose picks
        /// did nothing. `BootController` awaited this, caught nothing, and
        /// did nothing. The sentence explaining it went to `NoticeToast`,
        /// which holds a row for four seconds. After that the player had a
        /// live-looking screen, no explanation, and a relaunch.
        ///
        /// ONE SURFACE AT THE BOUNDARY, NOT FOURTEEN AT THE EXITS. The walk
        /// stops in exactly one place as seen from outside - here - so this
        /// covers every `return` above, every `return` any future beat adds,
        /// and the throw that used to land in `BootController`'s log. The
        /// per-exit alternative needs an audit that the next exit fails.
        ///
        /// **AND IT CANNOT SPIN, BECAUSE THE PLAYER IS THE GATE.**
        /// `AnotherTryAsync` awaits a tap, so there is always a human between
        /// two attempts and nothing here retries on its own. That is a
        /// stronger answer than sorting exits into transient and definitive,
        /// which a bare `return` carries no information to do:
        /// `FightAsync`'s `StartAsync` catch was right that "putting the same
        /// screen back so the player can produce the same refusal again is a
        /// loop, not a remedy" - and this does not put the same screen back.
        /// A definitive refusal lands on a DIFFERENT screen, which states the
        /// server's own sentence and re-offers nothing until it is asked to.
        /// `StartLeavesTheWalkAlive` still decides whether the walk goes
        /// straight back to a live Start button, and it is untouched: a
        /// transient failure never reaches this loop at all.
        public async Task RunAsync()
        {
            while (true)
            {
                try
                {
                    await WalkAsync();
                }
                catch (Exception error)
                {
                    // LOGGED WITH THE EXCEPTION AND SAID WITHOUT IT. See
                    // `FtueNotice.WalkThrew` for why the player does not get
                    // `ServerError.From(error).PlayerMessage` here.
                    Diagnostics.Defect("the walk threw", error);
                    _notice(FtueNotice.WalkThrew);
                }

                await AnotherTryAsync();
            }
        }

        /// One pass of the first hour, from the snapshot to the beat and
        /// round again. Every `return` in here ends it; `RunAsync` above is
        /// what stops that from ending the game.
        async Task WalkAsync()
        {
            while (true)
            {
                var snapshot = _snapshot();
                if (snapshot == null)
                {
                    // Cold start never produced one. There is nothing to
                    // derive a beat from, and inventing a fresh-account
                    // snapshot here would re-run beat 1 for a player who has
                    // simply lost their connection.
                    //
                    // THE SENTENCE AND THE COMMENT AGREE NOW, AND THEY DID
                    // NOT. This said `LoadRoster` - "Your roster could not be
                    // loaded" - beside a comment saying the COLD START is
                    // what produced nothing, with `/v1/roster` untouched.
                    // See `FtueNotice.ColdStartEmpty`.
                    _notice(FtueNotice.ColdStartEmpty);
                    return;
                }

                if (!await LoadRosterAsync()) return;
                if (!await ForfeitIfLockedAsync()) return;

                var beat = Ftue.Derive(snapshot, _roster.Known);

                // The skip latch - see the class comment. Applied AFTER the
                // derivation rather than folded into it, because `Derive` is
                // pure and knows nothing about this session.
                if (beat == Beat.NameFounder && _namingOffered) beat = Beat.SecondWave;

                switch (beat)
                {
                    case Beat.ColdOpen:
                        if (!await FightAsync(1)) return;
                        break;

                    case Beat.NameFounder:
                        await NameFounderAsync();
                        break;

                    case Beat.SecondWave:
                        if (!await FightAsync(2)) return;
                        break;

                    case Beat.GuidedSplice:
                        if (!await SpliceAsync(snapshot)) return;
                        break;

                    case Beat.Reveal:
                        // Unreachable: `Derive` never answers it, and
                        // `FtueTests.RevealIsNeverDerived_ItIsShownOffACommit`
                        // sweeps the whole input space to say so. The reveal
                        // is shown inside `SpliceAsync`, off its own commit.
                        // Listed rather than defaulted so that adding it to
                        // the derivation is a visible decision here.
                        goto case Beat.Lineage;

                    // NEITHER OF THE TWO `return`s BELOW IS A TERMINAL ONE,
                    // and calling them that is how this file read for two
                    // phases. `CampaignAsync` is a `while (true)` that the
                    // player never leaves by playing: it comes back only when
                    // it has GIVEN UP - no snapshot, no waves, a roster that
                    // would not load, a fight that stopped, a resync that
                    // failed. So "the first hour is over" and "the game
                    // stopped" arrive here as the same statement, which is
                    // why `RunAsync` treats every way out of the walk alike.
                    case Beat.Lineage:
                        await LineageAsync(lineage: null);
                        await CampaignAsync();
                        return;

                    case Beat.Done:
                        await CampaignAsync();
                        return;
                }

                if (!await ResyncAsync()) return;
            }
        }

        /// The screen the walk stops on, and the tap that restarts it.
        ///
        /// A SCREEN RATHER THAN A SHEET, AND THAT IS THE POINT OF IT.
        /// `ScreenHost.Show` REPLACES what is presented; `ShowSheet` overlays
        /// it. The defect here is a screen whose control is dead, so leaving
        /// that screen underneath an overlay would leave the player able to
        /// reach the dead control and would keep the thing that misled them
        /// on the glass. The stranded screen has to GO.
        ///
        /// THE SENTENCE OUTLIVES THE TOAST, WHICH IS HALF THE FIX. Every exit
        /// already said its own sentence, and `NoticeToast.HoldMs` is 4000 -
        /// so a player who looked away got four seconds of explanation and
        /// then a silent screen. `_lastNotice` is that sentence, and it sits
        /// on this screen for as long as the player leaves it there.
        ///
        /// CONSUMED RATHER THAN READ, so a second stop that somehow said
        /// nothing shows `InterruptedView.UnexplainedReason` instead of
        /// inheriting the previous stop's reason.
        ///
        /// THE TAP RE-ENTERS THE WALK AND DOES NOT RE-SYNC FIRST. `WalkAsync`
        /// re-reads `_snapshot()` and re-loads the roster on every pass and
        /// holds no progress of its own, which is this class's own stated
        /// design - so a retry derives the beat from what the server last
        /// said, and the resync the walk does at the end of each beat is
        /// still where a stale snapshot is refreshed. Forcing one here would
        /// put a network call in front of the recovery from a network
        /// failure.
        async Task AnotherTryAsync()
        {
            var reason = _lastNotice;
            _lastNotice = null;

            var interrupted = new InterruptedView();
            await _flow.ShowAsync(interrupted, resume => interrupted.Bind(reason, onRetry: resume));
        }

        // ---------------------------------------------------------------
        // Beats 1-3 and 5: a wave, start to finish
        // ---------------------------------------------------------------

        /// Deploy, fight, submit, and land on Post-Wave or Wave Defeat.
        ///
        /// DEPLOYMENT IS THE ROSTER'S ORDER, and the pockets follow from it.
        /// task-17-brief.md pre-fills "the pair in pockets 0 and 2", which
        /// `DeployScreenModel` cannot express: `DeployScreen.Build` assigns
        /// `Pocket = i` from the SELECTION index and the file's own comment
        /// says why that order is load-bearing ("two deployments that agree
        /// as multisets and differ in order are different deployments"). So
        /// the pair goes to pockets 0 and 1. Wave 1 is six pockets for two
        /// creatures precisely "so no placement is wrong"
        /// (`waves_01_12` via design section 6.2), which is what makes this a
        /// safe deviation rather than a silent one.
        async Task<bool> FightAsync(int waveId)
        {
            var selected = new List<Guid>(WantedFor(waveId));
            foreach (var creature in _roster.Known)
            {
                if (creature == null || creature.CommittedTo != null) continue;
                selected.Add(creature.CreatureId);
                if (selected.Count == WantedFor(waveId)) break;
            }

            var deployment = DeployScreen.Build(waveId, _roster, selected);

            // CHECKED BEFORE THE SCREEN GOES UP, not after, and that ordering
            // is the whole point: an undeployable model disables Start, and
            // Start is the ONLY thing that resumes this turn. Showing it
            // anyway would hang the first hour on a screen with no live
            // control - a player staring at a greyed button forever - which
            // is strictly worse than saying the model's own sentence and
            // stopping. (It is reachable: an empty roster, or every creature
            // `committed_to` a live issuance.)
            if (!deployment.CanDeploy)
            {
                _notice(deployment.Blocker);
                return false;
            }

            WaveStartResponse start;

            // THE `try` SPANS THE WHOLE BEAT AND THE `finally` CLEARS THE
            // STAGE, WHICH IS `NameFounderAsync`'s SHAPE AND IS THERE FOR ITS
            // REASONS. Both of them, restated because they pull in opposite
            // directions and only a `finally` around everything gets both:
            //
            //   - `ScreenFlow.ShowAsync(screen, bind)` passes `after: null`,
            //     so NOTHING HIDES THE VIEW when the turn resolves - it stays
            //     presented until the next `ScreenHost.Show`, and the turn's
            //     continuation runs synchronously inside the `Button.clicked`
            //     handler. So the deploy screen OUTLIVES THIS BEAT, through
            //     the whole of `StartAsync`'s round trip and past every exit
            //     below.
            //
            //     THAT FACT USED TO MAKE THE `finally` BLANK THE LANE, AND
            //     PHASE 9 TASK 21F MOVED THE FIX TO THE RIG RATHER THAN TO
            //     THIS SHAPE. `LaneStage.Clear` no longer wipes the render
            //     texture - the card holds it as a live background, so the
            //     wipe emptied a picture the player was still looking at on
            //     the four exits below that show no other screen first. See
            //     that method's note for the measurement. Nothing about the
            //     ordering here had to change.
            //   - `Show` leaves a camera enabled and a render texture being
            //     painted, and three paths below return early, so a `Clear`
            //     written after them would leave the stage running for the
            //     rest of the session on the outcomes a player hits most.
            //
            // `Show` IS INSIDE THE `try`, not above it, for `NameFounder
            // Async`'s reason: it builds creatures, reparents them and
            // enables a camera, and a throw partway through leaves a
            // half-shown stage that only a `finally` above it can clean up.
            try
            {
                var deployView = new DeployView();

                // THE LOOP IS A LOCAL THAT REWRITES ITSELF, NOT A SECOND
                // TURN. A tap on a field row changes the SELECTION and must
                // not resume the walk: `resume` is captured once, handed to
                // `onStart` on every re-bind, and called by nothing else. The
                // model is rebuilt from the ids rather than mutated, so
                // `DeployScreen.Build`'s refusals (a duplicate, a committed
                // creature, the floor, the cap) are re-applied on every tap
                // and the screen's blocker stays the model's own sentence.
                await _flow.ShowAsync(deployView, resume =>
                {
                    Action redraw = null;
                    redraw = () => deployView.Bind(
                        deployment,
                        onStart: resume,
                        lane: ShowLane(waveId, deployment),
                        roster: _roster.Known,
                        onToggle: id =>
                        {
                            if (!Toggle(selected, id)) return;
                            deployment = DeployScreen.Build(waveId, _roster, selected);
                            redraw();
                        },
                        facts: FactsFor(waveId));
                    redraw();
                });

                try
                {
                    // ONLY THE FIRST RETRY SPEAKS. `NoticeToast` "STACKS,
                    // NEVER REPLACES" and holds each row for four seconds,
                    // so noticing every attempt would pile three identical
                    // rows on top of each other across the backoff. One row
                    // at the first failure is the fact; the rest is repetition.
                    start = await DeployScreen.StartAsync(_api, deployment,
                        onRetry: (attempt, _) =>
                        {
                            if (attempt == 1) _notice(FtueNotice.ServerWakingUp);
                        });
                }
                catch (Exception error)
                {
                    // AT THE CALL SITE RATHER THAN INSIDE `StartFailureNotice`,
                    // which is pure and public precisely so a test can drive it
                    // - a log inside it would be a side effect in the one
                    // helper this file promises has none.
                    Diagnostics.Defect("wave/start failed", error);
                    _notice(StartFailureNotice(error));

                    // **RETURNING `true` HERE IS THE OTHER HALF OF THE FIX,
                    // AND IT IS NOT A TYPO.**
                    //
                    // `true` means "the walk may continue", not "the beat
                    // advanced" - nothing advanced, `start` was never
                    // assigned and nothing below this `catch` runs. What it
                    // buys is the SCREEN: `RunAsync` re-syncs, re-derives the
                    // same beat and shows the deploy screen again with a live
                    // Start button, and `CampaignAsync` returns to Campaign
                    // Select. `false` ended the walk outright, which is what
                    // the developer actually met - one tap, a notice, and a
                    // Start button that no longer resumed anything, because
                    // Start is the ONLY thing that resumes this turn (see the
                    // `CanDeploy` comment above). A tester who taps once and
                    // gets that concludes the button is broken.
                    //
                    // A DEFINITIVE REFUSAL STILL STOPS THE WALK. A 409, a
                    // 400, a `wave_locked` - those are verdicts, and putting
                    // the same screen back so the player can produce the same
                    // refusal again is a loop, not a remedy. `StartFailure
                    // Notice` shows the server's own sentence for those.
                    return StartLeavesTheWalkAlive(error);
                }

                // GUARDED FOR THE SAME REASON `StartAsync` IS, six lines up, and
                // it is not hypothetical: `SpecsFor` throws when the bundle names
                // content this client build has no enum for (see its own comment,
                // and `SpecsFor_RefusesAThingThisEngineDoesNotCarry`), `SeedOf`
                // throws on a seed that is not one, and `WaveHost.RunAsync`
                // throws `InvalidOperationException` when the wave scene or its
                // runner is missing - and `WaveDef.ForId` throws
                // `WaveCompositionException` for any id this build does not
                // author, which Campaign Select can now reach because it renders
                // whatever `config.waves` sends.
                //
                // Unguarded, all four escape `RunAsync` into `BootController`'s
                // outermost catch and leave the player on a Deploy screen whose
                // Start button no longer resumes anything, with nothing said.
                // That is the awaited-screen model's own failure class: an
                // exception between the screen and its continuation strands the
                // turn. The throw stays - conflating an Aberrant with tier 0 is
                // the worse answer - this is what makes it audible.
                WaveReport report;
                try
                {
                    report = await _play(
                        start.WaveId, SpecsFor(start.Deployment), SeedOf(start.Seed), inputEnabled: true);
                }
                catch (Exception error)
                {
                    // LOGGED WITH THE EXCEPTION AND SAID WITHOUT IT, which is
                    // `RunAsync`'s rule applied at the site the four throws
                    // above are actually thrown from - Phase 9 Task 21h.
                    // `PlayerMessage` no longer carries `Exception.Message`
                    // (see `ServerError`'s transport branch), and this is the
                    // one catch in this file where that message was the only
                    // record of the failure anywhere. Every other one wraps a
                    // server CALL, so what it catches is either a
                    // `BroodlineApiException` carrying a status and a body or
                    // one of the transport shapes `Retry.IsTransient` names -
                    // both of which are legible from the outside. This one
                    // wraps the WAVE, and what it catches is a
                    // `TimeoutException` from `WaveHost` or a
                    // `WaveCompositionException` from `WaveDef.ForId`, with
                    // nothing else anywhere going to write it down.
                    Diagnostics.Defect("the wave produced no report", error);
                    _notice(ServerError.From(error).PlayerMessage);
                    return false;
                }

                var submitted = await _outbox.SubmitWaveAsync(start.IssuanceId, report.ReplayBytes);
                if (submitted.Outcome != OutboxOutcome.Sent)
                {
                    // The SERVER's verdict is the only one that counts
                    // (`client_architecture` section 7), and a queued submission
                    // has not produced one. Showing Post-Wave off the local
                    // report would congratulate a player on a wave the server
                    // has not seen, and advancing the walk would derive the next
                    // beat from a snapshot that never moved.
                    _notice(FtueNotice.For(submitted.Outcome, FtueNotice.SubmitWave, submitted.Error));
                    return false;
                }

                var response = submitted.Response;
                var granted = response.Granted == null
                    ? new List<CreatureDto>()
                    : new List<CreatureDto>(response.Granted);

                // THE TWO FACTS NEITHER SCREEN'S OWN PAYLOAD CARRIES, and this
                // is the only place in the app that holds both. The verdict
                // screens state which wave was fought and how many creatures
                // went into it; `WaveSubmitResponse` has no wave id at all and
                // `WaveReport` has no deployment, so both are passed from the
                // issuance that started the wave rather than added to a shared
                // payload for one screen's chrome.
                var deployed = start.Deployment == null ? (int?)null : start.Deployment.Count;

                if (response.Result == PostWaveScreen.WinResult)
                {
                    var postWave = new PostWaveView();
                    await _flow.ShowAsync(postWave, resume => postWave.Bind(
                        response, granted, next: resume, wave: start.WaveId, deployed: deployed));
                    return true;
                }

                // bible 4.11: "Free retry. No paywall on failure, ever." The
                // retry resumes the turn and the walk re-derives to this same
                // beat, because nothing cleared - which is the retry.
                var defeat = new WaveDefeatView();
                await _flow.ShowAsync(defeat, resume => defeat.Bind(
                    report, granted, retry: resume, wave: start.WaveId, deployed: deployed));
                return true;
            }
            finally
            {
                // THE FIRST STATEMENT OF THE `finally`, IN ITS OWN try/catch,
                // which is `WaveHost.RunAsync`'s shape and `NameFounder
                // Async`'s: a throw out of the cleanup would REPLACE the
                // exception on its way out of the try and the original
                // failure would never be seen. Logged rather than swallowed.
                //
                // `!= null` RATHER THAN `?.`, AND THAT IS NOT STYLE.
                // `LaneStage` is a MonoBehaviour, and `?.` is a reference-null
                // test the compiler emits directly - it does not run
                // UnityEngine.Object's overloaded `==`, which is what reports
                // a DESTROYED object as null. On a stage whose GameObject has
                // gone (a scene change mid-beat, which this beat performs),
                // `?.` would call through to a dead native object; `!= null`
                // does not. `ShowLane` tests `_stage == null` for the same
                // reason.
                //
                // NO `await` HERE. `WaveHost`'s finally awaits its unload;
                // `Clear` is synchronous, and an await in this finally would
                // put a resumption point on the exception path of a beat for
                // no gain.
                //
                // WHAT THIS CALL DOES, AND WHAT IT DELIBERATELY NO LONGER
                // DOES - Phase 9 Task 21f. `Clear` releases the creature
                // bodies and leaves the rig stopped. It does NOT touch the
                // render texture, so the deploy screen this beat outlives
                // keeps the lane it was drawn with.
                //
                // THAT MATTERS ON THE EXITS THAT SHOW NO OTHER SCREEN FIRST,
                // WHICH IS FOUR OF THEM: the `CanDeploy` refusal above, the
                // `StartAsync` catch, the `_play` catch, and a submission the
                // outbox did not send. The win and defeat paths are safe only
                // because `PostWaveView`/`WaveDefeatView` are already up by
                // the time this runs; on the other four the player is left
                // looking at the deploy screen, and a wipe here emptied its
                // lane card. That is the "the lane is empty" the exit gate's
                // second walk reported, reproduced by backgrounding the
                // packaged app past `WaveHost.CompletionTimeoutSeconds`.
                //
                // No fourth member on `LaneStage` was needed after all: its
                // surface is still `Width`, `Height`, `OrthographicSize`,
                // `Create`, `Show`, `Clear`.
                try
                {
                    if (_stage != null) _stage.Clear();
                }
                catch (Exception error)
                {
                    Diagnostics.Defect("LaneStage.Clear threw", error);
                }
            }
        }

        /// The picture behind the deploy screen's lane card: this wave's
        /// dressing with the chosen creatures standing in their pockets.
        ///
        /// NULL IS A REAL ANSWER AND THE SCREEN HANDLES IT, exactly as
        /// `NameFounderAsync`'s portrait does. `_stage` is an optional
        /// constructor argument - `BootController` supplies one,
        /// `FtueDirectorTests` does not, and a batch-mode capture has no
        /// camera to run one - so `DeployView.Bind`'s `lane` is optional and
        /// `LanePreviewCard` falls back to its own --green-tint fill. A
        /// director without a stage shows exactly the screen it showed before.
        ///
        /// THE LOOKS ARE `WaveView.Build`'s, not this method's invention: a
        /// species and its two traits, with no growth, which is what the live
        /// wave assembles for the same creature. `CreatureLook.Growth01`
        /// stays at its default for the same reason `WaveView` leaves it
        /// there - a deployed creature is drawn at its full size in the lane.
        UnityEngine.Texture ShowLane(int waveId, DeployScreenModel deployment)
        {
            if (_stage == null) return null;

            var looks = new List<CreatureLook>(deployment.Slots.Count);
            var pockets = new List<int>(deployment.Slots.Count);
            foreach (var slot in deployment.Slots)
            {
                var creature = slot.Creature;
                looks.Add(new CreatureLook
                {
                    Species = creature.Species,
                    Trait1 = creature.Trait1,
                    Trait2 = creature.Trait2,
                });
                pockets.Add(slot.Pocket);
            }
            return _stage.Show(waveId, looks, pockets);
        }

        /// What the deploy screen states about the WAVE rather than about the
        /// deployment - see `DeployWaveFacts`, and note that every field is
        /// nullable because a readout that prints 0 for a number it was not
        /// told is a screen that lies.
        ///
        /// THE ENGINE IS READ HERE AND NOT IN THE VIEW. `Broodline.UI`
        /// references `Broodline.Model`, `Broodline.Net` and `Generated.Api`
        /// and deliberately not `Broodline.Sim` - `WaveScreensTests`' class
        /// comment draws the same line and says why ("no view and no view
        /// test needs an engine to render a defeat"). This director already
        /// has the engine, so the spawn count crosses the boundary as an int.
        ///
        /// `WaveDef.ForId` IS GUARDED BECAUSE IT THROWS BY DESIGN, and
        /// Campaign Select can reach an id this build does not author - the
        /// same hazard `_play`'s own try/catch names further up. A stat cell
        /// must not be the thing that strands the turn, so an unknown wave
        /// leaves `Foes` unstated and the loud failure keeps its own site.
        DeployWaveFacts FactsFor(int waveId)
        {
            var facts = new DeployWaveFacts();

            try
            {
                facts.Foes = WaveDef.ForId(waveId).Spawns.Length;
            }
            catch (WaveCompositionException)
            {
                // Left null: the cell states nothing rather than zero.
            }

            var snapshot = _snapshot();
            if (snapshot == null) return facts;

            if (snapshot.Balances != null && snapshot.Balances.TryGetValue(EnergyCurrency, out var energy))
            {
                facts.Energy = energy;
            }

            if (snapshot.Waves != null)
            {
                foreach (var wave in snapshot.Waves)
                {
                    if (wave == null || wave.Id != waveId) continue;
                    facts.RewardCurrency = wave.RewardCurrency;
                    facts.RewardAmount = wave.RewardAmount;
                    break;
                }
            }

            return facts;
        }

        /// The balance the handoff calls `GENE ENERGY`.
        ///
        /// "shards", WHICH IS THE WIRE'S KEY AND NOT A DISPLAY NOUN.
        /// `PlayerSnapshot.Balances` is a dictionary keyed by the currency
        /// ids `/v1/sync` sends, and the same word is what
        /// `WaveSummary.RewardCurrency` carries for the reward on the screen
        /// after this one. The handoff's label is editorial ("Gene energy");
        /// the key is the server's.
        const string EnergyCurrency = "shards";

        /// Add or remove one creature from the selection, in place. True when
        /// something changed and the screen needs redrawing.
        ///
        /// A REMOVAL IS ALWAYS ALLOWED AND THE MODEL SAYS WHAT IT COSTS.
        /// Taking the last creature out makes `DeployScreen.Build` answer
        /// `CanDeploy: false` with "Send at least one creature. A wave fought
        /// with nothing is not a wave." - which `DeployView` renders in coral
        /// directly above a greyed Start, and which the player undoes by
        /// tapping any row. Refusing the tap instead would make the screen
        /// silent about a rule it is perfectly able to state, and
        /// `WantedFor`'s own comment already says its number is "A FLOOR, NOT
        /// A PROMISE: `DeployScreen.Build` still refuses an illegal
        /// deployment" - it sizes the OPENING selection and nothing else.
        ///
        /// AN ADD PAST THE CAP IS REFUSED, WHICH IS THE ASYMMETRY. Over the
        /// cap the model's sentence is "A deployment is at most 5 creatures.",
        /// and unlike the floor there is no single tap that undoes it - the
        /// player would have to work out which of six to remove. `DeployView`
        /// already draws every undeployed row dimmed and disabled at the cap,
        /// so this branch is the second half of a rule the screen shows, not
        /// a silent no-op.
        ///
        /// ORDER IS PART OF THE REQUEST, so an add goes on the END: "Index ==
        /// deployment order", and `deploymentMatches` compares the replay's
        /// deployment against the stored one in order. Removing the creature
        /// in pocket 0 renumbers the rest, which is what the lane picture and
        /// the row badges then redraw - the screen showing the player exactly
        /// what the request will say.
        ///
        /// PUBLIC AND STATIC SO A HEADLESS TEST CAN REACH IT, which is the
        /// same reason `WantedFor` and `SpecsFor` are - "the parts a headless
        /// test can reach". The rest of the toggle loop cannot be reached
        /// that way: it lives inside `ScreenFlow.ShowAsync`'s bind callback
        /// and answers a `ClickEvent` that needs an attached Panel
        /// (`FtueDirectorTests`' class comment has the dead ends). This
        /// method is the whole of the RULE, and the rule is asymmetric, so it
        /// is the half that must not be trusted by inspection.
        public static bool Toggle(List<Guid> selected, Guid id)
        {
            if (selected.Remove(id)) return true;
            if (selected.Count >= DeployScreen.Cap) return false;
            selected.Add(id);
            return true;
        }

        // ---------------------------------------------------------------
        // Beat 4: name it
        // ---------------------------------------------------------------

        async Task NameFounderAsync()
        {
            var founder = FounderIn(_roster.Known);
            if (founder == null)
            {
                // `Derive` only answers `NameFounder` when the roster holds
                // one, so this is unreachable through `RunAsync` - kept
                // because a null here would be a `NullReferenceException`
                // inside an async void `Start`, which is the least
                // debuggable failure this file can produce.
                _namingOffered = true;
                return;
            }

            // THE CLEAR SPANS THE WHOLE BEAT, NOT THE TURN, AND THAT IS THE
            // DEFECT THIS `try` FIXES.
            //
            // `ScreenFlow.ShowAsync(screen, bind)` is `TurnAsync(screen,
            // bind, _host.Show, after: null)` - NOTHING HIDES THE VIEW WHEN
            // THE TURN RESOLVES. It stays presented until the next
            // `ScreenHost.Show`. And the turn's TaskCompletionSource is
            // completed with a plain `TrySetResult` (no
            // RunContinuationsAsynchronously), so the continuation runs
            // SYNCHRONOUSLY inside the `Button.clicked` handler - in the same
            // frame as the tap.
            //
            // Cleared there, on the accept path, the founder vanished from
            // the hero disc for the whole of `NameCreatureAsync` below,
            // leaving a dotted ring around an empty teal disc on a screen
            // still showing it. `PortraitStudio.Clear`'s own note says the
            // GL.Clear is deliberate: disabling the camera is not enough,
            // because `CreatureStage` is still displaying the same texture
            // reference. That is the ring-with-nothing-in-it 1157ed2 just
            // fixed on Pale's hero slot, arrived at from the other side.
            //
            // THE OPPOSITE HAZARD IS ALSO REAL AND THE PREVIOUS COMMENT
            // ARGUED ONLY IT: `Show` leaves a camera enabled and a creature
            // rotating every frame, and the skip path returns early, so a
            // Clear written below that branch would leave the studio running
            // for the rest of the session on the answer the player is most
            // likely to give. A `finally` around the whole beat gets both -
            // the `return` runs it, the accept path runs it after the round
            // trip rather than before - and covers the case the old placement
            // missed entirely: an exception out of `ShowAsync` skipped the
            // Clear and left the studio running with nothing to say so.
            //
            // `Show` IS INSIDE THE `try`, not above it. It builds a creature,
            // reparents it and enables a camera; a throw partway through
            // leaves a half-shown studio that only a `finally` above it can
            // still clean up. `WaveHost.RunAsync` keeps its one unthrowable
            // assignment outside and everything that can fail inside, which
            // is the same line drawn in the same place.
            try
            {
                // THE FOUNDER TURNS WHILE IT IS BEING NAMED - Phase 9 design
                // section 3.8's first of three hero moments. The studio
                // renders one creature into one RenderTexture; the screen
                // frames that texture in a `HeroSlot` and knows nothing about
                // the camera.
                //
                // NULL IS A REAL ANSWER AND THE SCREEN HANDLES IT. `_studio`
                // is an optional constructor argument - `BootController`
                // supplies one, `FtueDirectorTests` does not, and a
                // batch-mode capture has no camera to turn - so `Bind`'s
                // trailing `portrait` is optional and falls back to the baked
                // sprite stack. A director without a studio shows exactly the
                // screen it showed before.
                var portrait = _studio == null
                    ? null
                    : _studio.Show(founder.Species, founder.Trait1, founder.Trait2, 0f);

                var view = new FounderNamingView();
                var chosen = await _flow.ShowAsync<string>(view, resume => view.Bind(
                    founder,
                    FounderNamingScreen.DefaultFor(founder),
                    onName: name => resume(name),
                    onSkip: () => resume(null),
                    portrait: portrait));

                // SET BEFORE the await's outcome is acted on and regardless
                // of which answer came back: the beat has been offered either
                // way, and that is what the latch records.
                _namingOffered = true;

                if (chosen == null) return;

                var named = await _outbox.NameCreatureAsync(founder.CreatureId, chosen);
                if (named.Outcome != OutboxOutcome.Sent)
                {
                    // SAID, NOT STOPPED. bible 3.3 makes the name optional
                    // and renameable from the Roster, so a name that could
                    // not be sent is a disappointment rather than a broken
                    // session - and `creature/name` does not queue (it is
                    // server-authoritative), so there is nothing to wait for.
                    _notice(FtueNotice.For(named.Outcome, FtueNotice.NameFounder, named.Error));
                }
            }
            finally
            {
                // THE FIRST STATEMENT OF THE `finally`, IN ITS OWN
                // try/catch, which is `WaveHost.RunAsync`'s shape and is
                // there for `WaveHost`'s reason: a throw out of the cleanup
                // would REPLACE the exception on its way out of the try and
                // the original failure would never be seen. Logged rather
                // than swallowed silently.
                //
                // `!= null` RATHER THAN `?.`, AND THAT IS NOT STYLE.
                // `PortraitStudio` is a MonoBehaviour, and `?.` is a
                // reference-null test the compiler emits directly - it does
                // not run UnityEngine.Object's overloaded `==`, which is what
                // reports a DESTROYED object as null. On a studio whose
                // GameObject has gone (a scene change mid-beat), `?.` would
                // call through to a dead native object; `!= null` does not.
                // The `Show` line above tests `_studio == null` for the same
                // reason.
                //
                // NO `await` HERE. `WaveHost`'s finally awaits its unload;
                // `Clear` is synchronous, and an await in this finally would
                // put a resumption point on the exception path of a UI beat
                // for no gain.
                try
                {
                    if (_studio != null) _studio.Clear();
                }
                catch (Exception error)
                {
                    Diagnostics.Defect("PortraitStudio.Clear threw", error);
                }
            }
        }

        // ---------------------------------------------------------------
        // Beats 6 and 7: the guided splice, and the mutation
        // ---------------------------------------------------------------

        async Task<bool> SpliceAsync(PlayerSnapshot snapshot)
        {
            var facts = snapshot.Ftue ?? new FtueFacts();

            FtueStockResponse stock = null;
            if (!facts.TutorialStockGranted)
            {
                var granted = await _outbox.FtueSpliceStockAsync();
                if (granted.Outcome != OutboxOutcome.Sent)
                {
                    _notice(FtueNotice.For(granted.Outcome, FtueNotice.SpliceStock, granted.Error));
                    return false;
                }
                stock = granted.Response;

                // The grant is new roster membership, and `DeployScreen` and
                // the splice both read this cache - so re-read it rather than
                // splicing against a roster that predates the creatures being
                // spliced.
                if (!await LoadRosterAsync()) return false;
            }

            var pair = TutorialPair(stock, _roster.Known);
            if (pair == null)
            {
                _notice(FtueNotice.NoTutorialPair);
                return false;
            }

            var parentA = pair[0];
            var parentB = pair[1];
            var lockedOut = LockedOut(_roster.Known);

            // splice_confirm_spec section 6: "the named Founder is visibly
            // locked out." `SpliceChamberView` renders the set; THIS is what
            // makes it true - a pair that contains a Founder is refused
            // before the screen is built, so there is no path by which the
            // tutorial consumes the creature the player just named.
            if (lockedOut.Contains(parentA.CreatureId) || lockedOut.Contains(parentB.CreatureId))
            {
                _notice(FtueNotice.FounderInTutorialPair);
                return false;
            }

            // An empty lock: the player has chosen to carry nothing forward
            // by force, which is the only state the tutorial's chamber can
            // express today - there is no lock picker on `SpliceChamberView`.
            var locked = new SpliceLock();

            SplicePreviewResponse preview;
            try
            {
                preview = await SpliceScreen.PreviewAsync(
                    _api, parentA.CreatureId, parentB.CreatureId, locked);
            }
            catch (Exception error)
            {
                Diagnostics.Defect("the splice preview did not arrive", error);
                _notice(ServerError.From(error).PlayerMessage);
                return false;
            }

            var model = SpliceScreen.Build(parentA, parentB, preview);

            // THE STUDIO IS OPEN FOR THE WHOLE OF THE BEAT AND CLEARED ONCE,
            // IN A `finally`, WHICH IS `NameFounderAsync`'s SHAPE AND
            // `WaveHost.RunAsync`'s BEFORE IT. The reasoning is identical and
            // that method's comment carries it in full; what differs here is
            // that this beat has TWO turns rather than one, and the cleanup
            // is still one `finally` because `PortraitStudio.Show` calls
            // `Clear` itself on the way in - the second creature replaces the
            // first with no gap, so there is nothing to clean up BETWEEN the
            // turns and everything to clean up after the last one.
            //
            // THE FIRST `Show` IS INSIDE THE `try`, not above it: it builds a
            // creature, reparents it and enables a camera, so a throw partway
            // through leaves a half-shown studio that only a `finally` above
            // it can still clean up.
            //
            // THE KNOWN GAP IS UNCHANGED AND NOT MADE WORSE. Task 14b's fix
            // shortens the window in which a cleared studio is still on
            // screen without closing it, because `ScreenFlow.ShowAsync`
            // passes `after: null` and nothing hides a resolved view. Here
            // the last `Clear` runs after `CampaignAsync`, by which time
            // `ScreenHost.Show` has replaced the reveal twice over - so this
            // beat's cleanup lands further from its own screen than the
            // founder beat's does, not nearer.
            try
            {
                // THE PREDICTED HYBRID TURNS WHILE IT IS BEING DECIDED -
                // Phase 9 design section 3.8's second of three hero moments.
                //
                // `growth01: 0.3f` BECAUSE IT IS NOT BORN YET. The founder
                // turns at 0 (an adult) and the reveal below turns at 0 too;
                // a prediction is the one of the three that is explicitly
                // about a creature that does not exist, and the handoff draws
                // it smaller than the reveal's (112px against 186).
                //
                // THE TRAITS ARE THE FORECAST'S, IN THE SERVER'S OWN ORDER,
                // AND THE BRIEF'S `model.Forecast?.Trait1` DOES NOT EXIST.
                // `SpliceForecast` (`Generated/Api/BroodlineApiClient.cs
                // :2480-2493`) carries `combat2`, `instinct`, `mutation` and
                // `aberrant` - a LIST of outcomes and two probabilities, with
                // no trait fields on it at all. `PredictedTrait` reads that
                // list by index without re-sorting it, and falls back to the
                // parent slot the body comes from, which is what the brief's
                // own `?? parentA.Trait1` intended.
                var predicted = _studio == null ? null : _studio.Show(
                    parentA.Species,
                    PredictedTrait(model.Forecast, 0, parentA.Trait1),
                    PredictedTrait(model.Forecast, 1, parentB.Trait2),
                    0.3f);

                var chamber = new SpliceChamberView();
                await _flow.ShowAsync(chamber, resume =>
                    chamber.Bind(model, lockedOut, onSplice: resume, predicted: predicted));

                return await CommitAndRevealAsync(model, parentA, parentB, locked);
            }
            finally
            {
                // THE FIRST STATEMENT OF THE `finally`, IN ITS OWN
                // try/catch - `WaveHost.RunAsync`'s shape, for
                // `WaveHost`'s reason: a throw out of the cleanup would
                // REPLACE the exception on its way out of the try and the
                // original failure would never be seen.
                //
                // `!= null` RATHER THAN `?.`, AND THAT IS NOT STYLE.
                // `PortraitStudio` is a MonoBehaviour and `?.` is a
                // reference-null test the compiler emits directly - it does
                // not run UnityEngine.Object's overloaded `==`, which is what
                // reports a DESTROYED object as null. `NameFounderAsync`'s
                // own note has the long form.
                try
                {
                    if (_studio != null) _studio.Clear();
                }
                catch (Exception error)
                {
                    Diagnostics.Defect("PortraitStudio.Clear threw", error);
                }
            }
        }

        /// One published outcome's trait, by position, or a fallback.
        ///
        /// BY POSITION AND NOT BY PROBABILITY, WHICH IS THE WHOLE POINT.
        /// `combat2` arrives ordered by `merged()`
        /// (`services/api/src/splice/distribution.ts`) and re-sorting it here
        /// would be this client deciding which outcome is likeliest - the one
        /// thing `SpliceConfirmTests` exists to forbid. Read in the order it
        /// was sent, and where it is shorter than two, the parent slot the
        /// body is coming from.
        static string PredictedTrait(SpliceForecast forecast, int index, string fallback)
        {
            if (forecast == null || forecast.Combat2 == null) return fallback;

            var i = 0;
            foreach (var outcome in forecast.Combat2)
            {
                if (i++ != index) continue;
                return string.IsNullOrEmpty(outcome.Trait) ? fallback : outcome.Trait;
            }
            return fallback;
        }

        /// The rest of beat 6 and the whole of beat 7, split out so that
        /// `SpliceAsync`'s `try`/`finally` around the studio reads as one
        /// thing rather than wrapping sixty lines of round trips.
        async Task<bool> CommitAndRevealAsync(
            SpliceScreenModel model, CreatureDto parentA, CreatureDto parentB, SpliceLock locked)
        {
            // splice_confirm_spec section 4's ordering, and the reason
            // `SpliceChamberView.Bind` carries one callback and no per-dialog
            // handles: "wiring ConfirmDialog in front of this callback
            // belongs to whatever screen composes the two." This is that
            // composer. `model.Dialogs` is Standard first, then one per
            // Founder parent - empty of Founder dialogs here by construction,
            // and walked in order anyway so the tutorial and the live game
            // run the same code.
            foreach (var dialog in model.Dialogs)
            {
                var confirmed = await ConfirmAsync(model, dialog);
                if (!confirmed) return true;    // Cancel is a real answer; the walk re-derives.
            }

            // `bodyFrom` HAS NO DEFAULT, by `SpliceScreen.CommitAsync`'s own
            // argument check: "the body choice is the player's only
            // controlled lever". There is no body picker on the chamber yet
            // (Tasks 15/16 built none), so the tutorial sends parent A's
            // species. THAT IS AN OWED CONTROL, not a design choice - the
            // task report books it.
            var committed = await _outbox.SpliceCommitAsync(
                parentA.CreatureId, parentB.CreatureId, locked, parentA.Species);
            if (committed.Outcome != OutboxOutcome.Sent)
            {
                _notice(FtueNotice.For(committed.Outcome, FtueNotice.SpliceCommit, committed.Error));
                return false;
            }

            // THE TREE IS FETCHED HERE, BEFORE THE REVEAL, and it is the same
            // response beat 8 renders. Two things come out of one call: the
            // mutation flag, which `SpliceCommitResponse` deliberately does
            // not carry (routes/splice.ts withholds it while design section
            // 10 defers Aberrant content), and the tree itself. So the flag
            // the reveal celebrates still arrives through a server response
            // and is never inferred on the client.
            LineageResponse lineage = null;
            try
            {
                lineage = await _api.LineageAsync();
            }
            catch (Exception error)
            {
                // The splice HAPPENED. A failed tree read must not look like
                // a failed splice, so this says so and still shows the
                // reveal - unmutated, because the only source of that fact
                // is the read that just failed.
                Diagnostics.Defect("the lineage read after a commit failed", error);
                _notice(ServerError.From(error).PlayerMessage);
            }

            var child = committed.Response.Child;
            var mutated = MutatedIn(lineage, child == null ? Guid.Empty : child.CreatureId);

            // THE CHILD TURNS ON THE SCREEN BUILT TO CELEBRATE IT - design
            // section 3.8's third hero moment, and the one the whole first
            // hour ends on. `growth01: 0f` because it is a finished creature
            // now; `Show` clears the predicted hybrid on its way in, so the
            // band never holds two.
            //
            // A NULL CHILD KEEPS THE BAKED STACK, which is the same fallback
            // the other two turns take: `SpliceRevealView.Bind` branches on a
            // null portrait and `Child` is required on the wire anyway.
            var portrait = _studio == null || child == null ? null
                : _studio.Show(child.Species, child.Trait1, child.Trait2, 0f);

            var reveal = new SpliceRevealView();
            await _flow.ShowAsync(reveal, resume =>
                reveal.Bind(committed.Response, parentA, parentB, mutated, next: resume,
                    portrait: portrait));

            // splice_confirm_spec section 5: "Then show the lineage." Shown
            // from the response already in hand rather than re-fetched, so
            // the tree the player sees is the tree the mutation flag came
            // from.
            await LineageAsync(lineage);
            await CampaignAsync();

            // `false` MEANS "STOP THE WALK", AND IT IS NOT A CELEBRATION.
            // Session one does end on the tree, but control does not stop
            // here - it stops wherever `CampaignAsync` gave up, one line up,
            // and that is a failure every time. See the same correction at
            // `WalkAsync`'s two post-`CampaignAsync` returns.
            return false;
        }

        /// One confirmation, as a sheet that answers.
        ///
        /// `Named` for a Founder dialog and `Standard` otherwise, chosen by
        /// REFERENCE against the model's own `FounderDialogs` rather than by
        /// position or by reading the button text. `ConfirmDialog.Named` is
        /// what enforces splice_confirm_spec section 4's "no destructive
        /// default" and its no-dismiss-by-scrim, and deciding which one to
        /// build from an ordering or a string prefix would put both
        /// guarantees on something a copy edit can move.
        Task<bool> ConfirmAsync(SpliceScreenModel model, SpliceDialog dialog)
        {
            var isFounderDialog = false;
            foreach (var founder in model.FounderDialogs)
            {
                if (ReferenceEquals(founder, dialog)) { isFounderDialog = true; break; }
            }

            return _flow.ShowSheetAsync<bool>(resume => isFounderDialog
                ? ConfirmDialog.Named(dialog, confirm: () => resume(true), cancel: () => resume(false))
                : ConfirmDialog.Standard(dialog, confirm: () => resume(true), cancel: () => resume(false)));
        }

        // ---------------------------------------------------------------
        // Beat 8: the tree
        // ---------------------------------------------------------------

        async Task LineageAsync(LineageResponse lineage)
        {
            if (lineage == null)
            {
                try
                {
                    lineage = await _api.LineageAsync();
                }
                catch (Exception error)
                {
                    Diagnostics.Defect("the lineage read failed", error);
                    _notice(ServerError.From(error).PlayerMessage);
                    return;
                }
            }

            var founder = FounderIn(_roster.Known);
            var highlight = founder == null ? Guid.Empty : founder.CreatureId;

            var view = new LineageView();
            await _flow.ShowAsync(view, resume => view.Bind(lineage, highlight, next: resume));
        }

        // ---------------------------------------------------------------
        // After the first hour
        // ---------------------------------------------------------------

        /// design section 5.2: "Campaign Select is how a player reaches wave
        /// 6 after wave 2 - without it the first hour ends at beat 8 with no
        /// route to the designed loss, which the same list places in this
        /// phase."
        async Task CampaignAsync()
        {
            while (true)
            {
                // THESE TWO USED TO BE SILENT, AND THEY ARE THE REASON THE
                // RECOVERY SCREEN CAN TRUST `_lastNotice` - Phase 9 Task 21g.
                // A bare `return` here ends `CampaignAsync`, which ends
                // `WalkAsync`, which is the whole walk; with nothing said,
                // the recovery screen would have shown whatever sentence the
                // LAST beat happened to leave behind. A wrong explanation is
                // worse than a vague one. See the two constants for why they
                // are two sentences and not one.
                var snapshot = _snapshot();
                if (snapshot == null)
                {
                    _notice(FtueNotice.CampaignUnreadable);
                    return;
                }

                var waves = WaveIdsIn(snapshot);
                if (waves.Count == 0)
                {
                    _notice(FtueNotice.NoWavesAuthored);
                    return;
                }

                var view = new CampaignSelectView();
                var picked = await _flow.ShowAsync<int>(view, resume =>
                    view.Bind(waves, snapshot.HighestWaveCleared, onPick: resume));

                if (!await LoadRosterAsync()) return;
                if (!await FightAsync(picked)) return;
                if (!await ResyncAsync()) return;
            }
        }

        // ---------------------------------------------------------------
        // Plumbing
        // ---------------------------------------------------------------

        async Task<bool> LoadRosterAsync()
        {
            var error = await _roster.LoadAsync(_api);
            if (error == null) return true;

            // THE `ServerError` RATHER THAN AN EXCEPTION, because this is the
            // one of the nine that never holds one - `RosterScreen.LoadAsync`
            // catches it and hands back the classified refusal. Its
            // `ToString()` is where `Diagnostic` surfaces, so the transport
            // message this task stopped showing a player still reaches the log.
            Diagnostics.Defect("the roster would not load", error);
            _notice(error.PlayerMessage);
            return false;
        }

        /// Phase 9 design §2.1 part 3. Shown BEFORE the beat is derived, so
        /// `FightAsync` never meets an all-committed roster. Returns false only
        /// when the forfeit did not free the roster - the server refused, or
        /// the call failed - in which case the notice says so and the walk
        /// stops on a toast rather than a blank screen.
        async Task<bool> ForfeitIfLockedAsync()
        {
            if (!NeedsForfeit(_roster.Known)) return true;

            await _flow.ShowSheetAsync<bool>(resume =>
                new AbandonedWaveSheet(onForfeit: () => resume(true)));

            try
            {
                await _api.AbandonWaveAsync();
            }
            catch (Exception error)
            {
                Diagnostics.Defect("wave/abandon failed", error);
                _notice(ServerError.From(error).PlayerMessage);
                return false;
            }

            if (!await LoadRosterAsync()) return false;
            if (NeedsForfeit(_roster.Known))
            {
                _notice(FtueNotice.ForfeitFailed);
                return false;
            }
            return true;
        }

        async Task<bool> ResyncAsync()
        {
            try
            {
                await _resync();
                return true;
            }
            catch (Exception error)
            {
                Diagnostics.Defect("the resync failed", error);
                _notice(ServerError.From(error).PlayerMessage);
                return false;
            }
        }

        // ---------------------------------------------------------------
        // Pure helpers - the parts a headless test can reach
        // ---------------------------------------------------------------

        /// What a failed `wave/start` is told to the player.
        ///
        /// PURE AND PUBLIC FOR ONE REASON: it is the whole of the policy this
        /// task changed, and `FightAsync` is private and unreachable from
        /// EditMode (this suite's header records why - the walk advances on a
        /// `Button.clicked` that needs an attached `Panel`). Left inside the
        /// `catch`, the decision would be read and never executed by a test.
        /// Out here, `FtueDirectorTests` drives both arms.
        ///
        /// The two arms are the two kinds of failure, and they are
        /// `Retry.IsTransient`'s division, not a second one:
        ///
        ///   - The server never answered, or answered 5xx. It may answer next
        ///     time, nothing was spent, and the remedy is to tap Start again -
        ///     so this build's own sentence, which names that remedy.
        ///   - The server answered with a verdict. `ServerError` already owns
        ///     what to say about those and says the SERVER's own sentence,
        ///     which solo_execution 6.2 makes the better one. Substituting
        ///     "tap Start again" for `wave_locked` would be this client
        ///     inventing a remedy for a refusal that has none.
        public static string StartFailureNotice(Exception error)
        {
            return Retry.IsTransient(error)
                ? FtueNotice.StartWaveUnreached
                : ServerError.From(error).PlayerMessage;
        }

        /// Whether a failed `wave/start` leaves the walk able to go on.
        ///
        /// True means `RunAsync` loops and puts the deploy screen back with a
        /// live Start button; false ends the walk on the notice. Same
        /// division as `StartFailureNotice`, and deliberately the same
        /// predicate rather than a parallel one that could drift: a sentence
        /// that says "tap Start again" beside a screen that has gone is worse
        /// than either half alone.
        public static bool StartLeavesTheWalkAlive(Exception error)
        {
            return Retry.IsTransient(error);
        }

        /// How many creatures to pre-select for a wave.
        ///
        /// Waves 1 and 2 are design section 6.1's authored table: "expected
        /// roster 2" and "expected roster 3". Every later wave is the
        /// deployment CAP, because nothing authors an expected roster for
        /// them and sending fewer than the player owns to the wave that is
        /// designed to be lost (bible 9.3, wave 6) would make the lesson land
        /// for the wrong reason.
        ///
        /// A FLOOR, NOT A PROMISE: `DeployScreen.Build` still refuses an
        /// illegal deployment, and a player who owns fewer simply sends
        /// fewer.
        public static int WantedFor(int waveId)
        {
            if (waveId <= 1) return 2;
            if (waveId == 2) return 3;
            return DeployScreen.Cap;
        }

        /// Whether any known creature is committed to a live issuance - the
        /// state Phase 8's eyes-on pass found in its first minute, in which
        /// `FightAsync` would find nothing to deploy and the walk would end.
        public static bool NeedsForfeit(IReadOnlyList<CreatureDto> known)
        {
            if (known == null) return false;
            foreach (var creature in known)
                if (creature != null && creature.CommittedTo != null) return true;
            return false;
        }

        /// The issuance's deployment, as the engine's own structs.
        ///
        /// ORDER IS PRESERVED AND NEVER SORTED, for the reason
        /// `DeployScreen.Build` states: "Index == deployment order", and
        /// `deploymentMatches` compares the replay's deployment against the
        /// stored one in order.
        ///
        /// A NULL TIER ON A REAL TRAIT THROWS rather than becoming zero.
        /// `PlayerSnapshot`'s own rule is that "zero would sort and display
        /// as 'less than tier I'", and `data_model` section 2 makes a null
        /// tier on a trait an Aberrant - a different thing entirely. No
        /// authored content can produce one (design section 10 defers
        /// Aberrant traits out of this phase, so the bundle authors none),
        /// which is exactly why that impossible state is made loud instead
        /// of simulated as a weaker creature.
        ///
        /// A NULL TIER ON `Trait.None` IS NOT THAT STATE. An absent trait
        /// has no coverage to null out - `Trait.None` at tier 0 is the
        /// engine's own coherent spelling of an empty combat slot, and the
        /// server sends it exactly that way: `ftue/founder.ts`'s Founder
        /// (Hollow, no traits at all) is granted on the first wave clear and
        /// deployed into the second wave by design (`wave2TrioDeployment` in
        /// the api test suite), so this is authored, tested content, not a
        /// theoretical shape. Treating it as an Aberrant stopped the first
        /// hour at that fight; `Tier` now tells the two apart by trait,
        /// which is the only field that distinguishes them.
        public static CreatureSpec[] SpecsFor(ICollection<CreatureSpecDto> deployment)
        {
            if (deployment == null) throw new ArgumentNullException(nameof(deployment));

            var specs = new CreatureSpec[deployment.Count];
            var i = 0;
            foreach (var dto in deployment)
            {
                if (dto == null) throw new ArgumentException("The issuance carried a null deployment slot.", nameof(deployment));
                specs[i++] = new CreatureSpec
                {
                    Species = Parse<Species>(dto.Species, "species"),
                    Trait1 = Parse<Trait>(dto.Trait1, "trait"),
                    Tier1 = Tier(dto.Tier1, dto.Trait1),
                    Trait2 = Parse<Trait>(dto.Trait2, "trait"),
                    Tier2 = Tier(dto.Tier2, dto.Trait2),
                    Instinct = Parse<Instinct>(dto.Instinct, "instinct"),
                    Pocket = dto.Pocket,
                };
            }
            return specs;
        }

        static TEnum Parse<TEnum>(string value, string what) where TEnum : struct
        {
            if (!Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed))
            {
                throw new ArgumentException(
                    "The issuance named a " + what + " this engine does not carry: '" + value +
                    "'. The bundle and the client build disagree.");
            }
            return parsed;
        }

        static int Tier(int? tier, string trait)
        {
            if (tier != null) return tier.Value;

            // Case-insensitively, matching how Parse<TEnum> above already reads
            // the wire's trait names (ignoreCase: true) - a null tier on
            // `Trait.None` is an absence, not an Aberrant, and 0 is its coherent
            // engine representation. See the comment above this method.
            if (string.Equals(trait, "None", StringComparison.OrdinalIgnoreCase)) return 0;

            throw new ArgumentException(
                "The issuance carried '" + trait + "' with no coverage tier. A null tier is an Aberrant " +
                "(data_model 2), not a tier of zero, and this phase's bundle authors none.");
        }

        /// The seed, as the engine's PRNG selector.
        ///
        /// The wire carries it as a STRING because it is a 63-bit integer and
        /// JSON numbers are doubles - `issueWave` masks the draw to 63 bits
        /// precisely so it survives Postgres's signed bigint, and parsing it
        /// as a double here would silently round it and simulate a different
        /// wave than the server re-simulates.
        public static ulong SeedOf(string seed)
        {
            if (!ulong.TryParse(seed, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                throw new ArgumentException("The issuance's seed is not a seed: '" + seed + "'.", nameof(seed));
            }
            return parsed;
        }

        /// The two creatures beat 6 splices.
        ///
        /// THE STOCK GRANT'S OWN RESPONSE IS THE SOURCE when there is one -
        /// `POST /v1/ftue/splice-stock` hands back exactly the creatures it
        /// created, so no "which of these is new" inference is needed.
        /// (task-17-brief.md reaches for `c.AcquiredAfter(stockGrant)`, which
        /// is not a member of `CreatureDto` and could not be: nothing on the
        /// wire carries an acquisition time.)
        ///
        /// The fallback is for a RESUMED session - the grant landed on a
        /// previous run, so `ftue.tutorialStockGranted` is true and no
        /// response is in hand. Then it is the first two non-Founder,
        /// uncommitted creatures in roster order, which is the same set the
        /// grant produced for a player who has not spliced yet.
        ///
        /// FOUNDERS ARE NEVER RETURNED, from either branch. That is
        /// splice_confirm_spec section 6 and design section 5's beat 6:
        /// "using two PROVIDED creatures - never the named one."
        public static IReadOnlyList<CreatureDto> TutorialPair(
            FtueStockResponse stock, IReadOnlyList<CreatureDto> roster)
        {
            var fromStock = Eligible(stock == null ? null : stock.Creatures);
            if (fromStock.Count >= 2) return new[] { fromStock[0], fromStock[1] };

            var fromRoster = Eligible(roster);
            if (fromRoster.Count >= 2) return new[] { fromRoster[0], fromRoster[1] };

            return null;
        }

        static List<CreatureDto> Eligible(IEnumerable<CreatureDto> creatures)
        {
            var eligible = new List<CreatureDto>();
            if (creatures == null) return eligible;
            foreach (var creature in creatures)
            {
                if (creature == null || creature.IsFounder || creature.CommittedTo != null) continue;
                eligible.Add(creature);
            }
            return eligible;
        }

        /// Every Founder in the roster - splice_confirm_spec section 6's
        /// "visibly locked out". Every Founder, not just the named one:
        /// bible 3.3 flags the first five, and the tutorial has no business
        /// consuming any of them.
        public static ISet<Guid> LockedOut(IReadOnlyList<CreatureDto> roster)
        {
            var locked = new HashSet<Guid>();
            if (roster == null) return locked;
            foreach (var creature in roster)
            {
                if (creature != null && creature.IsFounder) locked.Add(creature.CreatureId);
            }
            return locked;
        }

        /// Whether the server's tree says this child mutated. False when the
        /// tree could not be read or does not carry the child - an absent
        /// fact is not a mutation, and celebrating one that did not happen is
        /// the failure `routes/splice.ts` withholds the commit flag to avoid.
        public static bool MutatedIn(LineageResponse lineage, Guid childId)
        {
            if (lineage == null || lineage.Nodes == null || childId == Guid.Empty) return false;
            foreach (var node in lineage.Nodes)
            {
                if (node != null && node.CreatureId == childId) return node.Mutated;
            }
            return false;
        }

        public static CreatureDto FounderIn(IReadOnlyList<CreatureDto> roster)
        {
            if (roster == null) return null;
            foreach (var creature in roster)
            {
                if (creature != null && creature.IsFounder) return creature;
            }
            return null;
        }

        /// The bundle's authored wave ids, as `/v1/sync` sent them and in
        /// that order - `config.waves` IS the campaign's order, and
        /// re-sorting it here would be the client deciding something the
        /// bundle decided.
        public static IReadOnlyList<int> WaveIdsIn(PlayerSnapshot snapshot)
        {
            var ids = new List<int>();
            if (snapshot == null || snapshot.Waves == null) return ids;
            foreach (var wave in snapshot.Waves)
            {
                if (wave != null) ids.Add(wave.Id);
            }
            return ids;
        }
    }
}
