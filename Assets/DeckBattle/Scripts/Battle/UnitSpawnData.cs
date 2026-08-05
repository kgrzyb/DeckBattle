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
        public readonly string DisplayName;

        public UnitSpawnData(int unitId, UnitDefinition definition, BattleSide side, HexCoord startHex)
            : this(unitId, UnitCombatSpec.FromDefinition(definition), side, startHex, 0, definition != null ? definition.DisplayName : null)
        {
        }

        public UnitSpawnData(int unitId, UnitDefinition definition, BattleSide side, HexCoord startHex, int attackBonusNextCombat)
            : this(unitId, UnitCombatSpec.FromDefinition(definition), side, startHex, attackBonusNextCombat, definition != null ? definition.DisplayName : null)
        {
        }

        public UnitSpawnData(
            int unitId,
            UnitCombatSpec combatSpec,
            BattleSide side,
            HexCoord startHex,
            int attackBonusNextCombat = 0,
            string displayName = null)
        {
            UnitId = unitId;
            CombatSpec = combatSpec;
            Side = side;
            StartHex = startHex;
            AttackBonusNextCombat = Math.Max(0, attackBonusNextCombat);
            DisplayName = displayName;
        }
    }
}
