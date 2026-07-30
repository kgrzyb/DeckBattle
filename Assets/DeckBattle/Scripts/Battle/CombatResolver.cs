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
            if (unit == null || amount <= 0 || unit.IsDefeated)
            {
                return;
            }

            int threshold = unit.Definition.ManaThreshold;
            unit.CurrentMana = threshold > 0
                ? Math.Min(threshold, Math.Max(0, unit.CurrentMana + amount))
                : Math.Max(0, unit.CurrentMana + amount);
            eventQueue?.Enqueue(BattleEvent.UnitManaChanged(unit.UnitId, unit.CurrentMana));

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
