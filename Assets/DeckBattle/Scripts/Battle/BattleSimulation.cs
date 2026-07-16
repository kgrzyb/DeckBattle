using System;
using System.Collections.Generic;

namespace DeckBattle
{
    public sealed class BattleSimulation
    {
        private readonly List<UnitRuntimeState> units;
        private readonly Dictionary<HexCoord, UnitRuntimeState> unitByHex;
        private readonly Dictionary<int, UnitRuntimeState> unitById;

        private BattleSimulation(
            HexBoard board,
            List<UnitRuntimeState> units,
            Dictionary<HexCoord, UnitRuntimeState> unitByHex,
            Dictionary<int, UnitRuntimeState> unitById,
            BattleRuntimeTuning tuning)
        {
            Board = board;
            this.units = units;
            this.unitByHex = unitByHex;
            this.unitById = unitById;
            Tuning = tuning;
        }

        public HexBoard Board { get; private set; }
        public BattleRuntimeTuning Tuning { get; private set; }
        public bool IsBattleEnded { get; private set; }
        public bool HasWinner { get; private set; }
        public BattleSide Winner { get; private set; }

        public IReadOnlyList<UnitRuntimeState> Units
        {
            get { return units; }
        }

        public static BattleSimulation Create(HexBoard board, IList<UnitSpawnData> spawnData)
        {
            return Create(board, spawnData, BattleRuntimeTuning.Default);
        }

        public static BattleSimulation Create(HexBoard board, IList<UnitSpawnData> spawnData, BattleRuntimeTuning tuning)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            if (spawnData == null)
            {
                throw new ArgumentNullException(nameof(spawnData));
            }

            var units = new List<UnitRuntimeState>(spawnData.Count);
            var unitByHex = new Dictionary<HexCoord, UnitRuntimeState>(spawnData.Count);
            var unitById = new Dictionary<int, UnitRuntimeState>(spawnData.Count);

            for (int i = 0; i < spawnData.Count; i++)
            {
                UnitSpawnData spawn = spawnData[i];
                ValidateSpawn(board, spawn, unitByHex);

                int unitId = i + 1;
                var unit = new UnitRuntimeState(unitId, spawn.Definition, spawn.Side, spawn.StartHex);
                units.Add(unit);
                unitByHex.Add(spawn.StartHex, unit);
                unitById.Add(unitId, unit);
            }

            return new BattleSimulation(board, units, unitByHex, unitById, tuning);
        }

        public bool TryGetUnitAt(HexCoord hex, out UnitRuntimeState unit)
        {
            return unitByHex.TryGetValue(hex, out unit);
        }

        public bool TryGetUnitById(int unitId, out UnitRuntimeState unit)
        {
            return unitById.TryGetValue(unitId, out unit);
        }

        public void MoveUnit(UnitRuntimeState unit, HexCoord destination)
        {
            if (unit == null)
            {
                throw new ArgumentNullException(nameof(unit));
            }

            if (!Board.IsWalkable(destination))
            {
                throw new ArgumentException("Destination is not walkable.", nameof(destination));
            }

            UnitRuntimeState occupyingUnit;
            if (unitByHex.TryGetValue(destination, out occupyingUnit) && occupyingUnit != unit)
            {
                throw new ArgumentException("Destination is occupied.", nameof(destination));
            }

            unitByHex.Remove(unit.CurrentHex);
            unit.CurrentHex = destination;
            unitByHex[destination] = unit;
        }

        public void DefeatUnit(UnitRuntimeState unit)
        {
            if (unit == null)
            {
                throw new ArgumentNullException(nameof(unit));
            }

            unit.IsDefeated = true;
            unit.CurrentHp = Math.Min(0, unit.CurrentHp);
            unit.ClearTarget();
            unitByHex.Remove(unit.CurrentHex);
        }

        public void CompleteBattle(BattleSide winner, bool hasWinner)
        {
            IsBattleEnded = true;
            HasWinner = hasWinner;
            Winner = winner;
        }

        private static void ValidateSpawn(HexBoard board, UnitSpawnData spawn, Dictionary<HexCoord, UnitRuntimeState> occupiedHexes)
        {
            if (spawn.Definition == null)
            {
                throw new ArgumentException("Spawn data contains a null unit definition.", nameof(spawn));
            }

            if (!board.IsValidHex(spawn.StartHex))
            {
                throw new ArgumentException("Spawn data contains a unit outside the board.", nameof(spawn));
            }

            if (!board.IsWalkable(spawn.StartHex))
            {
                throw new ArgumentException("Spawn data contains a unit on a blocked hex.", nameof(spawn));
            }

            if (occupiedHexes.ContainsKey(spawn.StartHex))
            {
                throw new ArgumentException("Spawn data contains duplicate starting hexes.", nameof(spawn));
            }
        }
    }
}
