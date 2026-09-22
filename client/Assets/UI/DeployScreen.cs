using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Broodline.Api;

namespace Broodline.UI
{
    /// One chosen creature and the pocket it fights from.
    public sealed class DeploymentSlot
    {
        public CreatureDto Creature { get; internal set; }
        public int Pocket { get; internal set; }
    }

    /// What the deploy screen states about the WAVE, as opposed to about the
    /// deployment. Phase 9 Task 17.
    ///
    /// A SEPARATE OBJECT BECAUSE `DeployScreenModel` IS A PROTOCOL GUARD AND
    /// THIS IS DECORATION. That model exists so "an ILLEGAL deployment cannot
    /// be turned into a request" - `ToRequest` is the only way out of it and
    /// every one of its properties is `internal set`, so nothing outside
    /// `Broodline.UI` can build one except through `DeployScreen.Build`.
    /// Four display facts have no business widening that surface, and the
    /// director (in `Broodline.Game`) could not write them into it anyway.
    /// So they travel beside the model rather than inside it, and `Bind`
    /// takes both.
    ///
    /// EVERY FIELD IS NULLABLE AND NULL MEANS "NOT KNOWN", NOT ZERO. A
    /// screen that printed `0` for a balance it had not been told is a screen
    /// that lies about the player's wallet; a blank readout is merely quiet.
    /// `DeployScreen.EnergyValue`, `FoesStatValue` and `RewardValue` are
    /// where that is enforced, once each. A null `DeployWaveFacts` is the
    /// same as an empty one, which is what `DeployView.Bind` does with it.
    public sealed class DeployWaveFacts
    {
        /// `PlayerSnapshot.Balances["shards"]` - the handoff's `GENE ENERGY`.
        public int? Energy;

        /// `WaveDef.ForId(n).Spawns.Length`, PASSED IN AS AN INT. The UI
        /// assembly references `Broodline.Model`, `Broodline.Net` and
        /// `Generated.Api` and deliberately not `Broodline.Sim` - the same
        /// line `WaveScreensTests`' class comment draws and for the same
        /// reason ("no view and no view test needs an engine"). The director
        /// has the engine; this is the number it read.
        public int? Foes;

        /// `WaveSummary.RewardCurrency` / `.RewardAmount` from
        /// `config.waves`, as the snapshot cached them.
        public string RewardCurrency;
        public int? RewardAmount;

        /// The wave's own sentence, when something authors one. Null falls
        /// back to `DeployScreen.DefaultBrief`, which is the state of every
        /// wave in this build - see that constant.
        public string Brief;
    }

    /// The deployment screen: choose who fights this wave.
    ///
    /// The whole point of this model is that an ILLEGAL deployment cannot be
    /// turned into a request. `ToRequest` is the only way out of it and it
    /// refuses unless `CanDeploy`.
    public sealed class DeployScreenModel
    {
        public int WaveId { get; internal set; }
        public IReadOnlyList<DeploymentSlot> Slots { get; internal set; }
        public bool CanDeploy { get; internal set; }

        /// Why not, when not. Empty when `CanDeploy`.
        public string Blocker { get; internal set; }

        /// The Start button's label - `DeployScreen.Cta`, mirrored the same
        /// way `SpliceScreenModel.CtaLabel` mirrors `SpliceScreen.Cta`.
        /// Every player-facing string is authored outside the view (see
        /// `Progression.Order`, `SpliceScreen.Cta`), even one this plain,
        /// so `DeployView` reads it here rather than writing it itself.
        public string CtaLabel { get; internal set; }

