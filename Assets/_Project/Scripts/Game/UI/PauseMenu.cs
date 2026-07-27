using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChainRiposte.Game.UI
{
    /// <summary>
    /// 전투 씬(퍼즐 + 보스전)의 일시정지 / 설정. 모바일 관행대로 <b>화면 우상단</b>에 둔다.
    ///
    /// <para>퍼즐 카운트다운과 전투 채보가 모두 <c>Time.deltaTime</c>(스케일 시간)으로 도므로
    /// <c>Time.timeScale = 0</c> 하나로 게임이 멈춘다. UI 애니(그림자·페이드)는 unscaled라 계속 돈다.</para>
    ///
    /// <list type="bullet">
    /// <item><b>일시정지 버튼</b> — 토글. 멈추면 아이콘이 ▶로 바뀌고 딤이 입력을 막는다.</item>
    /// <item><b>설정 버튼</b> — 멈춘 뒤 <see cref="OptionsPanel"/>을 연다. 닫으면 재개.</item>
    /// <item><b>지도로 나가기</b> — 일시정지 패널에서. 확인 한 번 받고 나간다.</item>
    /// </list>
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        [Header("우상단 버튼")]
        [SerializeField] private Button pauseButton;
        [Tooltip("일시정지 버튼의 그림 — 멈추면 play 로 바뀐다")]
        [SerializeField] private Image pauseButtonIcon;
        [SerializeField] private Sprite pauseSprite;
        [SerializeField] private Sprite playSprite;
        [Tooltip("Sprite Swap 을 쓰는 버튼일 때의 눌림 그림. 비워도 된다(그때는 눌림 그림이 안 바뀐다).")]
        [SerializeField] private Sprite pausePressedSprite;
        [SerializeField] private Sprite playPressedSprite;
        [SerializeField] private Button settingsButton;

        [Header("일시정지 패널")]
        [Tooltip("멈췄을 때 입력을 막고 메뉴를 담는 딤. 평소엔 꺼져 있다.")]
        [SerializeField] private GameObject pausePanel;
        [Tooltip("패널 안의 계속하기(재개) 버튼. 없어도 우상단 토글로 재개된다.")]
        [SerializeField] private Button resumeButton;
        [Tooltip("지도로 나가기. 확인 패널을 거친다.")]
        [SerializeField] private Button quitButton;

        [Header("설정 / 확인")]
        [SerializeField] private GameObject optionsPanel;
        [Tooltip("지도로 나가기 확인 패널 (예/아니오). 없으면 바로 나간다.")]
        [SerializeField] private GameObject quitConfirmPanel;
        [SerializeField] private Button quitConfirmYes;
        [SerializeField] private Button quitConfirmNo;

        private bool _pausedByMenu;   // 우상단 토글/패널로 멈춤
        private bool _pausedByOptions; // 설정 창으로 멈춤

        private void Awake()
        {
            if (pauseButton != null) pauseButton.onClick.AddListener(TogglePause);
            if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
            if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
            if (quitButton != null) quitButton.onClick.AddListener(AskQuit);
            if (quitConfirmYes != null) quitConfirmYes.onClick.AddListener(QuitToMap);
            if (quitConfirmNo != null) quitConfirmNo.onClick.AddListener(() => SetActive(quitConfirmPanel, false));

            SetActive(pausePanel, false);
            SetActive(optionsPanel, false);
            SetActive(quitConfirmPanel, false);
            UpdateIcon();
        }

        // 씬을 떠날 때 멈춘 채로 넘어가면 다음 씬이 얼어붙는다 — 반드시 원복한다.
        private void OnDisable() => Time.timeScale = 1f;

        private void TogglePause()
        {
            if (_pausedByMenu)
                Resume();
            else
                Pause();
        }

        private void Pause()
        {
            _pausedByMenu = true;
            SetActive(pausePanel, true);
            ApplyTimeScale();
            UpdateIcon();
        }

        private void Resume()
        {
            _pausedByMenu = false;
            SetActive(pausePanel, false);
            SetActive(quitConfirmPanel, false);
            ApplyTimeScale();
            UpdateIcon();
        }

        private void OpenSettings()
        {
            _pausedByOptions = true;
            SetActive(optionsPanel, true);
            ApplyTimeScale();
        }

        /// <summary>
        /// 설정 창은 스스로 <c>SetActive(false)</c>로 닫힌다(닫기 버튼이 그렇게 배선됨).
        /// 그 순간을 잡아 재개한다 — OptionsPanel을 고치지 않고 여기서 감시한다.
        /// </summary>
        private void Update()
        {
            if (_pausedByOptions && (optionsPanel == null || !optionsPanel.activeSelf))
            {
                _pausedByOptions = false;
                ApplyTimeScale();
            }
        }

        private void AskQuit()
        {
            if (quitConfirmPanel != null)
                SetActive(quitConfirmPanel, true);
            else
                QuitToMap();
        }

        private void QuitToMap()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("StageSelect");
        }

        /// <summary>멈춤 이유가 하나라도 있으면 0, 없으면 1.</summary>
        private void ApplyTimeScale() => Time.timeScale = (_pausedByMenu || _pausedByOptions) ? 0f : 1f;

        private void UpdateIcon()
        {
            if (pauseButtonIcon == null)
                return;
            Sprite icon = _pausedByMenu ? playSprite : pauseSprite;
            if (icon != null)
                pauseButtonIcon.sprite = icon;

            // 눌림 그림도 같이 갈아야 한다 — 아이콘만 ▶로 바꾸면 누르는 동안 ⏸의 눌림 그림이 뜬다.
            Sprite pressed = _pausedByMenu ? playPressedSprite : pausePressedSprite;
            if (pauseButton == null || pressed == null)
                return;

            SpriteState state = pauseButton.spriteState;
            state.pressedSprite = pressed;
            pauseButton.spriteState = state;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
                target.SetActive(active);
        }
    }
}
