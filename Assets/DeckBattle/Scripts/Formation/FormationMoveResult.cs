namespace DeckBattle
{
    public readonly struct FormationMoveResult
    {
        public readonly bool Success;
        public readonly FormationMoveFailReason FailReason;
        public readonly RuntimeUnit SwappedUnit;

        private FormationMoveResult(bool success, FormationMoveFailReason failReason, RuntimeUnit swappedUnit)
        {
            Success = success;
            FailReason = failReason;
            SwappedUnit = swappedUnit;
        }

        public static FormationMoveResult Failed(FormationMoveFailReason failReason)
        {
            return new FormationMoveResult(false, failReason, null);
        }

        public static FormationMoveResult Succeeded()
        {
            return new FormationMoveResult(true, FormationMoveFailReason.None, null);
        }

        public static FormationMoveResult SucceededWithSwap(RuntimeUnit swappedUnit)
        {
            return new FormationMoveResult(true, FormationMoveFailReason.None, swappedUnit);
        }
    }
}
