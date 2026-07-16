using System.Collections;
using ChainRiposte.Core.Stage;
using ChainRiposte.Game.Config;
using TMPro;
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
    ///
    /// 비주얼(노드/배경/캐릭터/패널)은 전부 <b>씬에 실물로 배치</b>하고 인스펙터로 참조만 받는다.
    /// 이 컴포넌트는 "행동"(클릭 판정·이동·정보 표시)만 담당한다.
    /// → 초기 레이아웃은 <c>Tools ▸ ChainRiposte ▸ Build StageSelect Layout</c>로 한 번 생성한 뒤
    ///   씬 뷰에서 자유롭게 드래그·교체한다.
    /// </summary>
    public sealed class StageSelectController : MonoBehaviour
    {
        [Header("씬 참조 — 빌더가 자동으로 채우거나 직접 드래그")]
        [Tooltip("경로 순서대로 (1-1 → 2-3). 각 노드의 위치를 그대로 경로로 사용한다.")]
        [SerializeField] private MapNode[] nodes = System.Array.Empty<MapNode>();
        [SerializeField] private Transform character;
        [SerializeField] private CameraFit2D cameraFit;
        [Tooltip("선택 사항 — 있으면 노드들을 잇는 경로선을 자동으로 채운다.")]
        [SerializeField] private LineRenderer pathLine;

        [Header("정보 패널 (Canvas)")]
        [SerializeField] private GameObject infoPanel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text infoText;
        [SerializeField] private Button startButton;

        [Header("동작 값")]
        [SerializeField, Min(0.5f)] private float moveSpeed = 6f;
        [Tooltip("캐릭터가 노드보다 살짝 위에 서도록 하는 오프셋")]
        [SerializeField] private Vector3 characterOffset = new(0f, 0.7f, 0f);
        [Tooltip("클릭을 노드로 인정하는 반경 (월드 유닛)")]
        [SerializeField, Min(0.1f)] private float clickRadius = 0.8f;
        [Tooltip("카메라가 노드 전체를 담을 때 가장자리 여백")]
        [SerializeField, Min(0f)] private float cameraPadding = 1.5f;

        private StageConfig[] _configs;
        private Camera _camera;
        private int _currentIndex;
        private bool _moving;

        private void Awake()
        {
            _camera = Camera.main;
            _configs = new StageConfig[nodes.Length];

            if (nodes.Length == 0 || character == null)
            {
                Debug.LogError(
                    $"{nameof(StageSelectController)}: 노드/캐릭터 참조가 비어 있습니다. " +
                    "Tools ▸ ChainRiposte ▸ Build StageSelect Layout 을 실행하세요.", this);
                enabled = false;
                return;
            }

            if (startButton != null)
                startButton.onClick.AddListener(StartStage);

            RefreshPathLine();
            character.position = NodeWorld(0) + characterOffset;
        }

        private void Start()
        {
            FitCameraToNodes();
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
            float best = clickRadius;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] == null)
                    continue;
                float dist = Vector2.Distance(world, nodes[i].Position);
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
            if (infoPanel != null)
                infoPanel.SetActive(false);

            int step = target > _currentIndex ? 1 : -1;
            while (_currentIndex != target)
            {
                int next = _currentIndex + step;
                Vector3 destination = NodeWorld(next) + characterOffset;
                while ((character.position - destination).sqrMagnitude > 0.0004f)
                {
                    character.position = Vector3.MoveTowards(
                        character.position, destination, moveSpeed * Time.deltaTime);
                    yield return null;
                }
                _currentIndex = next;
            }

            _moving = false;
            ShowInfo(target);
        }

        private void ShowInfo(int index)
        {
            StageDataSO stage = nodes[index].Stage;
            if (stage == null)
            {
                Debug.LogWarning($"{nameof(StageSelectController)}: 노드 {index}에 스테이지가 지정되지 않았습니다.", nodes[index]);
                return;
            }

            StageConfig config = _configs[index] ??= stage.ToConfig();
            int width = config.ActiveMask.GetLength(0);
            int height = config.ActiveMask.GetLength(1);

            if (titleText != null)
                titleText.text = $"STAGE {DisplayName(index)}";
            if (infoText != null)
                infoText.text =
                    $"WORLD {index / 3 + 1}   BOARD {width}x{height}   TURNS {config.TurnLimit}\n" +
                    $"BOSS  {config.Boss?.Name ?? "???"}";
            if (infoPanel != null)
                infoPanel.SetActive(true);
        }

        private void StartStage()
        {
            StageSelection.Selected = nodes[_currentIndex].Stage;
            SceneManager.LoadScene("Main");
        }

        private static string DisplayName(int index) => $"{index / 3 + 1}-{index % 3 + 1}";

        private Vector3 NodeWorld(int index) => nodes[index].Position;

        /// <summary>경로선(있을 때)을 노드 위치로 갱신 — 노드를 옮기면 선도 따라온다.</summary>
        private void RefreshPathLine()
        {
            if (pathLine == null)
                return;
            pathLine.positionCount = nodes.Length;
            for (int i = 0; i < nodes.Length; i++)
                pathLine.SetPosition(i, nodes[i].Position);
        }

        private void FitCameraToNodes()
        {
            if (cameraFit == null || nodes.Length == 0)
                return;

            var bounds = new Bounds(nodes[0].Position, Vector3.zero);
            for (int i = 1; i < nodes.Length; i++)
                bounds.Encapsulate(nodes[i].Position);
            bounds.Expand(cameraPadding * 2f);
            cameraFit.FitTo(bounds);
        }
    }
}
