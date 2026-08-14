using UnityEngine;

namespace DeckBattle
{
    [CreateAssetMenu(fileName = "BattleTimingConfig", menuName = "Deck Battle/Battle Timing Config")]
    public sealed class BattleTimingConfig : ScriptableObject
    {
        [Header("Combat")]
        public float CombatTickDuration = BattleTiming.DefaultCombatTickDuration;
        public int MaxCombatTicks = BattleTiming.DefaultMaxCombatTicks;
        public int MaxTicksPerFrame = BattleTiming.DefaultMaxTicksPerFrame;

        [Header("Combat Acceleration")]
        [Min(0f)] public float CombatAccelerationDelay = BattleTiming.DefaultCombatAccelerationDelay;
        [Min(BattleTiming.MinAcceleratedCombatSpeed)] public float AcceleratedCombatSpeed = BattleTiming.DefaultAcceleratedCombatSpeed;

        [Header("Round Resolution")]
        public float RoundResolutionDelay = BattleTiming.DefaultRoundResolutionDelay;

        [Header("Animation")]
        [Min(0f)] public float AnimationCrossFadeDuration = BattleTiming.DefaultAnimationCrossFadeDuration;

        private void OnValidate()
        {
            CombatTickDuration = Mathf.Max(BattleTiming.MinCombatTickDuration, CombatTickDuration);
            MaxCombatTicks = Mathf.Max(1, MaxCombatTicks);
            MaxTicksPerFrame = Mathf.Max(1, MaxTicksPerFrame);
            CombatAccelerationDelay = BattleTiming.ResolveCombatAccelerationDelay(CombatAccelerationDelay);
            AcceleratedCombatSpeed = BattleTiming.ResolveAcceleratedCombatSpeed(AcceleratedCombatSpeed);
            RoundResolutionDelay = Mathf.Max(0f, RoundResolutionDelay);
            AnimationCrossFadeDuration = BattleTiming.ResolveAnimationCrossFadeDuration(AnimationCrossFadeDuration);
        }
    }
}
