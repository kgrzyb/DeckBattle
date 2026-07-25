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
        ProjectileHit = 9,
        AttackWindupStarted = 10,
        AttackWindupCancelled = 11,
        AttackFired = 12,
        AttackWinddownEnded = 13,
        ProjectileResolved = 14
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
        public readonly UnitSpecialKind SpecialKind;
        public readonly BattleSide Winner;
        public readonly bool HasWinner;
        public readonly int SequenceId;

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
            UnitSpecialKind specialKind,
            BattleSide winner,
            bool hasWinner,
            int sequenceId = 0)
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
            SpecialKind = specialKind;
            Winner = winner;
            HasWinner = hasWinner;
            SequenceId = sequenceId;
        }

        public static BattleEvent UnitMoved(int unitId, HexCoord from, HexCoord to)
        {
            return new BattleEvent(BattleEventType.UnitMoved, unitId, 0, from, to, 0, 0, 0, 0, 0f, UnitSpecialKind.None, BattleSide.Player, false);
        }

        public static BattleEvent UnitAttackStarted(int attackerId, int targetId)
        {
            return new BattleEvent(BattleEventType.UnitAttackStarted, attackerId, targetId, default, default, 0, 0, 0, 0, 0f, UnitSpecialKind.None, BattleSide.Player, false);
        }

        public static BattleEvent AttackWindupStarted(int attackerId, int targetId, int sequenceId, float duration)
        {
            return new BattleEvent(BattleEventType.AttackWindupStarted, attackerId, targetId, default, default, 0, 0, 0, 0, duration, UnitSpecialKind.None, BattleSide.Player, false, sequenceId);
        }

        public static BattleEvent AttackWindupCancelled(int attackerId, int targetId, int sequenceId)
        {
            return new BattleEvent(BattleEventType.AttackWindupCancelled, attackerId, targetId, default, default, 0, 0, 0, 0, 0f, UnitSpecialKind.None, BattleSide.Player, false, sequenceId);
        }

        public static BattleEvent AttackFired(int attackerId, int targetId, int sequenceId)
        {
            return new BattleEvent(BattleEventType.AttackFired, attackerId, targetId, default, default, 0, 0, 0, 0, 0f, UnitSpecialKind.None, BattleSide.Player, false, sequenceId);
        }

        public static BattleEvent AttackWinddownEnded(int attackerId, int sequenceId)
        {
            return new BattleEvent(BattleEventType.AttackWinddownEnded, attackerId, 0, default, default, 0, 0, 0, 0, 0f, UnitSpecialKind.None, BattleSide.Player, false, sequenceId);
        }

        public static BattleEvent UnitDamaged(int targetId, int amount, int remainingHp)
        {
            return new BattleEvent(BattleEventType.UnitDamaged, targetId, 0, default, default, amount, remainingHp, 0, 0, 0f, UnitSpecialKind.None, BattleSide.Player, false);
        }

        public static BattleEvent UnitDied(int unitId)
        {
            return new BattleEvent(BattleEventType.UnitDied, unitId, 0, default, default, 0, 0, 0, 0, 0f, UnitSpecialKind.None, BattleSide.Player, false);
        }

        public static BattleEvent BattleEnded(BattleSide winner, bool hasWinner)
        {
            return new BattleEvent(BattleEventType.BattleEnded, 0, 0, default, default, 0, 0, 0, 0, 0f, UnitSpecialKind.None, winner, hasWinner);
        }

        public static BattleEvent UnitManaChanged(int unitId, int currentMana)
        {
            return new BattleEvent(BattleEventType.UnitManaChanged, unitId, 0, default, default, 0, 0, currentMana, 0, 0f, UnitSpecialKind.None, BattleSide.Player, false);
        }

        public static BattleEvent UnitSpecialActivated(int unitId, UnitSpecialKind specialKind, float duration)
        {
            return new BattleEvent(BattleEventType.UnitSpecialActivated, unitId, 0, default, default, 0, 0, 0, 0, duration, specialKind, BattleSide.Player, false);
        }

        public static BattleEvent UnitCrit(int attackerId, int targetId)
        {
            return new BattleEvent(BattleEventType.UnitCrit, attackerId, targetId, default, default, 0, 0, 0, 0, 0f, UnitSpecialKind.None, BattleSide.Player, false);
        }

        public static BattleEvent ProjectileLaunched(
            int projectileId,
            int attackerId,
            int targetId,
            HexCoord from,
            HexCoord targetHex,
            float duration)
        {
            return new BattleEvent(BattleEventType.ProjectileLaunched, attackerId, targetId, from, targetHex, 0, 0, 0, projectileId, duration, UnitSpecialKind.None, BattleSide.Player, false);
        }

        public static BattleEvent ProjectileHit(int projectileId, int attackerId, int targetId, HexCoord targetHex)
        {
            return new BattleEvent(BattleEventType.ProjectileHit, attackerId, targetId, default, targetHex, 0, 0, 0, projectileId, 0f, UnitSpecialKind.None, BattleSide.Player, false);
        }

        public static BattleEvent ProjectileResolved(int projectileId, int attackerId, int targetId, HexCoord targetHex, bool didHit)
        {
            return new BattleEvent(BattleEventType.ProjectileResolved, attackerId, targetId, default, targetHex, didHit ? 1 : 0, 0, 0, projectileId, 0f, UnitSpecialKind.None, BattleSide.Player, false);
        }
    }
}
