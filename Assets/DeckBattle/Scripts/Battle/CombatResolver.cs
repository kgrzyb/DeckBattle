using System;

namespace DeckBattle
{
    public static class CombatResolver
    {
        private const long MicrosecondsPerSecond = 1000000L;

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
            if (threshold > 0 && currentMana >= threshold)
            {
                unit.PassiveManaRemainder = 0L;
            }

            if (currentMana == previousMana)
            {
                return;
            }

            unit.CurrentMana = currentMana;
            eventQueue?.Enqueue(BattleEvent.UnitManaChanged(unit.UnitId, currentMana));
        }

        internal static void AccumulatePassiveMana(
            BattleSimulation simulation,
            UnitRuntimeState unit,
            long tickDurationMicroseconds,
            BattleEventQueue eventQueue)
        {
            if (unit == null || tickDurationMicroseconds <= 0L)
            {
                return;
            }

            if (unit.IsDefeated || unit.SpecialPhase != UnitSpecialPhase.Idle)
            {
                return;
            }

            long gainedMicroMana = (long)unit.CombatSpec.ManaPerSecond * tickDurationMicroseconds;
            long accumulatedMicroMana = unit.PassiveManaRemainder + gainedMicroMana;
            int wholeMana = (int)(accumulatedMicroMana / MicrosecondsPerSecond);
            unit.PassiveManaRemainder = accumulatedMicroMana % MicrosecondsPerSecond;
            if (wholeMana > 0)
            {
                AddMana(simulation, unit, wholeMana, eventQueue);
            }
        }

        internal static void GrantCombatManaPulse(
            BattleSimulation simulation,
            UnitRuntimeState unit,
            BattleEventQueue eventQueue)
        {
            if (unit == null)
            {
                return;
            }

            AddMana(simulation, unit, unit.CombatSpec.ManaPerSecond, eventQueue);
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
