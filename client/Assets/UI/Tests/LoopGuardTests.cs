using System;
using System.Collections.Generic;
using Broodline.Api;
using NUnit.Framework;

namespace Broodline.UI.Tests
{
    /// The two guards that are not about copy.
    ///
    /// 1. An error code this build has never heard of must not be swallowed.
    ///    `Generated.Api` types `ErrorResponse.code` as a bare string, and
    ///    Phase 6 added `deployment_mismatch` to the server AFTER the client
    ///    contract was generated - so an unknown code is a normal event.
    ///
    /// 2. An empty deployment must be inexpressible. routes/wave.ts refuses
    ///    it as invalid_request on the grounds that "no deployment screen can
    ///    express" one; this is the file that makes that sentence true.
    public class LoopGuardTests
    {
        // ---------------------------------------------------------------
        // Unknown error codes
        // ---------------------------------------------------------------

        [Test]
        public void AnUnknownCode_KeepsItsRawStringAndOffersNoRemedy()
        {
            var error = ServerError.From(409, "sunspot_interference", "The sun did something.");

            // Not swallowed: the raw code survives for the log and telemetry.
            Assert.AreEqual("sunspot_interference", error.Code);
            Assert.IsFalse(error.IsRecognised);
            Assert.AreEqual(ServerErrorKind.Unrecognised, error.Kind);

            // Not guessed at: no remedy is offered for a refusal this build
            // cannot classify.
            Assert.AreEqual(Remedy.None, error.Remedy);

            // The server's own sentence is still shown - it is a better one
            // than any the client could invent.
            Assert.AreEqual("The sun did something.", error.PlayerMessage);
            Assert.AreEqual(409, error.StatusCode);

            // And the log line names the code, so an unrecognised one is
            // visible the day it ships.
            StringAssert.Contains("sunspot_interference", error.ToString());
        }

        [Test]
        public void EveryCodeTheServerCanSend_IsRecognised()
        {
            // The vocabulary in services/api/src/http/errors.ts. A code added
            // there and not here reddens this test rather than silently
            // landing in the no-remedy bucket.
            var codes = new[]
            {
                "invalid_request", "idempotency_key_reused", "unauthorized", "not_found",
                "conflict", "client_too_old", "internal",
                "wave_locked", "replay_cap_reached", "issuance_invalid",
                "submission_rejected", "engine_too_old", "sim_unavailable",
                "deployment_mismatch", "roster_full", "creature_not_owned",
                "creature_committed", "generation_ceiling", "insufficient_charges",
            };

            foreach (var code in codes)
            {
                var error = ServerError.From(409, code, "message");
                Assert.IsTrue(error.IsRecognised, code + " is not recognised by this build.");
                Assert.AreEqual(code, error.Code);
            }
        }

        [Test]
        public void TheThreeSpliceRefusals_LeadToThreeDifferentRemedies()
        {
            // errors.ts keeps three distinct codes on one HTTP status so the
            // Splice Chamber does not have to guess - "collapsing them into
            // `conflict` would make the screen guess."
            Assert.AreEqual(Remedy.RefreshRoster,
                ServerError.From(409, "creature_not_owned", "m").Remedy);
            Assert.AreEqual(Remedy.WaitForCreature,
                ServerError.From(409, "creature_committed", "m").Remedy);
            Assert.AreEqual(Remedy.UpgradeChamber,
                ServerError.From(409, "generation_ceiling", "m").Remedy);
            Assert.AreEqual(Remedy.WaitForCharge,
                ServerError.From(409, "insufficient_charges", "m").Remedy);
        }

        [Test]
        public void DeploymentMismatch_SendsTheScreenBackToTheRoster()
        {
            // The code this phase added, and the one the client has no
            // compile-time knowledge of. Its next action differs from a seed
            // mismatch's, which is the whole reason it is a distinct code.
            var error = ServerError.From(409, "deployment_mismatch", "m");

            Assert.IsTrue(error.IsRecognised);
            Assert.AreEqual(Remedy.RefreshRoster, error.Remedy);
        }

        [Test]
        public void AnUntypedApiFailure_StillYieldsItsCode()
        {
            // NSwag only types the body for statuses the OpenAPI document
            // DECLARES on that operation, and /v1/splice/preview declares no
            // 409 - so this path is reached in normal operation, not only on a
            // server fault. Dropping the code here would lose
            // `creature_committed` on the preview route entirely.
            var thrown = new BroodlineApiException(
                "Error", 409, "{\"code\":\"creature_committed\",\"message\":\"out fighting\"}",
                new Dictionary<string, IEnumerable<string>>(), null);

            var error = ServerError.From(thrown);

            Assert.AreEqual(ServerErrorKind.CreatureCommitted, error.Kind);
            Assert.AreEqual(Remedy.WaitForCreature, error.Remedy);
            Assert.AreEqual("out fighting", error.PlayerMessage);
            StringAssert.Contains("creature_committed", error.RawBody);
        }

