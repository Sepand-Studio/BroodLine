using System;
using Broodline.Sim.Combat;
using Broodline.View;
using NUnit.Framework;

namespace Broodline.Frontier.Tests
{
    /// The proof app's gate: formation rules and terminal feedback cues.
    /// Hash neutrality now lives beside production presentation in View/Tests.
    /// The art-core tests live in `Art/Tests/FrontierArtTests.cs`.
    public sealed class FrontierProofTests
    {
        [Test]
        public void FormationRejectsOverlapsAndUnknownPockets()
        {
            Assert.Throws<ArgumentException>(() => FrontierFormation.Create(new[] { 0, 0, 4 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => FrontierFormation.Create(new[] { 0, 2, 5 }));
            Assert.That(FrontierFormation.Create(new[] { 0, 2, 4 })[0].Trait1, Is.EqualTo(Trait.Taunt));
        }

        [Test]
        public void PlacementSwapsOccupantsAndInvalidInputDoesNotMutate()
        {
            var formation = new[] { 0, 2, 4 };
            Assert.That(FrontierFormation.Place(formation, 0, 2), Is.True);
            CollectionAssert.AreEqual(new[] { 2, 0, 4 }, formation);
            Assert.That(FrontierFormation.Place(formation, 0, 2), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => FrontierFormation.Place(formation, 0, 5));
            CollectionAssert.AreEqual(new[] { 2, 0, 4 }, formation);
            Assert.That(FrontierFormation.Place(formation, 2, 1), Is.True);
            CollectionAssert.AreEqual(new[] { 2, 0, 1 }, formation);
        }

        [Test]
        public void FeedbackCapturesTerminalBreachOnlyOnce()
        {
            var wave = WaveDef.ForId(6);
            var runner = new SimRunner(wave, wave.Lane, Array.Empty<CreatureSpec>(), 6);
            var feedback = new FrontierBattleFeedback(runner); int breaches = 0, damage = 0;
            Action<FrontierCue> receive = cue => { if (cue.Kind == FrontierCueKind.Breach) breaches++; if (cue.Kind == FrontierCueKind.Damage) damage++; };
            while (!runner.Done) { runner.Step(); feedback.Observe(runner, receive); }
            Assert.That(breaches, Is.EqualTo(1)); Assert.That(damage, Is.EqualTo(0));
            feedback.Observe(runner, receive);
            Assert.That(breaches, Is.EqualTo(1));
        }
    }
}