        /// The wave/start body. THROWS rather than sending an illegal one.
        ///
        /// An empty deployment is refused by the server as `invalid_request`
        /// (routes/wave.ts's DEPLOYMENT_FLOOR: "an empty ARRAY is a request to
        /// fight a wave with nothing, which no deployment screen can express").
        /// This is the screen making that sentence true - if it could express
        /// one, the server's floor would be the only thing standing between a
        /// player and an undefended wave the Ark might survive and be PAID for.
        public WaveStartRequest ToRequest()
        {
            if (!CanDeploy)
            {
                throw new InvalidOperationException(
                    "This deployment cannot be sent: " + Blocker);
            }

            var body = new WaveStartRequest { WaveId = WaveId };
            foreach (var slot in Slots)
            {
                body.Deployment.Add(new DeployedCreature
                {
                    CreatureId = slot.Creature.CreatureId,
                    Pocket = slot.Pocket,
                });
            }
            return body;
        }
    }

    /// Choose a deployment, then fight.
    public static class DeployScreen
    {
        /// services/api/src/wave/issuance.ts. Mirrored, not invented - and
        /// mirrored as a CLIENT-SIDE FLOOR AND CAP so the screen refuses what
        /// the route would refuse, rather than discovering it over HTTP.
        public const int Floor = 1;
        public const int Cap = 5;

        /// The Start button's label, in one place - see `CtaLabel`.
        public const string Cta = "Start";

        /// The scaffold's header, which is ONE WORD because the number
        /// beside it is a second Label.
        ///
        /// PHASE 9 TASK 17 REPLACED "Deployment" WITH THE HANDOFF'S OWN
        /// HEADER, AND THE OLD REASONING IS KEPT HERE BECAUSE HALF OF IT
        /// STILL HOLDS. What it said: the handoff has no deploy screen - its
        /// README folds pocket assignment into "Wave Defense" as
        /// `phase: 'placing'`, with a defender tray on the combat screen
        /// itself - so the title was the one word the model already uses for
        /// the thing being assembled rather than copy invented for a header.
        /// And: "IT DOES NOT NAME THE WAVE, though `WaveId` is right there.
        /// The scaffold's title is set at CONSTRUCTION and the wave arrives
        /// at `Bind`."
        ///
        /// WHAT CHANGED IS THE SECOND HALF, AND ONLY BECAUSE THE TITLE IS
        /// TWO LABELS NOW. `Wave Defense.dc.html:40` draws `Wave {{ wave }}`
        /// as the page title under the `HOLLOW REACH · DEFENSE` kicker, so
        /// the handoff DOES name the wave in the header of the screen this
        /// one was split out of. The construction-versus-bind objection is
        /// answered structurally rather than argued with: this constant is
        /// the part that is fixed at construction, `WaveStatValue` is the
        /// part `Bind` writes, and `DeployView` puts them on one line. A
        /// header that had to be rewritten wholesale at bind time is still
        /// the thing this project does not do.
        ///
        /// NOT UPPERCASE. It is a page title in Baloo, not a `.lbl` - the
        /// kicker above it is the uppercase one. See `Eyebrow`.
        public const string Title = "Wave";

        /// The two labels composed, for a reader that wants the header as
        /// one string - a test, or a caller with nowhere to put two Labels.
        /// `DeployView` renders the pair and never this.
        public static string WaveTitle(int waveId)
        {
            return Title + " " + WaveStatValue(waveId);
        }

        /// What the field list says when it holds nothing.
        ///
        /// IT STATES THE LIST'S STATE AND GIVES NO INSTRUCTION, because the
        /// instruction is already on screen: an empty deployment sets
        /// `Blocker` to "Send at least one creature...", which `DeployView`
        /// renders in coral directly above the CTA. splice_confirm_spec 3's
        /// "three different phrasings of the same fact reads as evasion"
        /// applies to a refusal and its empty state as much as to a
        /// destruction notice, so these two say different things: one names
        /// the emptiness, one says what to do about it.
        ///
        /// THE SENTENCE MOVED WITH THE LIST IN PHASE 9 TASK 17. It used to
        /// read "No creatures deployed.", which was true of the old list -
        /// that list held the assembled deployment. The list under "On the
        /// field" now holds the ROSTER, one tappable row per creature the
        /// player owns, so its empty state is that there is nothing to
        /// choose from rather than that nothing has been chosen. Saying
        /// "deployed" over an empty roster would name the wrong emptiness
        /// and point the player at the one thing they cannot fix here.
        public const string EmptyMessage = "No creatures to deploy.";