        [Test]
        public void ABodyThatIsNotTheErrorShape_IsARefusalWithNoInventedRemedy()
        {
            // A proxy's HTML error page, say. Unreadable is not the same as
            // absent: the body is kept, no remedy is guessed, and the player
            // is told only what is known to be true.
            var thrown = new BroodlineApiException(
                "Error", 502, "<html>Bad Gateway</html>",
                new Dictionary<string, IEnumerable<string>>(), null);

            var error = ServerError.From(thrown);

            Assert.IsNotNull(error);
            Assert.IsFalse(error.IsRecognised);
            Assert.AreEqual(string.Empty, error.Code);
            Assert.AreEqual(Remedy.None, error.Remedy);
            Assert.AreEqual(ServerError.UnreadableRefusal, error.PlayerMessage);
            // Nothing is discarded because it could not be understood.
            StringAssert.Contains("Bad Gateway", error.RawBody);
        }

        [Test]
        public void AnUnknownCodeOnAnUntypedStatus_IsStillNotSwallowed()
        {
            var thrown = new BroodlineApiException(
                "Error", 409, "{\"code\":\"sunspot_interference\",\"message\":\"The sun.\"}",
                new Dictionary<string, IEnumerable<string>>(), null);

            var error = ServerError.From(thrown);

            Assert.AreEqual("sunspot_interference", error.Code);
            Assert.IsFalse(error.IsRecognised);
            Assert.AreEqual(Remedy.None, error.Remedy);
            Assert.AreEqual("The sun.", error.PlayerMessage);
        }

        [Test]
        public void ATypedApiFailure_IsClassifiedFromTheBodysCode()
        {
            var thrown = new BroodlineApiException<ErrorResponse>(
                "Error", 409, "{}", new Dictionary<string, IEnumerable<string>>(),
                new ErrorResponse { Code = "roster_full", Message = "No room." }, null);

            var error = ServerError.From(thrown);

            Assert.AreEqual(ServerErrorKind.RosterFull, error.Kind);
            Assert.AreEqual(Remedy.MakeRoomInRoster, error.Remedy);
            Assert.AreEqual("No room.", error.PlayerMessage);
        }

        [Test]
        public void ATransportFailure_IsNeverMistakenForARefusal()
        {
            var error = ServerError.From(new InvalidOperationException("socket closed"));

            Assert.IsNotNull(error);
            Assert.AreEqual(0, error.StatusCode);
            Assert.AreEqual(Remedy.RetryLater, error.Remedy);
        }

        // ---------------------------------------------------------------
        // Deployments
        // ---------------------------------------------------------------

        static CreatureDto Creature()
        {
            return new CreatureDto
            {
                CreatureId = Guid.NewGuid(),
                Species = "Vetch Crawler",
                Generation = 1,
                Trait1 = "Chill", Tier1 = 1,
                Trait2 = "Guard", Tier2 = 1,
                Instinct = "Forage",
                Name = null, IsFounder = false, CommittedTo = null,
            };
        }

        static RosterScreen RosterOf(params CreatureDto[] creatures)
        {
            var roster = new RosterScreen();
            foreach (var creature in creatures) roster.Observe(creature);
            return roster;
        }

        [Test]
        public void AnEmptyDeployment_CannotBeTurnedIntoARequest()
        {
            var model = DeployScreen.Build(6, RosterOf(Creature()), new List<Guid>());

            Assert.IsFalse(model.CanDeploy);
            Assert.IsNotEmpty(model.Blocker);
            Assert.Throws<InvalidOperationException>(() => model.ToRequest());
        }

        [Test]
        public void ANullSelection_IsTheSameRefusalAsAnEmptyOne()
        {
            var model = DeployScreen.Build(6, RosterOf(Creature()), null);

            Assert.IsFalse(model.CanDeploy);
            Assert.Throws<InvalidOperationException>(() => model.ToRequest());
        }

        [Test]
        public void MoreThanFive_IsRefusedBeforeTheRequestIsBuilt()
        {
            var six = new CreatureDto[6];
            var ids = new List<Guid>();
            for (var i = 0; i < 6; i++)
            {
                six[i] = Creature();
                ids.Add(six[i].CreatureId);
            }

            var model = DeployScreen.Build(6, RosterOf(six), ids);

            Assert.IsFalse(model.CanDeploy);
            Assert.Throws<InvalidOperationException>(() => model.ToRequest());
        }

