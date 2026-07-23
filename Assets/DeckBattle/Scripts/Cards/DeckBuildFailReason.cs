namespace DeckBattle
{
    public enum DeckBuildFailReason
    {
        None = 0,
        UnknownCard = 1,
        CardNotOwned = 2,
        AlreadyInDeck = 3,
        DeckFull = 4,
        DeckTooSmall = 5,
        DeckEmpty = 6
    }
}
