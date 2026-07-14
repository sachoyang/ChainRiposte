using ChainRiposte.Core.Board;

namespace ChainRiposte.Core.Match
{
    /// <summary>리필로 새로 생성된 타일과 안착 위치. 뷰는 보드 상단 밖에서 떨어지는 연출로 재생한다.</summary>
    public sealed class TileSpawn
    {
        public Tile Tile { get; }
        public GridPos Position { get; }

        public TileSpawn(Tile tile, GridPos position)
        {
            Tile = tile;
            Position = position;
        }
    }
}
