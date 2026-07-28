namespace ChainRiposte.Core.Board
{
    /// <summary>
    /// 타일 인스턴스에 걸린 기믹 상태 (GDD §3.6). 기믹이 꺼진 스테이지에서는 항상 기본값이라
    /// 보드/중력/매치 알고리즘은 기믹 존재 여부를 몰라도 된다.
    /// </summary>
    public sealed class TileStatus
    {
        /// <summary>사슬 결박 — 스왑/낙하 불가. 매치 또는 인접 매치로 해제된다.</summary>
        public bool Chained;

        /// <summary>시한폭탄 남은 턴. 0이면 폭탄이 아니다.</summary>
        public int BombTurnsRemaining;

        public bool IsBomb => BombTurnsRemaining > 0;

        /// <summary>
        /// 성난 몬스터 — 공격까지 남은 턴. 0이면 평범한 타일이다.
        /// 폭탄과 달리 <b>때린 뒤에도 사라지지 않고 재장전</b>한다. 없애는 방법은 매치뿐이다.
        /// </summary>
        public int EnrageTurnsRemaining;

        public bool IsEnraged => EnrageTurnsRemaining > 0;
    }
}
