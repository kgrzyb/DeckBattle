namespace DeckBattle
{
    public static class BattleTiming
    {
        public const float DefaultCombatTickDuration = 0.35f;
        public const float MinCombatTickDuration = 0.05f;
        public const int DefaultMaxCombatTicks = 1000;
        public const int DefaultMaxTicksPerFrame = 4;
        public const float DefaultRoundResolutionDelay = 0.25f;
        public const float DefaultCombatAccelerationDelay = 10f;
        public const float DefaultAcceleratedCombatSpeed = 2f;
        public const float MinAcceleratedCombatSpeed = 1f;
        public const float DefaultAnimationCrossFadeDuration = 0.1f;

        public static float ResolveCombatAccelerationDelay(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? DefaultCombatAccelerationDelay
                : System.Math.Max(0f, value);
        }

        public static float ResolveAcceleratedCombatSpeed(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? DefaultAcceleratedCombatSpeed
                : System.Math.Max(MinAcceleratedCombatSpeed, value);
        }

        public static float ResolveAnimationCrossFadeDuration(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? DefaultAnimationCrossFadeDuration
                : System.Math.Max(0f, value);
        }
    }
}
