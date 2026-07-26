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
        public int TargetUnitId;
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
            CurrentHp = definition.MaxHp;
            TargetUnitId = NoTargetUnitId;
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
            TargetUnitId = target != null ? target.UnitId : NoTargetUnitId;
        }

        public void ClearTarget()
        {
            TargetUnitId = NoTargetUnitId;
        }

        public void ResetForBattle(HexCoord startHex)
        {
            CurrentHex = startHex;
            CurrentHp = Definition.MaxHp;
            TargetUnitId = NoTargetUnitId;
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
