using System;

namespace DeckBattle
{
    public enum UnitAttackPhase
    {
        AcquireReload = 0,
        Windup = 1,
        Winddown = 2
    }

    public sealed class UnitRuntimeState
    {
        public const int NoTargetUnitId = 0;

        public readonly int UnitId;
        public readonly UnitDefinition Definition;
        public readonly BattleSide Side;

        public int CurrentHp;
        public HexCoord CurrentHex;
        public HexCoord PreviousHex;
        public int TargetUnitId;
        public int EngagedTargetUnitId;
        public int PursuitStepsUsed;
        public double NextAttackTime;
        public UnitAttackPhase AttackPhase;
        public int LockedAttackTargetUnitId;
        public int AttackSequenceId;
        public double AttackCycleStartTime;
        public double WindupEndTime;
        public int CurrentMana;
        public bool IsMoving;
        public HexCoord MovementDestination;
        public float MovementTimeRemaining;
        public UnitSpecialDefinition ActiveSpecial;
        public double SpecialEndTime;
        public float SpecialAttackCooldownMultiplier;
        public int AttackBonusNextCombat;
        public bool IsDefeated;

        public UnitRuntimeState(int unitId, UnitDefinition definition, BattleSide side, HexCoord startHex)
            : this(unitId, definition, side, startHex, 0)
        {
        }

        public UnitRuntimeState(int unitId, UnitDefinition definition, BattleSide side, HexCoord startHex, int attackBonusNextCombat)
        {
            if (unitId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(unitId));
            }

            UnitId = unitId;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Side = side;
            CurrentHex = startHex;
            PreviousHex = startHex;
            CurrentHp = definition.MaxHp;
            TargetUnitId = NoTargetUnitId;
            ResetPursuit();
            NextAttackTime = double.PositiveInfinity;
            ResetAttackCycle();
            CurrentMana = 0;
            IsMoving = false;
            MovementDestination = startHex;
            MovementTimeRemaining = 0f;
            ActiveSpecial = null;
            SpecialEndTime = double.PositiveInfinity;
            SpecialAttackCooldownMultiplier = 1f;
            AttackBonusNextCombat = Math.Max(0, attackBonusNextCombat);
            IsDefeated = false;
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
        }

        public void ClearTarget()
        {
            TargetUnitId = NoTargetUnitId;
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
            CurrentHp = Definition.MaxHp;
            ClearTarget();
            NextAttackTime = double.PositiveInfinity;
            ResetAttackCycle();
            CurrentMana = 0;
            IsMoving = false;
            MovementDestination = startHex;
            MovementTimeRemaining = 0f;
            ActiveSpecial = null;
            SpecialEndTime = double.PositiveInfinity;
            SpecialAttackCooldownMultiplier = 1f;
            AttackBonusNextCombat = 0;
            IsDefeated = false;
        }

        public void ResetAttackCycle()
        {
            AttackPhase = UnitAttackPhase.AcquireReload;
            LockedAttackTargetUnitId = NoTargetUnitId;
            AttackSequenceId = 0;
            AttackCycleStartTime = double.PositiveInfinity;
            WindupEndTime = double.PositiveInfinity;
        }
    }
}
