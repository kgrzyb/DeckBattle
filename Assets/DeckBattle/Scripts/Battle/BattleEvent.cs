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
        UnitCrit = 7,
        ProjectileLaunched = 8,
        ProjectileHit = 9,
        AttackWindupStarted = 10,
        AttackWindupCancelled = 11,
        AttackFired = 12,
        AttackWinddownEnded = 13,
        ProjectileResolved = 14,
        StatusApplied = 15,
        StatusRefreshed = 16,
        StatusStackChanged = 17,
        StatusRemoved = 18,
        StatusRejected = 19,
        PeriodicEffectTicked = 20,
        ShieldChanged = 21,
        UnitHealed = 22,
        ManaDrained = 23,
        DamageRedirected = 24,
        UnitTargetChanged = 27,
        SpecialCastStarted = 28,
        SpecialStrikeFired = 29,
        SpecialAreaImpact = 30,
        SpecialCastCancelled = 31,
        SpecialCastCompleted = 32
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
        public readonly StatusKind StatusKind;
        public readonly int StatusStackCount;
        public readonly int StatusStackDelta;
        public readonly int PresentationId;
        public readonly bool IsCritical;
        public readonly int StrikeIndex;

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
            int sequenceId = 0,
            StatusKind statusKind = StatusKind.None,
            int statusStackCount = 0,
            int statusStackDelta = 0,
            int presentationId = 0,
            bool isCritical = false,
            int strikeIndex = 0)
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
            StatusKind = statusKind;
            StatusStackCount = statusStackCount;
            StatusStackDelta = statusStackDelta;
            PresentationId = presentationId;
            IsCritical = isCritical;
            StrikeIndex = strikeIndex;
        }

        public static BattleEvent UnitMoved(int unitId, HexCoord from, HexCoord to, float duration = 0f)
        {
            return new BattleEvent(BattleEventType.UnitMoved, unitId, 0, from, to, 0, 0, 0, 0, duration, UnitSpecialKind.None, BattleSide.Player, false);
        }

        public static BattleEvent UnitAttackStarted(int attackerId, int targetId)
        {
            return new BattleEvent(BattleEventType.UnitAttackStarted, attackerId, targetId, default, default, 0, 0, 0, 0, 0f, UnitSpecialKind.None, BattleSide.Player, false);
        }

        public static BattleEvent AttackWindupStarted(
            int attackerId,
            int targetId,
            int sequenceId,
            float duration,
            HexCoord targetHex)
        {
            return new BattleEvent(
                BattleEventType.AttackWindupStarted,
                attackerId,
                targetId,
                default,
                targetHex,
                0,
                0,
                0,
                0,
                duration,
                UnitSpecialKind.None,
                BattleSide.Player,
                false,
                sequenceId);
        }

        public static BattleEvent AttackWindupCancelled(int attackerId, int targetId, int sequenceId)
        {
            return new BattleEvent(BattleEventType.AttackWindupCancelled, attackerId, targetId, default, default, 0, 0, 0, 0, 0f, UnitSpecialKind.None, BattleSide.Player, false, sequenceId);
        }

        public static BattleEvent AttackFired(int attackerId, int targetId, int sequenceId, float winddownDuration, HexCoord from, HexCoord targetHex)
        {
            return new BattleEvent(BattleEventType.AttackFired, attackerId, targetId, from, targetHex, 0, 0, 0, 0, winddownDuration, UnitSpecialKind.None, BattleSide.Player, false, sequenceId);
        }

        public static BattleEvent AttackWinddownEnded(int attackerId, int sequenceId)
        {
            return new BattleEvent(BattleEventType.AttackWinddownEnded, attackerId, 0, default, default, 0, 0, 0, 0, 0f, UnitSpecialKind.None, BattleSide.Player, false, sequenceId);
        }

        public static BattleEvent UnitTargetChanged(int unitId, int targetUnitId, HexCoord targetHex)
        {
            return new BattleEvent(BattleEventType.UnitTargetChanged, unitId, targetUnitId, default, targetHex, 0, 0, 0, 0, 0f, UnitSpecialKind.None, BattleSide.Player, false);
        }

        public static BattleEvent UnitDamaged(int targetId, int amount, int remainingHp, HexCoord targetHex, bool isCritical = false)
        {
            return new BattleEvent(
                BattleEventType.UnitDamaged,
                targetId,
                0,
                default,
                targetHex,
                amount,
                remainingHp,
                0,
                0,
                0f,
                UnitSpecialKind.None,
                BattleSide.Player,
                false,
                isCritical: isCritical);
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

        public static BattleEvent SpecialCastStarted(
            int attackerId,
            int targetId,
            UnitSpecialKind specialKind,
            int sequenceId,
            float duration,
            HexCoord targetHex)
        {
            return new BattleEvent(
                BattleEventType.SpecialCastStarted,
                attackerId,
                targetId,
                default,
                targetHex,
                0,
                0,
                0,
                0,
                duration,
                specialKind,
                BattleSide.Player,
                false,
                sequenceId);
        }

        public static BattleEvent SpecialCastCancelled(int unitId, UnitSpecialKind specialKind, int sequenceId)
        {
            return new BattleEvent(
                BattleEventType.SpecialCastCancelled,
                unitId,
                0,
                default,
                default,
                0,
                0,
                0,
                0,
                0f,
                specialKind,
                BattleSide.Player,
                false,
                sequenceId);
        }

        public static BattleEvent SpecialCastCompleted(
            int unitId,
            UnitSpecialKind specialKind,
            int sequenceId,
            float duration)
        {
            return new BattleEvent(
                BattleEventType.SpecialCastCompleted,
                unitId,
                0,
                default,
                default,
                0,
                0,
                0,
                0,
                duration,
                specialKind,
                BattleSide.Player,
                false,
                sequenceId);
        }

        public static BattleEvent SpecialStrikeFired(
            int attackerId,
            int targetId,
            UnitSpecialKind specialKind,
            int sequenceId,
            int strikeIndex,
            HexCoord targetHex)
        {
            return new BattleEvent(
                BattleEventType.SpecialStrikeFired,
                attackerId,
                targetId,
                default,
                targetHex,
                0,
                0,
                0,
                0,
                0f,
                specialKind,
                BattleSide.Player,
                false,
                sequenceId,
                strikeIndex: strikeIndex);
        }

        public static BattleEvent SpecialAreaImpact(
            int unitId,
            UnitSpecialKind specialKind,
            int sequenceId,
            HexCoord centerHex,
            int radius)
        {
            return new BattleEvent(
                BattleEventType.SpecialAreaImpact,
                unitId,
                0,
                centerHex,
                centerHex,
                radius,
                0,
                0,
                0,
                0f,
                specialKind,
                BattleSide.Player,
                false,
                sequenceId);
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
            float duration,
            int presentationId)
        {
            return new BattleEvent(
                BattleEventType.ProjectileLaunched,
                attackerId,
                targetId,
                from,
                targetHex,
                0,
                0,
                0,
                projectileId,
                duration,
                UnitSpecialKind.None,
                BattleSide.Player,
                false,
                presentationId: presentationId);
        }

        public static BattleEvent ProjectileHit(int projectileId, int attackerId, int targetId, HexCoord targetHex)
        {
            return new BattleEvent(BattleEventType.ProjectileHit, attackerId, targetId, default, targetHex, 0, 0, 0, projectileId, 0f, UnitSpecialKind.None, BattleSide.Player, false);
        }

        public static BattleEvent ProjectileResolved(int projectileId, int attackerId, int targetId, HexCoord targetHex, bool didHit)
        {
            return new BattleEvent(BattleEventType.ProjectileResolved, attackerId, targetId, default, targetHex, didHit ? 1 : 0, 0, 0, projectileId, 0f, UnitSpecialKind.None, BattleSide.Player, false);
        }

        public static BattleEvent StatusApplied(int targetId, int sourceId, StatusKind kind, int stacks, float duration)
        {
            return new BattleEvent(BattleEventType.StatusApplied, targetId, sourceId, default, default, 0, 0, 0, 0, duration, UnitSpecialKind.None, BattleSide.Player, false, 0, kind, stacks, stacks);
        }

        public static BattleEvent StatusRefreshed(int targetId, int sourceId, StatusKind kind, int stacks, float duration, int stackDelta = 0)
        {
            return new BattleEvent(BattleEventType.StatusRefreshed, targetId, sourceId, default, default, 0, 0, 0, 0, duration, UnitSpecialKind.None, BattleSide.Player, false, 0, kind, stacks, stackDelta);
        }

        public static BattleEvent StatusRemoved(int targetId, int sourceId, StatusKind kind, int stacks)
        {
            return new BattleEvent(BattleEventType.StatusRemoved, targetId, sourceId, default, default, 0, 0, 0, 0, 0f, UnitSpecialKind.None, BattleSide.Player, false, 0, kind, stacks, -stacks);
        }

        public static BattleEvent StatusRejected(int targetId, int sourceId, StatusKind kind, StatusApplicationResult reason)
        {
            return new BattleEvent(BattleEventType.StatusRejected, targetId, sourceId, default, default, (int)reason, 0, 0, 0, 0f, UnitSpecialKind.None, BattleSide.Player, false, 0, kind, 0);
        }

        public static BattleEvent PeriodicEffectTicked(int targetId, int sourceId, StatusKind kind, int amount)
        {
            return new BattleEvent(BattleEventType.PeriodicEffectTicked, targetId, sourceId, default, default, amount, 0, 0, 0, 0f, UnitSpecialKind.None, BattleSide.Player, false, 0, kind, 0);
        }

        public static BattleEvent ShieldChanged(int unitId, int remainingShield)
        {
            return new BattleEvent(BattleEventType.ShieldChanged, unitId, 0, default, default, remainingShield, 0, 0, 0, 0f, UnitSpecialKind.None, BattleSide.Player, false);
        }

        public static BattleEvent UnitHealed(int unitId, int amount, int currentHp)
        {
            return new BattleEvent(BattleEventType.UnitHealed, unitId, 0, default, default, amount, currentHp, 0, 0, 0f, UnitSpecialKind.None, BattleSide.Player, false);
        }

        public static BattleEvent ManaDrained(int unitId, int sourceId, int amount, int currentMana)
        {
            return new BattleEvent(BattleEventType.ManaDrained, unitId, sourceId, default, default, amount, 0, currentMana, 0, 0f, UnitSpecialKind.None, BattleSide.Player, false);
        }

        public static BattleEvent DamageRedirected(int protectedUnitId, int guardUnitId, int amount)
        {
            return new BattleEvent(BattleEventType.DamageRedirected, protectedUnitId, guardUnitId, default, default, amount, 0, 0, 0, 0f, UnitSpecialKind.None, BattleSide.Player, false);
        }

    }
}
