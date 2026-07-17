using System;
using System.Collections.Generic;

namespace DeckBattle
{
    public sealed class BattleSimulation
    {
        private readonly List<UnitRuntimeState> units;
        private readonly List<ProjectileRuntimeState> projectiles;
        private readonly Dictionary<HexCoord, UnitRuntimeState> unitByHex;
        private readonly Dictionary<int, UnitRuntimeState> unitById;
        private int nextProjectileId;

        private BattleSimulation(
            HexBoard board,
            List<UnitRuntimeState> units,
            List<ProjectileRuntimeState> projectiles,
            Dictionary<HexCoord, UnitRuntimeState> unitByHex,
            Dictionary<int, UnitRuntimeState> unitById,
            BattleRuntimeTuning tuning,
            DeterministicRandom rng)
        {
            Board = board;
            this.units = units;
            this.projectiles = projectiles;
            this.unitByHex = unitByHex;
            this.unitById = unitById;
            Tuning = tuning;
            Random = rng;
            nextProjectileId = 1;
        }

        public HexBoard Board { get; private set; }
        public BattleRuntimeTuning Tuning { get; private set; }
        public DeterministicRandom Random { get; private set; }
        public bool IsBattleEnded { get; private set; }
        public bool HasWinner { get; private set; }
        public BattleSide Winner { get; private set; }

        public IReadOnlyList<UnitRuntimeState> Units
        {
            get { return units; }
        }

        public IReadOnlyList<ProjectileRuntimeState> Projectiles
        {
            get { return projectiles; }
        }

        public static BattleSimulation Create(HexBoard board, IList<UnitSpawnData> spawnData)
        {
            return Create(board, spawnData, BattleRuntimeTuning.Default);
        }

        public static BattleSimulation Create(HexBoard board, IList<UnitSpawnData> spawnData, BattleRuntimeTuning tuning)
        {
            return Create(board, spawnData, tuning, 1);
        }

        public static BattleSimulation Create(HexBoard board, IList<UnitSpawnData> spawnData, BattleRuntimeTuning tuning, int randomSeed)
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
                ValidateSpawn(board, spawn, unitByHex, unitById);

                var unit = new UnitRuntimeState(spawn.UnitId, spawn.Definition, spawn.Side, spawn.StartHex);
                units.Add(unit);
                unitByHex.Add(spawn.StartHex, unit);
                unitById.Add(spawn.UnitId, unit);
            }

            return new BattleSimulation(board, units, new List<ProjectileRuntimeState>(4), unitByHex, unitById, tuning, new DeterministicRandom(randomSeed));
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

        public void StartUnitMovement(UnitRuntimeState unit, HexCoord destination)
        {
            if (unit == null)
            {
                throw new ArgumentNullException(nameof(unit));
            }

            if (!unit.IsAlive)
            {
                throw new ArgumentException("Defeated units cannot move.", nameof(unit));
            }

            if (unit.IsMoving)
            {
                throw new InvalidOperationException("Unit is already moving.");
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

            unit.IsMoving = true;
            unit.MovementDestination = destination;
            unit.MovementTimeRemaining = Tuning.MovementStepDuration;
        }

        public void CompleteUnitMovement(UnitRuntimeState unit)
        {
            if (unit == null)
            {
                throw new ArgumentNullException(nameof(unit));
            }

            if (!unit.IsMoving)
            {
                return;
            }

            HexCoord destination = unit.MovementDestination;
            if (!Board.IsWalkable(destination))
            {
                throw new InvalidOperationException("Movement destination is no longer walkable.");
            }

            UnitRuntimeState occupyingUnit;
            if (unitByHex.TryGetValue(destination, out occupyingUnit) && occupyingUnit != unit)
            {
                throw new InvalidOperationException("Movement destination is occupied.");
            }

            unitByHex.Remove(unit.CurrentHex);
            unit.CurrentHex = destination;
            unitByHex[destination] = unit;
            unit.IsMoving = false;
            unit.MovementTimeRemaining = 0f;
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
            unit.IsMoving = false;
            unit.MovementDestination = unit.CurrentHex;
            unit.MovementTimeRemaining = 0f;
            unitByHex.Remove(unit.CurrentHex);
        }

        public ProjectileRuntimeState SpawnProjectile(
            UnitRuntimeState attacker,
            UnitRuntimeState target,
            ProjectileDefinition projectileDefinition,
            int damage,
            bool isCritical)
        {
            if (attacker == null)
            {
                throw new ArgumentNullException(nameof(attacker));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (projectileDefinition == null)
            {
                throw new ArgumentNullException(nameof(projectileDefinition));
            }

            float distance = Board.Distance(attacker.CurrentHex, target.CurrentHex) * Board.HexSize;
            float travelDuration = distance / projectileDefinition.Speed;
            var projectile = new ProjectileRuntimeState(
                nextProjectileId,
                attacker.UnitId,
                target.UnitId,
                attacker.Definition,
                projectileDefinition,
                attacker.CurrentHex,
                target.CurrentHex,
                travelDuration,
                damage,
                isCritical);
            nextProjectileId++;
            projectiles.Add(projectile);
            return projectile;
        }

        public void RemoveProjectileAt(int index)
        {
            if (index < 0 || index >= projectiles.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            projectiles.RemoveAt(index);
        }

        public void CompleteBattle(BattleSide winner, bool hasWinner)
        {
            IsBattleEnded = true;
            HasWinner = hasWinner;
            Winner = winner;
        }

        private static void ValidateSpawn(
            HexBoard board,
            UnitSpawnData spawn,
            Dictionary<HexCoord, UnitRuntimeState> occupiedHexes,
            Dictionary<int, UnitRuntimeState> occupiedUnitIds)
        {
            if (spawn.UnitId <= 0)
            {
                throw new ArgumentException("Spawn data contains a non-positive unit id.", nameof(spawn));
            }

            if (occupiedUnitIds.ContainsKey(spawn.UnitId))
            {
                throw new ArgumentException("Spawn data contains duplicate unit ids.", nameof(spawn));
            }

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
