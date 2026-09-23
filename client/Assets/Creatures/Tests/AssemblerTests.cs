using System.Collections.Generic;
using Broodline.Creatures;
using NUnit.Framework;
using UnityEngine;

namespace Broodline.Creatures.Tests
{
    public class AssemblerTests
    {
        static CreatureLook Vetch(string t1 = "carapace", string t2 = "taunt") =>
            new CreatureLook { Species = "vetch", Trait1 = t1, Trait2 = t2, Growth01 = 0f };

        [Test]
        public void Build_MountsCombatOneAtDorsal_AndCombatTwoAtFlank_BySlotNotByTrait()
        {
            // rig_proof.md 3.1: slot index decides the socket. Swap the traits
            // and the parts swap sockets; nothing about the trait chooses.
            var a = CreatureAssembler.Build(Vetch("carapace", "cinder"));
            var b = CreatureAssembler.Build(Vetch("cinder", "carapace"));
            try
            {
                Assert.AreEqual("carapace", a.transform.Find(Sockets.Dorsal).GetChild(0).name);
                Assert.AreEqual("cinder", a.transform.Find(Sockets.Flank).GetChild(0).name);
                Assert.AreEqual("cinder", b.transform.Find(Sockets.Dorsal).GetChild(0).name);
                Assert.AreEqual("carapace", b.transform.Find(Sockets.Flank).GetChild(0).name);
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
        }

        [Test]
        public void Build_CinderplateIsVetchWithCinderAtDorsal_AndNothingElseMakesIt()
        {
            // bible 10.9: the mascot, produced by the pipeline.
            var c = CreatureAssembler.Build(Vetch("cinder", "carapace"));
            try
            {
                Assert.IsNotNull(c.GetComponentInChildren<SkinnedMeshRenderer>());
                Assert.AreEqual("cinder", c.transform.Find(Sockets.Dorsal).GetChild(0).name);
                Assert.IsNotNull(c.GetComponent<CreatureMotion>());
            }
            finally { Object.DestroyImmediate(c); }
        }

        [Test]
        public void Build_UnknownTrait_LeavesTheSocketEmpty_AndUnknownSpecies_IsLoud()
        {
            var c = CreatureAssembler.Build(Vetch("no-such-trait", null));
            try { Assert.AreEqual(0, c.transform.Find(Sockets.Dorsal).childCount); }
            finally { Object.DestroyImmediate(c); }

            var m = CreatureAssembler.Build(new CreatureLook { Species = "ash" });
            try { StringAssert.StartsWith("missing-", m.name); }
            finally { Object.DestroyImmediate(m); }
        }

        [Test]
        public void Growth_MovesMassOutward_OnOneRig()
        {
            // bible 10.2 rule 3: runt to apex is the same creature with mass
            // moved outward - bigger head, heavier limbs, same profile. The
            // head bone grows more than the root because its primitives say so.
            var runt = CreatureAssembler.Build(Vetch());
            var apex = CreatureAssembler.Build(new CreatureLook { Species = "vetch", Trait1 = "carapace", Trait2 = "taunt", Growth01 = 1f });
            try
            {
                float headRunt = runt.transform.Find("root/head").localScale.x;
                float headApex = apex.transform.Find("root/head").localScale.x;
                float rootApex = apex.GetComponent<CreatureMotion>().GrowthScale;
                Assert.AreEqual(1f, headRunt, 1e-5f);
                Assert.Greater(headApex, rootApex, "the head grows more than the body");
                Assert.Greater(rootApex, 1f);
            }
            finally { Object.DestroyImmediate(runt); Object.DestroyImmediate(apex); }
        }

        [Test]
        public void Motion_Breathes_BobsWhenMoving_AndDroopsWhenHurt()
        {
            var c = CreatureAssembler.Build(Vetch());
            try
            {
                var motion = c.GetComponent<CreatureMotion>();
                var root = c.transform.Find("root");
                motion.Tick(0f, 0f);
                var restY = root.localPosition.y;
                motion.Tick(0.4f, 0.4f);
                Assert.AreNotEqual(root.localScale.y, 1f, "breathing scales the root");

                motion.Moving = true; motion.Tick(0.8f, 0.4f);
                Assert.AreNotEqual(restY, root.localPosition.y, "moving bobs the root");

                motion.Hurt01 = 1f; motion.Tick(1.2f, 0.4f);
                Assert.Greater(Mathf.Abs(root.localRotation.eulerAngles.x % 360f), 0.5f, "hurt droops the root");
                // _Desaturate is written through a MaterialPropertyBlock, not the
                // material itself (a hundred creatures on the wave lane cannot each
                // own a material instance), so it is read back the same way:
                // GetPropertyBlock fills a block with what SetPropertyBlock last wrote.
                var block = new MaterialPropertyBlock();
                c.GetComponentInChildren<SkinnedMeshRenderer>().GetPropertyBlock(block);
                Assert.AreEqual(1f, block.GetFloat("_Desaturate"), 0.05f);
            }
            finally { Object.DestroyImmediate(c); }
        }

        /// rig_proof.md section 3: "The two combat sockets must be spatially
        /// separate enough that any two parts can coexist without collision",
        /// and section 4 item 3 makes "two parts on one body simultaneously,
        /// dorsal and flank, no collision, both readable" one of the ten things
        /// the proof has to demonstrate. bible 10.4 is why: both combat traits
        /// must be visible on the body, and two parts that overlap defeat that.
        ///
        /// NOTHING ASSERTED IT UNTIL NOW, and the margin had already started
        /// closing. Task 12b lowered and narrowed Vetch's shell to put it on
        /// its legs, which brought the two combat sockets from 0.688 apart to
        /// 0.540 - about a fifth - in the same change that grew the taunt's
        /// banner by half in each visible direction. That was checked by hand
        /// and it held, but a margin nobody measures is a margin the next
        /// recipe change eats in silence. This is the measurement.
        ///
        /// THE CLAIM IS DELIBERATELY STRONGER THAN THE SPEC'S. Two meshes whose
        /// world AABBs miss each other certainly do not collide; the converse
        /// does not follow, so a failure here is a prompt to look rather than
        /// proof of a collision. Erring that way is right for a gate meant to
        /// catch a shrinking margin before it reaches zero.
        ///
        /// The boxes come from the MESH VERTICES through each part's own
        /// localToWorldMatrix, not from Renderer.bounds - that is the AABB of
        /// an already-axis-aligned box after rotation, and the flank socket
        /// rotates by 90 degrees, so it would inflate the flank part's box and
        /// report a collision that is not there.
        ///
        /// Raiders are absent on purpose: `CreatureAssembler.Build` mounts no
        /// parts on them (they carry `sk_kit`, not the two combat sockets), so
        /// there is nothing here to compare. The loop is over
        /// `SpeciesRecipes.All` x `PartRecipes.All` squared, so Task 15's five
        /// species and nine parts join it without a line changing here.
        [Test]
        public void NoDorsalPart_CollidesWithAnyFlankPart_OnAnyBody()
        {
            var measured = new List<string>();
            float tightest = float.MaxValue;
            string tightestPair = "none";

            foreach (var body in SpeciesRecipes.All)
                foreach (var dorsal in PartRecipes.All)
                    foreach (var flank in PartRecipes.All)
                    {
                        var creature = CreatureAssembler.Build(new CreatureLook
                        {
                            Species = body.Id, Trait1 = dorsal.Id, Trait2 = flank.Id, Growth01 = 0f,
                        });
                        try
                        {
                            var pair = body.Id + " " + dorsal.Id + "@" + Sockets.Dorsal +
                                       " / " + flank.Id + "@" + Sockets.Flank;
                            var a = MountedBounds(creature, Sockets.Dorsal, dorsal.Id, pair);
                            var b = MountedBounds(creature, Sockets.Flank, flank.Id, pair);
                            float clearance = Clearance(a, b);
                            measured.Add(pair + " " + clearance.ToString("F3"));
                            Assert.Greater(clearance, 0f,
                                pair + ": the two parts' world bounds overlap on every axis (" +
                                a + " against " + b + "). rig_proof.md section 3 requires the two " +
                                "combat sockets to be far enough apart that ANY two parts coexist - " +
                                "move a socket or shrink a part, and re-run generate-creatures.sh.");
                            if (clearance < tightest) { tightest = clearance; tightestPair = pair; }
                        }
                        finally { Object.DestroyImmediate(creature); }
                    }

            // What the loop above actually compared, asserted rather than
            // assumed - a body whose parts silently failed to mount would leave
            // this list short and every remaining assertion trivially true.
            int expected = SpeciesRecipes.All.Count * PartRecipes.All.Count * PartRecipes.All.Count;
            Assert.AreEqual(expected, measured.Count,
                "compared " + measured.Count + " dorsal/flank combinations, not the " + expected +
                " that " + SpeciesRecipes.All.Count + " bodies and " + PartRecipes.All.Count +
                " parts imply");

            // The MARGIN is the interesting number and a pass throws it away.
            // Logged rather than asserted, on the same reasoning SilhouetteTests
            // gives: a floor on the tightest pair would be a second, tighter
            // threshold nobody agreed to. Measure, print, gate on one line.
            Debug.Log("rig_proof 3, dorsal/flank clearance over " + measured.Count +
                      " combinations: tightest " + tightest.ToString("F3") + " on " + tightestPair +
                      ". All: " + string.Join(", ", measured));
        }

        /// The world-space AABB of the one part mounted at `socket`, built from
        /// its mesh vertices so a rotated socket does not inflate it.
        static Bounds MountedBounds(GameObject creature, string socket, string part, string pair)
        {
            var t = creature.transform.Find(socket);
            Assert.IsNotNull(t, pair + ": no " + socket + " on the prefab");
            Assert.AreEqual(1, t.childCount,
                pair + ": " + socket + " carries " + t.childCount + " parts, expected exactly one (" +
                part + ") - a part that failed to mount would make this test vacuous");
            var mounted = t.GetChild(0);
            Assert.AreEqual(part, mounted.name, pair + ": " + socket + " carries " + mounted.name);

            var filter = mounted.GetComponentInChildren<MeshFilter>();
            Assert.IsNotNull(filter, pair + ": " + part + " has no MeshFilter");
            Assert.IsNotNull(filter.sharedMesh, pair + ": " + part + " has no mesh");
            var verts = filter.sharedMesh.vertices;
            Assert.Greater(verts.Length, 0, pair + ": " + part + "'s mesh has no vertices");

            var m = filter.transform.localToWorldMatrix;
            var bounds = new Bounds(m.MultiplyPoint3x4(verts[0]), Vector3.zero);
            for (int i = 1; i < verts.Length; i++) bounds.Encapsulate(m.MultiplyPoint3x4(verts[i]));
            return bounds;
        }

        /// The gap on the axis that separates the two boxes best. Positive is
        /// clearance; negative means they overlap on all three axes at once,
        /// which is the only way two AABBs can intersect.
        static float Clearance(Bounds a, Bounds b)
        {
            float x = Mathf.Max(a.min.x, b.min.x) - Mathf.Min(a.max.x, b.max.x);
            float y = Mathf.Max(a.min.y, b.min.y) - Mathf.Min(a.max.y, b.max.y);
            float z = Mathf.Max(a.min.z, b.min.z) - Mathf.Min(a.max.z, b.max.z);
            return Mathf.Max(x, Mathf.Max(y, z));
        }
    }
}
