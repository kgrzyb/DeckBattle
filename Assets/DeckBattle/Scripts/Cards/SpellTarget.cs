namespace DeckBattle
{
    public readonly struct SpellTarget
    {
        public readonly bool HasCoord;
        public readonly HexCoord Coord;
        public readonly RuntimeUnit Unit;

        private SpellTarget(bool hasCoord, HexCoord coord, RuntimeUnit unit)
        {
            HasCoord = hasCoord;
            Coord = coord;
            Unit = unit;
        }

        public static SpellTarget ForUnit(RuntimeUnit unit)
        {
            return new SpellTarget(false, default(HexCoord), unit);
        }

        public static SpellTarget None()
        {
            return new SpellTarget(false, default(HexCoord), null);
        }

        public static SpellTarget ForCoord(HexCoord coord)
        {
            return new SpellTarget(true, coord, null);
        }
    }
}
