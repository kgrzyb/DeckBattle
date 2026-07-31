using System;

namespace DeckBattle
{
    public readonly struct BattleRuntimeTuning
    {
        public static readonly BattleRuntimeTuning Default = new BattleRuntimeTuning(1f, 0, 0.4f, 2);

        public readonly float AttackCooldownMultiplier;
        public readonly int AttackRangeBonus;
        public readonly float MovementStepDuration;
        public readonly int MaxPursuitStepsAfterAttack;
        public readonly int MaxStatusesPerUnit;
        public readonly float MinDamageMultiplier;
        public readonly float MaxDamageMultiplier;
        public readonly float MinAttackCooldownMultiplier;
        public readonly float MaxAttackCooldownMultiplier;
        public readonly float MaxMovementSlowMultiplier;

        public BattleRuntimeTuning(
            float attackCooldownMultiplier,
            int attackRangeBonus,
            float movementStepDuration = 0.4f,
            int maxPursuitStepsAfterAttack = 2,
            int maxStatusesPerUnit = 8,
            float minDamageMultiplier = 0.1f,
            float maxDamageMultiplier = 3f,
            float minAttackCooldownMultiplier = 0.1f,
            float maxAttackCooldownMultiplier = 3f,
            float maxMovementSlowMultiplier = 3f)
        {
            AttackCooldownMultiplier = Math.Max(0.01f, attackCooldownMultiplier);
            AttackRangeBonus = attackRangeBonus;
            MovementStepDuration = Math.Max(0.01f, movementStepDuration);
            MaxPursuitStepsAfterAttack = Math.Max(0, maxPursuitStepsAfterAttack);
            MaxStatusesPerUnit = Math.Max(1, maxStatusesPerUnit);
            MinDamageMultiplier = Math.Max(0.01f, minDamageMultiplier);
            MaxDamageMultiplier = Math.Max(MinDamageMultiplier, maxDamageMultiplier);
            MinAttackCooldownMultiplier = Math.Max(0.01f, minAttackCooldownMultiplier);
            MaxAttackCooldownMultiplier = Math.Max(MinAttackCooldownMultiplier, maxAttackCooldownMultiplier);
            MaxMovementSlowMultiplier = Math.Max(1f, maxMovementSlowMultiplier);
        }

        public int GetAttackRange(UnitCombatSpec combatSpec)
        {
            return Math.Max(1, combatSpec.AttackRange + AttackRangeBonus);
        }

        public float GetAttackCooldown(UnitCombatSpec combatSpec)
        {
            return GetAttackCooldown(combatSpec, null);
        }

        public float GetAttackCooldown(UnitCombatSpec combatSpec, UnitRuntimeState runtimeState)
        {
            float statusMultiplier = EffectiveStatsResolver.GetAttackCooldownMultiplier(runtimeState, this);
            return Math.Max(0.01f, combatSpec.AttackCooldown * AttackCooldownMultiplier * statusMultiplier);
        }
    }
}
