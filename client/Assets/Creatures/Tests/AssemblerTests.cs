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
                Assert.AreEqual(1f, c.GetComponentInChildren<SkinnedMeshRenderer>().material.GetFloat("_Desaturate"), 0.05f);
            }
            finally { Object.DestroyImmediate(c); }
        }
    }
}
