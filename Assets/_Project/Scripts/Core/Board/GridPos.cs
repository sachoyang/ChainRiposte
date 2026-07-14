using System;
using System.Collections.Generic;

namespace ChainRiposte.Core.Board
{
    /// <summary>
    /// 보드 셀 좌표. x=열(왼쪽→오른쪽), y=행(아래→위).
    /// 중력은 -y 방향이므로 y=0이 '바닥'이다.
    /// </summary>
    public readonly struct GridPos : IEquatable<GridPos>
    {
        public readonly int X;
        public readonly int Y;

        public GridPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        public GridPos Up => new(X, Y + 1);
        public GridPos Down => new(X, Y - 1);
        public GridPos Left => new(X - 1, Y);
        public GridPos Right => new(X + 1, Y);

        /// <summary>상하좌우 4방향 인접 좌표 (보드 범위 검사는 하지 않는다).</summary>
        public IEnumerable<GridPos> Neighbors4()
        {
            yield return Up;
            yield return Down;
            yield return Left;
            yield return Right;
        }

        public bool Equals(GridPos other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPos other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);

        public static bool operator ==(GridPos a, GridPos b) => a.Equals(b);
        public static bool operator !=(GridPos a, GridPos b) => !a.Equals(b);

        public override string ToString() => $"({X},{Y})";
    }
}
