namespace DeckBattle
{
    public readonly struct FormationMoveResult
    {
        public readonly bool Success;
        public readonly FormationMoveFailReason FailReason;

        private FormationMoveResult(bool success, FormationMoveFailReason failReason)
        {
            Success = success;
            FailReason = failReason;
        }

        public static FormationMoveResult Failed(FormationMoveFailReason failReason)
        {
            return new FormationMoveResult(false, failReason);
        }

        public static FormationMoveResult Succeeded()
        {
            return new FormationMoveResult(true, FormationMoveFailReason.None);
        }
    }
}
