namespace ChainRiposte.Core.Flow
{
    /// <summary>게임의 최상위 진행 단계.</summary>
    public enum GamePhase
    {
        None = 0,

        /// <summary>3매치 퍼즐 + 영혼석 파밍.</summary>
        Puzzle,

        /// <summary>보스 난입 후 2버튼 패링 전투.</summary>
        Combat,

        /// <summary>체간 파괴 → 인살 성공, 스테이지 클리어.</summary>
        Victory,

        /// <summary>플레이어 HP 소진 또는 턴 소진.</summary>
        Defeat,
    }
}
