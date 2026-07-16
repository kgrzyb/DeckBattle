using System;

namespace DeckBattle
{
    public readonly struct HexCoord : IEquatable<HexCoord>
    {
        public readonly int Q;
        public readonly int R;

        public HexCoord(int q, int r)
        {
            Q = q;
            R = r;
        }

        public int DistanceTo(HexCoord other)
        {
            OffsetToCube(this, out int fromX, out int fromY, out int fromZ);
            OffsetToCube(other, out int toX, out int toY, out int toZ);
            int dx = Math.Abs(fromX - toX);
            int dy = Math.Abs(fromY - toY);
            int dz = Math.Abs(fromZ - toZ);
            return Math.Max(dx, Math.Max(dy, dz));
        }

        public bool Equals(HexCoord other)
        {
            return Q == other.Q && R == other.R;
        }

        public override bool Equals(object obj)
        {
            return obj is HexCoord other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Q * 397) ^ R;
            }
        }

        public override string ToString()
        {
            return "(" + Q + ", " + R + ")";
        }

        public static bool operator ==(HexCoord left, HexCoord right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(HexCoord left, HexCoord right)
        {
            return !left.Equals(right);
        }

        private static void OffsetToCube(HexCoord coord, out int x, out int y, out int z)
        {
            x = coord.Q - (coord.R - (coord.R & 1)) / 2;
            z = coord.R;
            y = -x - z;
        }
    }
}
