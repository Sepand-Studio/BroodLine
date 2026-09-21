using System.Linq;
using Broodline.Creatures;
using NUnit.Framework;

namespace Broodline.Creatures.Tests
{
    public class RecipeTests
    {
        static readonly string[] Standard = { Sockets.Dorsal, Sockets.Flank, Sockets.Crown };

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
            foreach (var body in SpeciesRecipes.All)
            {
                var bones = body.Bones.Select(b => b.Name).ToHashSet();
                foreach (var p in body.Primitives)
                    Assert.IsTrue(bones.Contains(p.Bone), body.Id + " primitive names unknown bone " + p.Bone);
                Assert.LessOrEqual(body.Bones.Length, 8, body.Id + " exceeds eight bones");
                Assert.AreEqual(1, body.Bones.Count(b => b.Parent == null), body.Id + " must have exactly one root bone");
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
