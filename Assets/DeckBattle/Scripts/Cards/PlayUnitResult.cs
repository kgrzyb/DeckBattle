namespace DeckBattle
{
    public readonly struct PlayUnitResult
    {
        public readonly bool Success;
        public readonly PlayUnitFailReason FailReason;
        public readonly RuntimeUnit Unit;

        private PlayUnitResult(bool success, PlayUnitFailReason failReason, RuntimeUnit unit)
        {
            Success = success;
            FailReason = failReason;
            Unit = unit;
        }

        public static PlayUnitResult Failed(PlayUnitFailReason failReason)
        {
            return new PlayUnitResult(false, failReason, null);
        }

        public static PlayUnitResult Succeeded(RuntimeUnit unit)
        {
            return new PlayUnitResult(true, PlayUnitFailReason.None, unit);
        }
    }
}
