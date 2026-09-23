using System;
using Broodline.Frontier;
using Broodline.Sim.Combat;
using Broodline.View;

// Runs against the actual engine/presentation-observer source with no Unity dependency.
internal static class FrontierCombatCheck
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    static void Compare(int id, int[] pockets)
    {
        var wave = WaveDef.ForId(id); var formation = FrontierFormation.Create(pockets);
        var direct = new SimRunner(wave, wave.Lane, formation, 6);
        while (direct.Step()) { }
        var observed = new SimRunner(wave, wave.Lane, formation, 6);
        var initialHp = observed.CreatureHp.ToArray();
        var pair = new WavePair(observed); var clock = new WaveClock();
        var feedback = new FrontierBattleFeedback(observed);
        int enemyDamage = 0, companionDamage = 0, defeats = 0, breaches = 0, cues = 0;
        Action<FrontierCue> receive = cue =>
        {
            cues++;
            if (cue.Kind == FrontierCueKind.Damage)
            { Check(cue.Amount > 0, "nonpositive damage"); if (cue.Defender) companionDamage += cue.Amount; else enemyDamage += cue.Amount; }
            if (cue.Kind == FrontierCueKind.Defeated && !cue.Defender) defeats++;
            if (cue.Kind == FrontierCueKind.Breach) breaches++;
        };
        int frames = 0;
        while (!clock.Terminated && frames++ < Stats.HardTickCap * 4)
            clock.Advance(observed, frames % 3 == 0 ? .1 : 1.0 / 60, () =>
            {
                pair.Advance(observed); feedback.Observe(observed, receive);
                int before = cues; feedback.Observe(observed, receive);
                Check(before == cues, "duplicate cues for the same tick");
            });
        Check(clock.Terminated, "not terminated");
        Check(direct.Outcome.Hash == observed.Outcome.Hash, "observer changed hash");
        Check(pair.Current.Integrity == observed.Integrity && pair.Current.Tick == observed.Tick, "stale final snapshot");
        int expectedEnemyDamage = 0, expectedCompanionDamage = 0, expectedDefeats = 0;
        for (int i = 0; i < observed.RaiderCount; i++)
        {
            expectedEnemyDamage += Stats.RaiderHp(observed.RaiderType[i]) - observed.RaiderHp[i];
            if (observed.RaiderHp[i] <= 0) expectedDefeats++;
        }
        for (int i = 0; i < initialHp.Length; i++) expectedCompanionDamage += initialHp[i] - observed.CreatureHp[i];
        Check(enemyDamage == expectedEnemyDamage && companionDamage == expectedCompanionDamage, "damage cue totals do not match HP loss");
        Check(defeats == expectedDefeats, "missing/duplicate defeat cue");
        Check(breaches == observed.Outcome.Breaches.Length, "missing/duplicate breach cue");
    }

    static void EdgeCases()
    {
        var wave = WaveDef.ForId(6);
        var runner = new SimRunner(wave, wave.Lane, Array.Empty<CreatureSpec>(), 6);
        var feedback = new FrontierBattleFeedback(runner); int breaches = 0;
        Action<FrontierCue> receive = cue => { if (cue.Kind == FrontierCueKind.Breach) breaches++; };
        while (!runner.Done) { runner.Step(); feedback.Observe(runner, receive); }
        feedback.Observe(runner, receive);
        Check(breaches == 1, "terminal breach not delivered exactly once");
        wave = WaveDef.ForId(7);
        runner = new SimRunner(wave, wave.Lane, FrontierFormation.Create(new[] { 0, 2, 4 }), 6);
        feedback = new FrontierBattleFeedback(runner); int rallies = 0;
        var clock = new WaveClock(); clock.RequestRally(0);
        clock.Advance(runner, 1.0 / 30, () => feedback.Observe(runner, cue => { if (cue.Kind == FrontierCueKind.Rally) rallies++; }));
        Check(runner.RallyUsed && rallies == 1 && !runner.TryRally(1), "Rally consumption/feedback mismatch");
        feedback.Observe(runner, cue => { if (cue.Kind == FrontierCueKind.Rally) rallies++; });
        Check(rallies == 1, "duplicate Rally cue");
        int[] invalid = { 0, 2, 4 }; bool rejected = false;
        try { FrontierFormation.Place(invalid, 0, 5); } catch (ArgumentOutOfRangeException) { rejected = true; }
        Check(rejected && invalid[0] == 0 && invalid[1] == 2 && invalid[2] == 4, "invalid placement mutated formation");
    }

    static int Main()
    {
        int formations = 0, placements = 0;
        for (int a = 0; a < 5; a++) for (int b = 0; b < 5; b++) for (int c = 0; c < 5; c++)
        {
            if (a == b || a == c || b == c) continue;
            var pockets = new[] { a, b, c };
            foreach (int wave in new[] { 6, 7 }) { Compare(wave, pockets); formations++; }
            for (int companion = 0; companion < 3; companion++) for (int pocket = 0; pocket < 5; pocket++)
            {
                var moved = (int[])pockets.Clone(); int occupant = Array.IndexOf(pockets, pocket);
                bool changed = FrontierFormation.Place(moved, companion, pocket);
                Check(changed == (pockets[companion] != pocket), "incorrect changed result");
                Check(moved[companion] == pocket, "selected companion not placed");
                if (occupant >= 0) Check(moved[occupant] == pockets[companion], "occupied stone did not swap");
                FrontierFormation.Create(moved); placements++;
            }
        }
        EdgeCases();
        Console.WriteLine("PASS: " + formations + " wave/formation combinations; unchanged hashes, final snapshots, exact damage/defeat/breach cue totals, and no duplicate cues.");
        Console.WriteLine("PASS: " + placements + " placements/swaps; invalid-input atomicity, terminal breach and tick-boundary Rally.");
        return 0;
    }
}
