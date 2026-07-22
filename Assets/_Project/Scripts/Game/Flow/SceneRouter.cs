using UnityEngine.SceneManagement;

namespace ChainRiposte.Game.Flow
{
    /// <summary>
    /// 씬 이름을 한곳에 모은 전환 창구. 흐름은 인트로 → 타이틀 → 스테이지선택 → 게임.
    /// 컨트롤러가 씬 이름 문자열을 직접 들고 있지 않게 해서 오타로 전환이 죽는 사고를 막는다.
    ///
    /// 각 씬은 여전히 <b>단독 실행이 가능해야 한다</b> — 앞 씬에서만 세팅되는 값에 의존하지 말 것
    /// (예: Main은 StageSelection이 비어 있으면 GameManager 인스펙터의 기본 스테이지를 쓴다).
    /// </summary>
    public static class SceneRouter
    {
        public const string Intro = "Intro";
        public const string Title = "Title";
        public const string StageSelect = "StageSelect";
        public const string Main = "Main";

        /// <summary>전환 페이드 길이(초). 연출 톤을 바꾸려면 여기만 만지면 된다.</summary>
        public static float FadeSeconds = 0.35f;

        public static void GoIntro() => Go(Intro);
        public static void GoTitle() => Go(Title);
        public static void GoStageSelect() => Go(StageSelect);
        public static void GoMain() => Go(Main);

        /// <summary>검게 덮었다가 씬을 바꾸고 다시 걷어낸다.</summary>
        public static void Go(string sceneName) => ScreenFader.Instance.LoadWithFade(sceneName, FadeSeconds);

        /// <summary>페이드 없이 즉시 전환 (디버그·에디터용).</summary>
        public static void GoImmediate(string sceneName) => SceneManager.LoadScene(sceneName);
    }
}
