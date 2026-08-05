namespace DeckBattle
{
    public readonly struct PlayUnitResult
    {
        public readonly bool Success;
        public readonly PlayUnitFailReason FailReason;
        public readonly RuntimeUnit Unit;
        public readonly int QueuedCombatEffectCount;

        private PlayUnitResult(bool success, PlayUnitFailReason failReason, RuntimeUnit unit, int queuedCombatEffectCount)
        {
            Success = success;
            FailReason = failReason;
            Unit = unit;
            QueuedCombatEffectCount = queuedCombatEffectCount;
        }

        public static PlayUnitResult Failed(PlayUnitFailReason failReason)
        {
            return new PlayUnitResult(false, failReason, null, 0);
        }

        public static PlayUnitResult Succeeded(RuntimeUnit unit, int queuedCombatEffectCount = 0)
        {
            return new PlayUnitResult(true, PlayUnitFailReason.None, unit, queuedCombatEffectCount);
        }
    }
}
