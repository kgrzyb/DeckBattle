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
            AttackCooldownRemaining = 0f;
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
            AttackCooldownRemaining = 0f;
            IsDefeated = false;
        }
    }
}
