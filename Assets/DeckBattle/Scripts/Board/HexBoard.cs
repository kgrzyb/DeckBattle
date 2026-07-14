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

        public Vector3 ToLocalPosition(HexCoord coord)
        {
            float rowOffset = (coord.R & 1) == 0 ? -0.25f : 0.25f;
            float centeredQ = coord.Q - (Width - 1) * 0.5f + rowOffset;
            float x = HexSize * Mathf.Sqrt(3f) * centeredQ;
            float z = HexSize * 1.5f * coord.R;
            float centerZ = HexSize * 1.5f * (Height - 1) * 0.5f;
            return new Vector3(x, 0f, z - centerZ);
        }
    }
}
