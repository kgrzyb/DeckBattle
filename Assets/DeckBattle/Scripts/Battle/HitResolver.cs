using System;

namespace DeckBattle
{
    public static class HitResolver
    {
        public static HitResolutionResult ResolveHit(BattleSimulation simulation, UnitRuntimeState attacker, UnitRuntimeState target, int damage, bool isCritical, BattleEventQueue eventQueue)
        {
            if (simulation == null) throw new ArgumentNullException(nameof(simulation));
            if (target == null || !target.IsAlive) return default;

            if (eventQueue != null && isCritical)
            {
                eventQueue.Enqueue(BattleEvent.UnitCrit(attacker != null ? attacker.UnitId : 0, target.UnitId));
            }

            int safeDamage = Math.Max(0, damage);
            target.CurrentHp -= safeDamage;
            if (eventQueue != null)
            {
                eventQueue.Enqueue(BattleEvent.UnitDamaged(target.UnitId, safeDamage, Math.Max(0, target.CurrentHp)));
            }

            if (safeDamage > 0)
            {
                CombatResolver.AddMana(simulation, target, target.Definition.ManaPerDamageTaken, eventQueue);
            }

            bool died = target.CurrentHp <= 0 && !target.IsDefeated;
            if (died)
            {
                simulation.DefeatUnit(target);
                if (eventQueue != null) eventQueue.Enqueue(BattleEvent.UnitDied(target.UnitId));
            }

            return new HitResolutionResult(true, safeDamage, died);
        }
    }

    public readonly struct HitResolutionResult
    {
        public readonly bool DidHit;
        public readonly int Damage;
        public readonly bool Died;

        public HitResolutionResult(bool didHit, int damage, bool died)
        {
            DidHit = didHit;
            Damage = damage;
            Died = died;
        }
    }
}
