namespace ChainRiposte.Core.Flow
{
    /// <summary>게임의 최상위 진행 단계.</summary>
    public enum GamePhase
    {
        None = 0,

        /// <summary>3매치 퍼즐 + 영혼석 파밍.</summary>
        Puzzle,

        /// <summary>
        /// 보스 돌입 직전 — 파밍한 포인트를 분배할 시간. <b>시간 제한이 없다.</b>
        /// 퍼즐은 카운트다운으로 계속 쫓기므로, 성장을 결정하는 순간만큼은 쫓기지 않게 한다.
        /// </summary>
        Intermission,

        /// <summary>보스 난입 후 2버튼 패링 전투.</summary>
        Combat,

        /// <summary>체간 파괴 → 인살 성공, 스테이지 클리어.</summary>
        Victory,

        /// <summary>플레이어 HP 소진 또는 턴 소진.</summary>
        Defeat,
    }
}
