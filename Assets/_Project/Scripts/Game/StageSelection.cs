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
        /// 고른 판이 사슬의 <b>몇 번째 고리</b>인가 (첫 판 = 0). 난이도 곡선의 입력이다
        /// (<c>Docs/PROGRESSION.md</c> §2.5) — 보스 에셋을 스테이지들이 돌려 쓰므로,
        /// 뒤로 갈수록 부풀리는 몫은 이 깊이가 정한다.
        ///
        /// <para>최종 고리 여부와 같은 이유로 <b>지도가 세어서 실어 보낸다</b> — 스테이지 목록은
        /// 노드 순서로만 존재하고, Main 씬은 자기가 몇 번째인지 알 방법이 없다.</para>
        /// </summary>
        public static int SelectedLinkDepth;

        /// <summary>
        /// 고른 판이 <b>그 보스를 마지막으로 만나는 판</b>인가 — 기억은 여기서만 떨어진다
        /// (<c>Docs/PROGRESSION.md</c> §2.2).
        ///
        /// <para>같은 보스가 여러 판에 걸쳐 나오는 것은 <b>상처를 입고 다시 나오는</b> 것이고,
        /// 마지막 판이 그 보스의 끝이다. 그때 비로소 기억을 삼킨다 — 첫 만남에 주면
        /// 뒤의 두 판은 "이미 다 가진 보스를 또 베는" 판이 된다.</para>
        ///
        /// <para>최종 고리·고리 깊이와 같은 이유로 <b>지도가 세어서 실어 보낸다</b> — 어느 판이
        /// 그 보스의 마지막인지는 노드 순서로만 알 수 있고, Main 씬은 그 목록을 갖고 있지 않다.</para>
        /// </summary>
        public static bool SelectedIsBossFinale;

        /// <summary>
        /// 넷은 <b>항상 같이</b> 정해진다 — 스테이지만 바꾸고 나머지를 그대로 두면
        /// 앞 판의 "마지막이었다"·"몇 번째였다"가 다음 판에 묻어 엉뚱한 곳에서 엔딩이 나거나
        /// 첫 판이 후반 난이도로 부푼다.
        /// </summary>
        public static void Select(StageDataSO stage, bool isFinalLink, int linkDepth, bool isBossFinale)
        {
            Selected = stage;
            SelectedIsFinalLink = isFinalLink;
            SelectedLinkDepth = linkDepth;
            SelectedIsBossFinale = isBossFinale;
        }

        /// <summary>
        /// 부팅 시 선택을 비운다. <b>도메인 리로드를 끈 환경</b>에서는 정적 필드가 플레이를 넘어
        /// 살아남으므로, 지난 플레이에서 고른 판이 남아 <b>Main 단독 실행이 지도를 거친 것처럼</b> 동작한다
        /// (그 판의 고리 깊이로 난이도가 부풀고, 최종 고리였다면 엔딩까지 난다).
        ///
        /// <para>다른 정적 서비스는 전부 이 훅을 갖고 있는데 여기만 없었다 —
        /// 정적 상태를 들면 초기화도 같이 든다.</para>
        /// </summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Selected = null;
            SelectedIsFinalLink = false;
            SelectedLinkDepth = 0;
            SelectedIsBossFinale = false;
        }
    }
}
