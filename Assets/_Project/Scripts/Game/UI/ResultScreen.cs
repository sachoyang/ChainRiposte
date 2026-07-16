using ChainRiposte.Core.Flow;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChainRiposte.Game.UI
{
    /// <summary>
    /// 승리/패배 결과 화면 — 퍼즐 패배(턴 소진/기믹)와 전투 결과 모두 여기로 수렴한다.
    /// UI는 씬에 실물로 배치(TMP)하고 이 컴포넌트는 참조만 받는다. 재시작은 씬 리로드.
    /// 초기 레이아웃은 <c>Tools ▸ ChainRiposte ▸ Build Main Scene UI</c>로 생성 후 씬에서 편집.
    /// </summary>
    public sealed class ResultScreen : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;

        [Header("씬 참조 (빌더가 자동 배선)")]
        [Tooltip("결과 화면 전체 루트 — 평소엔 꺼져 있다가 승/패 시 켜진다.")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button mapButton;

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
            if (next != GamePhase.Victory && next != GamePhase.Defeat)
                return;

            bool victory = next == GamePhase.Victory;
            titleText.text = victory ? "STAGE CLEAR" : "DEFEAT";
            titleText.color = victory ? new Color(0.95f, 0.83f, 0.35f) : new Color(0.85f, 0.2f, 0.25f);
            panelRoot.SetActive(true);
        }

        private static void Restart() =>
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);

        private static void GoToMap() =>
            SceneManager.LoadScene("StageSelect");
    }
}
