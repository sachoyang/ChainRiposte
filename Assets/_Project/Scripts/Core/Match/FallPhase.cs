using System.Collections.Generic;

namespace ChainRiposte.Core.Match
{
    /// <summary>
    /// 낙하 정착(Settle)의 한 웨이브: 직선 낙하 + 대각선 슬라이드 이동들, 그 뒤의 리필 스폰들.
    /// 뷰는 웨이브를 순서대로 재생해 '미끄러지듯' 채워지는 연출을 만든다.
    /// </summary>
    public sealed class FallPhase
    {
        public IReadOnlyList<TileMove> Moves { get; }
        public IReadOnlyList<TileSpawn> Spawns { get; }

        public FallPhase(IReadOnlyList<TileMove> moves, IReadOnlyList<TileSpawn> spawns)
        {
            Moves = moves;
            Spawns = spawns;
        }
    }
}
