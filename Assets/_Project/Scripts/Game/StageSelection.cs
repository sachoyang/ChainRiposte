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

        /// <summary>
        /// 고른 판이 <b>정렬된 지도의 마지막 고리</b>였나 — 클리어하면 엔딩이다.
        ///
        /// <para>"무엇이 최종 고리인가"를 Main 씬이 스스로 알 방법은 없다. 스테이지 목록은 지도의
        /// 노드 순서로만 존재하기 때문이다. 그래서 <b>목록이 실제로 있는 곳</b>(월드맵)에서 세어
        /// 여기에 실어 보낸다 — 스테이지를 추가하거나 재배치해도 엔딩 조건이 따라온다.
        /// 스테이지 이름을 코드에 적는 방식(<c>"Stage_2_3"</c>)은 그 순간 깨진다.</para>
        /// </summary>
        public static bool SelectedIsFinalLink;

        /// <summary>
        /// 둘은 <b>항상 같이</b> 정해진다 — 스테이지만 바꾸고 깃발을 그대로 두면
        /// 앞 판의 "마지막이었다"가 다음 판에 묻어 엉뚱한 곳에서 엔딩이 난다.
        /// </summary>
        public static void Select(StageDataSO stage, bool isFinalLink)
        {
            Selected = stage;
            SelectedIsFinalLink = isFinalLink;
        }
    }
}
