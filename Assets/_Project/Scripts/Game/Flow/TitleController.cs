using ChainRiposte.Game.Localization;
using ChainRiposte.Game.Progress;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChainRiposte.Game.Flow
{
    /// <summary>
    /// 타이틀 씬. 이어하기 / 새 게임 / 옵션 / 나가기.
    /// 세이브가 없으면 이어하기는 비활성이고, 새 게임은 세이브가 있을 때만 확인을 받는다.
    ///
    /// UI는 씬에 실물로 배치하고 여기서는 참조만 받는다
    /// (초기 배치는 <c>Tools ▸ ChainRiposte ▸ Build App Scenes</c>).
    /// </summary>
    public sealed class TitleController : MonoBehaviour
    {
        [Header("씬 참조 (빌더가 자동 배선)")]
        [SerializeField] private Button continueButton;
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button quitButton;
        [Tooltip("옵션 패널. 비워두면 옵션 버튼이 비활성화된다.")]
        [SerializeField] private GameObject optionsPanel;

        [Header("확인 대화상자 (선택 — 비우면 확인 없이 바로 진행)")]
        [SerializeField] private GameObject confirmPanel;
        [SerializeField] private TMP_Text confirmText;
        [SerializeField] private Button confirmYesButton;
        [SerializeField] private Button confirmNoButton;

        private void Awake()
        {
            if (continueButton == null || newGameButton == null)
            {
                Debug.LogError(
                    $"{nameof(TitleController)}: 버튼 참조가 비어 있습니다. " +
                    "Tools ▸ ChainRiposte ▸ Build App Scenes 를 실행하세요.", this);
                enabled = false;
                return;
            }

            continueButton.onClick.AddListener(Continue);
            newGameButton.onClick.AddListener(RequestNewGame);

            if (optionsButton != null)
            {
                optionsButton.onClick.AddListener(OpenOptions);
                optionsButton.interactable = optionsPanel != null;
            }

            if (quitButton != null)
                quitButton.onClick.AddListener(Quit);

            if (confirmNoButton != null)
                confirmNoButton.onClick.AddListener(() => SetActive(confirmPanel, false));

            SetActive(optionsPanel, false);
            SetActive(confirmPanel, false);
        }

        private void Start()
        {
            // 저장된 진행도가 하나도 없으면 이어할 것이 없다.
            continueButton.interactable = HasSave;
        }

        private static bool HasSave => ProgressService.Current.AttemptedStageIds.Count > 0;

        private static void Continue() => SceneRouter.GoStageSelect();

        private void RequestNewGame()
        {
            if (!HasSave || confirmPanel == null)
            {
                StartNewGame();
                return;
            }

            if (confirmText != null)
                confirmText.text = Loc.GetText("title.newgame.confirm");

            confirmYesButton.onClick.RemoveAllListeners();
            confirmYesButton.onClick.AddListener(StartNewGame);
            SetActive(confirmPanel, true);
        }

        private static void StartNewGame()
        {
            ProgressService.ResetAll();
            SceneRouter.GoStageSelect();
        }

        private void OpenOptions() => SetActive(optionsPanel, true);

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
                target.SetActive(active);
        }
    }
}
