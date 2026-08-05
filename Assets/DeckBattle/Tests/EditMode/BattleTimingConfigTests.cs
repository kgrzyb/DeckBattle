using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace DeckBattle.Tests
{
    public sealed class BattleTimingConfigTests
    {
        [Test]
        public void OnValidate_ClampsCombatAccelerationControls()
        {
            BattleTimingConfig config = ScriptableObject.CreateInstance<BattleTimingConfig>();
            try
            {
                config.CombatAccelerationDelay = -2f;
                config.AcceleratedCombatSpeed = 0.5f;

                Validate(config);

                Assert.AreEqual(0f, config.CombatAccelerationDelay);
                Assert.AreEqual(1f, config.AcceleratedCombatSpeed);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void OnValidate_ReplacesInvalidCombatAccelerationControlsWithDefaults()
        {
            BattleTimingConfig config = ScriptableObject.CreateInstance<BattleTimingConfig>();
            try
            {
                config.CombatAccelerationDelay = float.NaN;
                config.AcceleratedCombatSpeed = float.PositiveInfinity;

                Validate(config);

                Assert.AreEqual(BattleTiming.DefaultCombatAccelerationDelay, config.CombatAccelerationDelay);
                Assert.AreEqual(BattleTiming.DefaultAcceleratedCombatSpeed, config.AcceleratedCombatSpeed);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        private static void Validate(BattleTimingConfig config)
        {
            typeof(BattleTimingConfig)
                .GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(config, null);
        }
    }
}
