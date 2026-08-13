namespace DeckBattle
{
    public readonly struct DamageRequest
    {
        public readonly UnitRuntimeState Source;
        public readonly int Amount;
        public readonly DamageKind Kind;
        public readonly bool IsCritical;
        public readonly bool IsRedirected;
        public readonly bool BypassesGuard;
        public readonly bool CanTriggerMark;
        public readonly bool CanApplyLifesteal;
        public readonly int ExecuteHpThresholdPercent;

        public DamageRequest(
            UnitRuntimeState source,
            int amount,
            DamageKind kind = DamageKind.Direct,
            bool isCritical = false,
            bool isRedirected = false,
            bool bypassesGuard = false,
            bool canTriggerMark = true,
            bool canApplyLifesteal = true,
            int executeHpThresholdPercent = 0)
        {
            Source = source;
            Amount = amount;
            Kind = kind;
            IsCritical = isCritical;
            IsRedirected = isRedirected;
            BypassesGuard = bypassesGuard;
            CanTriggerMark = canTriggerMark;
            CanApplyLifesteal = canApplyLifesteal;
            ExecuteHpThresholdPercent = executeHpThresholdPercent < 0
                ? 0
                : executeHpThresholdPercent > 100 ? 100 : executeHpThresholdPercent;
        }
    }
}
