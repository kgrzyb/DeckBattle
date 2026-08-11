using UnityEngine;

namespace DeckBattle
{
    [CreateAssetMenu(fileName = "BattleRuntimeTuningConfig", menuName = "Deck Battle/Battle Runtime Tuning Config")]
    public sealed class BattleRuntimeTuningConfig : ScriptableObject
    {
        [Header("Base Combat")]
        [Min(0.01f)] public float AttackCooldownMultiplier = 1f;
        public int AttackRangeBonus;
        [Min(0.01f)] public float MovementStepDuration = 0.4f;
        [Min(0)] public int MaxPursuitStepsAfterAttack = 2;

        [Header("Specials")]
        [Min(0f)] public float SpecialRecoveryLockDuration = 0.5f;

        [Header("Status Limits")]
        [Min(1)] public int MaxStatusesPerUnit = 8;
        [Min(0.01f)] public float MinDamageMultiplier = 0.1f;
        [Min(0.01f)] public float MaxDamageMultiplier = 3f;
        [Min(0.01f)] public float MinAttackCooldownMultiplier = 0.1f;
        [Min(0.01f)] public float MaxAttackCooldownMultiplier = 3f;
        [Min(1f)] public float MaxMovementSlowMultiplier = 3f;

        public BattleRuntimeTuning CreateRuntimeTuning()
        {
            return new BattleRuntimeTuning(
                AttackCooldownMultiplier,
                AttackRangeBonus,
                MovementStepDuration,
                MaxPursuitStepsAfterAttack,
                MaxStatusesPerUnit,
                MinDamageMultiplier,
                MaxDamageMultiplier,
                MinAttackCooldownMultiplier,
                MaxAttackCooldownMultiplier,
                MaxMovementSlowMultiplier,
                SpecialRecoveryLockDuration);
        }

        private void OnValidate()
        {
            AttackCooldownMultiplier = Mathf.Max(0.01f, AttackCooldownMultiplier);
            MovementStepDuration = Mathf.Max(0.01f, MovementStepDuration);
            MaxPursuitStepsAfterAttack = Mathf.Max(0, MaxPursuitStepsAfterAttack);
            SpecialRecoveryLockDuration = Mathf.Max(0f, SpecialRecoveryLockDuration);
            MaxStatusesPerUnit = Mathf.Max(1, MaxStatusesPerUnit);
            MinDamageMultiplier = Mathf.Max(0.01f, MinDamageMultiplier);
            MaxDamageMultiplier = Mathf.Max(MinDamageMultiplier, MaxDamageMultiplier);
            MinAttackCooldownMultiplier = Mathf.Max(0.01f, MinAttackCooldownMultiplier);
            MaxAttackCooldownMultiplier = Mathf.Max(MinAttackCooldownMultiplier, MaxAttackCooldownMultiplier);
            MaxMovementSlowMultiplier = Mathf.Max(1f, MaxMovementSlowMultiplier);
        }
    }
}