        /// The incoming-wave card's three cells - `Wave Defense.dc.html:
        /// 217-229`. `Cap` is in the middle one because a count with no
        /// ceiling is not a decision: "3" does not tell a player whether
        /// there is room for a fourth and "3 / 5" does.
        ///
        /// UPPERCASE, AND THAT IS THE HANDOFF'S `.lbl` BAKED IN. All three
        /// go through `.lbl { text-transform: uppercase }` there, UI Toolkit
        /// has no `text-transform`, and the user's ruling at the Task 13/14
        /// boundary is that the casing lives in the NAMED CONSTANT and never
        /// as a literal in markup or in a test. `StatCell`'s label carries
        /// `t-micro`, which IS this project's `.lbl`. (Older stat rows on
        /// other screens - the region card's, Post-Wave's - still ship mixed
        /// case; they are not this task's and are named in its report.)
        public const string FoesStatLabel = "FOES";
        public const string DeployedStatLabel = "DEPLOYED";
        public const string RewardStatLabel = "REWARD";

        /// "3 / 5". The cap is `Cap`, which is mirrored from
        /// services/api/src/wave/issuance.ts - the view never writes either
        /// number itself.
        public static string DeployedStatValue(int count)
        {
            return count.ToString(CultureInfo.InvariantCulture)
                + " / " + Cap.ToString(CultureInfo.InvariantCulture);
        }

        /// The wave id as the screen prints it. InvariantCulture for the
        /// reason every other number in this assembly is - `CreatureLabel`'s
        /// `Generation` and `CampaignSelectScreen.RowLabel` both take it, so a
        /// device locale that groups digits cannot make one screen's wave 1000
        /// read differently from another's. Two other files' comments point
        /// here for that rule (`RegionScreen`, `WaveScreens`), which is why
        /// the name is unchanged although the cell it was named for is gone.
        ///
        /// IT IS THE TITLE'S NUMERAL NOW. Phase 9 Task 17 replaced the
        /// two-cell stat row ("Wave 6", "Deployed 3 / 5") with the handoff's
        /// header, so this string lands on the `.t-num` Label beside `Title`
        /// rather than in a `StatCell`. `WaveStatLabel` went with the cell;
        /// this did not, because the number is still on the screen.
        public static string WaveStatValue(int waveId)
        {
            return waveId.ToString(CultureInfo.InvariantCulture);
        }

        /// A slot's row, in the list's detail line.
        ///
        /// POCKETS ARE 1-INDEXED HERE AND 0-INDEXED ON THE WIRE. `Build`
        /// assigns `Pocket = i` because "Index == deployment order" is what
        /// the engine's parallel arrays and `deploymentMatches` compare
        /// against, and that is a protocol fact. "Pocket 0" is not a sentence
        /// a player reads, so the display adds one - in this method, once,
        /// rather than at a call site that would be the only place the two
        /// numbering schemes were known to differ.
        public static string PocketLabel(int pocket)
        {
            return "Pocket " + (pocket + 1).ToString(CultureInfo.InvariantCulture);
        }

        // ---------------------------------------------------------------
        // Phase 9 Task 17 - `Wave Defense.dc.html` in `phase: 'placing'`.
        //
        // EVERY EYEBROW AND EVERY `.lbl` BELOW SHIPS UPPERCASE AND NOTHING
        // RENDERS IT SO. The handoff puts them through `.lbl {
        // text-transform: uppercase }` (`Wave Defense.dc.html:15`), UI
        // Toolkit has no `text-transform` at all, and the user ruled at the
        // Task 13/14 boundary that the casing is baked into the NAMED
        // CONSTANT and never scattered as a literal in markup or in a test.
        // `SpliceScreen.Eyebrow` and `FounderNamingScreen.Eyebrow` are the
        // same constant for the same reason.
        // ---------------------------------------------------------------

