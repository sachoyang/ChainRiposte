using ChainRiposte.Core.Board;

namespace ChainRiposte.Core.Match
{
    /// <summary>인접 매치로 벽이 받은 피해 기록.</summary>
    public sealed class WallHit
    {
        public GridPos Position { get; }
        public int Damage { get; }
        public bool Destroyed { get; }

        public WallHit(GridPos position, int damage, bool destroyed)
        {
            Position = position;
            Damage = damage;
            Destroyed = destroyed;
        }
    }
}
