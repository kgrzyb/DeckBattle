using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeckBattle
{
    public sealed class HexBoard
    {
        private static readonly HexCoord[] Directions =
        {
            new HexCoord(1, 0),
            new HexCoord(1, -1),
            new HexCoord(0, -1),
            new HexCoord(-1, 0),
            new HexCoord(-1, 1),
            new HexCoord(0, 1)
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
            return from.DistanceTo(to);
        }

        public int FillNeighbors(HexCoord coord, IList<HexCoord> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            int added = 0;
            for (int i = 0; i < Directions.Length; i++)
            {
                HexCoord direction = Directions[i];
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
            for (int dq = -range; dq <= range; dq++)
            {
                int minDr = Math.Max(-range, -dq - range);
                int maxDr = Math.Min(range, -dq + range);
                for (int dr = minDr; dr <= maxDr; dr++)
                {
                    HexCoord coord = new HexCoord(center.Q + dq, center.R + dr);
                    if (!Contains(coord))
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
                    if (!IsWalkable(neighbor) || cameFrom.ContainsKey(neighbor))
                    {
                        continue;
                    }

                    cameFrom.Add(neighbor, current);
                    frontier.Add(neighbor);
                }
            }

            return false;
        }

        public Vector3 ToLocalPosition(HexCoord coord)
        {
            float rowOffset = (coord.R & 1) == 0 ? -0.25f : 0.25f;
            float centeredQ = coord.Q - (Width - 1) * 0.5f + rowOffset;
            float x = HexSize * Mathf.Sqrt(3f) * centeredQ;
            float z = HexSize * 1.5f * coord.R;
            float centerZ = HexSize * 1.5f * (Height - 1) * 0.5f;
            return new Vector3(x, 0f, z - centerZ);
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

            public PathfindingWorkspace(int boardCellCapacity)
            {
                int capacity = Math.Max(1, boardCellCapacity);
                CameFrom = new Dictionary<HexCoord, HexCoord>(capacity);
                Frontier = new List<HexCoord>(capacity);
                Neighbors = new List<HexCoord>(6);
                ReversedPath = new List<HexCoord>(capacity);
            }

            internal void Clear()
            {
                CameFrom.Clear();
                Frontier.Clear();
                Neighbors.Clear();
                ReversedPath.Clear();
            }
        }
    }
}
