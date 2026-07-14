using ChainRiposte.Core.Board;

namespace ChainRiposte.Core.Intrusion
{
    /// <summary>
    /// 보스 타일 하나의 듀얼 카운트다운 (GDD §4.2).
    /// 실시간 초와 잔여 턴이 독립적으로 줄어들며, 어느 쪽이든 먼저 0이 되면 기습 돌입.
    /// </summary>
    public sealed class BossCountdown
    {
        public Tile Tile { get; }
        public float SecondsRemaining { get; internal set; }
        public int TurnsRemaining { get; internal set; }

        public bool Expired => SecondsRemaining <= 0f || TurnsRemaining <= 0;

        public BossCountdown(Tile tile, float seconds, int turns)
        {
            Tile = tile;
            SecondsRemaining = seconds;
            TurnsRemaining = turns;
        }
    }
}
