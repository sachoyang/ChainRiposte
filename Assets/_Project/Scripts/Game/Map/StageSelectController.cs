using System.Collections;
using ChainRiposte.Core.Stage;
using ChainRiposte.Game.Config;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChainRiposte.Game.Map
{
    /// <summary>
    /// NSMB식 월드맵 (GDD §9). 노드를 클릭하면 캐릭터가 경로를 따라 자동 이동하고,
    /// 도착하면 하단 패널에 스테이지 정보 + START를 띄운다.
    /// 맵/UI 전부 코드 조립 — 에셋 단계에서 교체 예정. 진행도 잠금은 추후 (지금은 전부 선택 가능).
    /// </summary>
    public sealed class StageSelectController : MonoBehaviour
    {
        [Tooltip("경로 순서대로 (1-1 → 2-3). 앞 3개=월드1, 뒤 3개=월드2")]
        [SerializeField] private StageDataSO[] stages = System.Array.Empty<StageDataSO>();
        [SerializeField] private CameraFit2D cameraFit;
        [SerializeField, Min(0.5f)] private float moveSpeed = 6f;

        [Header("월드 색 (배경/노드)")]
        [SerializeField] private Color world1Color = new(0.22f, 0.34f, 0.24f);
        [SerializeField] private Color world2Color = new(0.30f, 0.20f, 0.36f);

        /// <summary>S자 경로 — 아래(월드1)에서 위(월드2)로.</summary>
        private static readonly Vector2[] NodePositions =
        {
            new(-3.0f, -3.2f), new(-0.3f, -2.6f), new(2.6f, -2.0f),  // 월드 1
            new(2.6f, 0.4f), new(-0.3f, 1.2f), new(-3.0f, 2.0f),     // 월드 2
        };

        private readonly Transform[] _nodes = new Transform[NodePositions.Length];
        private StageConfig[] _configs;
        private Transform _character;
        private Camera _camera;
        private int _currentIndex;
        private bool _moving;

        private GameObject _panel;
        private Text _titleText;
        private Text _infoText;

        private void Awake()
        {
            _camera = Camera.main;
            _configs = new StageConfig[stages.Length];
            BuildMap();
            BuildUi();
        }

        private void Start()
        {
            cameraFit.FitTo(new Bounds(new Vector3(0f, -0.5f, 0f), new Vector3(9f, 9f, 0f)));
            ShowInfo(_currentIndex);
        }

        private void Update()
        {
            if (_moving)
                return;

            Pointer pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame)
                return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            Vector3 world = _camera.ScreenToWorldPoint(pointer.position.ReadValue());
            int nearest = -1;
            float best = 0.8f; // 클릭 인정 반경
            for (int i = 0; i < _nodes.Length && i < stages.Length; i++)
            {
                float dist = Vector2.Distance(world, _nodes[i].position);
                if (dist < best)
                {
                    best = dist;
                    nearest = i;
                }
            }

            if (nearest >= 0 && nearest != _currentIndex)
                StartCoroutine(MoveRoutine(nearest));
        }

        /// <summary>노드를 하나씩 거쳐 이동 — 경로를 따라 걷는 NSMB 느낌.</summary>
        private IEnumerator MoveRoutine(int target)
        {
            _moving = true;
            _panel.SetActive(false);

            int step = target > _currentIndex ? 1 : -1;
            while (_currentIndex != target)
            {
                int next = _currentIndex + step;
                Vector3 destination = NodeWorld(next);
                while ((_character.position - destination).sqrMagnitude > 0.0004f)
                {
                    _character.position = Vector3.MoveTowards(
                        _character.position, destination, moveSpeed * Time.deltaTime);
                    yield return null;
                }
                _currentIndex = next;
            }

            _moving = false;
            ShowInfo(target);
        }

        private void ShowInfo(int index)
        {
            StageConfig config = _configs[index] ??= stages[index].ToConfig();
            int width = config.ActiveMask.GetLength(0);
            int height = config.ActiveMask.GetLength(1);

            _titleText.text = $"STAGE {DisplayName(index)}";
            _infoText.text =
                $"WORLD {index / 3 + 1}   BOARD {width}x{height}   TURNS {config.TurnLimit}\n" +
                $"BOSS  {config.Boss?.Name ?? "???"}";
            _panel.SetActive(true);
        }

        private void StartStage()
        {
            StageSelection.Selected = stages[_currentIndex];
            SceneManager.LoadScene("Main");
        }

        private static string DisplayName(int index) => $"{index / 3 + 1}-{index % 3 + 1}";

        private Vector3 NodeWorld(int index) =>
            new(NodePositions[index].x, NodePositions[index].y, 0f);

        // ── 맵 조립 (월드 배경 2장 + 경로 + 노드 + 캐릭터) ──

        private void BuildMap()
        {
            CreateSprite("World1Bg", new Vector3(0f, -2.6f, 1f), new Vector3(10f, 4.4f, 1f), world1Color * 0.5f, 0);
            CreateSprite("World2Bg", new Vector3(0f, 1.4f, 1f), new Vector3(10f, 4.4f, 1f), world2Color * 0.5f, 0);

            for (int i = 0; i + 1 < NodePositions.Length; i++)
                CreatePathSegment(NodeWorld(i), NodeWorld(i + 1));

            for (int i = 0; i < NodePositions.Length; i++)
            {
                Color color = i < 3 ? world1Color : world2Color;
                _nodes[i] = CreateSprite($"Node_{DisplayName(i)}", NodeWorld(i), Vector3.one * 0.8f, color * 1.8f, 2).transform;
                CreateLabel(_nodes[i], DisplayName(i));
            }

            _character = CreateSprite("Character", NodeWorld(0) + Vector3.up * 0.7f,
                new Vector3(0.5f, 0.6f, 1f), new Color(0.92f, 0.88f, 0.75f), 3).transform;
        }

        private SpriteRenderer CreateSprite(string objName, Vector3 position, Vector3 scale, Color color, int sortingOrder)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            go.transform.localScale = scale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = PlaceholderSprite.Square;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private void CreatePathSegment(Vector3 a, Vector3 b)
        {
            Vector3 delta = b - a;
            SpriteRenderer segment = CreateSprite("Path", (a + b) * 0.5f,
                new Vector3(delta.magnitude, 0.12f, 1f), new Color(0.75f, 0.70f, 0.58f, 0.85f), 1);
            segment.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private static void CreateLabel(Transform node, string label)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(node, false);
            go.transform.localPosition = new Vector3(0f, -1.1f, 0f);
            go.transform.localScale = Vector3.one * 0.35f; // 부모 스케일 보정 포함
            var mesh = go.AddComponent<TextMesh>();
            mesh.text = label;
            mesh.fontSize = 48;
            mesh.characterSize = 0.1f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.color = new Color(0.92f, 0.90f, 0.85f);
        }

        // ── 정보 패널 (하단 고정 — 세로/가로 공용) ──

        private static Font BuiltinFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private void BuildUi()
        {
            var canvasGo = new GameObject("MapCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            _panel = new GameObject("InfoPanel");
            _panel.transform.SetParent(canvasGo.transform, false);
            var panelRect = _panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(0f, 360f);
            var panelImage = _panel.AddComponent<Image>();
            panelImage.color = new Color(0.10f, 0.09f, 0.13f, 0.92f);

            _titleText = CreatePanelText("Title", new Vector2(50f, -30f), 64, TextAnchor.UpperLeft);
            _titleText.fontStyle = FontStyle.Bold;
            _infoText = CreatePanelText("Info", new Vector2(50f, -120f), 42, TextAnchor.UpperLeft);

            var buttonGo = new GameObject("StartButton");
            buttonGo.transform.SetParent(_panel.transform, false);
            var buttonRect = buttonGo.AddComponent<RectTransform>();
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(1f, 0.5f);
            buttonRect.pivot = new Vector2(1f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(-50f, 0f);
            buttonRect.sizeDelta = new Vector2(300f, 200f);
            var buttonImage = buttonGo.AddComponent<Image>();
            buttonImage.color = new Color(0.55f, 0.16f, 0.18f, 1f);
            var button = buttonGo.AddComponent<Button>();
            button.onClick.AddListener(StartStage);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(buttonGo.transform, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var label = labelGo.AddComponent<Text>();
            label.font = BuiltinFont;
            label.fontSize = 56;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.92f, 0.90f, 0.85f);
            label.text = "START";
            label.raycastTarget = false;
        }

        private Text CreatePanelText(string objName, Vector2 pos, int size, TextAnchor align)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(_panel.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(700f, 160f);
            var text = go.AddComponent<Text>();
            text.font = BuiltinFont;
            text.fontSize = size;
            text.alignment = align;
            text.color = new Color(0.92f, 0.90f, 0.85f);
            text.raycastTarget = false;
            return text;
        }
    }
}
