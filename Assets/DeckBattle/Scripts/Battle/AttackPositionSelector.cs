using System;
using System.Collections.Generic;

namespace DeckBattle
{
    public static class AttackPositionSelector
    {
        public static bool TrySelectAttackPosition(
            BattleSimulation simulation,
            UnitRuntimeState attacker,
            UnitRuntimeState target,
            out HexCoord attackPosition)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            var workspace = new Workspace(simulation.Board.Width * simulation.Board.Height);
            return TrySelectAttackPosition(simulation, attacker, target, workspace, out attackPosition);
        }

        public static bool TrySelectAttackPosition(
            BattleSimulation simulation,
            UnitRuntimeState attacker,
            UnitRuntimeState target,
            Workspace workspace,
            out HexCoord attackPosition)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            if (attacker == null)
            {
                throw new ArgumentNullException(nameof(attacker));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (workspace == null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            attackPosition = default;
            if (!attacker.IsAlive || !target.IsAlive || attacker.Side == target.Side)
            {
                return false;
            }

            HexBoard board = simulation.Board;
            int attackRange = simulation.Tuning.GetAttackRange(attacker.Definition);
            if (board.Distance(attacker.CurrentHex, target.CurrentHex) <= attackRange)
            {
                attackPosition = attacker.CurrentHex;
                return true;
            }

            workspace.Clear();
            FillOccupiedHexes(simulation.Units, workspace.OccupiedHexes);
            board.FillHexesInRange(target.CurrentHex, attackRange, workspace.RangeHexes);

            bool hasBest = false;
            int bestPathSteps = int.MaxValue;
            HexCoord bestPosition = default;

            for (int i = 0; i < workspace.RangeHexes.Count; i++)
            {
                HexCoord candidate = workspace.RangeHexes[i];
                if (!IsCandidateAttackPosition(board, attacker, target, candidate, attackRange, workspace.OccupiedHexes))
                {
                    continue;
                }

                if (!board.TryFindPath(attacker.CurrentHex, candidate, workspace.Path, workspace.Pathfinding, workspace.OccupiedHexes))
                {
                    continue;
                }

                int pathSteps = workspace.Path.Count - 1;
                if (!IsBetterPosition(candidate, pathSteps, hasBest, bestPosition, bestPathSteps))
                {
                    continue;
                }

                hasBest = true;
                bestPathSteps = pathSteps;
                bestPosition = candidate;
            }

            if (!hasBest)
            {
                return false;
            }

            attackPosition = bestPosition;
            return true;
        }

        private static bool IsCandidateAttackPosition(
            HexBoard board,
            UnitRuntimeState attacker,
            UnitRuntimeState target,
            HexCoord candidate,
            int attackRange,
            HashSet<HexCoord> occupiedHexes)
        {
            if (!board.IsWalkable(candidate))
            {
                return false;
            }

            if (board.Distance(candidate, target.CurrentHex) > attackRange)
            {
                return false;
            }

            if (candidate != attacker.CurrentHex && occupiedHexes.Contains(candidate))
            {
                return false;
            }

            return candidate != target.CurrentHex;
        }

        private static bool IsBetterPosition(
            HexCoord candidate,
            int pathSteps,
            bool hasBest,
            HexCoord bestPosition,
            int bestPathSteps)
        {
            if (!hasBest)
            {
                return true;
            }

            if (pathSteps != bestPathSteps)
            {
                return pathSteps < bestPathSteps;
            }

            if (candidate.Q != bestPosition.Q)
            {
                return candidate.Q < bestPosition.Q;
            }

            return candidate.R < bestPosition.R;
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
