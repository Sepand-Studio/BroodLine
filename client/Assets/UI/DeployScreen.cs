using System;
using System.Collections.Generic;
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
            return await api.StartWaveAsync(body).ConfigureAwait(false);
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
            }).ConfigureAwait(false);
        }
    }
}
