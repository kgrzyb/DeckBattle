using System;

namespace DeckBattle
{
    public readonly struct UnitSpawnData
    {
        public readonly int UnitId;
        public readonly UnitCombatSpec CombatSpec;
        public readonly BattleSide Side;
        public readonly HexCoord StartHex;
        public readonly int AttackBonusNextCombat;

        public UnitSpawnData(int unitId, UnitDefinition definition, BattleSide side, HexCoord startHex)
            : this(unitId, UnitCombatSpec.FromDefinition(definition), side, startHex, 0)
        {
        }

        public UnitSpawnData(int unitId, UnitDefinition definition, BattleSide side, HexCoord startHex, int attackBonusNextCombat)
            : this(unitId, UnitCombatSpec.FromDefinition(definition), side, startHex, attackBonusNextCombat)
        {
        }

        public UnitSpawnData(int unitId, UnitCombatSpec combatSpec, BattleSide side, HexCoord startHex, int attackBonusNextCombat = 0)
        {
            UnitId = unitId;
            CombatSpec = combatSpec;
            Side = side;
            StartHex = startHex;
            AttackBonusNextCombat = Math.Max(0, attackBonusNextCombat);
        }
    }
}
