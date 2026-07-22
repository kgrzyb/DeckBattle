using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeckBattle
{
    public sealed class HexBoard
    {
        private static readonly HexCoord[] EvenRowDirections =
        {
            new HexCoord(1, 0),
            new HexCoord(0, -1),
            new HexCoord(-1, -1),
            new HexCoord(-1, 0),
            new HexCoord(-1, 1),
            new HexCoord(0, 1)
        };

        private static readonly HexCoord[] OddRowDirections =
        {
            new HexCoord(1, 0),
            new HexCoord(1, -1),
            new HexCoord(0, -1),
            new HexCoord(-1, 0),
            new HexCoord(0, 1),
            new HexCoord(1, 1)
        };

        public readonly int Width;
        public readonly int Height;
        public readonly float HexSize;

        private readonly HashSet<HexCoord> blockedHexes = new HashSet<HexCoord>();

        public HexBoard(int width, int height, float hexSize)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            if (height <= 1)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            if (hexSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(hexSize));
            }

            Width = width;
            Height = height;
            HexSize = hexSize;
        }

        public bool Contains(HexCoord coord)
        {
            return coord.Q >= 0 && coord.Q < Width && coord.R >= 0 && coord.R < Height;
        }

        public bool IsValidHex(HexCoord coord)
        {
            return Contains(coord);
        }

        public bool IsWalkable(HexCoord coord)
        {
            return Contains(coord) && !blockedHexes.Contains(coord);
        }

        public void SetWalkable(HexCoord coord, bool walkable)
        {
            if (!Contains(coord))
            {
                throw new ArgumentOutOfRangeException(nameof(coord));
            }

            if (walkable)
            {
                blockedHexes.Remove(coord);
            }
            else
            {
                blockedHexes.Add(coord);
            }
        }

        public bool IsDeploymentCoord(BattleSide side, HexCoord coord)
        {
            if (!Contains(coord))
            {
                return false;
            }

            int deploymentRows = Height / 2;
            if (side == BattleSide.Player)
            {
                return coord.R < deploymentRows;
            }

            return coord.R >= Height - deploymentRows;
        }

        public int Distance(HexCoord from, HexCoord to)
        {
            OffsetToCube(from, out int fromX, out int fromY, out int fromZ);
            OffsetToCube(to, out int toX, out int toY, out int toZ);
            int dx = Math.Abs(fromX - toX);
            int dy = Math.Abs(fromY - toY);
            int dz = Math.Abs(fromZ - toZ);
            return Math.Max(dx, Math.Max(dy, dz));
        }

        public int FillNeighbors(HexCoord coord, IList<HexCoord> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            int added = 0;
            HexCoord[] directions = (coord.R & 1) == 0 ? EvenRowDirections : OddRowDirections;
            for (int i = 0; i < directions.Length; i++)
            {
                HexCoord direction = directions[i];
                HexCoord neighbor = new HexCoord(coord.Q + direction.Q, coord.R + direction.R);
                if (!Contains(neighbor))
                {
                    continue;
                }

                results.Add(neighbor);
                added++;
            }

            return added;
        }

        public List<HexCoord> GetNeighbors(HexCoord coord)
        {
            var neighbors = new List<HexCoord>(6);
            FillNeighbors(coord, neighbors);
            return neighbors;
        }

        public int FillHexesInRange(HexCoord center, int range, IList<HexCoord> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            if (range < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(range));
            }

            int added = 0;
            for (int r = 0; r < Height; r++)
            {
                for (int q = 0; q < Width; q++)
                {
                    HexCoord coord = new HexCoord(q, r);
                    if (Distance(center, coord) > range)
                    {
                        continue;
                    }

                    results.Add(coord);
                    added++;
                }
            }

            return added;
        }

        public List<HexCoord> GetHexesInRange(HexCoord center, int range)
        {
            var hexes = new List<HexCoord>();
            FillHexesInRange(center, range, hexes);
            return hexes;
        }

        public bool TryFindPath(HexCoord start, HexCoord goal, IList<HexCoord> path)
        {
            var workspace = new PathfindingWorkspace(Width * Height);
            return TryFindPath(start, goal, path, workspace);
        }

        public bool TryFindPath(HexCoord start, HexCoord goal, IList<HexCoord> path, PathfindingWorkspace workspace)
        {
            return TryFindPath(start, goal, path, workspace, null);
        }

        public bool TryFindPath(
            HexCoord start,
            HexCoord goal,
            IList<HexCoord> path,
            PathfindingWorkspace workspace,
            HashSet<HexCoord> additionalBlockedHexes)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (workspace == null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            path.Clear();
            workspace.Clear();

            if (!IsWalkable(start) || !IsWalkable(goal))
            {
                return false;
            }

            Dictionary<HexCoord, HexCoord> cameFrom = workspace.CameFrom;
            List<HexCoord> frontier = workspace.Frontier;
            List<HexCoord> neighbors = workspace.Neighbors;

            cameFrom.Add(start, start);
            frontier.Add(start);

            int readIndex = 0;
            while (readIndex < frontier.Count)
            {
                HexCoord current = frontier[readIndex];
                readIndex++;

                if (current == goal)
                {
                    BuildPath(start, goal, path, workspace.ReversedPath, cameFrom);
                    return true;
                }

                neighbors.Clear();
                FillNeighbors(current, neighbors);
                for (int i = 0; i < neighbors.Count; i++)
                {
                    HexCoord neighbor = neighbors[i];
                    if (!IsWalkable(neighbor)
                        || IsDynamicallyBlocked(neighbor, start, additionalBlockedHexes)
                        || cameFrom.ContainsKey(neighbor))
                    {
                        continue;
                    }

                    cameFrom.Add(neighbor, current);
                    frontier.Add(neighbor);
                }
            }

            return false;
        }

        public bool TryFindShortestPathToAny(
            HexCoord start,
            IList<HexCoord> goals,
            IList<HexCoord> path,
            PathfindingWorkspace workspace,
            HashSet<HexCoord> additionalBlockedHexes,
            out HexCoord selectedGoal,
            out HexCoord nextStep,
            out int pathSteps)
        {
            if (goals == null)
            {
                throw new ArgumentNullException(nameof(goals));
            }

            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (workspace == null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            path.Clear();
            workspace.Clear();
            selectedGoal = default;
            nextStep = default;
            pathSteps = 0;

            if (!IsWalkable(start))
            {
                return false;
            }

            for (int i = 0; i < goals.Count; i++)
            {
                HexCoord goal = goals[i];
                if (!IsWalkable(goal)
                    || (goal != start && IsDynamicallyBlocked(goal, start, additionalBlockedHexes)))
                {
                    continue;
                }

                workspace.Goals.Add(goal);
            }

            if (workspace.Goals.Count == 0)
            {
                return false;
            }

            if (workspace.Goals.Contains(start))
            {
                selectedGoal = start;
                nextStep = start;
                path.Add(start);
                return true;
            }

            Dictionary<HexCoord, HexCoord> cameFrom = workspace.CameFrom;
            List<HexCoord> frontier = workspace.Frontier;
            List<HexCoord> neighbors = workspace.Neighbors;
            cameFrom.Add(start, start);
            frontier.Add(start);

            int readIndex = 0;
            int levelEnd = frontier.Count;
            int distance = 0;
            bool foundAtCurrentLevel = false;
            HexCoord bestGoal = default;

            while (readIndex < frontier.Count)
            {
                while (readIndex < levelEnd)
                {
                    HexCoord current = frontier[readIndex];
                    readIndex++;

                    if (workspace.Goals.Contains(current))
                    {
                        if (!foundAtCurrentLevel || CompareHexCoords(current, bestGoal) < 0)
                        {
                            bestGoal = current;
                        }

                        foundAtCurrentLevel = true;
                    }

                    neighbors.Clear();
                    FillNeighbors(current, neighbors);
                    for (int i = 0; i < neighbors.Count; i++)
                    {
                        HexCoord neighbor = neighbors[i];
                        if (!IsWalkable(neighbor)
                            || IsDynamicallyBlocked(neighbor, start, additionalBlockedHexes)
                            || cameFrom.ContainsKey(neighbor))
                        {
                            continue;
                        }

                        cameFrom.Add(neighbor, current);
                        frontier.Add(neighbor);
                    }
                }

                if (foundAtCurrentLevel)
                {
                    selectedGoal = bestGoal;
                    pathSteps = distance;
                    BuildPath(start, selectedGoal, path, workspace.ReversedPath, cameFrom);
                    nextStep = path.Count > 1 ? path[1] : start;
                    return true;
                }

                distance++;
                levelEnd = frontier.Count;
            }

            return false;
        }

        public bool TryFindShortestPathToAny(
            HexCoord start,
            IList<HexCoord> goals,
            IList<HexCoord> path,
            PathfindingWorkspace workspace,
            out HexCoord selectedGoal,
            out HexCoord nextStep,
            out int pathSteps)
        {
            return TryFindShortestPathToAny(
                start,
                goals,
                path,
                workspace,
                null,
                out selectedGoal,
                out nextStep,
                out pathSteps);
        }

        private static bool IsDynamicallyBlocked(
            HexCoord coord,
            HexCoord start,
            HashSet<HexCoord> additionalBlockedHexes)
        {
            if (additionalBlockedHexes == null || coord == start)
            {
                return false;
            }

            return additionalBlockedHexes.Contains(coord);
        }

        private static int CompareHexCoords(HexCoord left, HexCoord right)
        {
            int qCompare = left.Q.CompareTo(right.Q);
            if (qCompare != 0)
            {
                return qCompare;
            }

            return left.R.CompareTo(right.R);
        }

        public Vector3 ToLocalPosition(HexCoord coord)
        {
            float rowOffset = (coord.R & 1) == 0 ? -0.25f : 0.25f;
            float centeredColumn = coord.Q - (Width - 1) * 0.5f + rowOffset;
            float x = HexSize * Mathf.Sqrt(3f) * centeredColumn;
            float z = HexSize * 1.5f * coord.R;
            float centerZ = HexSize * 1.5f * (Height - 1) * 0.5f;
            return new Vector3(x, 0f, z - centerZ);
        }

        private static void OffsetToCube(HexCoord coord, out int x, out int y, out int z)
        {
            x = coord.Q - (coord.R - (coord.R & 1)) / 2;
            z = coord.R;
            y = -x - z;
        }

        private static void BuildPath(
            HexCoord start,
            HexCoord goal,
            IList<HexCoord> path,
            List<HexCoord> reversedPath,
            Dictionary<HexCoord, HexCoord> cameFrom)
        {
            reversedPath.Clear();

            HexCoord current = goal;
            reversedPath.Add(current);
            while (current != start)
            {
                current = cameFrom[current];
                reversedPath.Add(current);
            }

            for (int i = reversedPath.Count - 1; i >= 0; i--)
            {
                path.Add(reversedPath[i]);
            }
        }

        public sealed class PathfindingWorkspace
        {
            internal readonly Dictionary<HexCoord, HexCoord> CameFrom;
            internal readonly List<HexCoord> Frontier;
            internal readonly List<HexCoord> Neighbors;
            internal readonly List<HexCoord> ReversedPath;
            internal readonly HashSet<HexCoord> Goals;

            public PathfindingWorkspace(int boardCellCapacity)
            {
                int capacity = Math.Max(1, boardCellCapacity);
                CameFrom = new Dictionary<HexCoord, HexCoord>(capacity);
                Frontier = new List<HexCoord>(capacity);
                Neighbors = new List<HexCoord>(6);
                ReversedPath = new List<HexCoord>(capacity);
                Goals = new HashSet<HexCoord>(capacity);
            }

            internal void Clear()
            {
                CameFrom.Clear();
                Frontier.Clear();
                Neighbors.Clear();
                ReversedPath.Clear();
                Goals.Clear();
            }
        }
    }
}
