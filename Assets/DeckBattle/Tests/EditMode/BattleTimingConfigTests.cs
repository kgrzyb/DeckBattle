using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace DeckBattle.Tests
{
    public sealed class BattleTimingConfigTests
    {
        [Test]
        public void OnValidate_ClampsTimedPresentationControls()
        {
            BattleTimingConfig config = ScriptableObject.CreateInstance<BattleTimingConfig>();
            try
            {
                config.CombatAccelerationDelay = -2f;
                config.AcceleratedCombatSpeed = 0.5f;
                config.AnimationCrossFadeDuration = -0.1f;

                Validate(config);

                Assert.AreEqual(0f, config.CombatAccelerationDelay);
                Assert.AreEqual(1f, config.AcceleratedCombatSpeed);
                Assert.AreEqual(0f, config.AnimationCrossFadeDuration);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void OnValidate_ReplacesInvalidTimedPresentationControlsWithDefaults()
        {
            BattleTimingConfig config = ScriptableObject.CreateInstance<BattleTimingConfig>();
            try
            {
                config.CombatAccelerationDelay = float.NaN;
                config.AcceleratedCombatSpeed = float.PositiveInfinity;
                config.AnimationCrossFadeDuration = float.NaN;

                Validate(config);

                Assert.AreEqual(BattleTiming.DefaultCombatAccelerationDelay, config.CombatAccelerationDelay);
                Assert.AreEqual(BattleTiming.DefaultAcceleratedCombatSpeed, config.AcceleratedCombatSpeed);
                Assert.AreEqual(BattleTiming.DefaultAnimationCrossFadeDuration, config.AnimationCrossFadeDuration);
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
