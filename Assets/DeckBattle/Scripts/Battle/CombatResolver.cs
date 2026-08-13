using System;

namespace DeckBattle
{
    public static class CombatResolver
    {
        internal static void AddMana(
            BattleSimulation simulation,
            UnitRuntimeState unit,
            int amount,
            BattleEventQueue eventQueue)
        {
            if (unit == null
                || amount <= 0
                || unit.IsDefeated
                || unit.SpecialPhase != UnitSpecialPhase.Idle)
            {
                return;
            }

            int previousMana = unit.CurrentMana;
            int threshold = unit.CombatSpec.ManaThreshold;
            int currentMana = threshold > 0
                ? Math.Min(threshold, Math.Max(0, unit.CurrentMana + amount))
                : Math.Max(0, unit.CurrentMana + amount);
            if (currentMana == previousMana)
            {
                return;
            }

            unit.CurrentMana = currentMana;
            eventQueue?.Enqueue(BattleEvent.UnitManaChanged(unit.UnitId, currentMana));
        }

        internal static void GrantManaPulse(
            BattleSimulation simulation,
            UnitRuntimeState unit,
            BattleEventQueue eventQueue)
        {
            if (unit == null)
            {
                return;
            }

            AddMana(simulation, unit, unit.CombatSpec.ManaPerTick, eventQueue);
        }
    }

    public readonly struct CombatResolutionResult
    {
        public readonly int Attacks;
        public readonly int TotalDamage;
        public readonly int Deaths;

        public CombatResolutionResult(int attacks, int totalDamage, int deaths)
        {
            Attacks = attacks;
            TotalDamage = totalDamage;
            Deaths = deaths;
        }
    }
}
