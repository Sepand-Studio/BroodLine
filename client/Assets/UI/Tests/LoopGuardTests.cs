using System;
using System.Collections.Generic;
using System.Net.Http;
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

        // ---------------------------------------------------------------
        // "Empty" and "unknown" are not the same state
        // ---------------------------------------------------------------

        [Test]
        public void ARosterNothingHasLoaded_DoesNotClaimToBeComplete()
        {
            // `_order.Count >= ServerCount` is `0 >= 0` on a fresh cache, so
            // WITHOUT an explicit load state this reads as "roster complete, 0
            // creatures" before anything has been fetched - indistinguishable
            // from a genuine new player.
            var roster = new RosterScreen();

            Assert.AreEqual(RosterLoadState.NeverLoaded, roster.LoadState);
            Assert.IsFalse(roster.IsComplete);
            Assert.IsFalse(roster.HasCounts);
            Assert.IsNotEmpty(roster.IncompleteNotice);
        }

        [Test]
        public void AFailedLoad_SaysTheRosterIsNotAllOfIt()
        {
            // THE CASE THIS STATE EXISTS FOR. A player with twelve creatures
            // whose load failed must not be shown an empty roster and told
            // that is correct - that is worse than not having the endpoint,
            // because the client has stopped knowing it is missing data.
            var roster = new RosterScreen();
            roster.MarkLoadFailed();

            Assert.AreEqual(RosterLoadState.Failed, roster.LoadState);
            Assert.IsFalse(roster.IsComplete);
            StringAssert.Contains("could not be loaded", roster.IncompleteNotice);
        }

        [Test]
        public void AFailedLoadAfterAGoodOne_StopsClaimingCompleteness()
        {
            // A refresh that fails must not leave the previous answer wearing
            // a completeness claim it no longer earns.
            var roster = new RosterScreen();
            var response = new RosterResponse { Cap = 20 };
            response.Creatures.Add(Creature());
            roster.ApplyRoster(response);
            Assert.IsTrue(roster.IsComplete);

            roster.MarkLoadFailed();

            Assert.IsFalse(roster.IsComplete);
            Assert.IsNotEmpty(roster.IncompleteNotice);
            // The creatures are KEPT - they were real when the server handed
            // them over, and clearing them would turn a failed refresh into an
            // empty screen. What changes is the claim.
            Assert.AreEqual(1, roster.Known.Count);
        }

        [Test]
        public void LoadAsyncAgainstADeadServer_RecordsTheFailureOnTheCache()
        {
            // The wiring, not just the flag: a caller who forgets a try/catch
            // is exactly how the cache ends up silently claiming to be
            // complete, so the failure is recorded by the call itself.
            var roster = new RosterScreen();
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var api = new BroodlineApiClient(http) { BaseUrl = "http://127.0.0.1:1" };

            // Blocking rather than an async test method: a refused connection
            // to a dead local port returns immediately, and this keeps the
            // test inside the plain [Test] path.
            var error = roster.LoadAsync(api).GetAwaiter().GetResult();

            Assert.IsNotNull(error);
            Assert.AreEqual(RosterLoadState.Failed, roster.LoadState);
            Assert.IsFalse(roster.IsComplete);
            StringAssert.Contains("could not be loaded", roster.IncompleteNotice);
        }

        [Test]
        public void AGrantDoesNotMakeTheRosterComplete()
        {
            // A claim's grant is not a listing. Only the server saying "here
            // is all of it" counts as that - otherwise a session that claimed
            // two creatures would think it had seen the whole roster.
            var roster = new RosterScreen();
            var claim = new NodeClaimResponse { Slot = 1, Shards = 40, Balance = 100 };
            claim.Creatures.Add(Creature());
            claim.Creatures.Add(Creature());

            roster.ApplyClaim(claim);

            Assert.AreEqual(2, roster.Known.Count);
            Assert.AreEqual(RosterLoadState.NeverLoaded, roster.LoadState);
            Assert.IsFalse(roster.IsComplete);
        }

        [Test]
        public void AnUnknownCap_DoesNotGreyOutTheClaimButton()
        {
            // False when unknown is the safe direction: roster_full is a
            // refusal the server makes in its own transaction, so letting the
            // player tap costs a refusal, while claiming FULL on no
            // information costs them the button entirely.
            var roster = new RosterScreen();

            Assert.IsFalse(roster.HasCounts);
            Assert.IsFalse(roster.IsFull);
        }

        [Test]
        public void AMapPollWithNoRosterObject_DoesNotReportAFullHatchery()
        {
            // RegionScreen has no load-state ambiguity - a failed load builds
            // no model at all - but it shared the `0 >= 0` shape. And the
            // shape is REACHABLE: the generated RegionStateResponse
            // default-initialises Roster to `new Roster()`, so this response
            // carries Count 0 / Cap 0 and a null check would not catch it.
            var model = RegionScreen.Build(new RegionStateResponse { RegionId = "r1", Epoch = 1 });

            Assert.IsNotNull(model.RosterCap);
            Assert.IsFalse(model.HasRosterCounts);
            Assert.IsFalse(model.RosterIsFull);
        }

        [Test]
        public void AnUnknownCap_DoesNotGreyOutEveryNodeOnTheMap()
        {
            // The inverted check: an unset cap of 0 makes `count + grants > 0`
            // true for every granting node, so without the gate a response
            // carrying no roster headline would tell a player with an empty
            // roster that it has no room for anything.
            var state = new RegionStateResponse { RegionId = "r1", Epoch = 1 };
            state.Nodes.Add(new Nodes
            {
                Slot = 1, Type = "common_vein", Accrued = 40, Remaining = 3, Grants = 2,
            });

            var model = RegionScreen.Build(state);

            Assert.IsFalse(model.HasRosterCounts);
            Assert.IsTrue(model.Nodes[0].CanClaim);
            Assert.IsEmpty(model.Nodes[0].Blocker);
        }

        [Test]
        public void AMapPollWithARosterObject_ReportsTheHatcheryHonestly()
        {
            var full = RegionScreen.Build(new RegionStateResponse
            {
                RegionId = "r1", Epoch = 1, Roster = new Roster { Count = 20, Cap = 20 },
            });
            Assert.IsTrue(full.HasRosterCounts);
            Assert.IsTrue(full.RosterIsFull);

            var room = RegionScreen.Build(new RegionStateResponse
            {
                RegionId = "r1", Epoch = 1, Roster = new Roster { Count = 3, Cap = 20 },
            });
            Assert.IsTrue(room.HasRosterCounts);
            Assert.IsFalse(room.RosterIsFull);
        }

        [Test]
        public void APartialRosterSaysSo_RatherThanPresentingItselfAsWhole()
        {
            // The map's `roster` is a headline and carries no members, so a
            // session that has polled the map but not yet loaded /v1/roster
            // knows the roster's SIZE and only the creatures it happened to be
            // handed. Showing that as if it were everything is how a player
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
        public void LoadingTheRoster_MakesAColdLaunchComplete()
        {
            // GET /v1/roster is what closes the loop across a relaunch. After
            // it answers, the cache holds the player's whole live roster and
            // says so.
            var roster = new RosterScreen();
            roster.ApplyRegionState(new RegionStateResponse
            {
                RegionId = "r1", Epoch = 1, Roster = new Roster { Count = 3, Cap = 20 },
            });
            Assert.IsFalse(roster.IsComplete);

            var a = Creature();
            var b = Creature();
            var c = Creature();
            var response = new RosterResponse { Cap = 20 };
            response.Creatures.Add(a);
            response.Creatures.Add(b);
            response.Creatures.Add(c);

            roster.ApplyRoster(response);

            Assert.IsTrue(roster.IsComplete);
            Assert.IsEmpty(roster.IncompleteNotice);
            Assert.AreEqual(3, roster.Known.Count);
            Assert.AreEqual(20, roster.ServerCap);
            Assert.IsNotNull(roster.Find(b.CreatureId));
        }

        [Test]
        public void TheRosterResponseReplaces_RatherThanMerges()
        {
            // The server has just stated the COMPLETE live set, so a creature
            // the cache holds and the response does not is a creature that is
            // gone. Merging would keep a splice's consumed parent on screen
            // forever.
            var stale = Creature();
            var roster = RosterOf(stale);
            Assert.IsNotNull(roster.Find(stale.CreatureId));

            var fresh = Creature();
            var response = new RosterResponse { Cap = 20 };
            response.Creatures.Add(fresh);

            roster.ApplyRoster(response);

            Assert.IsNull(roster.Find(stale.CreatureId));
            Assert.AreEqual(1, roster.Known.Count);
            Assert.IsTrue(roster.IsComplete);
        }

        [Test]
        public void AnEmptyRoster_IsCompleteAndSaysNothingIsMissing()
        {
            // A new player owns zero creatures. That is a complete answer, not
            // a partial one - the roster screen should send them to harvest,
            // not tell them something failed to load.
            var roster = new RosterScreen();
            roster.ApplyRoster(new RosterResponse { Cap = 20 });

            Assert.IsTrue(roster.IsComplete);
            Assert.IsEmpty(roster.IncompleteNotice);
            Assert.AreEqual(0, roster.Known.Count);
            Assert.IsFalse(roster.IsFull);
        }

        [Test]
        public void ALoadedRosterFeedsTheDeployScreenDirectly()
        {
            // The point of the endpoint: after a cold launch the player can
            // choose a deployment from creatures nothing in this session
            // happened to grant.
            var a = Creature();
            var b = Creature();
            b.CommittedTo = Guid.NewGuid();

            var roster = new RosterScreen();
            var response = new RosterResponse { Cap = 20 };
            response.Creatures.Add(a);
            response.Creatures.Add(b);
            roster.ApplyRoster(response);

            Assert.IsTrue(DeployScreen.Build(6, roster, new List<Guid> { a.CreatureId }).CanDeploy);
            // And the committed one is still greyed out, from the server's own
            // `committedTo`.
            Assert.IsFalse(DeployScreen.Build(6, roster, new List<Guid> { b.CreatureId }).CanDeploy);
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
