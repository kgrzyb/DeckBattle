using System;

namespace DeckBattle
{
    public sealed class UnitRuntimeState
    {
        public const int NoTargetUnitId = 0;

        public readonly int UnitId;
        public readonly UnitDefinition Definition;
        public readonly BattleSide Side;

        public int CurrentHp;
        public HexCoord CurrentHex;
        public int TargetUnitId;
        public float AttackCooldownRemaining;
        public int CurrentMana;
        public bool IsMoving;
        public HexCoord MovementDestination;
        public float MovementTimeRemaining;
        public float SpecialDurationRemaining;
        public float AttackCooldownMultiplier;
        public bool IsDefeated;

        public UnitRuntimeState(int unitId, UnitDefinition definition, BattleSide side, HexCoord startHex)
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
            AttackCooldownRemaining = Math.Max(0.01f, definition.AttackCooldown);
            CurrentMana = 0;
            IsMoving = false;
            MovementDestination = startHex;
            MovementTimeRemaining = 0f;
            SpecialDurationRemaining = 0f;
            AttackCooldownMultiplier = 1f;
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
            AttackCooldownRemaining = Math.Max(0.01f, Definition.AttackCooldown);
            CurrentMana = 0;
            IsMoving = false;
            MovementDestination = startHex;
            MovementTimeRemaining = 0f;
            SpecialDurationRemaining = 0f;
            AttackCooldownMultiplier = 1f;
            IsDefeated = false;
        }
    }
}
