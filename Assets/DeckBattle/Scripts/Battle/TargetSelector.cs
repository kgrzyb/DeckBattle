using System;
using System.Collections.Generic;

namespace DeckBattle
{
    public static class TargetSelector
    {
        public static UnitRuntimeState SelectTarget(BattleSimulation simulation, UnitRuntimeState attacker)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            if (attacker == null)
            {
                throw new ArgumentNullException(nameof(attacker));
            }

            if (!attacker.IsAlive)
            {
                return null;
            }

            var workspace = new Workspace(simulation.Board.Width * simulation.Board.Height);
            return SelectTarget(simulation, attacker, workspace);
        }

        public static UnitRuntimeState SelectTarget(BattleSimulation simulation, UnitRuntimeState attacker, Workspace workspace)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            if (attacker == null)
            {
                throw new ArgumentNullException(nameof(attacker));
            }

            if (workspace == null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            if (!attacker.IsAlive)
            {
                return null;
            }

            workspace.Clear();
            FillOccupiedHexes(simulation.Units, workspace.OccupiedHexes);

            UnitRuntimeState nearestEnemy = null;
            int nearestEnemyDistance = int.MaxValue;

            IReadOnlyList<UnitRuntimeState> units = simulation.Units;
            for (int i = 0; i < units.Count; i++)
            {
                UnitRuntimeState candidate = units[i];
                if (!IsLiveEnemy(attacker, candidate))
                {
                    continue;
                }

                int distance = simulation.Board.Distance(attacker.CurrentHex, candidate.CurrentHex);
                if (IsBetterNearestTarget(candidate, distance, nearestEnemy, nearestEnemyDistance))
                {
                    nearestEnemy = candidate;
                    nearestEnemyDistance = distance;
                }
            }

            if (nearestEnemy == null)
            {
                return null;
            }

            if (HasReachableAttackPosition(simulation, attacker, nearestEnemy, workspace))
            {
                return nearestEnemy;
            }

            UnitRuntimeState lowestHpReachable = null;
            int lowestReachableHp = int.MaxValue;
            for (int i = 0; i < units.Count; i++)
            {
                UnitRuntimeState candidate = units[i];
                if (!IsLiveEnemy(attacker, candidate))
                {
                    continue;
                }

                if (HasReachableAttackPosition(simulation, attacker, candidate, workspace)
                    && IsBetterLowestHpTarget(candidate, lowestHpReachable, lowestReachableHp))
                {
                    lowestHpReachable = candidate;
                    lowestReachableHp = candidate.CurrentHp;
                }
            }

            return lowestHpReachable;
        }

        private static bool IsLiveEnemy(UnitRuntimeState attacker, UnitRuntimeState candidate)
        {
            return candidate != null && candidate.IsAlive && candidate.Side != attacker.Side;
        }

        private static void FillOccupiedHexes(IReadOnlyList<UnitRuntimeState> units, HashSet<HexCoord> occupiedHexes)
        {
            for (int i = 0; i < units.Count; i++)
            {
                UnitRuntimeState unit = units[i];
                if (unit != null && unit.IsAlive)
                {
                    occupiedHexes.Add(unit.CurrentHex);
                    if (unit.IsMoving)
                    {
                        occupiedHexes.Add(unit.MovementDestination);
                    }
                }
            }
        }

        private static bool HasReachableAttackPosition(
            BattleSimulation simulation,
            UnitRuntimeState attacker,
            UnitRuntimeState target,
            Workspace workspace)
        {
            HexBoard board = simulation.Board;
            int range = simulation.Tuning.GetAttackRange(attacker.Definition);
            if (board.Distance(attacker.CurrentHex, target.CurrentHex) <= range)
            {
                return true;
            }

            workspace.RangeHexes.Clear();
            board.FillHexesInRange(target.CurrentHex, range, workspace.RangeHexes);

            for (int i = 0; i < workspace.RangeHexes.Count; i++)
            {
                HexCoord attackHex = workspace.RangeHexes[i];
                if (attackHex == target.CurrentHex)
                {
                    continue;
                }

                if (attackHex != attacker.CurrentHex && workspace.OccupiedHexes.Contains(attackHex))
                {
                    continue;
                }

                if (!board.TryFindPath(attacker.CurrentHex, attackHex, workspace.Path, workspace.Pathfinding))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static bool IsBetterNearestTarget(
            UnitRuntimeState candidate,
            int distance,
            UnitRuntimeState selected,
            int selectedDistance)
        {
            if (selected == null)
            {
                return true;
            }

            if (distance != selectedDistance)
            {
                return distance < selectedDistance;
            }

            return candidate.UnitId < selected.UnitId;
        }

        private static bool IsBetterLowestHpTarget(UnitRuntimeState candidate, UnitRuntimeState selected, int selectedHp)
        {
            if (selected == null)
            {
                return true;
            }

            if (candidate.CurrentHp != selectedHp)
            {
                return candidate.CurrentHp < selectedHp;
            }

            return candidate.UnitId < selected.UnitId;
        }

        public sealed class Workspace
        {
            internal readonly HashSet<HexCoord> OccupiedHexes;
            internal readonly List<HexCoord> RangeHexes;
            internal readonly List<HexCoord> Path;
            internal readonly HexBoard.PathfindingWorkspace Pathfinding;

            public Workspace(int boardCellCapacity)
            {
                int capacity = Math.Max(1, boardCellCapacity);
                OccupiedHexes = new HashSet<HexCoord>();
                RangeHexes = new List<HexCoord>(capacity);
                Path = new List<HexCoord>(capacity);
                Pathfinding = new HexBoard.PathfindingWorkspace(capacity);
            }

            internal void Clear()
            {
                OccupiedHexes.Clear();
                RangeHexes.Clear();
                Path.Clear();
                Pathfinding.Clear();
            }
        }
    }
}
