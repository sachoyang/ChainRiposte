using ChainRiposte.Game.Localization;
using ChainRiposte.Game.Progress;
using ChainRiposte.Game.UI;
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

        [Tooltip("첫 등반 튜토리얼 판(Stage_Tutorial). 꽂아 두면 새 게임 직후 지도를 건너뛰고 " +
                 "이 판으로 곧장 들어간다 — 한 번 끝내면 그 다음부터는 평소대로 지도로 간다.\n" +
                 "비워 두면 튜토리얼이 없던 것처럼 지도로 간다.")]
        [SerializeField] private Config.StageDataSO tutorialStage;

        [Header("확인 대화상자 (선택 — 비우면 확인 없이 바로 진행)")]
        [SerializeField] private GameObject confirmPanel;
        [SerializeField] private TMP_Text confirmText;
        [SerializeField] private Button confirmYesButton;
        [SerializeField] private Button confirmNoButton;

        [Header("캐릭터 선택 (비우거나 캐릭터가 하나뿐이면 건너뛴다)")]
        [SerializeField] private CharacterSelectPanel characterSelect;

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

            if (characterSelect != null)
            {
                characterSelect.Chosen += _ => StartNewGame();
                characterSelect.Cancelled += () => characterSelect.gameObject.SetActive(false);
            }

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
                AfterNewGameConfirmed();
                return;
            }

            if (confirmText != null)
                confirmText.text = Loc.GetText("title.newgame.confirm");

            confirmYesButton.onClick.RemoveAllListeners();
            confirmYesButton.onClick.AddListener(AfterNewGameConfirmed);
            SetActive(confirmPanel, true);
        }

        /// <summary>
        /// 진행도를 지우기로 한 뒤. 고를 캐릭터가 둘 이상이면 여기서 고르게 하고,
        /// 하나뿐이면 굳이 화면을 띄우지 않는다 (선택지 없는 선택 화면은 방해일 뿐이다).
        /// </summary>
        private void AfterNewGameConfirmed()
        {
            SetActive(confirmPanel, false);

            if (characterSelect != null && CharacterSelectPanel.IsNeeded)
            {
                characterSelect.Open();
                return;
            }

            StartNewGame();
        }

        private void StartNewGame()
        {
            ProgressService.ResetAll();
            // 고른 캐릭터의 사슬을 처음부터 — 이전 회차 빌드/소울을 물려받지 않게 (Docs/PROGRESSION.md)
            RunStateService.StartNewRun();

            // 처음이면 지도를 건너뛰고 튜토리얼 판으로 곧장 (Docs/TUTORIAL.md §7).
            // ResetAll 이 튜토리얼 기록도 지웠으므로 "새 게임 = 튜토리얼부터"가 규칙 하나로 유지된다.
            if (tutorialStage != null && tutorialStage.RunsFirstClimbTutorial
                && !Tutorial.TutorialService.HasSeen(Tutorial.TutorialDirector.TopicId))
            {
                // 지도를 안 거치므로 고리 깊이 0·최종 아님으로 들어간다 — 난이도가 부풀지 않는다.
                StageSelection.Select(tutorialStage, isFinalLink: false, linkDepth: 0, isBossFinale: false);
                SceneRouter.GoMain();
                return;
            }

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
