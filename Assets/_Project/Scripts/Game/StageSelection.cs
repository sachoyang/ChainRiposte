using ChainRiposte.Game.Config;

namespace ChainRiposte.Game
{
    /// <summary>
    /// 월드맵에서 고른 스테이지를 Main 씬으로 전달하는 홀더.
    /// null이면 GameManager는 인스펙터의 기본 스테이지를 쓴다 (Main 씬 단독 실행 지원).
    /// </summary>
    public static class StageSelection
    {
        public static StageDataSO Selected;
    }
}
