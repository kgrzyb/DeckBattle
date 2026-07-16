using System;

namespace DeckBattle
{
    public readonly struct UnitSpawnData
    {
        public readonly int UnitId;
        public readonly UnitDefinition Definition;
        public readonly BattleSide Side;
        public readonly HexCoord StartHex;

        public UnitSpawnData(int unitId, UnitDefinition definition, BattleSide side, HexCoord startHex)
        {
            UnitId = unitId;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Side = side;
            StartHex = startHex;
        }
    }
}
