namespace DeckBattle
{
    public readonly struct DeckValidationResult
    {
        public readonly bool IsValid;
        public readonly int CardCount;
        public readonly int MissingCardCount;
        public readonly int DuplicateCardCount;
        public readonly DeckBuildFailReason Reason;

        public DeckValidationResult(
            bool isValid,
            int cardCount,
            int missingCardCount,
            int duplicateCardCount,
            DeckBuildFailReason reason)
        {
            IsValid = isValid;
            CardCount = cardCount;
            MissingCardCount = missingCardCount;
            DuplicateCardCount = duplicateCardCount;
            Reason = reason;
        }
    }
}