        [Test]
        public void OneToFive_Deploys_AndKeepsSelectionOrder()
        {
            var a = Creature();
            var b = Creature();
            // Selected b first. The engine indexes by deployment order, so the
            // order the player chose is part of what they asked for.
            var model = DeployScreen.Build(6, RosterOf(a, b), new List<Guid> { b.CreatureId, a.CreatureId });

            Assert.IsTrue(model.CanDeploy);
            Assert.IsEmpty(model.Blocker);

            var body = model.ToRequest();
            Assert.AreEqual(6, body.WaveId);
            Assert.AreEqual(2, body.Deployment.Count);

            var sent = new List<DeployedCreature>(body.Deployment);
            Assert.AreEqual(b.CreatureId, sent[0].CreatureId);
            Assert.AreEqual(a.CreatureId, sent[1].CreatureId);
            Assert.AreEqual(0, sent[0].Pocket);
            Assert.AreEqual(1, sent[1].Pocket);
        }

        [Test]
        public void TheSameCreatureTwice_AsksForTwoCreaturesOutOfOne()
        {
            var a = Creature();
            var model = DeployScreen.Build(6, RosterOf(a), new List<Guid> { a.CreatureId, a.CreatureId });

            Assert.IsFalse(model.CanDeploy);
            Assert.Throws<InvalidOperationException>(() => model.ToRequest());
        }

        [Test]
        public void ACreatureAlreadyOutFighting_IsNotDeployedAgain()
        {
            var a = Creature();
            a.CommittedTo = Guid.NewGuid();

            var model = DeployScreen.Build(6, RosterOf(a), new List<Guid> { a.CreatureId });

            Assert.IsFalse(model.CanDeploy);
            StringAssert.Contains("out fighting", model.Blocker);
        }

        [Test]
        public void ACreatureTheCacheDoesNotHold_SendsTheScreenBackToTheRoster()
        {
            var model = DeployScreen.Build(6, RosterOf(Creature()), new List<Guid> { Guid.NewGuid() });

            Assert.IsFalse(model.CanDeploy);
            StringAssert.Contains("Refresh your roster", model.Blocker);
        }

        // ---------------------------------------------------------------
        // The roster cache says when it is partial
        // ---------------------------------------------------------------

        [Test]
        public void APartialRosterSaysSo_RatherThanPresentingItselfAsWhole()
        {
            // There is no endpoint that lists owned creatures, so a cold start
            // knows the roster's SIZE and none of its members. Showing what
            // happens to be cached as if it were everything is how a player
            // concludes a creature was lost.
            var roster = new RosterScreen();
            roster.ApplyRegionState(new RegionStateResponse
            {
                RegionId = "r1",
                Epoch = 1,
                Roster = new Roster { Count = 4, Cap = 20 },
            });
            roster.Observe(Creature());

            Assert.IsFalse(roster.IsComplete);
            StringAssert.Contains("1 of 4", roster.IncompleteNotice);
        }

        [Test]
        public void ASplicedParentStopsBeingDrawn()
        {
            // Applying the outcome the server reported, not deciding liveness.
            var a = Creature();
            var b = Creature();
            var roster = RosterOf(a, b);
            roster.ApplyRegionState(new RegionStateResponse
            {
                RegionId = "r1", Epoch = 1, Roster = new Roster { Count = 2, Cap = 20 },
            });

            var child = Creature();
            roster.ApplySplice(
                new SpliceCommitResponse
                {
                    Child = child, SpliceId = Guid.NewGuid(), Seed = "0", Balance = 2,
                },
                a.CreatureId, b.CreatureId);

            Assert.IsNull(roster.Find(a.CreatureId));
            Assert.IsNull(roster.Find(b.CreatureId));
            Assert.IsNotNull(roster.Find(child.CreatureId));
            Assert.AreEqual(1, roster.Known.Count);
            Assert.AreEqual(1, roster.ServerCount);
        }

        // ---------------------------------------------------------------
        // The map screen does not offer a claim the server will refuse
        // ---------------------------------------------------------------

        [Test]
        public void AFullRosterBlocksAGrantingNode_AndSaysTheShardsGoToo()
        {
            var state = new RegionStateResponse
            {
                RegionId = "r1",
                Epoch = 1,
                Roster = new Roster { Count = 20, Cap = 20 },
            };
            state.Nodes.Add(new Nodes { Slot = 1, Type = "common_vein", Accrued = 40, Remaining = 3, Grants = 2 });

            var model = RegionScreen.Build(state);

            Assert.IsFalse(model.Nodes[0].CanClaim);
            // design 4.3 refuses the WHOLE claim rather than truncating it.
            StringAssert.Contains("shards", model.Nodes[0].Blocker);
        }

        [Test]
        public void ANodeWithRoomIsClaimable()
        {
            var state = new RegionStateResponse
            {
                RegionId = "r1",
                Epoch = 1,
                Roster = new Roster { Count = 2, Cap = 20 },
            };
            state.Nodes.Add(new Nodes { Slot = 1, Type = "common_vein", Accrued = 40, Remaining = 3, Grants = 2 });

            var model = RegionScreen.Build(state);

            Assert.IsTrue(model.Nodes[0].CanClaim);
            Assert.IsEmpty(model.Nodes[0].Blocker);
        }
    }
}
