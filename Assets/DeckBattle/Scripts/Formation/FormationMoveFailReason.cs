namespace DeckBattle
{
    public enum FormationMoveFailReason
    {
        None = 0,
        UnitMissing = 1,
        InvalidTile = 2,
        TileOccupied = 3,
        NotInPreparation = 4,
        PlayerReady = 5
    }
}
