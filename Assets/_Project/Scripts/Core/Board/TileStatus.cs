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
        public int EnrageCountdown;

        public bool IsEnraged => EnrageCountdown > 0;

        /// <summary>
        /// 이 타일에 <b>이미 기믹이 하나 걸려 있는가</b>.
        ///
        /// <para><b>타일 하나에는 기믹도 하나</b>가 규칙이다(사용자 결정). 겹치면 화면에서 무엇이 급한지
        /// 읽을 수 없고 — 사슬 위에 폭탄 뱃지가 얹히고 그 위에 붉은 틴트가 깔린다 — 해법도 서로 부딪힌다
        /// (사슬은 매치해도 안 사라지는데 폭탄은 매치로 없애야 한다).</para>
        ///
        /// <para>거는 쪽(각 기믹)이 이 값을 보고 후보에서 빼는 것으로 지킨다.</para>
        /// </summary>
        public bool HasGimmick => Chained || IsBomb || IsEnraged;
    }
}
