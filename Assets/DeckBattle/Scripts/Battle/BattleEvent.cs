namespace DeckBattle
{
    public enum BattleEventType
    {
        UnitMoved = 0,
        UnitAttackStarted = 1,
        UnitDamaged = 2,
        UnitDied = 3,
        BattleEnded = 4
    }

    public readonly struct BattleEvent
    {
        public readonly BattleEventType Type;
        public readonly int UnitId;
        public readonly int TargetUnitId;
        public readonly HexCoord From;
        public readonly HexCoord To;
        public readonly int Amount;
        public readonly int RemainingHp;
        public readonly BattleSide Winner;
        public readonly bool HasWinner;

        private BattleEvent(
            BattleEventType type,
            int unitId,
            int targetUnitId,
            HexCoord from,
            HexCoord to,
            int amount,
            int remainingHp,
            BattleSide winner,
            bool hasWinner)
        {
            Type = type;
            UnitId = unitId;
            TargetUnitId = targetUnitId;
            From = from;
            To = to;
            Amount = amount;
            RemainingHp = remainingHp;
            Winner = winner;
            HasWinner = hasWinner;
        }

        public static BattleEvent UnitMoved(int unitId, HexCoord from, HexCoord to)
        {
            return new BattleEvent(BattleEventType.UnitMoved, unitId, 0, from, to, 0, 0, BattleSide.Player, false);
        }

        public static BattleEvent UnitAttackStarted(int attackerId, int targetId)
        {
            return new BattleEvent(BattleEventType.UnitAttackStarted, attackerId, targetId, default, default, 0, 0, BattleSide.Player, false);
        }

        public static BattleEvent UnitDamaged(int targetId, int amount, int remainingHp)
        {
            return new BattleEvent(BattleEventType.UnitDamaged, targetId, 0, default, default, amount, remainingHp, BattleSide.Player, false);
        }

        public static BattleEvent UnitDied(int unitId)
        {
            return new BattleEvent(BattleEventType.UnitDied, unitId, 0, default, default, 0, 0, BattleSide.Player, false);
        }

        public static BattleEvent BattleEnded(BattleSide winner, bool hasWinner)
        {
            return new BattleEvent(BattleEventType.BattleEnded, 0, 0, default, default, 0, 0, winner, hasWinner);
        }
    }
}