        /// The kicker over the title - `Wave Defense.dc.html:39`. It says
        /// where in the app the player is standing (the region, and what
        /// they are doing there) rather than what the screen does.
        ///
        /// THE SEPARATOR IS U+00B7 MIDDLE DOT, the handoff's own character,
        /// not a hyphen and not a bullet.
        public const string Eyebrow = "HOLLOW REACH · DEFENSE";

        /// The pocket's name on the lane card and on its field row - "A"
        /// through "E".
        ///
        /// 0-INDEXED ON THE WIRE, LETTERED FOR THE PLAYER, which is
        /// `PocketLabel`'s rule in a second alphabet: `Build` assigns
        /// `Pocket = i` because "Index == deployment order" is a protocol
        /// fact, and "pocket 0" is not something a player reads. The two
        /// conversions live here, together, rather than at the call sites
        /// that would be the only places the schemes were known to differ.
        ///
        /// IT IS A NAME AND NOT A NUMBER, which is why nothing that wears it
        /// carries `t-num` - `FieldSlotRow`'s own class comment makes the
        /// same point about the same letter: "bible 10.6's floor is about
        /// numbers a decision depends on, and 'A' is an identifier."
        ///
        /// FIVE LETTERS, NOT THE HANDOFF'S FOUR. `Wave Defense.dc.html` draws
        /// slots A-D and states `{{ deployedCount }} / 4`; this project's cap
        /// is `DeployScreen.Cap` (5), mirrored from
        /// services/api/src/wave/issuance.ts. Where the handoff's number and
        /// the server's differ, the server's wins and the screen says so -
        /// `DeployedStatValue` already prints "/ 5".
        public static string PocketTag(int pocket)
        {
            if (pocket < 0) return string.Empty;
            return ((char)('A' + pocket)).ToString(CultureInfo.InvariantCulture);
        }

        /// The badge on a field row for a creature that is NOT deployed.
        ///
        /// THE HANDOFF'S OWN MARK FOR AN UNOCCUPIED SLOT. `Wave Defense
        /// .dc.html:98-113` draws every empty pocket as a `#f7f5fb` tile with
        /// a violet ring and a "+" in it, against a solid white tile with the
        /// creature's name in it when the pocket is taken. An undeployed
        /// creature has no pocket, so there is no letter to print - and a
        /// blank badge would read as a rendering fault where this reads as an
        /// invitation.
        ///
        /// NOT A `t-num` AND NOT AN ICON. It is one glyph in the row's own
        /// badge, which is `FieldSlotRow`'s 18px `--field-badge` square, and
        /// swapping a Label for a tinted PNG there would mean the badge had
        /// two layouts depending on its state.
        public const string EmptyPocketTag = "+";

        /// The two readouts across the top - `Wave Defense.dc.html:52` and
        /// `:61`.
        public const string EnergyLabel = "GENE ENERGY";
        public const string IntegrityLabel = "ARK INTEGRITY";

        /// What the integrity readout says BEFORE a wave, which is always the
        /// same thing.
        ///
        /// NOT `WaveDef.Integrity`, AND THAT IS WORTH ONE SENTENCE SO NOBODY
        /// WIRES THE TWO TOGETHER LATER. The engine's `Integrity` is a COUNT
        /// of breaches the Ark survives (2 on waves 1, 2 and 6; 3 on wave 7)
        /// and the handoff's readout is a PERCENTAGE of a bar. They are
        /// different quantities with the same name. This screen is shown
        /// before anything has broken through, so the honest reading is
        /// "untouched" - and a literal is the honest way to say a thing that
        /// cannot vary here. The live figure belongs to `WaveHudView`, which
        /// reads `HudSnapshot.Integrity`.
        public const string IntegrityFull = "100%";

        /// The incoming-wave card's eyebrow - `Wave Defense.dc.html:216`.
        public static string IncomingHeading(int waveId)
        {
            return "INCOMING WAVE " + WaveStatValue(waveId);
        }

