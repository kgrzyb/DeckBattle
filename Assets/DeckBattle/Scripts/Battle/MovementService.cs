using System;
using System.Collections.Generic;

namespace DeckBattle
{
    public static class MovementService
    {
        public static bool MoveTowardsTarget(HexBoard board, RuntimeUnit mover, RuntimeUnit target, IList<RuntimeUnit> allUnits)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            var workspace = new MovementWorkspace(board.Width * board.Height);
            return MoveTowardsTarget(board, mover, target, allUnits, workspace);
        }

        public static bool MoveTowardsTarget(
            HexBoard board,
            RuntimeUnit mover,
            RuntimeUnit target,
            IList<RuntimeUnit> allUnits,
            MovementWorkspace workspace)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            if (mover == null)
            {
                throw new ArgumentNullException(nameof(mover));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (allUnits == null)
            {
                throw new ArgumentNullException(nameof(allUnits));
            }

            if (workspace == null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            if (!mover.IsAlive || !target.IsAlive)
            {
                return false;
            }

            int currentDistance = board.Distance(mover.BattleCoord, target.BattleCoord);
            if (currentDistance <= mover.Definition.AttackRange || mover.Definition.MoveRange <= 0)
            {
                return false;
            }

            HexCoord bestCoord = mover.BattleCoord;
            int bestDistanceToTarget = currentDistance;
            int bestSteps = 0;

            workspace.Clear();
            List<HexCoord> visited = workspace.Visited;
            List<PathNode> frontier = workspace.Frontier;
            List<HexCoord> neighbors = workspace.Neighbors;
            visited.Add(mover.BattleCoord);
            frontier.Add(new PathNode(mover.BattleCoord, 0));

            int readIndex = 0;
            while (readIndex < frontier.Count)
            {
                PathNode node = frontier[readIndex];
                readIndex++;

                if (node.Steps >= mover.Definition.MoveRange)
                {
                    continue;
                }

                neighbors.Clear();
                board.FillNeighbors(node.Coord, neighbors);
                for (int i = 0; i < neighbors.Count; i++)
                {
                    HexCoord neighbor = neighbors[i];
                    if (ContainsCoord(visited, neighbor) || IsOccupied(neighbor, mover, allUnits))
                    {
                        continue;
                    }

                    int steps = node.Steps + 1;
                    visited.Add(neighbor);
                    frontier.Add(new PathNode(neighbor, steps));

                    int distanceToTarget = board.Distance(neighbor, target.BattleCoord);
                    if (IsBetterMove(neighbor, steps, distanceToTarget, bestCoord, bestSteps, bestDistanceToTarget))
                    {
                        bestCoord = neighbor;
                        bestSteps = steps;
                        bestDistanceToTarget = distanceToTarget;
                    }
                }
            }

            if (bestDistanceToTarget >= currentDistance)
            {
                return false;
            }

            mover.BattleCoord = bestCoord;
            return true;
        }

        private static bool IsOccupied(HexCoord coord, RuntimeUnit ignoredUnit, IList<RuntimeUnit> allUnits)
        {
            for (int i = 0; i < allUnits.Count; i++)
            {
                RuntimeUnit unit = allUnits[i];
                if (unit == null || unit == ignoredUnit || !unit.IsAlive)
                {
                    continue;
                }

                if (unit.BattleCoord == coord)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsCoord(List<HexCoord> coords, HexCoord coord)
        {
            for (int i = 0; i < coords.Count; i++)
            {
                if (coords[i] == coord)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsBetterMove(HexCoord coord, int steps, int distanceToTarget, HexCoord bestCoord, int bestSteps, int bestDistanceToTarget)
        {
            if (distanceToTarget != bestDistanceToTarget)
            {
                return distanceToTarget < bestDistanceToTarget;
            }

            if (steps != bestSteps)
            {
                return bestSteps == 0 || steps < bestSteps;
            }

            if (coord.Q != bestCoord.Q)
            {
                return coord.Q < bestCoord.Q;
            }

            return coord.R < bestCoord.R;
        }

        public sealed class MovementWorkspace
        {
            internal readonly List<HexCoord> Visited;
            internal readonly List<PathNode> Frontier;
            internal readonly List<HexCoord> Neighbors;

            public MovementWorkspace(int boardCellCapacity)
            {
                int capacity = Math.Max(1, boardCellCapacity);
                Visited = new List<HexCoord>(capacity);
                Frontier = new List<PathNode>(capacity);
                Neighbors = new List<HexCoord>(6);
            }

            internal void Clear()
            {
                Visited.Clear();
                Frontier.Clear();
                Neighbors.Clear();
            }
        }

        internal readonly struct PathNode
        {
            public readonly HexCoord Coord;
            public readonly int Steps;

            public PathNode(HexCoord coord, int steps)
            {
                Coord = coord;
                Steps = steps;
            }
        }
    }
}
