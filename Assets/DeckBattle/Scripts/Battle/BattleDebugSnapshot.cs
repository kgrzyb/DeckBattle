using System;
using System.Collections.Generic;

namespace DeckBattle
{
    public sealed class BattleDebugSnapshot
    {
        private readonly Dictionary<HexCoord, int> occupiedHexes;
        private readonly Dictionary<HexCoord, int> reservedHexes;

        public BattleDebugSnapshot(int capacity)
        {
            int initialCapacity = Math.Max(1, capacity);
            occupiedHexes = new Dictionary<HexCoord, int>(initialCapacity);
            reservedHexes = new Dictionary<HexCoord, int>(initialCapacity);
        }

        public IReadOnlyDictionary<HexCoord, int> OccupiedHexes
        {
            get { return occupiedHexes; }
        }

        public IReadOnlyDictionary<HexCoord, int> ReservedHexes
        {
            get { return reservedHexes; }
        }

        public void Capture(BattleSimulation simulation, IReadOnlyList<BattleEvent> events)
        {
            occupiedHexes.Clear();
            reservedHexes.Clear();

            if (simulation == null)
            {
                return;
            }

            for (int i = 0; i < simulation.Units.Count; i++)
            {
                UnitRuntimeState unit = simulation.Units[i];
                if (unit != null && unit.IsAlive)
                {
                    occupiedHexes[unit.CurrentHex] = unit.UnitId;
                    if (unit.IsMoving)
                    {
                        reservedHexes[unit.MovementDestination] = unit.UnitId;
                    }
                }
            }

            if (events == null)
            {
                return;
            }

            for (int i = 0; i < events.Count; i++)
            {
                BattleEvent battleEvent = events[i];
                if (battleEvent.Type == BattleEventType.UnitMoved)
                {
                    reservedHexes[battleEvent.To] = battleEvent.UnitId;
                }
            }
        }
    }
}
