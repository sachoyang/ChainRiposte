using ChainRiposte.Core.Board;

namespace ChainRiposte.Core.Match
{
    /// <summary>중력 낙하로 인한 타일 이동 기록. 뷰의 낙하 애니메이션 재생용.</summary>
    public sealed class TileMove
    {
        public Tile Tile { get; }
        public GridPos From { get; }
        public GridPos To { get; }

        public TileMove(Tile tile, GridPos from, GridPos to)
        {
            Tile = tile;
            From = from;
            To = to;
        }
    }
}