        /// The tag at that card's right edge, in --coral-text -
        /// `Wave Defense.dc.html:217`, whose own value is `ARMORED PUSH`.
        ///
        /// "STANDARD" BECAUSE NOTHING IN THIS BUILD AUTHORS A WAVE FLAVOUR.
        /// The handoff's tag is a per-wave editorial label and `WaveDef`
        /// carries no field for one - it has an id, an integrity, a lane and
        /// a spawn table. Inventing a flavour per wave here would be the
        /// screen deriving content, which client_architecture section 11
        /// forbids in the same words for the view layer ("if the view needs
        /// a number that the engine does not expose, the engine gains an
        /// accessor"). So the tag states the only thing that is true of
        /// every authored wave, and the day a wave carries a flavour this
        /// constant becomes a lookup.
        public const string StandardTag = "STANDARD";

        /// The sentence under that eyebrow - `Wave Defense.dc.html:219`,
        /// whose own value describes wave 7's composition.
        ///
        /// A DEFAULT, NOT THE ONLY ANSWER. `DeployWaveFacts.Brief` carries a
        /// wave's own sentence when something authored one, and today
        /// nothing does: the vocabulary this project has for describing a
        /// wave is `WaveDefeatScreen.Diagnosis`, which is written in the
        /// past tense off a `BreachSummary` that only exists after a defeat.
        /// Bending it into a forecast would make the screen claim to know
        /// which raider will break through, which is the one thing the
        /// engine decides and the client may not.
        public const string DefaultBrief = "Raiders are coming down the lane.";

        /// The field card's eyebrow and the note at its right edge -
        /// `Wave Defense.dc.html:242` and `:243`. The note is LOWERCASE in
        /// the handoff and is not a `.lbl`, so it is not uppercased.
        ///
        /// "tap a row", NOT THE HANDOFF'S "tap a slot on the lane". The
        /// handoff's four pockets are tappable tiles drawn at authored
        /// coordinates ON its lane picture; this lane is rendered by a
        /// camera from wave data, so its pockets are wherever the wave puts
        /// them and nothing holding the finished texture knows where that is
        /// on screen (`LanePreviewCard`'s class comment has the full
        /// version). The rows below are the tappable thing here, and a hint
        /// that pointed at the picture would point at nothing.
        public const string FieldHeading = "ON THE FIELD";
        public const string FieldHint = "tap a row to place";

        /// The wave's spawn count, as the `FOES` cell prints it.
        ///
        /// AN `int?` BECAUSE "0 FOES" IS A CLAIM AND A BLANK IS NOT. A
        /// caller that does not know the count - every test and fixture that
        /// binds without `DeployWaveFacts`, and any future caller reaching
        /// this screen before a sync - must not be made to say that a wave
        /// is empty. The same rule as `WaveDefeatScreen.Resupply`'s empty
        /// grant: "says nothing rather than promising a resupply that is not
        /// there."
        public static string FoesStatValue(int? spawnCount)
        {
            return spawnCount == null
                ? string.Empty
                : spawnCount.Value.ToString(CultureInfo.InvariantCulture);
        }

        /// The player's spendable balance, as the `GENE ENERGY` readout
        /// prints it. `int?` for `FoesStatValue`'s reason.
        public static string EnergyValue(int? balance)
        {
            return balance == null
                ? string.Empty
                : balance.Value.ToString(CultureInfo.InvariantCulture);
        }

        /// The wave's reward - "120 shards", `Wave Defense.dc.html:229`.
        ///
        /// THE CURRENCY IS THE SERVER'S WORD AND IS NOT TRANSLATED HERE.
        /// `WaveSummary.RewardCurrency` comes off `config.waves` in
        /// `/v1/sync`; a client-side map from "shards" to a display noun
        /// would be a second table to keep in step with the server's, and
        /// `PostWaveScreen` already prints the wire's word for the same
        /// reward on the screen after this one.
        public static string RewardValue(string currency, int? amount)
        {
            if (amount == null) return string.Empty;
            var number = amount.Value.ToString(CultureInfo.InvariantCulture);
            return string.IsNullOrEmpty(currency) ? number : number + " " + currency;
        }

