using System.Collections;
using ChainRiposte.Core.Flow;
using ChainRiposte.Game.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChainRiposte.Game.UI
{
    /// <summary>
    /// 승리/패배 결과 화면.
    ///
    /// <list type="bullet">
    /// <item><b>승리</b>(보스 인살) — 다시 시작은 필요 없다. 짧은 텀을 두고 <b>지도로 자동 복귀</b>한다.</item>
    /// <item><b>마지막 고리</b> — 순서가 <b>인살 연출 → 엔딩 영상 → 스테이지 클리어 → 타이틀</b>이다.
    /// 클리어 문구가 영상보다 먼저 뜨면 판이 이미 끝난 것으로 읽혀서 영상이 부록처럼 보인다.</item>
    /// <item><b>패배</b> — 다시 시작 / 지도 두 버튼을 띄운다. 퍼즐 패배(턴 소진·기믹)와 전투 패배가 모두 여기로 온다.</item>
    /// </list>
    ///
    /// UI는 씬에 실물로 배치(TMP)하고 이 컴포넌트는 참조만 받는다. 재시작은 씬 리로드.
    /// </summary>
    public sealed class ResultScreen : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;

        [Header("씬 참조 (빌더가 자동 배선)")]
        [Tooltip("결과 화면 전체 루트 — 평소엔 꺼져 있다가 승/패 시 켜진다.")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text titleText;
        [Tooltip("패배에서만 보인다 — 승리는 지도로 자동 복귀하므로 숨긴다.")]
        [SerializeField] private Button restartButton;
        [SerializeField] private Button mapButton;

        [Header("승리 연출")]
        [Tooltip("승리 후 지도로 넘어가기까지의 텀(초).")]
        [SerializeField, Min(0f)] private float victoryToMapDelay = 1.6f;

        [Header("엔딩 (마지막 고리를 끊었을 때)")]
        [Tooltip("캐릭터별 엔딩 영상을 트는 자리. 비어 있어도 엔딩은 난다 — 영상만 없다.")]
        [SerializeField] private EndingVideoPlayer endingVideo;
        [Tooltip("인살 연출이 끝나고 영상이 시작되기까지의 숨 고르기(초). " +
                 "0이면 처형 직후 곧바로 영상이 붙어 숨 쉴 틈이 없다.")]
        [SerializeField, Min(0f)] private float endingVideoDelay = 0.8f;
        [Tooltip("엔딩 뒤에 타이틀로 돌아간다. 끄면 지도로 돌아간다(엔딩 뒤 이어서 놀게 하고 싶을 때).")]
        [SerializeField] private bool endingReturnsToTitle = true;

        private void Awake()
        {
            if (panelRoot == null || restartButton == null || mapButton == null)
            {
                Debug.LogError($"{nameof(ResultScreen)}: UI 참조가 비어 있습니다. " +
                    "Tools ▸ ChainRiposte ▸ Build Main Scene UI 를 실행하세요.", this);
                enabled = false;
                return;
            }

            restartButton.onClick.AddListener(Restart);
            mapButton.onClick.AddListener(GoToMap);
            panelRoot.SetActive(false);
            gameManager.Session.PhaseChanged += OnPhaseChanged;
        }

        private void OnDestroy()
        {
            if (gameManager != null && gameManager.Session != null)
                gameManager.Session.PhaseChanged -= OnPhaseChanged;
        }

        private void OnPhaseChanged(GamePhase previous, GamePhase next)
        {
            if (next == GamePhase.Victory)
                ShowVictory();
            else if (next == GamePhase.Defeat)
                ShowDefeat();
        }

        private void ShowVictory()
        {
            titleText.text = Loc.GetText("result.victory");
            titleText.color = new Color(0.95f, 0.83f, 0.35f);
            // 승리엔 버튼이 없다 — 잠깐 결과를 보여 준 뒤 지도로 넘어간다.
            restartButton.gameObject.SetActive(false);
            mapButton.gameObject.SetActive(false);

            // 마지막 고리를 끊었으면 지도가 아니라 엔딩으로 간다. '무엇이 마지막인가'는
            // 지도가 알려 준 것을 GameManager가 들고 있다 — 여기서 스테이지 이름을 알 필요가 없다.
            if (gameManager.IsFinalLink)
            {
                StartCoroutine(EndingRoutine());
                return;
            }

            panelRoot.SetActive(true);
            StartCoroutine(GoToMapAfterDelay());
        }

        /// <summary>
        /// 엔딩 — <b>인살 연출 → 영상 → 스테이지 클리어</b> 순서다(사용자 지정).
        ///
        /// <para>클리어 문구를 영상 <b>앞</b>에 띄우면 그 판이 이미 끝난 것으로 읽혀서, 뒤에 나오는 영상이
        /// 엔딩이 아니라 결과 화면에 붙은 부록처럼 보인다. 마지막 고리는 <b>영상이 이야기의 끝</b>이고
        /// 클리어 문구는 그 뒤의 정산이다.</para>
        ///
        /// <para>영상이 있으면 틀고, 없으면 곧바로 클리어 문구로 간다 —
        /// <b>영상이 없다고 엔딩 자체가 없어지면 안 된다.</b></para>
        /// </summary>
        private IEnumerator EndingRoutine()
        {
            // 인살 히트스톱이 timeScale을 떨어뜨려 둔 채로 올 수 있다. 아래는 전부 실시간이지만
            // 영상 소리·UI 애니가 스케일 시간을 타는 경우를 대비해 여기서 되돌린다.
            Time.timeScale = 1f;

            yield return new WaitForSecondsRealtime(endingVideoDelay);

            if (endingVideo != null)
                yield return endingVideo.Play(EndingVideoPlayer.ResolveClip());

            // 영상이 다 끝난 뒤에야 스테이지 클리어
            panelRoot.SetActive(true);
            yield return new WaitForSecondsRealtime(victoryToMapDelay);

            if (endingReturnsToTitle)
                Flow.SceneRouter.GoTitle();
            else
                GoToMap();
        }

        /// <summary>
        /// 왜 졌는지 같이 알려 준다. <b>패배 조건이 여럿이라 "패배" 한 줄로는 무엇을 고쳐야 할지 모른다</b> —
        /// 턴이 모자랐는지 맞아 죽었는지에 따라 다음 판의 플레이가 완전히 달라진다.
        ///
        /// <para>사유는 세션에서 따로 들고 오지 않고 <b>상태로 읽는다</b>. 체력이 0이면 맞아 죽은 것이고
        /// (퍼즐의 잡몹·폭탄이든 전투의 보스든), 아니면 턴이 다한 것뿐이다. 사유를 값으로 들고 다니면
        /// 새 패배 조건이 생길 때마다 전달 경로를 하나씩 늘려야 한다.</para>
        /// </summary>
        private void ShowDefeat()
        {
            bool killed = gameManager.Session.Health.Current <= 0;
            titleText.text = Loc.GetText("result.defeat")
                + "\n<size=55%>" + Loc.GetText(killed ? "result.defeat.hp" : "result.defeat.turns") + "</size>";
            titleText.color = new Color(0.85f, 0.2f, 0.25f);
            restartButton.gameObject.SetActive(true);
            mapButton.gameObject.SetActive(true);
            panelRoot.SetActive(true);
        }

        private IEnumerator GoToMapAfterDelay()
        {
            // 실시간으로 기다린다 — 혹시 인살 연출이 timeScale을 건드려도 텀이 늘어지지 않게.
            yield return new WaitForSecondsRealtime(victoryToMapDelay);
            GoToMap();
        }

        private static void Restart()
        {
            Time.timeScale = 1f; // 일시정지 중 죽었을 수 있으니 원복하고 리로드
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private static void GoToMap()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("StageSelect");
        }
    }
}
