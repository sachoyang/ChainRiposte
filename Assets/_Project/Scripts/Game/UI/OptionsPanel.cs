using System.Collections.Generic;
using ChainRiposte.Game.Localization;
using ChainRiposte.Game.Options;
using ChainRiposte.Game.Progress;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChainRiposte.Game.UI
{
    /// <summary>
    /// 옵션 패널 — 배경음/효과음 볼륨, 화면 방향, 언어, 진행도 초기화.
    ///
    /// UI는 씬에 실물로 배치하고 여기서는 참조만 받는다. 예외는 <b>언어 버튼</b>뿐 —
    /// 개수가 CSV 언어 컬럼 수로 정해지므로 씬의 템플릿 버튼을 복제해 만든다
    /// (퍼즐 보드 타일과 같은 규칙: 데이터로 개수가 정해지는 것만 코드 생성).
    /// </summary>
    public sealed class OptionsPanel : MonoBehaviour
    {
        [Header("씬 참조 (빌더가 자동 배선)")]
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;
        [Tooltip("자동 / 세로 고정 / 가로 고정 순서로 3개")]
        [SerializeField] private Button[] orientationButtons = System.Array.Empty<Button>();
        [Tooltip("언어 버튼이 복제되어 붙을 부모")]
        [SerializeField] private Transform languageButtonParent;
        [Tooltip("복제 원본. 시작할 때 꺼진다.")]
        [SerializeField] private Button languageButtonTemplate;
        [SerializeField] private Button resetProgressButton;
        [SerializeField] private Button closeButton;

        [Header("확인 대화상자")]
        [SerializeField] private GameObject confirmPanel;
        [SerializeField] private TMP_Text confirmText;
        [SerializeField] private Button confirmYesButton;
        [SerializeField] private Button confirmNoButton;

        [Header("선택 표시 색")]
        [SerializeField] private Color selectedColor = new(0.55f, 0.16f, 0.18f, 1f);
        [SerializeField] private Color unselectedColor = new(0.20f, 0.18f, 0.24f, 1f);

        private readonly List<(Button button, SystemLanguage language)> _languageButtons = new();

        private void Awake()
        {
            if (bgmSlider == null || sfxSlider == null || closeButton == null)
            {
                Debug.LogError(
                    $"{nameof(OptionsPanel)}: UI 참조가 비어 있습니다. " +
                    "Tools ▸ ChainRiposte ▸ Build App Scenes 를 실행하세요.", this);
                enabled = false;
                return;
            }

            bgmSlider.onValueChanged.AddListener(value => GameOptions.BgmVolume = value);
            sfxSlider.onValueChanged.AddListener(value => GameOptions.SfxVolume = value);
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));

            for (int i = 0; i < orientationButtons.Length; i++)
            {
                var mode = (OrientationMode)i;
                if (orientationButtons[i] != null)
                    orientationButtons[i].onClick.AddListener(() => GameOptions.Orientation = mode);
            }

            if (resetProgressButton != null)
                resetProgressButton.onClick.AddListener(RequestResetProgress);
            if (confirmNoButton != null)
                confirmNoButton.onClick.AddListener(() => SetActive(confirmPanel, false));

            BuildLanguageButtons();
            SetActive(confirmPanel, false);
        }

        private void OnEnable()
        {
            GameOptions.Changed += Refresh;
            Loc.LanguageChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            GameOptions.Changed -= Refresh;
            Loc.LanguageChanged -= Refresh;
        }

        /// <summary>CSV의 언어 컬럼 수만큼 버튼을 만든다 — 시트에 컬럼을 늘리면 여기도 자동으로 늘어난다.</summary>
        private void BuildLanguageButtons()
        {
            if (languageButtonTemplate == null || languageButtonParent == null)
                return;

            languageButtonTemplate.gameObject.SetActive(false);

            foreach (SystemLanguage language in Loc.SupportedLanguages)
            {
                Button button = Instantiate(languageButtonTemplate, languageButtonParent);
                button.gameObject.name = $"Language_{language}";
                button.gameObject.SetActive(true);

                SystemLanguage captured = language;
                button.onClick.AddListener(() => Loc.Language = captured);
                _languageButtons.Add((button, language));
            }
        }

        private void RequestResetProgress()
        {
            if (confirmPanel == null)
            {
                ProgressService.ResetAll();
                return;
            }

            // 위와 같은 이유 — 프리팹의 LocalizedText 가 켜질 때 덮어쓰므로 키로 갈아 끼운다.
            LocalizedText.SetKey(confirmText, "options.reset.confirm");

            confirmYesButton.onClick.RemoveAllListeners();
            confirmYesButton.onClick.AddListener(() =>
            {
                ProgressService.ResetAll();
                SetActive(confirmPanel, false);
            });

            SetActive(confirmPanel, true);
        }

        private void Refresh()
        {
            // 슬라이더를 코드로 되돌릴 때 onValueChanged가 다시 돌지 않게 한다 (무한 왕복 방지)
            bgmSlider.SetValueWithoutNotify(GameOptions.BgmVolume);
            sfxSlider.SetValueWithoutNotify(GameOptions.SfxVolume);

            for (int i = 0; i < orientationButtons.Length; i++)
                Tint(orientationButtons[i], (int)GameOptions.Orientation == i);

            foreach ((Button button, SystemLanguage language) in _languageButtons)
            {
                Tint(button, language == Loc.Language);

                // 버튼 라벨은 언어별 키로 — 없으면 enum 이름이 그대로 나온다
                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null)
                    label.text = Loc.GetText($"language.{language}");
            }
        }

        private void Tint(Button button, bool selected)
        {
            if (button == null)
                return;

            var image = button.GetComponent<Image>();
            if (image != null)
                image.color = selected ? selectedColor : unselectedColor;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
                target.SetActive(active);
        }
    }
}
