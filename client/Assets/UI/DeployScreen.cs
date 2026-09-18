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

        /// The scaffold's header.
        ///
        /// NOT FROM THE HANDOFF, AND THAT IS STATED RATHER THAN PAPERED
        /// OVER. The handoff has no deploy screen: its README folds pocket
        /// assignment into "Wave Defense" as `phase: 'placing'`, with a
        /// defender tray on the combat screen itself. Broodline splits the
        /// two - `DeployView` places, `WaveHudView` fights - so this title is
        /// the one word the model already uses for the thing being assembled
        /// ("This deployment cannot be sent", "A deployment is at most 5
        /// creatures") rather than copy invented for the header.
        ///
        /// IT DOES NOT NAME THE WAVE, though `WaveId` is right there. The
        /// scaffold's title is set at CONSTRUCTION and the wave arrives at
        /// `Bind`, and `FounderNamingScreen.Title`'s comment already settled
        /// which of those the header is: it names the SCREEN and never
        /// changes. The wave id is a fact about this deployment, so it is a
        /// stat cell instead.
        public const string Title = "Deployment";

        /// What the pocket list says when it holds nothing.
        ///
        /// IT STATES THE LIST'S STATE AND GIVES NO INSTRUCTION, because the
        /// instruction is already on screen: an empty deployment sets
        /// `Blocker` to "Send at least one creature...", which `DeployView`
        /// renders in coral directly above the CTA. splice_confirm_spec 3's
        /// "three different phrasings of the same fact reads as evasion"
        /// applies to a refusal and its empty state as much as to a
        /// destruction notice, so these two say different things: one names
        /// the emptiness, one says what to do about it.
        public const string EmptyMessage = "No creatures deployed.";

        /// The two facts the stat row states. `Cap` is in the second because
        /// a count with no ceiling is not a decision - "3" does not tell a
        /// player whether there is room for a fourth and "3 / 5" does.
        public const string WaveStatLabel = "Wave";
        public const string DeployedStatLabel = "Deployed";

        /// "3 / 5". The cap is `Cap`, which is mirrored from
        /// services/api/src/wave/issuance.ts - the view never writes either
        /// number itself.
        public static string DeployedStatValue(int count)
        {
            return count.ToString(CultureInfo.InvariantCulture)
                + " / " + Cap.ToString(CultureInfo.InvariantCulture);
        }

        /// The wave id as the stat cell prints it. InvariantCulture for the
        /// reason every other number in this assembly is - `CreatureLabel`'s
        /// `Generation` and `CampaignSelectScreen.RowLabel` both take it, so a
        /// device locale that groups digits cannot make one screen's wave 1000
        /// read differently from another's.
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
