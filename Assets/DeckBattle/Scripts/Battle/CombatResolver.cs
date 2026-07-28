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

            if (threshold <= 0 || unit.CurrentMana < threshold)
            {
                return;
            }

            TryActivateReadySpecial(simulation, unit, eventQueue);
        }

        public static bool TryActivateReadySpecial(
            BattleSimulation simulation,
            UnitRuntimeState unit,
            BattleEventQueue eventQueue)
        {
            if (simulation == null || unit == null || !unit.IsAlive)
            {
                return false;
            }

            int threshold = unit.Definition.ManaThreshold;
            if (threshold <= 0 || unit.CurrentMana < threshold || !UnitActionRules.CanActivateSpecial(unit))
            {
                return false;
            }

            UnitSpecialDefinition special = unit.Definition.Special;
            if (special == null
                || special.Kind != UnitSpecialKind.HasteBurst
                || special.AppliedStatus == null
                || special.AppliedStatus.Kind != StatusKind.Haste)
            {
                return false;
            }

            StatusApplicationResult applicationResult = StatusResolver.TryApply(
                simulation,
                unit,
                new StatusApplicationRequest(special.AppliedStatus, unit.UnitId),
                eventQueue);
            if (applicationResult != StatusApplicationResult.Applied
                && applicationResult != StatusApplicationResult.Refreshed)
            {
                return false;
            }

            unit.CurrentMana = 0;
            eventQueue?.Enqueue(BattleEvent.UnitManaChanged(unit.UnitId, unit.CurrentMana));
            eventQueue?.Enqueue(BattleEvent.UnitSpecialActivated(
                unit.UnitId,
                special.Kind,
                special.AppliedStatus.DefaultDuration));
            return true;
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
