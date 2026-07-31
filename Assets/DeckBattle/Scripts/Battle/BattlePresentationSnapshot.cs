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

        public UnitPresentationState(
            int unitId,
            int presentationId,
            BattleSide side,
            HexCoord hex,
            int currentHp,
            int maxHp,
            int currentMana,
            int maxMana)
        {
            UnitId = unitId;
            PresentationId = presentationId;
            Side = side;
            Hex = hex;
            CurrentHp = currentHp;
            MaxHp = maxHp;
            CurrentMana = currentMana;
            MaxMana = maxMana;
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

        public BattlePresentationSnapshot(int capacity)
        {
            units = new List<UnitPresentationState>(Math.Max(1, capacity));
        }

        public IReadOnlyList<UnitPresentationState> Units { get { return units; } }

        public void Capture(BattleSimulation simulation)
        {
            units.Clear();
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

                units.Add(new UnitPresentationState(
                    unit.UnitId,
                    unit.CombatSpec.PresentationId,
                    unit.Side,
                    unit.CurrentHex,
                    unit.CurrentHp,
                    unit.CombatSpec.MaxHp,
                    unit.CurrentMana,
                    unit.CombatSpec.ManaThreshold));
            }
        }
    }
}
