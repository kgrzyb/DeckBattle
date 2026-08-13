using System;

namespace DeckBattle
{
    public enum UnitAttackPhase
    {
        AcquireReload = 0,
        Windup = 1,
        Winddown = 2
    }

    public enum UnitSpecialPhase
    {
        Idle = 0,
        Casting = 1,
        RecoveryLock = 2
    }

    public sealed class UnitRuntimeState
    {
        public const int NoTargetUnitId = 0;

        public readonly int UnitId;
        public readonly UnitCombatSpec CombatSpec;
        public readonly BattleSide Side;
        public readonly string DisplayName;

        public int CurrentHp;
        public HexCoord CurrentHex;
        public HexCoord PreviousHex;
        public int TargetUnitId;
        public HexCoord LastKnownTargetHex;
        public int EngagedTargetUnitId;
        public int PursuitStepsUsed;
        public double NextAttackTime;
        public UnitAttackPhase AttackPhase;
        public int LockedAttackTargetUnitId;
        public int AttackSequenceId;
        public double AttackCycleStartTime;
        public double WindupEndTime;
        public UnitSpecialPhase SpecialPhase;
        public int SpecialSequenceId;
        public double SpecialEffectTime;
        public double SpecialCastEndTime;
        public double SpecialCastStartTime;
        public int LockedSpecialTargetUnitId;
        public int SpecialStrikesResolved;
        public double NextSpecialStrikeTime;
        public double LastSpecialRecoveryEndTime;
        public double ManaLockEndTime;
        public int CurrentMana;
        public bool IsMoving;
        public HexCoord MovementDestination;
        public float MovementTimeRemaining;
        public int AttackBonusNextCombat;
        public float BaseAttackBonusPercent;
        public bool IsDefeated;
        public readonly UnitStatusCollection Statuses;
        public UnitStatusSnapshot StatusSnapshot;
        public int StatusVersion;

        public UnitRuntimeState(int unitId, UnitDefinition definition, BattleSide side, HexCoord startHex)
            : this(unitId, UnitCombatSpec.FromDefinition(definition), side, startHex, 0, 8, definition != null ? definition.DisplayName : null)
        {
        }

        public UnitRuntimeState(int unitId, UnitDefinition definition, BattleSide side, HexCoord startHex, int attackBonusNextCombat, int maxStatusesPerUnit = 8)
            : this(unitId, UnitCombatSpec.FromDefinition(definition), side, startHex, attackBonusNextCombat, maxStatusesPerUnit, definition != null ? definition.DisplayName : null)
        {
        }

        public UnitRuntimeState(
            int unitId,
            UnitCombatSpec combatSpec,
            BattleSide side,
            HexCoord startHex,
            int attackBonusNextCombat = 0,
            int maxStatusesPerUnit = 8,
            string displayName = null)
        {
            if (unitId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(unitId));
            }

            UnitId = unitId;
            CombatSpec = combatSpec;
            Side = side;
            DisplayName = displayName;
            CurrentHex = startHex;
            PreviousHex = startHex;
            CurrentHp = combatSpec.MaxHp;
            TargetUnitId = NoTargetUnitId;
            LastKnownTargetHex = default;
            ResetPursuit();
            NextAttackTime = double.PositiveInfinity;
            ResetAttackCycle();
            ResetSpecialCycle();
            CurrentMana = 0;
            IsMoving = false;
            MovementDestination = startHex;
            MovementTimeRemaining = 0f;
            AttackBonusNextCombat = Math.Max(0, attackBonusNextCombat);
            BaseAttackBonusPercent = 0f;
            IsDefeated = false;
            Statuses = new UnitStatusCollection(maxStatusesPerUnit);
            StatusSnapshot = default;
            StatusVersion = 0;
        }

        public bool IsAlive
        {
            get { return !IsDefeated && CurrentHp > 0; }
        }

        public void SetTarget(UnitRuntimeState target)
        {
            int targetUnitId = target != null ? target.UnitId : NoTargetUnitId;
            if (TargetUnitId != targetUnitId)
            {
                TargetUnitId = targetUnitId;
                ResetPursuit();
            }

            if (target != null)
            {
                LastKnownTargetHex = target.CurrentHex;
            }
        }

        public void ClearTarget()
        {
            TargetUnitId = NoTargetUnitId;
            LastKnownTargetHex = default;
            ResetPursuit();
        }

        public void MarkTargetEngaged(int targetUnitId)
        {
            if (targetUnitId == NoTargetUnitId || TargetUnitId != targetUnitId)
            {
                return;
            }

            EngagedTargetUnitId = targetUnitId;
            PursuitStepsUsed = 0;
        }

        public void RecordPursuitStep(int targetUnitId)
        {
            if (targetUnitId == NoTargetUnitId
                || TargetUnitId != targetUnitId
                || EngagedTargetUnitId != targetUnitId)
            {
                return;
            }

            PursuitStepsUsed = Math.Min(int.MaxValue, PursuitStepsUsed + 1);
        }

        public bool CanPursueTarget(int targetUnitId, int maxPursuitSteps)
        {
            return targetUnitId != NoTargetUnitId
                && (EngagedTargetUnitId != targetUnitId
                    || PursuitStepsUsed < Math.Max(0, maxPursuitSteps));
        }

        public void ResetPursuit()
        {
            EngagedTargetUnitId = NoTargetUnitId;
            PursuitStepsUsed = 0;
        }

        public void ResetForBattle(HexCoord startHex)
        {
            CurrentHex = startHex;
            PreviousHex = startHex;
            CurrentHp = CombatSpec.MaxHp;
            ClearTarget();
            NextAttackTime = double.PositiveInfinity;
            ResetAttackCycle();
            ResetSpecialCycle();
            CurrentMana = 0;
            IsMoving = false;
            MovementDestination = startHex;
            MovementTimeRemaining = 0f;
            AttackBonusNextCombat = 0;
            BaseAttackBonusPercent = 0f;
            IsDefeated = false;
            Statuses.Clear();
            StatusSnapshot = default;
            StatusVersion = 0;
        }

        public void ResetAttackCycle()
        {
            AttackPhase = UnitAttackPhase.AcquireReload;
            LockedAttackTargetUnitId = NoTargetUnitId;
            AttackSequenceId = 0;
            AttackCycleStartTime = double.PositiveInfinity;
            WindupEndTime = double.PositiveInfinity;
        }

        public void ResetSpecialCycle()
        {
            SpecialPhase = UnitSpecialPhase.Idle;
            SpecialSequenceId = 0;
            SpecialEffectTime = double.PositiveInfinity;
            SpecialCastEndTime = double.PositiveInfinity;
            SpecialCastStartTime = double.PositiveInfinity;
            LockedSpecialTargetUnitId = NoTargetUnitId;
            SpecialStrikesResolved = 0;
            NextSpecialStrikeTime = double.PositiveInfinity;
            LastSpecialRecoveryEndTime = double.NegativeInfinity;
            ManaLockEndTime = double.PositiveInfinity;
        }
    }
}
