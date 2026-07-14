using System;
using System.Collections.Generic;

namespace ChainRiposte.Core.Board
{
    /// <summary>
    /// 그리드 마스킹이 적용된 퍼즐 보드.
    /// Inactive 셀에는 타일이 스폰되지도, 낙하하지도 않는다 — 비정형 보드(하트/해골 등)의 기반.
    /// 매치/중력 알고리즘은 이 클래스가 아닌 Match 모듈(3단계)이 담당한다.
    /// </summary>
    public sealed class BoardGrid
    {
        private readonly bool[,] _active; // [x, y]
        private readonly Tile[,] _tiles;  // [x, y], 빈 셀은 null

        public int Width { get; }
        public int Height { get; }

        /// <param name="activeMask">[x, y] true=Active. y=0이 바닥.</param>
        public BoardGrid(bool[,] activeMask)
        {
            if (activeMask == null)
                throw new ArgumentNullException(nameof(activeMask));

            Width = activeMask.GetLength(0);
            Height = activeMask.GetLength(1);
            if (Width == 0 || Height == 0)
                throw new ArgumentException("보드 크기는 1x1 이상이어야 합니다.", nameof(activeMask));

            _active = (bool[,])activeMask.Clone();
            _tiles = new Tile[Width, Height];
        }

        public bool IsInside(GridPos pos) =>
            pos.X >= 0 && pos.X < Width && pos.Y >= 0 && pos.Y < Height;

        /// <summary>보드 안이면서 마스킹상 활성인 셀인가.</summary>
        public bool IsActive(GridPos pos) => IsInside(pos) && _active[pos.X, pos.Y];

        public bool IsOccupied(GridPos pos) => IsActive(pos) && _tiles[pos.X, pos.Y] != null;

        /// <summary>비활성 셀이거나 비어 있으면 null.</summary>
        public Tile GetTile(GridPos pos) => IsActive(pos) ? _tiles[pos.X, pos.Y] : null;

        public void PlaceTile(GridPos pos, Tile tile)
        {
            if (tile == null)
                throw new ArgumentNullException(nameof(tile));
            if (!IsActive(pos))
                throw new InvalidOperationException($"비활성/범위 밖 셀에 타일 배치 불가: {pos}");
            if (_tiles[pos.X, pos.Y] != null)
                throw new InvalidOperationException($"이미 타일이 있는 셀: {pos} ({_tiles[pos.X, pos.Y]})");

            _tiles[pos.X, pos.Y] = tile;
        }

        /// <summary>셀을 비우고 제거된 타일을 반환한다. 빈 셀이면 null.</summary>
        public Tile RemoveTile(GridPos pos)
        {
            if (!IsActive(pos))
                throw new InvalidOperationException($"비활성/범위 밖 셀: {pos}");

            Tile removed = _tiles[pos.X, pos.Y];
            _tiles[pos.X, pos.Y] = null;
            return removed;
        }

        /// <summary>활성 셀 전체를 아래 행부터 순회한다.</summary>
        public IEnumerable<GridPos> ActivePositions()
        {
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (_active[x, y])
                        yield return new GridPos(x, y);
        }

        /// <summary>해당 좌표의 활성 인접 셀(상하좌우)만 반환한다.</summary>
        public IEnumerable<GridPos> ActiveNeighbors4(GridPos pos)
        {
            foreach (GridPos neighbor in pos.Neighbors4())
                if (IsActive(neighbor))
                    yield return neighbor;
        }

        /// <summary>
        /// 해당 열의 최하단 활성 셀 — 보스 타일의 '바닥 도달' 판정 기준.
        /// 열 전체가 비활성이면 null.
        /// </summary>
        public GridPos? BottomActiveCell(int column)
        {
            if (column < 0 || column >= Width)
                throw new ArgumentOutOfRangeException(nameof(column));

            for (int y = 0; y < Height; y++)
                if (_active[column, y])
                    return new GridPos(column, y);

            return null;
        }
    }
}
