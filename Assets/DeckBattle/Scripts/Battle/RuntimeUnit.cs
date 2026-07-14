namespace DeckBattle
{
    public sealed class RuntimeUnit
    {
        public readonly int RuntimeId;
        public readonly UnitDefinition Definition;
        public readonly BattleSide Side;
        public int CurrentHp;
        public HexCoord FormationCoord;
        public HexCoord BattleCoord;
        public bool IsDefeated;

        public RuntimeUnit(int runtimeId, UnitDefinition definition, BattleSide side, HexCoord formationCoord)
        {
            RuntimeId = runtimeId;
            Definition = definition;
            Side = side;
            FormationCoord = formationCoord;
            BattleCoord = formationCoord;
            CurrentHp = definition.MaxHp;
            IsDefeated = false;
        }

        public bool IsAlive
        {
            get { return !IsDefeated && CurrentHp > 0; }
        }

        public void ResetForRound()
        {
            CurrentHp = Definition.MaxHp;
            BattleCoord = FormationCoord;
            IsDefeated = false;
        }
    }
}