        /// Build from the ids the player selected, in selection order.
        ///
        /// ORDER IS PART OF THE REQUEST. The engine indexes its parallel
        /// arrays by deployment order ("Index == deployment order"), and
        /// `deploymentMatches` compares the replay's deployment against the
        /// stored one in order - two deployments that agree as multisets and
        /// differ in order are different deployments. So this preserves the
        /// order it was given and never sorts.
        public static DeployScreenModel Build(
            int waveId, RosterScreen roster, IReadOnlyList<Guid> selected)
        {
            if (roster == null) throw new ArgumentNullException("roster");

            var slots = new List<DeploymentSlot>();
            var model = new DeployScreenModel
            {
                WaveId = waveId,
                Slots = slots,
                CanDeploy = false,
                Blocker = string.Empty,
                CtaLabel = Cta,
            };

            if (waveId < 1)
            {
                model.Blocker = "There is no wave " + waveId + ".";
                return model;
            }

            var count = selected == null ? 0 : selected.Count;

            if (count < Floor)
            {
                model.Blocker = "Send at least one creature. A wave fought with nothing is not a wave.";
                return model;
            }
            if (count > Cap)
            {
                model.Blocker = "A deployment is at most " + Cap + " creatures.";
                return model;
            }

            var seen = new HashSet<Guid>();
            for (var i = 0; i < count; i++)
            {
                var id = selected[i];

                // A body naming one creature in two pockets asks for two
                // creatures out of one. The route calls that malformed; so
                // does this.
                if (!seen.Add(id))
                {
                    model.Blocker = "That creature is already in the deployment.";
                    return model;
                }

                var creature = roster.Find(id);
                if (creature == null)
                {
                    // creature_not_owned's remedy, before the request.
                    model.Blocker = "That creature is not loaded. Refresh your roster.";
                    return model;
                }
                if (creature.CommittedTo != null)
                {
                    model.Blocker = CreatureLabel.DisplayName(creature)
                        + " is already out fighting.";
                    return model;
                }

                slots.Add(new DeploymentSlot { Creature = creature, Pocket = i });
            }

            model.CanDeploy = true;
            return model;
        }

        /// Ask for an issuance. The deployment is resolved SERVER-SIDE off the
        /// rows the player owns - the request contributes only which creature
        /// and which pocket - so nothing on this screen can influence a stat.
        public static async Task<WaveStartResponse> StartAsync(
            BroodlineApiClient api, DeployScreenModel deployment)
        {
            if (api == null) throw new ArgumentNullException("api");
            if (deployment == null) throw new ArgumentNullException("deployment");

            // Throws for an illegal deployment before any request is built.
            var body = deployment.ToRequest();
            return await api.StartWaveAsync(body);
        }

        /// Submit the replay for the issuance this screen started.
        ///
        /// `deployment_mismatch` is the refusal to expect here and it is new
        /// this phase: it means the roster this screen was showing no longer
        /// agrees with what the issuance was minted for. Its remedy is to
        /// re-read the roster and start again, which is why ServerError maps
        /// it to `RefreshRoster` and not to the generic conflict path.
        public static async Task<WaveSubmitResponse> SubmitAsync(
            BroodlineApiClient api, string idempotencyKey, Guid issuanceId, string replay)
        {
            if (api == null) throw new ArgumentNullException("api");
            if (string.IsNullOrEmpty(idempotencyKey))
            {
                throw new ArgumentException(
                    "A submission needs an Idempotency-Key taken when the wave ended.",
                    "idempotencyKey");
            }
            if (string.IsNullOrEmpty(replay))
            {
                throw new ArgumentException("A submission needs a replay.", "replay");
            }

            return await api.SubmitWaveAsync(idempotencyKey, new WaveSubmitRequest
            {
                IssuanceId = issuanceId,
                Replay = replay,
            });
        }
    }
}
