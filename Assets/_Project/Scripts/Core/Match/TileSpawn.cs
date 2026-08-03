using ChainRiposte.Core.Board;

namespace ChainRiposte.Core.Match
{
    /// <summary>리필로 새로 생성된 타일과 안착 위치. 뷰는 보드 상단 밖에서 떨어지는 연출로 재생한다.</summary>
    public sealed class TileSpawn
    {
        public Tile Tile { get; }
        public GridPos Position { get; }

        /// <summary>
        /// <b>갇힌 칸</b>에 그 자리에서 생겨난 타일인가 (<see cref="BoardRefiller.FillSealedPockets"/>).
        ///
        /// <para>위가 벽으로 막히고 대각선도 닫힌 칸은 <b>어떤 타일도 도달할 수 없다.</b>
        /// 그런 칸까지 채우려면 제자리에서 생겨나는 수밖에 없는데, 뷰가 이것을 평소 리필처럼
        /// 보드 위에서 떨어뜨리면 <b>벽을 뚫고 내려오는 것처럼</b> 보인다.
        /// 그래서 "떨어진 것"과 "생겨난 것"을 구분해 둔다 — 뷰는 이걸 보고 제자리에서 띄운다.</para>
        /// </summary>
        public bool Sealed { get; }

        public TileSpawn(Tile tile, GridPos position, bool sealedPocket = false)
        {
            Tile = tile;
            Position = position;
            Sealed = sealedPocket;
        }
    }
}
