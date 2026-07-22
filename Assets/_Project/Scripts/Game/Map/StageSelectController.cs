using System.Collections;
using System.Collections.Generic;
using ChainRiposte.Core.Progress;
using ChainRiposte.Core.Stage;
using ChainRiposte.Game.Audio;
using ChainRiposte.Game.Config;
using ChainRiposte.Game.Localization;
using ChainRiposte.Game.Progress;
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
        [Tooltip("보스 초상. 아직 안 가본 스테이지에서는 검은 실루엣으로 칠한다. 비워도 동작한다.")]
        [SerializeField] private Image bossPortrait;
        [Tooltip("정보가 공개되지 않은 스테이지의 초상 색 (검은 그림자)")]
        [SerializeField] private Color silhouetteTint = new(0f, 0f, 0f, 0.85f);

        [Header("동작 값")]
        [SerializeField, Min(0.5f)] private float moveSpeed = 6f;
        [Tooltip("캐릭터가 노드보다 살짝 위에 서도록 하는 오프셋")]
        [SerializeField] private Vector3 characterOffset = new(0f, 0.7f, 0f);
        [Tooltip("클릭을 노드로 인정하는 반경 (월드 유닛)")]
        [SerializeField, Min(0.1f)] private float clickRadius = 0.8f;
        [Tooltip("카메라가 노드 전체를 담을 때 가장자리 여백")]
        [SerializeField, Min(0f)] private float cameraPadding = 1.5f;

        [Header("막힘 연출 (잠긴 노드)")]
        [Tooltip("막힌 노드 쪽으로 나아가는 비율. 0.35면 노드 간 거리의 35%까지 갔다가 되돌아온다.")]
        [SerializeField, Range(0.05f, 0.9f)] private float blockedBumpRatio = 0.35f;
        [Tooltip("부딪히는 횟수")]
        [SerializeField, Range(1, 3)] private int blockedBumpCount = 2;
        [Tooltip("막혔을 때 효과음 (비워도 동작)")]
        [SerializeField] private AudioClip blockedSfx;

        private StageConfig[] _configs;
        private string[] _stageIds;
        private StageProgress _progress;
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

            // 진행도(GDD §9.2) — 아직 못 깬 스테이지는 잠기고, 캐릭터는 가장 앞선 열린 노드에서 시작한다.
            _progress = ProgressService.Current;
            _stageIds = new string[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
                _stageIds[i] = nodes[i] != null && nodes[i].Stage != null ? nodes[i].Stage.StageId : string.Empty;

            RefreshNodeStates();
            _currentIndex = _progress.HighestUnlockedIndex(_stageIds);

            RefreshPathLine();
            character.position = NodeWorld(_currentIndex) + characterOffset;
        }

        /// <summary>진행도에 맞춰 노드의 잠금/클리어 표시를 갱신한다.</summary>
        private void RefreshNodeStates()
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] == null)
                    continue;
                nodes[i].ApplyState(_progress.IsUnlocked(_stageIds, i), _progress.IsCleared(_stageIds[i]));
            }
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

            if (nearest < 0 || nearest == _currentIndex)
                return;

            StartCoroutine(nodes[nearest].IsLocked ? BlockedRoutine(nearest) : MoveRoutine(nearest));
        }

        /// <summary>노드를 하나씩 거쳐 이동 — 경로를 따라 걷는 NSMB 느낌.</summary>
        private IEnumerator MoveRoutine(int target)
        {
            _moving = true;
            HidePanel();

            yield return WalkTo(target);

            _moving = false;
            ShowInfo(target);
        }

        /// <summary>
        /// 잠긴 노드를 눌렀을 때 — 갈 수 있는 데까지 걸어간 다음 <b>막힌 지점에서 부딪혀 튕긴다</b>.
        /// 제자리에서 안내만 띄우는 것보다 "여기서 막혔다"는 게 훨씬 잘 읽힌다.
        /// </summary>
        private IEnumerator BlockedRoutine(int target)
        {
            _moving = true;
            HidePanel();

            int step = target > _currentIndex ? 1 : -1;

            // 경로를 따라가며 잠기지 않은 마지막 노드까지만 간다
            int reachable = _currentIndex;
            for (int i = _currentIndex + step; i != target + step; i += step)
            {
                if (nodes[i] == null || nodes[i].IsLocked)
                    break;
                reachable = i;
            }

            if (reachable != _currentIndex)
                yield return WalkTo(reachable);

            int blocked = reachable + step;
            if (blocked >= 0 && blocked < nodes.Length)
                yield return BumpInto(blocked);

            _moving = false;
            ShowLocked(blocked >= 0 && blocked < nodes.Length ? blocked : target);
        }

        /// <summary>막힌 노드 쪽으로 살짝 나아갔다가 되돌아온다.</summary>
        private IEnumerator BumpInto(int blockedIndex)
        {
            AudioService.PlaySfx(blockedSfx);

            Vector3 origin = character.position;
            Vector3 toward = NodeWorld(blockedIndex) + characterOffset;

            for (int i = 0; i < blockedBumpCount; i++)
            {
                yield return MoveCharacterTo(Vector3.Lerp(origin, toward, blockedBumpRatio), moveSpeed);
                yield return MoveCharacterTo(origin, moveSpeed * 0.8f);
            }
        }

        private IEnumerator WalkTo(int target)
        {
            int step = target > _currentIndex ? 1 : -1;
            while (_currentIndex != target)
            {
                int next = _currentIndex + step;
                yield return MoveCharacterTo(NodeWorld(next) + characterOffset, moveSpeed);
                _currentIndex = next;
            }
        }

        private IEnumerator MoveCharacterTo(Vector3 destination, float speed)
        {
            while ((character.position - destination).sqrMagnitude > 0.0004f)
            {
                character.position = Vector3.MoveTowards(character.position, destination, speed * Time.deltaTime);
                yield return null;
            }
        }

        private void HidePanel()
        {
            if (infoPanel != null)
                infoPanel.SetActive(false);
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

            // 한 번이라도 들어가 본 스테이지만 보스·기믹을 공개한다 (GDD §9.2).
            bool revealed = _progress.IsRevealed(_stageIds[index]);

            if (titleText != null)
                titleText.text = Loc.GetText(
                    _progress.IsCleared(_stageIds[index]) ? "map.title.clear" : "map.title", DisplayName(index));
            if (infoText != null)
                infoText.text = Loc.GetText(
                    "map.info",
                    index / 3 + 1, width, height, config.TurnLimit,
                    revealed ? BossName(stage) : Loc.GetText("map.unknown"),
                    revealed ? GimmickSummary(stage) : Loc.GetText("map.unknown"));
            if (startButton != null)
                startButton.interactable = true;

            ApplyPortrait(stage, revealed);

            if (infoPanel != null)
                infoPanel.SetActive(true);
        }

        /// <summary>잠긴 노드를 눌렀을 때 — 이동은 하지 않고 패널로 이유만 표시한다. 정보는 일절 공개하지 않는다.</summary>
        private void ShowLocked(int index)
        {
            if (titleText != null)
                titleText.text = Loc.GetText("map.title.locked", DisplayName(index));
            if (infoText != null)
                infoText.text = Loc.GetText("map.locked.body");
            if (startButton != null)
                startButton.interactable = false;

            ApplyPortrait(nodes[index].Stage, revealed: false);

            if (infoPanel != null)
                infoPanel.SetActive(true);
        }

        /// <summary>공개 전에는 초상을 검은 실루엣으로 칠한다 — 실루엣만으로 다음 보스를 예고한다.</summary>
        private void ApplyPortrait(StageDataSO stage, bool revealed)
        {
            if (bossPortrait == null)
                return;

            Sprite portrait = stage != null && stage.BossData != null ? stage.BossData.Portrait : null;
            bossPortrait.enabled = portrait != null;
            if (portrait == null)
                return;

            bossPortrait.sprite = portrait;
            bossPortrait.color = revealed ? Color.white : silhouetteTint;
        }

        private static string BossName(StageDataSO stage) =>
            stage.BossData != null ? stage.BossData.DisplayName : Loc.GetText("map.unknown");

        /// <summary>이 스테이지에 나오는 기믹 이름들. 없으면 '없음'.</summary>
        private static string GimmickSummary(StageDataSO stage)
        {
            IReadOnlyList<GimmickType> gimmicks = stage.Gimmicks;
            if (gimmicks == null || gimmicks.Count == 0)
                return Loc.GetText("map.hazard.none");

            var names = new string[gimmicks.Count];
            for (int i = 0; i < gimmicks.Count; i++)
                names[i] = GimmickLabel(gimmicks[i]);
            return string.Join(" / ", names);
        }

        private static string GimmickLabel(GimmickType type) => type switch
        {
            GimmickType.SpreadingCorruption => Loc.GetText("gimmick.corruption"),
            GimmickType.TickingDeath => Loc.GetText("gimmick.timebomb"),
            GimmickType.LockedTiles => Loc.GetText("gimmick.chains"),
            _ => type.ToString().ToUpperInvariant(),
        };

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
