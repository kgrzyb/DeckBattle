using System;
using UnityEngine;

namespace DeckBattle
{
    public sealed class HexBoardLayout
    {
        public readonly int Width;
        public readonly int Height;
        public readonly float HexSize;

        public HexBoardLayout(HexBoard board)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            Width = board.Width;
            Height = board.Height;
            HexSize = board.HexSize;
        }

        public bool Matches(HexBoard board)
        {
            return board != null
                && Width == board.Width
                && Height == board.Height
                && Mathf.Approximately(HexSize, board.HexSize);
        }

        public Vector3 GetLocalPosition(HexCoord coord)
        {
            float rowOffset = (coord.R & 1) == 0 ? -0.25f : 0.25f;
            float centeredColumn = coord.Q - (Width - 1) * 0.5f + rowOffset;
            float x = HexSize * Mathf.Sqrt(3f) * centeredColumn;
            float z = HexSize * 1.5f * coord.R;
            float centerZ = HexSize * 1.5f * (Height - 1) * 0.5f;
            return new Vector3(x, 0f, z - centerZ);
        }

        public Vector3 GetLocalCenter()
        {
            Vector3 localMin = GetLocalPosition(new HexCoord(0, 0));
            Vector3 localMax = GetLocalPosition(new HexCoord(Width - 1, Height - 1));
            return (localMin + localMax) * 0.5f;
        }
    }
}
