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
        /// it: on the right flank (negative z, since +Z is the creature's left)
        /// Euler(90,0,0) sends +Y to +Z, straight into the body.
        ///
        /// It is also what re-checks the three sockets after a body is
        /// reshaped. Task 12b lowered and narrowed Vetch's shell and moved all
        /// three onto the new surface; nothing but this says they still face
        /// out of it.
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
            Assert.IsNotNull(SpeciesRecipes.For("Vetch"), "case-insensitive, like CreatureSprites and the retired SpeciesProxy");
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

        /// THE COLOUR RULE ITSELF, ASSERTED. `PartRecipes` derives every part's
        /// colour from the species bible 1.2 gives the trait to, at
        /// `PartValue` of its value and `PartSaturation` of its saturation -
        /// and the reason it derives rather than declares is that when the
        /// twelve were typed by hand, ten came out byte-identical to the body
        /// they mount on. This says the derivation is still what is happening,
        /// and it catches a misspelt owner, which `SpeciesColours.For` answers
        /// with magenta.
        ///
        /// `PartVisibilityTests` asserts the CONSEQUENCE - that a part actually
        /// separates from every body - over the baked sprites. This asserts the
        /// rule, which is cheaper and fails more legibly when it is the rule
        /// that broke.
        [Test]
        public void EveryPartsColour_IsDerivedFromTheSpeciesThatOwnsItsTrait()
        {
            var owners = new System.Collections.Generic.Dictionary<string, string>
            {
                { "carapace", "vetch" }, { "taunt", "vetch" },
                { "cinder", "ember" }, { "splash", "ember" },
                { "sprint", "skitter" }, { "litter", "skitter" },
                { "reach", "hollow" }, { "pierce", "hollow" },
                { "regrow", "loam" }, { "burrow", "loam" },
                { "screen", "pale" }, { "chill", "pale" },
            };
            Assert.AreEqual(owners.Count, PartRecipes.All.Count,
                "PartRecipes carries " + PartRecipes.All.Count + " parts and this test names " +
                owners.Count + " owners - a part added without one is a part with no colour rule");

            foreach (var part in PartRecipes.All)
            {
                Assert.IsTrue(owners.ContainsKey(part.Id), "no owning species for trait " + part.Id);
                var species = SpeciesColours.For(owners[part.Id]);
                AssertDerived(part.Id + " Base", part.Base, species.Base);
                AssertDerived(part.Id + " Under", part.Under, species.Under);
            }
        }

        static void AssertDerived(string what, Color actual, Color fromSpecies)
        {
            Color.RGBToHSV(fromSpecies, out var h, out var s, out var v);
            var expected = Color.HSVToRGB(h, Mathf.Clamp01(s * PartRecipes.PartSaturation),
                                          v * PartRecipes.PartValue);
            Assert.AreEqual(0f, new Vector3(actual.r - expected.r, actual.g - expected.g,
                                            actual.b - expected.b).magnitude, 2e-3f,
                what + " is " + actual + ", but its owning species scaled by PartValue " +
                PartRecipes.PartValue + " and PartSaturation " + PartRecipes.PartSaturation +
                " is " + expected + ". A part typed in hex is a part that can drift onto its " +
                "own body's colour - ten of twelve did.");
        }

        /// rig_proof.md section 3 authors a socket as a point ON the body's
        /// surface. `EverySocket_FacesOutOfTheBody` above only says which way
        /// it points, and that is satisfied by a socket floating clear of the
        /// body or buried deep inside it - Ember's dorsal was 0.151 INSIDE the
        /// torso when it was first authored, which faces outward perfectly well
        /// and mounts a part inside the animal.
        ///
        /// It also logs the local curvature under the two combat sockets, which
        /// section 3 asks to be comparable ("similar local curvature and
        /// similar scale"). Logged rather than asserted, and deliberately:
        /// Skitter measures 3.08 because its flank sits on a leg's bulge rather
        /// than on its 0.40-wide torso, and moving it to the torso costs 69% of
        /// the flank part's pixels against bible 10.4's "single most important
        /// functional requirement". Section 3 states the collision clause as a
        /// requirement and the curvature clause as advice that "costs nothing";
        /// on the smallest bodies it does cost something, and a threshold
        /// nobody agreed to would decide that trade silently.
        [Test]
        public void EveryCombatSocket_SitsOnTheBodysSurface()
        {
            const float tolerance = 0.05f;
            var curvature = new System.Collections.Generic.List<string>();
            foreach (var body in AllBodies)
            {
                foreach (var s in body.Sockets)
                {
                    float d = Sdf.Field(body.Primitives, body.Blend, s.Position);
                    Assert.AreEqual(0f, d, tolerance,
                        body.Id + " socket " + s.Name + " sits " + d.ToString("F3") + " from the surface " +
                        "(negative is inside). A socket off the surface mounts a part that floats or is buried.");
                }
                var dorsal = body.Sockets.FirstOrDefault(x => x.Name == Sockets.Dorsal);
                var flank = body.Sockets.FirstOrDefault(x => x.Name == Sockets.Flank);
                if (dorsal == null || flank == null) continue;
                float rd = RadiusUnder(body, dorsal.Position), rf = RadiusUnder(body, flank.Position);
                curvature.Add(body.Id + " " + rd.ToString("F3") + "/" + rf.ToString("F3") +
                              " = " + (Mathf.Max(rd, rf) / Mathf.Max(1e-4f, Mathf.Min(rd, rf))).ToString("F2"));
            }
            Debug.Log("rig_proof 3, local curvature under the two combat sockets (dorsal/flank radius, ratio): " +
                      string.Join(", ", curvature));
        }

        /// The radius of whichever primitive's surface the socket is nearest -
        /// a stand-in for local curvature that needs no second derivative and
        /// says the thing an author would say: "it is sitting on the neck".
        static float RadiusUnder(BodyRecipe body, Vector3 at)
        {
            float best = float.MaxValue, radius = 0f;
            foreach (var p in body.Primitives)
            {
                float d = Sdf.Eval(p, at);
                if (d >= best) continue;
                best = d;
                radius = p.Kind == PrimitiveKind.Box
                    ? Mathf.Min(p.Half.x, Mathf.Min(p.Half.y, p.Half.z))
                    : p.Radius;
            }
            return radius;
        }
    }
}
