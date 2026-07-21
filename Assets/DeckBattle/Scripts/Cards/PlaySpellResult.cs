namespace DeckBattle
{
    public readonly struct PlaySpellResult
    {
        public readonly bool Success;
        public readonly PlaySpellFailReason FailReason;
        public readonly RuntimeUnit TargetUnit;
        public readonly int Amount;

        private PlaySpellResult(bool success, PlaySpellFailReason failReason, RuntimeUnit targetUnit, int amount)
        {
            Success = success;
            FailReason = failReason;
            TargetUnit = targetUnit;
            Amount = amount;
        }

        public static PlaySpellResult Failed(PlaySpellFailReason failReason)
        {
            return new PlaySpellResult(false, failReason, null, 0);
        }

        public static PlaySpellResult Succeeded(RuntimeUnit targetUnit, int amount)
        {
            return new PlaySpellResult(true, PlaySpellFailReason.None, targetUnit, amount);
        }
    }
}
