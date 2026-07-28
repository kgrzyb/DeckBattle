using NUnit.Framework;
using UnityEngine;

namespace DeckBattle.Tests
{
    public sealed class BattleRuntimeTuningConfigTests
    {
        [Test]
        public void CreateRuntimeTuning_MapsAllConfigurableLimits()
        {
            BattleRuntimeTuningConfig config = ScriptableObject.CreateInstance<BattleRuntimeTuningConfig>();
            try
            {
                config.AttackCooldownMultiplier = 1.25f;
                config.AttackRangeBonus = 2;
                config.MovementStepDuration = 0.5f;
                config.MaxPursuitStepsAfterAttack = 4;
                config.MaxStatusesPerUnit = 12;
                config.MinDamageMultiplier = 0.2f;
                config.MaxDamageMultiplier = 2.5f;
                config.MinAttackCooldownMultiplier = 0.3f;
                config.MaxAttackCooldownMultiplier = 1.5f;
                config.MaxMovementSlowMultiplier = 2f;

                BattleRuntimeTuning tuning = config.CreateRuntimeTuning();

                Assert.AreEqual(1.25f, tuning.AttackCooldownMultiplier);
                Assert.AreEqual(2, tuning.AttackRangeBonus);
                Assert.AreEqual(0.5f, tuning.MovementStepDuration);
                Assert.AreEqual(4, tuning.MaxPursuitStepsAfterAttack);
                Assert.AreEqual(12, tuning.MaxStatusesPerUnit);
                Assert.AreEqual(0.2f, tuning.MinDamageMultiplier);
                Assert.AreEqual(2.5f, tuning.MaxDamageMultiplier);
                Assert.AreEqual(0.3f, tuning.MinAttackCooldownMultiplier);
                Assert.AreEqual(1.5f, tuning.MaxAttackCooldownMultiplier);
                Assert.AreEqual(2f, tuning.MaxMovementSlowMultiplier);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
    }
}
