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

        public int S
        {
            get { return -Q - R; }
        }

        public int DistanceTo(HexCoord other)
        {
            int dq = Math.Abs(Q - other.Q);
            int dr = Math.Abs(R - other.R);
            int ds = Math.Abs(S - other.S);
            return (dq + dr + ds) / 2;
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
    }
}
