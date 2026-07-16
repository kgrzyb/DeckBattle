using System;

namespace DeckBattle
{
    public readonly struct BattleRuntimeTuning
    {
        public static readonly BattleRuntimeTuning Default = new BattleRuntimeTuning(1f, 0, 1);

        public readonly float AttackCooldownMultiplier;
        public readonly int AttackRangeBonus;
        public readonly int MovementStepsPerTick;

        public BattleRuntimeTuning(float attackCooldownMultiplier, int attackRangeBonus, int movementStepsPerTick)
        {
            AttackCooldownMultiplier = Math.Max(0.01f, attackCooldownMultiplier);
            AttackRangeBonus = attackRangeBonus;
            MovementStepsPerTick = Math.Max(1, movementStepsPerTick);
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
            if (definition == null)
            {
                return 0.01f;
            }

            return Math.Max(0.01f, definition.AttackCooldown * AttackCooldownMultiplier);
        }
    }
}
