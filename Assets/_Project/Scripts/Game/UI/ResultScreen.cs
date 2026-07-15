using ChainRiposte.Core.Flow;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChainRiposte.Game.UI
{
    /// <summary>
    /// 승리/패배 결과 화면 — 퍼즐 패배(턴 소진/기믹)와 전투 결과 모두 여기로 수렴한다.
    /// 재시작은 씬 리로드 (컴포지션 루트가 전부 다시 조립되므로 상태 잔재가 없다).
    /// </summary>
    public sealed class ResultScreen : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;

        private GameObject _root;
        private Text _titleText;

        private void Awake()
        {
            BuildUi();
            _root.SetActive(false);
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
            _titleText.text = victory ? "STAGE CLEAR" : "DEFEAT";
            _titleText.color = victory ? new Color(0.95f, 0.83f, 0.35f) : new Color(0.85f, 0.2f, 0.25f);
            _root.SetActive(true);
        }

        private static void Restart() =>
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);

        private static void GoToMap() =>
            SceneManager.LoadScene("StageSelect");

        // ── UI 조립 ──

        private static Font BuiltinFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private void BuildUi()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20; // 항상 최상단
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            _root = new GameObject("Root");
            _root.transform.SetParent(transform, false);
            var rootRect = _root.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
            var dim = _root.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.72f); // 아래 화면을 어둡게 덮고 클릭도 차단

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(_root.transform, false);
            var titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = titleRect.anchorMax = titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0f, 160f);
            titleRect.sizeDelta = new Vector2(1000f, 240f);
            _titleText = titleGo.AddComponent<Text>();
            _titleText.font = BuiltinFont;
            _titleText.fontSize = 120;
            _titleText.fontStyle = FontStyle.Bold;
            _titleText.alignment = TextAnchor.MiddleCenter;
            _titleText.raycastTarget = false;

            CreateButton("RestartButton", new Vector2(0f, -120f), "RESTART", Restart);
            CreateButton("MapButton", new Vector2(0f, -320f), "MAP", GoToMap);
        }

        private void CreateButton(string objName, Vector2 pos, string labelText, UnityEngine.Events.UnityAction onClick)
        {
            var buttonGo = new GameObject(objName);
            buttonGo.transform.SetParent(_root.transform, false);
            var buttonRect = buttonGo.AddComponent<RectTransform>();
            buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = pos;
            buttonRect.sizeDelta = new Vector2(460f, 160f);
            var buttonImage = buttonGo.AddComponent<Image>();
            buttonImage.color = new Color(0.22f, 0.20f, 0.26f, 0.95f);
            var button = buttonGo.AddComponent<Button>();
            button.onClick.AddListener(onClick);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(buttonGo.transform, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var label = labelGo.AddComponent<Text>();
            label.font = BuiltinFont;
            label.fontSize = 56;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.92f, 0.90f, 0.85f);
            label.text = labelText;
            label.raycastTarget = false;
        }
    }
}
