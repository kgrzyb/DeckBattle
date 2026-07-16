using System;

namespace DeckBattle
{
    public readonly struct BattleRuntimeTuning
    {
        public static readonly BattleRuntimeTuning Default = new BattleRuntimeTuning(1f, 0, 0.4f);

        public readonly float AttackCooldownMultiplier;
        public readonly int AttackRangeBonus;
        public readonly float MovementStepDuration;

        public BattleRuntimeTuning(float attackCooldownMultiplier, int attackRangeBonus, float movementStepDuration = 0.4f)
        {
            AttackCooldownMultiplier = Math.Max(0.01f, attackCooldownMultiplier);
            AttackRangeBonus = attackRangeBonus;
            MovementStepDuration = Math.Max(0.01f, movementStepDuration);
        }

        public int GetAttackRange(UnitDefinition definition)
        {
            if (definition == null)
            {
                return 1;
            }

            return Math.Max(1, definition.AttackRange + AttackRangeBonus);
        }

        public float GetAttackCooldown(UnitDefinition definition)
        {
            return GetAttackCooldown(definition, null);
        }

        public float GetAttackCooldown(UnitDefinition definition, UnitRuntimeState runtimeState)
        {
            if (definition == null)
            {
                return 0.01f;
            }

            float runtimeMultiplier = runtimeState != null ? runtimeState.AttackCooldownMultiplier : 1f;
            return Math.Max(0.01f, definition.AttackCooldown * AttackCooldownMultiplier * Math.Max(0.01f, runtimeMultiplier));
        }
    }
}
