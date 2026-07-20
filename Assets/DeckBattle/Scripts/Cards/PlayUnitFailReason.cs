namespace DeckBattle
{
    public enum PlayUnitFailReason
    {
        None = 0,
        CardNotInHand = 1,
        NotEnoughAp = 2,
        NoDeploymentSlot = 3,
        InvalidTile = 4,
        TileOccupied = 5,
        UnitAlreadyPlayed = 6,
        NotInPreparation = 7,
        PlayerReady = 8
    }
}
