using System;

namespace DeckBattle
{
    public readonly struct UnitSpawnData
    {
        public readonly UnitDefinition Definition;
        public readonly BattleSide Side;
        public readonly HexCoord StartHex;

        public UnitSpawnData(UnitDefinition definition, BattleSide side, HexCoord startHex)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Side = side;
            StartHex = startHex;
        }
    }
}
