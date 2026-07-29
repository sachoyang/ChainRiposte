using System.Collections;
using System.Collections.Generic;
using ChainRiposte.Core.Progress;
using ChainRiposte.Core.Stage;
using ChainRiposte.Game.Audio;
using ChainRiposte.Game.Characters;
using ChainRiposte.Game.Config;
using ChainRiposte.Game.Localization;
using ChainRiposte.Game.Progress;
using ChainRiposte.Game.Theming;
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
        [Tooltip("월드맵 아바타 그림. 비우면 character 에서 찾는다. 고른 캐릭터의 그림으로 갈아 끼운다.")]
        [SerializeField] private SpriteRenderer characterRenderer;
        [SerializeField] private CameraFit2D cameraFit;
        [Tooltip("선택 사항 — 있으면 세로에서 길의 일부만 보이고 따라 스크롤한다. 비우면 예전처럼 전체를 한 화면에 담는다.")]
        [SerializeField] private MapCameraRig cameraRig;
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
        [Tooltip("선택 사항 — 꽂으면 정보 패널에 그 스테이지의 소울 광맥 잔량을 한 줄 더 띄운다. " +
            "비우면 그 줄만 빠지고 나머지는 그대로 (Main 의 GameManager 에 꽂은 것과 같은 에셋).")]
        [SerializeField] private RunEconomyConfigSO economyConfig;

        [Header("동작 값")]
        [SerializeField, Min(0.5f)] private float moveSpeed = 6f;
        [Tooltip("캐릭터가 노드보다 살짝 위에 서도록 하는 오프셋")]
        [SerializeField] private Vector3 characterOffset = new(0f, 0.7f, 0f);
        [Tooltip("클릭을 노드로 인정하는 반경 (월드 유닛)")]
        [SerializeField, Min(0.1f)] private float clickRadius = 0.8f;
        [Tooltip("클릭을 '길'로 인정하는 반경 (월드 유닛). 노드를 못 맞혔을 때 길을 눌러도 한 칸 움직이게 하는 판정 유도다. " +
            "0이면 끈다 — 노드를 정확히 눌러야만 움직인다.")]
        [SerializeField, Min(0f)] private float pathClickRadius = 1.2f;
        [Tooltip("카메라가 노드 전체를 담을 때 가장자리 여백")]
        [SerializeField, Min(0f)] private float cameraPadding = 1.5f;
        [Tooltip("길을 얼마나 부드럽게 이을지 (1이면 예전처럼 꺾은선). 경로선과 걷는 길이 같이 바뀐다.")]
        [SerializeField, Range(1, 24)] private int pathSmoothing = 8;

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
        private readonly List<Vector3> _nodePositions = new();
        private readonly List<Vector3> _pathBuffer = new();
        private readonly List<Vector3> _walkBuffer = new();

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

            ApplyCharacterVisual(); // 고른 캐릭터의 그림으로 아바타를 갈아 끼운다
            ApplyThemeLayout(); // 고른 캐릭터의 테마가 노드 위치를 정한다 (길 모양이 테마마다 다르다)

            RefreshNodeStates();
            _currentIndex = _progress.HighestUnlockedIndex(_stageIds);

            RefreshPathLine();
            character.position = NodeWorld(_currentIndex) + characterOffset;
        }

        /// <summary>
        /// 월드맵 아바타를 고른 캐릭터의 그림으로 갈아 끼운다. 전투 화면이 캐릭터 그림을 쓰는 것과 같은 규칙.
        /// 캐릭터가 없거나(Main 단독 실행) 그림이 없으면 씬에 꽂아둔 그림을 그대로 둔다.
        /// </summary>
        private void ApplyCharacterVisual()
        {
            if (characterRenderer == null && character != null)
                characterRenderer = character.GetComponentInChildren<SpriteRenderer>();
            if (characterRenderer == null)
                return;

            Sprite sprite = CharacterService.Current != null ? CharacterService.Current.MapSprite : null;
            if (sprite == null)
                return;

            characterRenderer.sprite = sprite;
            characterRenderer.color = Color.white; // 플레이스홀더 틴트를 벗기고 원색으로
        }

        /// <summary>
        /// 현재 테마가 정해 둔 자리로 노드를 옮긴다. 배경·보스처럼 <b>길 모양도 테마 데이터</b>라서
        /// 씬은 하나로 두고 위치만 갈아 끼운다. z는 씬 값을 지키고, 개수가 다르면 건드리지 않는다
        /// (씬에서 노드를 늘렸는데 테마 저장을 안 한 경우 옛 배치로 덮어쓰지 않게).
        /// </summary>
        private void ApplyThemeLayout()
        {
            ThemeSO theme = ThemeService.Current;
            if (theme == null || !theme.HasNodeLayout)
                return;

            IReadOnlyList<Vector2> layout = theme.NodeLayout;
            if (layout.Count != nodes.Length)
            {
                Debug.LogWarning(
                    $"{nameof(StageSelectController)}: 테마 '{theme.ThemeId}'의 노드 레이아웃 수({layout.Count})가 " +
                    $"씬 노드 수({nodes.Length})와 달라 적용하지 않습니다. 「길 그리기」에서 다시 저장하세요.", this);
                return;
            }

            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] == null)
                    continue;
                Vector3 position = nodes[i].transform.position;
                nodes[i].transform.position = new Vector3(layout[i].x, layout[i].y, position.z);
            }
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
            FrameCamera(instant: true);
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

            // 노드를 못 맞혔으면 '길'을 맞혔는지 본다 — 다음 노드가 화면 밖일 때의 유일한 이동 수단이다.
            if (nearest < 0)
                nearest = ResolvePathClick(world);
            if (nearest < 0)
                return;

            // 서 있는 노드를 다시 누르면 정보 패널을 되살린다.
            // 잠긴 노드를 눌러 LOCKED(START 비활성)가 떠 있을 때, 여기로 돌아올 방법이 이것뿐이다.
            if (nearest == _currentIndex)
            {
                ShowInfo(_currentIndex);
                return;
            }

            StartCoroutine(nodes[nearest].IsLocked ? BlockedRoutine(nearest) : MoveRoutine(nearest));
        }

        /// <summary>
        /// 길 위를 눌렀을 때 갈 곳 — <b>누른 쪽으로 한 칸</b>. 못 맞히면 -1.
        ///
        /// <para>세로 스크롤에서는 다음 노드가 화면 밖에 있어서 직접 누를 방법이 없다.
        /// 그래서 길 자체를 판정 유도 영역으로 쓴다.</para>
        ///
        /// <para>방향은 고정이 아니다 — 누른 지점이 <b>지금 서 있는 노드보다 앞이냐 뒤냐</b>로 그때그때 정한다.
        /// 뒤쪽 노드에 서서 앞쪽 길을 누르면 앞으로, 앞쪽 노드에 서서 뒤쪽 길을 누르면 뒤로 간다.</para>
        /// </summary>
        private int ResolvePathClick(Vector3 world)
        {
            if (pathClickRadius <= 0f || _pathBuffer.Count < 2)
                return -1;

            int hit = -1;
            float best = pathClickRadius;
            for (int i = 0; i < _pathBuffer.Count; i++)
            {
                float distance = Vector2.Distance(world, _pathBuffer[i]);
                if (distance < best)
                {
                    best = distance;
                    hit = i;
                }
            }

            if (hit < 0)
                return -1;

            // 길 위의 점 번호 → 노드 번호. MapPath.Build 가 한 구간마다 pathSmoothing 개씩 찍으므로
            // 나누면 그대로 "몇 번째 노드쯤"이 된다 (노드 i 는 정확히 i × pathSmoothing 번째 점).
            float atNode = hit / (float)Mathf.Max(1, pathSmoothing);

            // 서 있는 자리 근처를 눌렀으면 방향이 없다 — 노드를 다시 누른 것으로 친다
            if (Mathf.Abs(atNode - _currentIndex) < 0.25f)
                return _currentIndex;

            int step = atNode > _currentIndex ? 1 : -1;
            return Mathf.Clamp(_currentIndex + step, 0, nodes.Length - 1);
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
                FocusCamera(next); // 한 칸 먼저 카메라가 향한다 — 걸어가는 동안 부드럽게 따라붙는다
                yield return WalkSegment(_currentIndex, next);
                _currentIndex = next;
            }
        }

        /// <summary>
        /// 한 칸을 <b>그려진 곡선 위로</b> 걸어간다. 경로선과 같은 <see cref="MapPath"/>를 쓰므로
        /// 캐릭터가 길에서 벗어나지 않는다.
        /// </summary>
        private IEnumerator WalkSegment(int from, int to)
        {
            MapPath.Segment(NodePositions(), from, to, pathSmoothing, _walkBuffer);

            int index = 1;
            while (index < _walkBuffer.Count)
            {
                // 한 프레임에 갈 거리를 다 쓸 때까지 점을 넘어간다 — 점마다 멈추면 걸음이 끊겨 보인다.
                float budget = moveSpeed * Time.deltaTime;
                while (budget > 0f && index < _walkBuffer.Count)
                {
                    Vector3 destination = _walkBuffer[index] + characterOffset;
                    float distance = Vector3.Distance(character.position, destination);
                    if (distance <= budget)
                    {
                        character.position = destination;
                        budget -= distance;
                        index++;
                    }
                    else
                    {
                        character.position = Vector3.MoveTowards(character.position, destination, budget);
                        budget = 0f;
                    }
                }

                yield return null;
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
                    revealed ? GimmickSummary(stage) : Loc.GetText("map.unknown"))
                    + VeinLine(stage);
            if (startButton != null)
                startButton.interactable = true;

            ApplyPortrait(stage, revealed);

            if (infoPanel != null)
                infoPanel.SetActive(true);
        }

        /// <summary>
        /// 정보 패널에 덧붙이는 소울 광맥 한 줄 — <b>다시 가도 벌이가 있는 땅인지</b>를 여기서 판단한다.
        /// 경제 에셋을 안 꽂았거나 매장량이 무제한이면 빈 문자열이라 패널 모양이 그대로 유지된다.
        /// </summary>
        private string VeinLine(StageDataSO stage)
        {
            if (economyConfig == null || stage == null)
                return string.Empty;

            RunEconomyConfig economy = economyConfig.ToConfig();
            int budget = economy.ResolveBudget(stage.SoulBudget);
            if (budget <= 0)
                return string.Empty; // 매장량을 안 정한 스테이지 — 광맥 개념이 꺼져 있다

            int remaining = economy.RemainingSouls(stage.SoulBudget, RunStateService.Current.GetHarvested(stage.StageId));
            return "\n" + (remaining <= 0
                ? Loc.GetText("map.vein.depleted")
                : Loc.GetText("map.vein.remaining", remaining, budget));
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

        /// <summary>
        /// 공개 전에는 초상을 검은 실루엣으로 칠한다 — 실루엣만으로 다음 보스를 예고한다.
        ///
        /// <para>그림은 <b>전투 화면과 같은 규칙</b>(<see cref="BossVisual"/>)으로 고른다.
        /// 여기서만 <c>BossData.Portrait</c>를 직접 읽으면 캐릭터별 겉모습을 못 타서
        /// <b>맵에서 예고한 실루엣과 실제로 나오는 보스가 달라진다.</b>
        /// (전용 초상을 쓰고 싶으면 보스 에셋의 <c>battleSprite</c>를 비워 두면 초상으로 떨어진다.)</para>
        /// </summary>
        private void ApplyPortrait(StageDataSO stage, bool revealed)
        {
            if (bossPortrait == null)
                return;

            Sprite portrait = BossVisual.ResolveSprite(stage != null ? stage.BossData : null);
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
            StageSelection.Select(nodes[_currentIndex].Stage, IsLastLink(_currentIndex), LinkDepth(_currentIndex));
            SceneManager.LoadScene("Main");
        }

        /// <summary>
        /// 이 노드가 몇 번째 고리인가 = <b>앞에 스테이지가 걸린 노드가 몇 개인가</b> (첫 판 = 0).
        /// <see cref="IsLastLink"/>와 같은 규칙으로 세는 이유는, 지도에 장식용 빈 노드를 하나 찍은 순간
        /// 뒤 스테이지들의 난이도가 통째로 한 칸씩 밀리면 안 되기 때문이다.
        /// </summary>
        private int LinkDepth(int index)
        {
            int depth = 0;
            for (int i = 0; i < index && i < nodes.Length; i++)
                if (nodes[i] != null && nodes[i].Stage != null)
                    depth++;

            return depth;
        }

        /// <summary>
        /// 이 노드가 사슬의 마지막 고리인가 = <b>뒤에 스테이지가 걸린 노드가 더 없는가</b>.
        /// 노드 개수가 아니라 스테이지가 걸린 노드를 세는 이유는, 지도에 빈 노드를 하나 더 찍어 둔
        /// 순간 엔딩이 조용히 사라지면 안 되기 때문이다.
        /// </summary>
        private bool IsLastLink(int index)
        {
            for (int i = index + 1; i < nodes.Length; i++)
                if (nodes[i] != null && nodes[i].Stage != null)
                    return false;

            return true;
        }

        private static string DisplayName(int index) => $"{index / 3 + 1}-{index % 3 + 1}";

        private Vector3 NodeWorld(int index) => nodes[index].Position;

        /// <summary>
        /// 길의 모양을 다시 계산하고, 경로선이 있으면 거기에도 반영한다 — 노드를 옮기면 선도 따라온다.
        /// <b>선이 없어도 계산은 한다</b> — 길 클릭 판정(<see cref="ResolvePathClick"/>)이 같은 점들을 쓴다.
        /// </summary>
        private void RefreshPathLine()
        {
            MapPath.Build(NodePositions(), pathSmoothing, _pathBuffer);

            if (pathLine == null)
                return;

            pathLine.positionCount = _pathBuffer.Count;
            for (int i = 0; i < _pathBuffer.Count; i++)
                pathLine.SetPosition(i, _pathBuffer[i]);
        }

        /// <summary>노드 위치 목록. 길 모양과 걷는 길이 같은 값을 보게 하기 위한 한 곳.</summary>
        private List<Vector3> NodePositions()
        {
            _nodePositions.Clear();
            foreach (MapNode node in nodes)
                _nodePositions.Add(node != null ? node.Position : Vector3.zero);
            return _nodePositions;
        }

#if UNITY_EDITOR
        /// <summary>에디터 전용 — 씬에서 노드를 옮겼을 때 경로선을 바로 다시 그린다.</summary>
        public void RefreshPathLineEditorOnly() => RefreshPathLine();
#endif

        /// <summary>
        /// 화면 잡기. 리그가 있으면 방향별 규칙(세로=스크롤 / 가로=전체)을 리그가 정하고,
        /// 없으면 예전처럼 길 전체를 한 화면에 담는다.
        /// </summary>
        private void FrameCamera(bool instant)
        {
            if (nodes.Length == 0)
                return;

            Bounds bounds = NodeBounds();
            if (cameraRig != null)
            {
                cameraRig.Frame(bounds, NodeWorld(_currentIndex), instant);
                return;
            }

            if (cameraFit != null)
                cameraFit.FitTo(bounds);
        }

        /// <summary>지금 서 있는 곳을 보여 준다 — 리그가 없으면 아무 일도 하지 않는다.</summary>
        private void FocusCamera(int index)
        {
            if (cameraRig != null)
                cameraRig.Focus(NodeWorld(index));
        }

        private Bounds NodeBounds()
        {
            var bounds = new Bounds(nodes[0].Position, Vector3.zero);
            for (int i = 1; i < nodes.Length; i++)
                bounds.Encapsulate(nodes[i].Position);
            bounds.Expand(cameraPadding * 2f);
            return bounds;
        }
    }
}
