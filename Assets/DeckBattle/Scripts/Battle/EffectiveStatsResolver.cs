using System;

namespace DeckBattle
{
    public static class EffectiveStatsResolver
    {
        public static float GetAttackCooldownMultiplier(UnitRuntimeState unit, BattleRuntimeTuning tuning)
        {
            if (unit == null) return 1f;
            return Clamp(1f + unit.StatusSnapshot.Slow - unit.StatusSnapshot.Haste, tuning.MinAttackCooldownMultiplier, tuning.MaxAttackCooldownMultiplier);
        }

        public static float GetMovementStepDuration(UnitRuntimeState unit, BattleRuntimeTuning tuning)
        {
            float slowMultiplier = unit == null ? 1f : Clamp(1f + unit.StatusSnapshot.Slow, 1f, tuning.MaxMovementSlowMultiplier);
            return tuning.MovementStepDuration * slowMultiplier;
        }

        public static float GetOutgoingDamageMultiplier(UnitRuntimeState unit, BattleRuntimeTuning tuning)
        {
            if (unit == null) return 1f;
            return Clamp(1f + unit.StatusSnapshot.Empower - unit.StatusSnapshot.Weaken, tuning.MinDamageMultiplier, tuning.MaxDamageMultiplier);
        }

        public static float GetCriticalMultiplier(UnitRuntimeState unit)
        {
            return unit == null ? 1f : Math.Max(1f, unit.Definition.CritMultiplier + unit.StatusSnapshot.Criticality);
        }

        private static float Clamp(float value, float min, float max) { return Math.Max(min, Math.Min(max, value)); }
    }
}
