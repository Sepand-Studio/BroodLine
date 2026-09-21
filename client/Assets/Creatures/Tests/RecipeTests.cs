using System.Linq;
using Broodline.Creatures;
using NUnit.Framework;
using UnityEngine;

namespace Broodline.Creatures.Tests
{
    public class RecipeTests
    {
        static readonly string[] Standard = { Sockets.Dorsal, Sockets.Flank, Sockets.Crown };

        /// Bodies AND raiders. A raider is a BodyRecipe meshed by the same
        /// mesher, skinned by the same WeightsAt, and MesherTests already
        /// budgets them with `.Concat(RaiderRecipes.All)` - a bone gate that
        /// stops at the species is inconsistent with the suite it lives in.
        static System.Collections.Generic.IEnumerable<BodyRecipe> AllBodies =>
            SpeciesRecipes.All.Concat(RaiderRecipes.All);

        [Test]
        public void EveryBody_CarriesTheThreeStandardSockets()
        {
            // rig_proof.md section 3: authored on every body, before the first
            // creature is modelled. A socket that has never carried geometry
            // is not standardised; a body without one is not a body.
            foreach (var body in SpeciesRecipes.All)
            {
                var names = body.Sockets.Select(s => s.Name).ToArray();
                CollectionAssert.IsSubsetOf(Standard, names, body.Id + " is missing a standard socket");
                Assert.IsTrue(body.Sockets.All(s => s.Scale > 0f), body.Id + " has a zero-scale socket");
            }
        }

        [Test]
        public void EveryBody_NamesOnlyBonesItDeclares()
        {
            foreach (var body in AllBodies)
            {
                var bones = body.Bones.Select(b => b.Name).ToHashSet();
                foreach (var p in body.Primitives)
                    Assert.IsTrue(bones.Contains(p.Bone), body.Id + " primitive names unknown bone " + p.Bone);
                Assert.LessOrEqual(body.Bones.Length, 8, body.Id + " exceeds eight bones");
                Assert.AreEqual(1, body.Bones.Count(b => b.Parent == null), body.Id + " must have exactly one root bone");
            }
        }

        /// SurfaceNets.WeightsAt does `IndexOf(boneNames, p.Bone); if (bone < 0)
        /// bone = 0;` - a typo'd bone name meshes cleanly and silently skins to
        /// the root. The test above is the only thing that catches it, which is
        /// why it has to cover raiders too.
        [Test]
        public void EveryBone_NamesAParentItDeclares()
        {
            // CreatureGenerator indexes the parent with an unchecked IndexOf and
            // parents the bone to bones[that]. A parent that is not declared is
            // -1, and the generator dies with a bare IndexOutOfRangeException
            // naming nothing. Fail here instead, saying which bone and which
            // parent.
            foreach (var body in AllBodies)
            {
                var names = body.Bones.Select(b => b.Name).ToHashSet();
                foreach (var b in body.Bones)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(b.Name), body.Id + " has an unnamed bone");
                    if (b.Parent == null) continue;
                    Assert.IsTrue(names.Contains(b.Parent),
                        body.Id + " bone " + b.Name + " names undeclared parent " + b.Parent);
                    Assert.AreNotEqual(b.Name, b.Parent, body.Id + " bone " + b.Name + " is its own parent");
                }
                Assert.AreEqual(body.Bones.Length, names.Count, body.Id + " declares a duplicate bone name");
            }
        }

        /// Recipe.cs: "a part is authored at the origin with +Y pointing out of
        /// the body surface". So the socket's rotated +Y, in body space, must
        /// lead OUT of the body - and the field itself is the arbiter: step
        /// along it and the signed distance must rise, step against it and it
        /// must fall. Vetch's flank socket failed this as the brief authored
        /// it: at z = -0.5 (the right flank, since +Z is the creature's left)
        /// Euler(90,0,0) sends +Y to +Z, straight into the body.
        [Test]
        public void EverySocket_FacesOutOfTheBody()
        {
            const float eps = 0.05f;
            foreach (var body in AllBodies)
            {
                foreach (var s in body.Sockets)
                {
                    var outward = Quaternion.Euler(s.Euler) * Vector3.up;
                    float ahead = Sdf.Field(body.Primitives, body.Blend, s.Position + outward * eps);
                    float behind = Sdf.Field(body.Primitives, body.Blend, s.Position - outward * eps);
                    Assert.Greater(ahead, behind,
                        body.Id + " socket " + s.Name + " points into the body: stepping along its +Y " +
                        "(" + outward + ") moves toward the surface, not away from it");
                }
            }
        }

        [Test]
        public void SpeciesRecipes_AreLookedUpByLowercaseName_AndVetchExists()
        {
            Assert.IsNotNull(SpeciesRecipes.For("vetch"));
            Assert.IsNotNull(SpeciesRecipes.For("Vetch"), "case-insensitive, like SpeciesProxy was");
            Assert.IsNull(SpeciesRecipes.For("ash"), "an unknown species is null, never a guess");
        }

        [Test]
        public void PartRecipes_HaveColoursAndAreLookedUpByTrait()
        {
            foreach (var part in PartRecipes.All)
                Assert.AreNotEqual(part.Base, part.Under, part.Id + " needs a darker underside");
            Assert.IsNotNull(PartRecipes.For("cinder"));
            Assert.IsNotNull(PartRecipes.For("Carapace"));
        }
    }
}
