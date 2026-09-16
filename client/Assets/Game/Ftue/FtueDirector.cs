using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Broodline.Api;
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
        public const string LoadRoster = "Your roster could not be loaded. Try again.";
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
        readonly Action<string> _notice;

        readonly RosterScreen _roster = new RosterScreen();

        bool _namingOffered;

        public FtueDirector(
            BroodlineApiClient api,
            OutboxClient outbox,
            PlayWave play,
            ScreenFlow flow,
            Func<PlayerSnapshot> snapshot,
            Func<Task> resync,
            Action<string> notice)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
            _play = play ?? throw new ArgumentNullException(nameof(play));
            _flow = flow ?? throw new ArgumentNullException(nameof(flow));
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _resync = resync ?? throw new ArgumentNullException(nameof(resync));
            _notice = notice ?? throw new ArgumentNullException(nameof(notice));
        }

        // ---------------------------------------------------------------
        // The walk
        // ---------------------------------------------------------------

        public async Task RunAsync()
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
                    _notice(FtueNotice.LoadRoster);
                    return;
                }

                if (!await LoadRosterAsync().ConfigureAwait(false)) return;

                var beat = Ftue.Derive(snapshot, _roster.Known);

                // The skip latch - see the class comment. Applied AFTER the
                // derivation rather than folded into it, because `Derive` is
                // pure and knows nothing about this session.
                if (beat == Beat.NameFounder && _namingOffered) beat = Beat.SecondWave;

                switch (beat)
                {
                    case Beat.ColdOpen:
                        if (!await FightAsync(1).ConfigureAwait(false)) return;
                        break;

                    case Beat.NameFounder:
                        await NameFounderAsync().ConfigureAwait(false);
                        break;

                    case Beat.SecondWave:
                        if (!await FightAsync(2).ConfigureAwait(false)) return;
                        break;

                    case Beat.GuidedSplice:
                        if (!await SpliceAsync(snapshot).ConfigureAwait(false)) return;
                        break;

                    case Beat.Reveal:
                        // Unreachable: `Derive` never answers it, and
                        // `FtueTests.RevealIsNeverDerived_ItIsShownOffACommit`
                        // sweeps the whole input space to say so. The reveal
                        // is shown inside `SpliceAsync`, off its own commit.
                        // Listed rather than defaulted so that adding it to
                        // the derivation is a visible decision here.
                        goto case Beat.Lineage;

                    case Beat.Lineage:
                        await LineageAsync(lineage: null).ConfigureAwait(false);
                        await CampaignAsync().ConfigureAwait(false);
                        return;

                    case Beat.Done:
                        await CampaignAsync().ConfigureAwait(false);
                        return;
                }

                if (!await ResyncAsync().ConfigureAwait(false)) return;
            }
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

            var deployView = new DeployView();
            await _flow.ShowAsync(deployView, resume => deployView.Bind(deployment, onStart: resume))
                .ConfigureAwait(false);

            WaveStartResponse start;
            try
            {
                start = await DeployScreen.StartAsync(_api, deployment).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                _notice(ServerError.From(error).PlayerMessage);
                return false;
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
            // `Debug.LogError` and leave the player on a Deploy screen whose
            // Start button no longer resumes anything, with nothing said.
            // That is the awaited-screen model's own failure class: an
            // exception between the screen and its continuation strands the
            // turn. The throw stays - conflating an Aberrant with tier 0 is
            // the worse answer - this is what makes it audible.
            WaveReport report;
            try
            {
                report = await _play(
                    start.WaveId, SpecsFor(start.Deployment), SeedOf(start.Seed), inputEnabled: true)
                    .ConfigureAwait(false);
            }
            catch (Exception error)
            {
                _notice(ServerError.From(error).PlayerMessage);
                return false;
            }

            var submitted = await _outbox.SubmitWaveAsync(start.IssuanceId, report.ReplayBytes)
                .ConfigureAwait(false);
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

            if (response.Result == PostWaveScreen.WinResult)
            {
                var postWave = new PostWaveView();
                await _flow.ShowAsync(postWave, resume => postWave.Bind(response, granted, next: resume))
                    .ConfigureAwait(false);
                return true;
            }

            // bible 4.11: "Free retry. No paywall on failure, ever." The
            // retry resumes the turn and the walk re-derives to this same
            // beat, because nothing cleared - which is the retry.
            var defeat = new WaveDefeatView();
            await _flow.ShowAsync(defeat, resume => defeat.Bind(report, granted, retry: resume))
                .ConfigureAwait(false);
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

            var view = new FounderNamingView();
            var chosen = await _flow.ShowAsync<string>(view, resume => view.Bind(
                founder,
                FounderNamingScreen.DefaultFor(founder),
                onName: name => resume(name),
                onSkip: () => resume(null))).ConfigureAwait(false);

            // SET BEFORE the await's outcome is acted on and regardless of
            // which answer came back: the beat has been offered either way,
            // and that is what the latch records.
            _namingOffered = true;

            if (chosen == null) return;

            var named = await _outbox.NameCreatureAsync(founder.CreatureId, chosen).ConfigureAwait(false);
            if (named.Outcome != OutboxOutcome.Sent)
            {
                // SAID, NOT STOPPED. bible 3.3 makes the name optional and
                // renameable from the Roster, so a name that could not be
                // sent is a disappointment rather than a broken session -
                // and `creature/name` does not queue (it is
                // server-authoritative), so there is nothing to wait for.
                _notice(FtueNotice.For(named.Outcome, FtueNotice.NameFounder, named.Error));
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
                var granted = await _outbox.FtueSpliceStockAsync().ConfigureAwait(false);
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
                if (!await LoadRosterAsync().ConfigureAwait(false)) return false;
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
                    _api, parentA.CreatureId, parentB.CreatureId, locked).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                _notice(ServerError.From(error).PlayerMessage);
                return false;
            }

            var model = SpliceScreen.Build(parentA, parentB, preview);

            var chamber = new SpliceChamberView();
            await _flow.ShowAsync(chamber, resume => chamber.Bind(model, lockedOut, onSplice: resume))
                .ConfigureAwait(false);

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
                var confirmed = await ConfirmAsync(model, dialog).ConfigureAwait(false);
                if (!confirmed) return true;    // Cancel is a real answer; the walk re-derives.
            }

            // `bodyFrom` HAS NO DEFAULT, by `SpliceScreen.CommitAsync`'s own
            // argument check: "the body choice is the player's only
            // controlled lever". There is no body picker on the chamber yet
            // (Tasks 15/16 built none), so the tutorial sends parent A's
            // species. THAT IS AN OWED CONTROL, not a design choice - the
            // task report books it.
            var committed = await _outbox.SpliceCommitAsync(
                parentA.CreatureId, parentB.CreatureId, locked, parentA.Species).ConfigureAwait(false);
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
                lineage = await _api.LineageAsync().ConfigureAwait(false);
            }
            catch (Exception error)
            {
                // The splice HAPPENED. A failed tree read must not look like
                // a failed splice, so this says so and still shows the
                // reveal - unmutated, because the only source of that fact
                // is the read that just failed.
                _notice(ServerError.From(error).PlayerMessage);
            }

            var child = committed.Response.Child;
            var mutated = MutatedIn(lineage, child == null ? Guid.Empty : child.CreatureId);

            var reveal = new SpliceRevealView();
            await _flow.ShowAsync(reveal, resume =>
                reveal.Bind(committed.Response, parentA, parentB, mutated, next: resume))
                .ConfigureAwait(false);

            // splice_confirm_spec section 5: "Then show the lineage." Shown
            // from the response already in hand rather than re-fetched, so
            // the tree the player sees is the tree the mutation flag came
            // from.
            await LineageAsync(lineage).ConfigureAwait(false);
            await CampaignAsync().ConfigureAwait(false);
            return false;    // the walk is over; session one ended on the tree.
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
                    lineage = await _api.LineageAsync().ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    _notice(ServerError.From(error).PlayerMessage);
                    return;
                }
            }

            var founder = FounderIn(_roster.Known);
            var highlight = founder == null ? Guid.Empty : founder.CreatureId;

            var view = new LineageView();
            await _flow.ShowAsync(view, resume => view.Bind(lineage, highlight, next: resume))
                .ConfigureAwait(false);
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
                var snapshot = _snapshot();
                if (snapshot == null) return;

                var waves = WaveIdsIn(snapshot);
                if (waves.Count == 0) return;

                var view = new CampaignSelectView();
                var picked = await _flow.ShowAsync<int>(view, resume =>
                    view.Bind(waves, snapshot.HighestWaveCleared, onPick: resume)).ConfigureAwait(false);

                if (!await LoadRosterAsync().ConfigureAwait(false)) return;
                if (!await FightAsync(picked).ConfigureAwait(false)) return;
                if (!await ResyncAsync().ConfigureAwait(false)) return;
            }
        }

        // ---------------------------------------------------------------
        // Plumbing
        // ---------------------------------------------------------------

        async Task<bool> LoadRosterAsync()
        {
            var error = await _roster.LoadAsync(_api).ConfigureAwait(false);
            if (error == null) return true;
            _notice(error.PlayerMessage);
            return false;
        }

        async Task<bool> ResyncAsync()
        {
            try
            {
                await _resync().ConfigureAwait(false);
                return true;
            }
            catch (Exception error)
            {
                _notice(ServerError.From(error).PlayerMessage);
                return false;
            }
        }

        // ---------------------------------------------------------------
        // Pure helpers - the parts a headless test can reach
        // ---------------------------------------------------------------

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

        /// The issuance's deployment, as the engine's own structs.
        ///
        /// ORDER IS PRESERVED AND NEVER SORTED, for the reason
        /// `DeployScreen.Build` states: "Index == deployment order", and
        /// `deploymentMatches` compares the replay's deployment against the
        /// stored one in order.
        ///
        /// A NULL TIER THROWS rather than becoming zero. `PlayerSnapshot`'s
        /// own rule is that "zero would sort and display as 'less than tier
        /// I'", and `data_model` section 2 makes a null tier an Aberrant -
        /// a different thing entirely. No authored content can produce one
        /// (design section 10 defers Aberrant traits out of this phase, so
        /// the bundle authors none), which is exactly why the impossible
        /// state is made loud instead of simulated as a weaker creature.
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
