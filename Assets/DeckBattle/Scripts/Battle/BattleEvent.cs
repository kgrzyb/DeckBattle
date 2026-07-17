namespace DeckBattle
{
    public enum BattleEventType
    {
        UnitMoved = 0,
        UnitAttackStarted = 1,
        UnitDamaged = 2,
        UnitDied = 3,
        BattleEnded = 4,
        UnitManaChanged = 5,
        UnitSpecialActivated = 6,
        UnitCrit = 7,
        ProjectileLaunched = 8,
        ProjectileHit = 9
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
        public readonly int CurrentMana;
        public readonly int ProjectileId;
        public readonly float Duration;
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
            int currentMana,
            int projectileId,
            float duration,
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
            CurrentMana = currentMana;
            ProjectileId = projectileId;
            Duration = duration;
            Winner = winner;
            HasWinner = hasWinner;
        }

        public static BattleEvent UnitMoved(int unitId, HexCoord from, HexCoord to)
        {
            return new BattleEvent(BattleEventType.UnitMoved, unitId, 0, from, to, 0, 0, 0, 0, 0f, BattleSide.Player, false);
        }

        public static BattleEvent UnitAttackStarted(int attackerId, int targetId)
        {
            return new BattleEvent(BattleEventType.UnitAttackStarted, attackerId, targetId, default, default, 0, 0, 0, 0, 0f, BattleSide.Player, false);
        }

        public static BattleEvent UnitDamaged(int targetId, int amount, int remainingHp)
        {
            return new BattleEvent(BattleEventType.UnitDamaged, targetId, 0, default, default, amount, remainingHp, 0, 0, 0f, BattleSide.Player, false);
        }

        public static BattleEvent UnitDied(int unitId)
        {
            return new BattleEvent(BattleEventType.UnitDied, unitId, 0, default, default, 0, 0, 0, 0, 0f, BattleSide.Player, false);
        }

        public static BattleEvent BattleEnded(BattleSide winner, bool hasWinner)
        {
            return new BattleEvent(BattleEventType.BattleEnded, 0, 0, default, default, 0, 0, 0, 0, 0f, winner, hasWinner);
        }

        public static BattleEvent UnitManaChanged(int unitId, int currentMana)
        {
            return new BattleEvent(BattleEventType.UnitManaChanged, unitId, 0, default, default, 0, 0, currentMana, 0, 0f, BattleSide.Player, false);
        }

        public static BattleEvent UnitSpecialActivated(int unitId, float duration)
        {
            return new BattleEvent(BattleEventType.UnitSpecialActivated, unitId, 0, default, default, 0, 0, 0, 0, duration, BattleSide.Player, false);
        }

        public static BattleEvent UnitCrit(int attackerId, int targetId)
        {
            return new BattleEvent(BattleEventType.UnitCrit, attackerId, targetId, default, default, 0, 0, 0, 0, 0f, BattleSide.Player, false);
        }

        public static BattleEvent ProjectileLaunched(
            int projectileId,
            int attackerId,
            int targetId,
            HexCoord from,
            HexCoord targetHex,
            float duration)
        {
            return new BattleEvent(BattleEventType.ProjectileLaunched, attackerId, targetId, from, targetHex, 0, 0, 0, projectileId, duration, BattleSide.Player, false);
        }

        public static BattleEvent ProjectileHit(int projectileId, int attackerId, int targetId, HexCoord targetHex)
        {
            return new BattleEvent(BattleEventType.ProjectileHit, attackerId, targetId, default, targetHex, 0, 0, 0, projectileId, 0f, BattleSide.Player, false);
        }
    }
}
