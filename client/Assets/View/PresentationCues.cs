using System;
using Broodline.Sim.Combat;

namespace Broodline.View
{
    public enum FrontierCueKind { Attack, Damage, Chilled, Defeated, Breach, Rally }

    public readonly struct FrontierCue
    {
        public readonly FrontierCueKind Kind;
        public readonly bool Defender;
        public readonly int Entity, Amount, Target;
        public readonly long ProgressRaw;
        public FrontierCue(FrontierCueKind kind, bool defender, int entity, int amount = 0, int target = -1, long progressRaw = 0)
        { Kind = kind; Defender = defender; Entity = entity; Amount = amount; Target = target; ProgressRaw = progressRaw; }
    }

    /// Presentation events inferred from completed ticks. Never mutates the runner.
    /// The receiver is synchronous; catch-up ticks cannot overwrite unconsumed cues.
    public sealed class FrontierBattleFeedback
    {
        readonly int[] _defenderHp, _raiderHp, _attackAt;
        readonly bool[] _seen, _chilled, _breached;
        bool _rallyUsed;

        public FrontierBattleFeedback(SimRunner runner)
        {
            _defenderHp = runner.CreatureHp.ToArray();
            _attackAt = runner.CreatureNextAttackAt.ToArray();
            _raiderHp = new int[runner.RaiderHp.Length];
            _seen = new bool[_raiderHp.Length]; _chilled = new bool[_raiderHp.Length]; _breached = new bool[_raiderHp.Length];
            _rallyUsed = runner.RallyUsed;
            for (int i = 0; i < runner.RaiderCount; i++)
            { _seen[i] = true; _raiderHp[i] = runner.RaiderHp[i]; _chilled[i] = runner.RaiderChilled[i]; _breached[i] = runner.RaiderBreachedThisTick(i); }
        }

        public void Observe(SimRunner runner, Action<FrontierCue> receive)
        {
            if (receive == null) throw new ArgumentNullException(nameof(receive));
            for (int i = 0; i < _defenderHp.Length; i++)
            {
                int hp = runner.CreatureHp[i];
                if (hp < _defenderHp[i]) receive(new FrontierCue(FrontierCueKind.Damage, true, i, _defenderHp[i] - hp));
                if (hp <= 0 && _defenderHp[i] > 0) receive(new FrontierCue(FrontierCueKind.Defeated, true, i));
                if (runner.CreatureNextAttackAt[i] > _attackAt[i] && runner.CreatureTarget[i] >= 0)
                    receive(new FrontierCue(FrontierCueKind.Attack, true, i, target: runner.CreatureTarget[i]));
                if (!_rallyUsed && runner.RallyUsed && runner.CreatureRallyRemaining(i) > 0)
                    receive(new FrontierCue(FrontierCueKind.Rally, true, i));
                _defenderHp[i] = hp; _attackAt[i] = runner.CreatureNextAttackAt[i];
            }
            _rallyUsed = runner.RallyUsed;
            for (int i = 0; i < runner.RaiderCount; i++)
            {
                if (!_seen[i]) { _seen[i] = true; _raiderHp[i] = Stats.RaiderHp(runner.RaiderType[i]); }
                int hp = runner.RaiderHp[i]; long progress = runner.RaiderProgress[i].Raw;
                if (hp < _raiderHp[i]) receive(new FrontierCue(FrontierCueKind.Damage, false, i, _raiderHp[i] - hp, progressRaw: progress));
                if (hp <= 0 && _raiderHp[i] > 0) receive(new FrontierCue(FrontierCueKind.Defeated, false, i, progressRaw: progress));
                if (runner.RaiderChilled[i] && !_chilled[i]) receive(new FrontierCue(FrontierCueKind.Chilled, false, i, progressRaw: progress));
                if (runner.RaiderBreachedThisTick(i) && !_breached[i])
                { _breached[i] = true; receive(new FrontierCue(FrontierCueKind.Breach, false, i, progressRaw: progress)); }
                _raiderHp[i] = hp; _chilled[i] = runner.RaiderChilled[i];
            }
        }
    }
}
