using System;
using System.Collections.Generic;

namespace DeckBattle
{
    public readonly struct UnitPresentationState
    {
        public readonly int UnitId;
        public readonly int PresentationId;
        public readonly BattleSide Side;
        public readonly HexCoord Hex;
        public readonly int CurrentHp;
        public readonly int MaxHp;
        public readonly int CurrentMana;
        public readonly int MaxMana;
        public readonly string DisplayName;
        public readonly int StatusStartIndex;
        public readonly int StatusCount;
        public readonly int TotalShield;

        public UnitPresentationState(
            int unitId,
            int presentationId,
            BattleSide side,
            HexCoord hex,
            int currentHp,
            int maxHp,
            int currentMana,
            int maxMana,
            string displayName = null,
            int statusStartIndex = 0,
            int statusCount = 0,
            int totalShield = 0)
        {
            UnitId = unitId;
            PresentationId = presentationId;
            Side = side;
            Hex = hex;
            CurrentHp = currentHp;
            MaxHp = maxHp;
            CurrentMana = currentMana;
            MaxMana = maxMana;
            DisplayName = displayName;
            StatusStartIndex = Math.Max(0, statusStartIndex);
            StatusCount = Math.Max(0, statusCount);
            TotalShield = Math.Max(0, totalShield);
        }
    }

    public readonly struct StatusPresentationState
    {
        public readonly StatusKind Kind;
        public readonly int SourceUnitId;
        public readonly int Stacks;

        public StatusPresentationState(StatusKind kind, int sourceUnitId, int stacks)
        {
            Kind = kind;
            SourceUnitId = sourceUnitId;
            Stacks = stacks;
        }
    }

    public sealed class BattlePresentationSnapshot
    {
        private readonly List<UnitPresentationState> units;
        private readonly List<StatusPresentationState> statuses;

        public BattlePresentationSnapshot(int capacity)
        {
            units = new List<UnitPresentationState>(Math.Max(1, capacity));
            statuses = new List<StatusPresentationState>(Math.Max(1, capacity * 2));
        }

        public IReadOnlyList<UnitPresentationState> Units { get { return units; } }
        public IReadOnlyList<StatusPresentationState> Statuses { get { return statuses; } }

        public void Capture(BattleSimulation simulation)
        {
            units.Clear();
            statuses.Clear();
            if (simulation == null)
            {
                return;
            }

            for (int i = 0; i < simulation.Units.Count; i++)
            {
                UnitRuntimeState unit = simulation.Units[i];
                if (unit == null || !unit.IsAlive)
                {
                    continue;
                }

                int statusStartIndex = statuses.Count;
                for (int statusIndex = 0; statusIndex < unit.Statuses.Count; statusIndex++)
                {
                    StatusInstance status = unit.Statuses[statusIndex];
                    statuses.Add(new StatusPresentationState(status.Kind, status.SourceUnitId, status.Stacks));
                }

                units.Add(new UnitPresentationState(
                    unit.UnitId,
                    unit.CombatSpec.PresentationId,
                    unit.Side,
                    unit.CurrentHex,
                    unit.CurrentHp,
                    unit.CombatSpec.MaxHp,
                    unit.CurrentMana,
                    unit.CombatSpec.ManaThreshold,
                    unit.DisplayName,
                    statusStartIndex,
                    unit.Statuses.Count,
                    unit.StatusSnapshot.TotalShield));
            }
        }
    }
}
