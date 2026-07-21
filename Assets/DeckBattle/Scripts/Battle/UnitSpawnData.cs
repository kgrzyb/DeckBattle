using System;

namespace DeckBattle
{
    public readonly struct UnitSpawnData
    {
        public readonly int UnitId;
        public readonly UnitDefinition Definition;
        public readonly BattleSide Side;
        public readonly HexCoord StartHex;
        public readonly int AttackBonusNextCombat;

        public UnitSpawnData(int unitId, UnitDefinition definition, BattleSide side, HexCoord startHex)
            : this(unitId, definition, side, startHex, 0)
        {
        }

        public UnitSpawnData(int unitId, UnitDefinition definition, BattleSide side, HexCoord startHex, int attackBonusNextCombat)
        {
            UnitId = unitId;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Side = side;
            StartHex = startHex;
            AttackBonusNextCombat = Math.Max(0, attackBonusNextCombat);
        }
    }
}
