namespace DeckBattle
{
    public enum PlaySpellFailReason
    {
        None = 0,
        InvalidCardType = 1,
        NotInPreparation = 2,
        PlayerReady = 3,
        CardNotInHand = 4,
        SpellAlreadyPlayed = 5,
        NotEnoughAp = 6,
        InvalidTarget = 7,
        UnsupportedEffect = 8
    }
}
