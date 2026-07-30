using System;
using System.Collections;
using System.Collections.Generic;
using ChainRiposte.Core.Board;
using ChainRiposte.Core.Match;
using ChainRiposte.Core.Stage.Gimmicks;
using ChainRiposte.Game.Config;
using UnityEngine;

namespace ChainRiposte.Game.Puzzle
{
    /// <summary>
    /// 보드 모델의 시각화. 로직은 이미 완결된 SwapResult 기록을 시간순으로 재생만 한다.
    /// (연출 중에도 모델은 최종 상태 — 이 클래스는 모델을 절대 수정하지 않는다.)
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        [Header("타일 비주얼 (TileDefinitionSO의 색/스프라이트 매핑)")]
        [SerializeField] private TileDefinitionSO[] tileVisuals = Array.Empty<TileDefinitionSO>();
        [SerializeField] private Color wallColor = new(0.20f, 0.17f, 0.15f);
        [SerializeField] private Color bossColor = new(0.62f, 0.08f, 0.12f);
        [Tooltip("부패 타일 (GDD §3.6 전염 기믹)")]
        [SerializeField] private Color corruptionColor = new(0.36f, 0.16f, 0.42f);
        [SerializeField] private Color unknownColor = Color.magenta;

        [Header("에셋 스왑 — 지정하면 색 사각형 대신 스프라이트로 표시")]
        [Tooltip("벽 타일 아트 (비우면 wallColor 사각형)")]
        [SerializeField] private Sprite wallSprite;
        [Tooltip("벽 손상 단계 — 0=온전, 마지막=거의 부서짐. 채우면 어두워지는 대신 금이 간 그림으로 바뀐다.")]
        [SerializeField] private Sprite[] wallDamageSprites = Array.Empty<Sprite>();
        [Tooltip("보스 타일 아트 (비우면 bossColor 사각형)")]
        [SerializeField] private Sprite bossSprite;
        [Tooltip("배경 셀 아트 (비우면 체커 2색 사각형)")]
        [SerializeField] private Sprite cellSprite;
        [Tooltip("부패 타일 아트 (비우면 corruptionColor 사각형)")]
        [SerializeField] private Sprite corruptionSprite;
        [Tooltip("사슬 결박 오버레이 아트 (비우면 회색 띠)")]
        [SerializeField] private Sprite chainSprite;
        [Tooltip("시한폭탄 뱃지 — 타일 오른쪽 위. 비우면 숫자만 뜬다(그림 없이도 읽힌다).")]
        [SerializeField] private Sprite bombSprite;
        [Tooltip("성난 몬스터 뱃지 — 타일 왼쪽 위(폭탄과 겹치지 않게 반대쪽). 비우면 틴트+숫자만.")]
        [SerializeField] private Sprite enrageSprite;
        [Tooltip("뱃지가 차지할 크기 (셀 = 1)")]
        [SerializeField, Range(0.1f, 0.8f)] private float badgeSize = 0.42f;
        [Tooltip("사슬이 차지할 크기 (셀 = 1). 그림의 픽셀 크기와 무관하게 여기에 맞춘다.")]
        [SerializeField, Range(0.5f, 1.4f)] private float chainSize = 1f;
        [Tooltip("성난 몬스터의 틴트 — 공격 예고 중인 잡몹이 한눈에 보여야 '저놈부터 없앤다'가 성립한다.")]
        [SerializeField] private Color enrageTint = new(1f, 0.42f, 0.35f);

        [Header("성난 몬스터 연출 — 성났는지·맞았는지가 보드에서 읽혀야 한다")]
        [Tooltip("카운트가 1까지 줄었을 때의 숫자 크기 배율. 3·2·1이 같은 크기면 급해지는 게 안 읽힌다. 1이면 안 커진다.")]
        [SerializeField, Min(1f)] private float enrageCountMaxScale = 1.9f;
        [Tooltip("숫자가 한 칸 줄 때 순간적으로 튀는 배율. 1이면 안 튄다.")]
        [SerializeField, Min(1f)] private float enrageCountPopScale = 1.5f;
        [Tooltip("때릴 때 몬스터가 체력 바 쪽으로 달려가는 거리 (셀 1칸 기준). 0이면 안 달린다.")]
        [SerializeField, Min(0f)] private float enrageLungeDistance = 0.6f;
        [SerializeField, Min(0.05f)] private float enrageLungeSeconds = 0.34f;
        [Tooltip("몬스터가 달려드는 목표 — 퍼즐 하단 체력 바를 꽂는다. " +
            "비우면 화면 아래쪽으로 달려든다(하단 바가 있는 방향이라 배선을 덜 해도 말은 된다).")]
        [SerializeField] private RectTransform enrageAttackTarget;

        [Header("검기 — 매치 한 줄을 한 번에 벤다")]
        [SerializeField] private bool slashEnabled = true;
        [Tooltip("검기 아트. 비우면 양 끝이 뾰족한 눈 모양 플레이스홀더를 구워 쓴다. " +
            "가로로 누운 그림을 쓸 것 — 방향은 코드가 회전시킨다.")]
        [SerializeField] private Sprite slashSprite;
        [SerializeField] private Color slashColor = new(1f, 0.97f, 0.85f, 0.9f);
        [Tooltip("검기 굵기 (타일 1칸 = 1)")]
        [SerializeField, Min(0.01f)] private float slashThickness = 0.35f;
        [Tooltip("줄의 양 끝을 넘겨 긋는 길이. 0이면 벤 자국이 타일 안에 갇혀 '지나갔다'가 안 읽힌다.")]
        [SerializeField, Min(0f)] private float slashOvershoot = 0.55f;
        [SerializeField, Min(0.02f)] private float slashDuration = 0.22f;

        [Header("타일 크기 — 셀 1칸 기준. 그림의 픽셀 크기·PPU와 무관하게 여기에 맞춰진다")]
        [Tooltip("일반 몬스터 타일이 셀에서 차지하는 비율. 1보다 작아야 타일 사이가 벌어져 개수가 읽힌다.")]
        [SerializeField, Range(0.1f, 1.2f)] private float tileFillRatio = 0.9f;
        [Tooltip("벽 타일 비율. 벽은 '지형'이라 셀을 꽉 채워야 움직이는 타일과 확실히 구분된다.")]
        [SerializeField, Range(0.1f, 1.2f)] private float wallFillRatio = 1f;
        [Tooltip("보스 타일 비율. 판에서 가장 급한 타일이라 크게 보여야 한다.")]
        [SerializeField, Range(0.1f, 1.2f)] private float bossFillRatio = 1f;

        [Header("타일 배경판 — 아이콘만 있으면 타일 경계가 안 읽힌다")]
        [Tooltip("타일 SO에 전용 배경이 없을 때 쓰는 공용 받침. 비워도 SO의 배경 색만 넣으면 사각 받침이 깔린다.")]
        [SerializeField] private Sprite tileBackgroundSprite;
        [Tooltip("받침이 셀에서 차지하는 비율 (1 = 셀을 꽉 채움). 아이콘보다 커야 받침으로 읽힌다. " +
            "타일 사이가 붙어 보이면 1보다 살짝 낮춘다.")]
        [SerializeField, Min(0.1f)] private float tileBackgroundScale = 1f;
        [Tooltip("타일 SO가 배경 색을 안 정했을 때(알파 0) 대신 쓸 색. 여기도 알파가 0이면 받침을 안 그린다.")]
        [SerializeField] private Color tileBackgroundColor = new(1f, 1f, 1f, 0f);
        [Tooltip("벽에도 받침을 깔지 — 벽은 셀을 꽉 채우는 지형이라 보통은 필요 없다")]
        [SerializeField] private bool backgroundOnWalls;

        [Header("배경 셀 (체커 2색)")]
        [SerializeField] private Color cellColorA = new(0.16f, 0.15f, 0.19f);
        [SerializeField] private Color cellColorB = new(0.13f, 0.12f, 0.16f);

        [Header("연출 타이밍(초)")]
        [SerializeField, Min(0.01f)] private float swapDuration = 0.15f;
        [SerializeField, Min(0.01f)] private float clearDuration = 0.18f;
        [SerializeField, Min(0.01f)] private float fallDurationPerCell = 0.07f;
        [SerializeField, Min(0f)] private float stepPause = 0.05f;
        [Tooltip("데드락 리롤 — 타일이 새 자리로 날아가는 시간")]
        [SerializeField, Min(0.01f)] private float shuffleDuration = 0.45f;

        private readonly Dictionary<GridPos, TileView> _views = new();
        private readonly Dictionary<TileDefinition, Color> _colorByDefinition = new();
        private readonly Dictionary<TileDefinition, Sprite> _spriteByDefinition = new();
        private readonly Dictionary<TileDefinition, Sprite> _backgroundByDefinition = new();
        private readonly Dictionary<TileDefinition, Color> _backgroundColorByDefinition = new();
        private BoardGrid _board;
        private Transform _tileRoot;
        private Vector2 _originLocal; // (0,0) 셀의 로컬 위치 — 보드를 중앙 정렬

        public Bounds WorldBounds { get; private set; }

        /// <summary>캐스케이드 한 단계의 파괴 연출이 시작되는 순간 — 타일 깨짐 SFX/VFX 훅 (콤보는 step.ComboIndex).</summary>
        public event Action<CascadeStep> StepCleared;

        /// <summary>데드락 리롤이 시작되는 순간 — 배너/SFX 훅.</summary>
        public event Action Shuffling;

        public void Build(BoardGrid board)
        {
            _board = board;
            BuildColorTable();

            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
            _views.Clear();

            _originLocal = new Vector2(-(board.Width - 1) * 0.5f, -(board.Height - 1) * 0.5f);
            WorldBounds = new Bounds(transform.position, new Vector3(board.Width, board.Height, 0f));

            Transform cellRoot = CreateRoot("Cells");
            _tileRoot = CreateRoot("Tiles");

            foreach (GridPos pos in board.ActivePositions())
            {
                CreateCellBackground(cellRoot, pos);
                Tile tile = board.GetTile(pos);
                if (tile != null)
                    CreateTileView(tile, pos);
            }
        }

        public Vector3 GridToLocal(GridPos pos) =>
            new(_originLocal.x + pos.X, _originLocal.y + pos.Y, 0f);

        public bool TryWorldToGrid(Vector3 world, out GridPos pos)
        {
            Vector3 local = transform.InverseTransformPoint(world);
            pos = new GridPos(
                Mathf.RoundToInt(local.x - _originLocal.x),
                Mathf.RoundToInt(local.y - _originLocal.y));
            return _board != null && _board.IsActive(pos);
        }

        /// <summary>스왑 결과를 재생한다. 실패 스왑은 갔다가 되돌아오는 연출.</summary>
        public IEnumerator PlaySwapResult(SwapResult result)
        {
            if (!_views.TryGetValue(result.A, out TileView viewA) ||
                !_views.TryGetValue(result.B, out TileView viewB))
                yield break; // 스왑 불가 셀 (벽/빈 칸) — 연출 없음

            yield return WhenAll(new List<IEnumerator>
            {
                viewA.MoveTo(GridToLocal(result.B), swapDuration),
                viewB.MoveTo(GridToLocal(result.A), swapDuration),
            });

            if (!result.Success)
            {
                yield return WhenAll(new List<IEnumerator>
                {
                    viewA.MoveTo(GridToLocal(result.A), swapDuration),
                    viewB.MoveTo(GridToLocal(result.B), swapDuration),
                });
                yield break;
            }

            _views[result.A] = viewB;
            _views[result.B] = viewA;

            foreach (CascadeStep step in result.Steps)
                yield return PlayStep(step);

            if (!result.Gimmicks.IsEmpty)
                yield return PlayGimmickPhase(result.Gimmicks);

            if (result.Shuffled)
                yield return PlayShuffle(result.ShuffleMoves);
        }

        /// <summary>데드락 리롤 — 타일들이 한꺼번에 새 자리로 날아간다 (낙하가 아니므로 등속 이동).</summary>
        private IEnumerator PlayShuffle(IReadOnlyList<TileMove> moves)
        {
            Shuffling?.Invoke();

            // 낙하와 같은 규칙 — From을 전부 비운 뒤 To로 재등록해야 순열이 안 깨진다
            var moving = new List<(TileMove move, TileView view)>();
            foreach (TileMove move in moves)
            {
                if (_views.Remove(move.From, out TileView view))
                    moving.Add((move, view));
            }

            var anims = new List<IEnumerator>();
            foreach ((TileMove move, TileView view) in moving)
            {
                _views[move.To] = view;
                anims.Add(view.MoveTo(GridToLocal(move.To), shuffleDuration));
            }

            yield return WhenAll(anims);
        }

        private IEnumerator PlayStep(CascadeStep step)
        {
            StepCleared?.Invoke(step);
            yield return PlayClear(step);
            ApplyBadgeEvents(step.GimmickEvents);
            yield return PlayFallPhases(step.FallPhases);
            if (stepPause > 0f)
                yield return new WaitForSeconds(stepPause);
        }

        /// <summary>
        /// 턴 종료 기믹(확산·폭발)의 연출 — 사건 → 낙하 → 그 여파로 터진 연쇄 순 (GDD §3.6).
        /// </summary>
        public IEnumerator PlayGimmickPhase(GimmickPhase phase)
        {
            var anims = new List<IEnumerator>();

            foreach (GimmickEvent gimmickEvent in phase.Events)
            {
                switch (gimmickEvent.Type)
                {
                    case GimmickEventType.BombExploded:
                    case GimmickEventType.CorruptionCleared:
                        if (_views.Remove(gimmickEvent.Position, out TileView removed))
                            anims.Add(removed.ClearAndDestroy(clearDuration));
                        break;

                    case GimmickEventType.CorruptionSpread:
                        // 감염된 칸의 타일이 통째로 교체된다 — 옛 뷰를 버리고 부패 타일로 다시 만든다
                        if (_views.Remove(gimmickEvent.Position, out TileView infected))
                            Destroy(infected.gameObject);
                        CreateTileView(gimmickEvent.Tile, gimmickEvent.Position);
                        break;

                    default:
                        ApplyBadgeEvent(gimmickEvent);
                        break;
                }
            }

            yield return WhenAll(anims);
            yield return PlayFallPhases(phase.FallPhases);

            foreach (CascadeStep step in phase.Cascades)
                yield return PlayStep(step);
        }

        /// <summary>파괴를 동반하지 않는 기믹 사건(사슬 해제·폭탄 카운트)만 뱃지에 반영한다.</summary>
        private void ApplyBadgeEvents(IReadOnlyList<GimmickEvent> events)
        {
            foreach (GimmickEvent gimmickEvent in events)
                ApplyBadgeEvent(gimmickEvent);
        }

        private void ApplyBadgeEvent(GimmickEvent gimmickEvent)
        {
            if (gimmickEvent.Tile == null || !TryFindView(gimmickEvent.Tile.InstanceId, out TileView view))
                return;

            switch (gimmickEvent.Type)
            {
                case GimmickEventType.ChainBroken:
                    view.SetChained(false, chainSprite);
                    break;
                case GimmickEventType.BombArmed:
                case GimmickEventType.BombTicked:
                    view.SetBombTurns(gimmickEvent.Value);
                    break;
                case GimmickEventType.EnrageStarted:
                case GimmickEventType.EnrageTicked:
                    view.SetEnrageCountdown(gimmickEvent.Value, Enrage);
                    break;
                case GimmickEventType.EnrageAttacked:
                    // 때린 뒤에도 사라지지 않는다 — 재장전된 카운트를 그대로 다시 보여주고,
                    // 체력 바 쪽으로 달려들었다 돌아온다.
                    view.SetEnrageCountdown(gimmickEvent.Tile.Status.EnrageCountdown, Enrage);
                    // 코루틴은 <b>타일 쪽에서</b> 돌린다 — 연출 도중 그 타일이 사라지면 같이 멈춰야 한다.
                    view.StartCoroutine(view.PunchOnce());
                    view.StartCoroutine(view.LungeToward(
                        ResolveAttackTargetWorld(view.transform.position), enrageLungeDistance, enrageLungeSeconds));
                    break;
            }
        }

        /// <summary>성난 몬스터 뱃지의 겉모습 — 튜닝 다이얼은 전부 이 컴포넌트의 인스펙터에 있다.</summary>
        private TileView.EnrageStyle Enrage =>
            new(enrageTint, enrageCountMaxScale, enrageCountPopScale);

        /// <summary>기믹 상태 그림 한 벌 (사슬·폭탄·성남). 인스펙터 값을 타일에 그대로 넘긴다.</summary>
        private TileView.StatusArt Status =>
            new(chainSprite, bombSprite, enrageSprite, badgeSize, chainSize);

        /// <summary>
        /// 몬스터가 달려들 목표를 <b>월드 좌표</b>로 바꾼다. 체력 바는 UI(화면 좌표)에 있고
        /// 타일은 월드에 있으므로 한 번 건너와야 한다.
        ///
        /// <para>배선이 없거나 카메라를 못 찾으면 <b>화면 아래쪽</b>으로 떨어진다 —
        /// 하단 바가 있는 방향이라 배선을 덜 해도 연출이 말이 되고, 무엇보다 멈추지 않는다.</para>
        /// </summary>
        private Vector3 ResolveAttackTargetWorld(Vector3 from)
        {
            Vector3 fallback = from + Vector3.down * 10f;
            if (enrageAttackTarget == null)
                return fallback;

            Camera boardCamera = Camera.main;
            if (boardCamera == null)
                return fallback;

            // Overlay 캔버스는 RectTransform.position 자체가 화면 좌표라 카메라를 넘기면 안 된다.
            var canvas = enrageAttackTarget.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCamera, enrageAttackTarget.position);
            Vector3 world = boardCamera.ScreenToWorldPoint(
                new Vector3(screen.x, screen.y, Mathf.Abs(boardCamera.transform.position.z - from.z)));
            world.z = from.z;
            return world;
        }

        /// <summary>
        /// 매치를 벤다 — <b>줄 하나당 검기 하나</b>. 매치의 단위는 타일이 아니라 라인이다.
        ///
        /// <para><see cref="MatchGroup"/>은 ㄱ/T자로 겹친 가로·세로 런을 <b>한 그룹으로 병합</b>해 두므로
        /// 좌표만 있고 어느 줄이었는지는 없다. 여기서 행·열별 연속 구간으로 되돌려 다시 쪼갠다 —
        /// Core가 연출을 위해 자료구조를 바꿀 이유는 없고, 되돌리는 건 순수 기하라 안전하다.</para>
        ///
        /// <para>쪼갠 줄들은 <b>동시에</b> 터진다. 순차로 내면 "두 번 벴다"가 되어 한 수의 무게가 흩어진다.</para>
        /// </summary>
        private void PlaySlashes(CascadeStep step)
        {
            if (!slashEnabled)
                return;

            foreach (MatchGroup group in step.Matches)
            {
                foreach ((GridPos from, GridPos to) in ResolveRuns(group.Positions))
                {
                    SlashView slash = SlashView.Create(
                        transform, GridToLocal(from), GridToLocal(to),
                        slashSprite, slashColor, slashThickness, slashOvershoot, sortingOrder: 20);
                    StartCoroutine(slash.Play(slashDuration));
                }
            }
        }

        /// <summary>
        /// 좌표 뭉치를 곧은 줄(3칸 이상 연속)들로 되돌린다. 곧은 3매치는 1개, ㄱ/T자는 가로 1 + 세로 1이 나온다.
        /// 3칸 미만인 조각은 버린다 — 그건 줄이 아니라 다른 줄에 딸린 꼬리다.
        /// </summary>
        private static List<(GridPos from, GridPos to)> ResolveRuns(IReadOnlyList<GridPos> positions)
        {
            var runs = new List<(GridPos, GridPos)>();
            var set = new HashSet<GridPos>(positions);

            foreach (GridPos pos in positions)
            {
                // 각 줄의 시작점에서만 센다 — 앞칸이 같은 줄에 있으면 그 칸이 이미 셌다.
                if (!set.Contains(new GridPos(pos.X - 1, pos.Y)))
                {
                    int length = 1;
                    while (set.Contains(new GridPos(pos.X + length, pos.Y)))
                        length++;
                    if (length >= 3)
                        runs.Add((pos, new GridPos(pos.X + length - 1, pos.Y)));
                }

                if (!set.Contains(new GridPos(pos.X, pos.Y - 1)))
                {
                    int length = 1;
                    while (set.Contains(new GridPos(pos.X, pos.Y + length)))
                        length++;
                    if (length >= 3)
                        runs.Add((pos, new GridPos(pos.X, pos.Y + length - 1)));
                }
            }

            return runs;
        }

        private IEnumerator PlayClear(CascadeStep step)
        {
            PlaySlashes(step); // 사라지기 전에 그어야 '베어서 사라졌다'로 읽힌다

            var anims = new List<IEnumerator>();

            // 매치 좌표가 아니라 '실제로 사라진 칸'을 기준으로 지운다 —
            // 사슬 타일은 살아남고, 인접한 부패 타일은 함께 사라진다 (GDD §3.6)
            foreach (GridPos pos in step.ClearedPositions)
            {
                if (_views.Remove(pos, out TileView view))
                    anims.Add(view.ClearAndDestroy(clearDuration));
            }

            foreach (WallHit hit in step.WallHits)
            {
                if (!_views.TryGetValue(hit.Position, out TileView wallView))
                    continue;

                if (hit.Destroyed)
                {
                    _views.Remove(hit.Position);
                    anims.Add(wallView.ClearAndDestroy(clearDuration));
                }
                else
                {
                    wallView.ApplyWallDamage(hit.Damage);
                }
            }

            yield return WhenAll(anims);
        }

        /// <summary>낙하 웨이브를 순서대로 재생 — 직선 낙하와 대각선 슬라이드가 지그재그로 이어진다.</summary>
        private IEnumerator PlayFallPhases(IReadOnlyList<FallPhase> phases)
        {
            foreach (FallPhase phase in phases)
            {
                var anims = new List<IEnumerator>();

                // 이동 — From을 전부 비운 뒤 To로 재등록 (같은 열 연속 이동의 키 충돌 방지)
                var moving = new List<(TileMove move, TileView view)>();
                foreach (TileMove move in phase.Moves)
                {
                    if (_views.Remove(move.From, out TileView view))
                        moving.Add((move, view));
                }

                foreach ((TileMove move, TileView view) in moving)
                {
                    _views[move.To] = view;
                    int distance = Mathf.Abs(move.From.Y - move.To.Y) + Mathf.Abs(move.From.X - move.To.X);
                    anims.Add(view.MoveTo(GridToLocal(move.To), fallDurationPerCell * Mathf.Max(1, distance)));
                }

                // 스폰 — 보드 상단 밖에서 낙하. 같은 열의 연속 스폰은 위로 쌓아 겹침 방지
                var stackByColumn = new Dictionary<int, int>();
                foreach (TileSpawn spawn in phase.Spawns)
                {
                    stackByColumn.TryGetValue(spawn.Position.X, out int stack);
                    stackByColumn[spawn.Position.X] = stack + 1;

                    TileView view = CreateTileView(spawn.Tile, spawn.Position);
                    int startY = _board.Height + stack;
                    view.transform.localPosition = GridToLocal(new GridPos(spawn.Position.X, startY));
                    anims.Add(view.MoveTo(GridToLocal(spawn.Position), fallDurationPerCell * (startY - spawn.Position.Y)));
                }

                yield return WhenAll(anims);
            }
        }

        private bool TryFindView(long tileId, out TileView found)
        {
            foreach (TileView view in _views.Values)
            {
                if (view.TileId != tileId)
                    continue;
                found = view;
                return true;
            }

            found = null;
            return false;
        }

        private TileView CreateTileView(Tile tile, GridPos pos)
        {
            Sprite sprite = SpriteFor(tile);
            // 스프라이트가 있으면 원색 그대로(흰 틴트), 없으면 플레이스홀더 착색
            Color color = sprite != null ? Color.white : ColorFor(tile);
            // 안전망: 같은 칸에 뷰가 남아 있으면 스프라이트가 겹쳐 보인다 (모델은 한 칸에 타일 하나)
            if (_views.TryGetValue(pos, out TileView stale))
            {
                Debug.LogWarning($"[BoardView] {pos}에 뷰가 이미 있음 — 모델/뷰 불일치. 옛 뷰를 제거한다.");
                Destroy(stale.gameObject);
            }

            var view = TileView.Create(_tileRoot, tile, new TileView.Visual
            {
                Sprite = sprite,
                Color = color,
                Background = BackgroundFor(tile),
                BackgroundColor = BackgroundColorFor(tile),
                IconSize = FillRatioFor(tile),
                BackgroundSize = tileBackgroundScale,
            });
            if (tile.Category == TileCategory.Wall && wallDamageSprites.Length > 0)
                view.SetWallStages(wallDamageSprites);
            view.transform.localPosition = GridToLocal(pos);
            view.ApplyStatus(tile, Status, Enrage); // 사슬/폭탄/성남 뱃지 (GDD §3.6)
            _views[pos] = view;
            return view;
        }

        private Color ColorFor(Tile tile)
        {
            switch (tile.Category)
            {
                case TileCategory.Wall: return wallColor;
                case TileCategory.Boss: return bossColor;
                case TileCategory.Corruption: return corruptionColor;
                default:
                    return _colorByDefinition.TryGetValue(tile.Definition, out Color color) ? color : unknownColor;
            }
        }

        /// <summary>이 타일이 셀에서 차지할 크기 (셀 = 1). 부패는 일반 타일과 같은 무게로 읽혀야 한다.</summary>
        private float FillRatioFor(Tile tile)
        {
            switch (tile.Category)
            {
                case TileCategory.Wall: return wallFillRatio;
                case TileCategory.Boss: return bossFillRatio;
                default: return tileFillRatio;
            }
        }

        private Sprite SpriteFor(Tile tile)
        {
            switch (tile.Category)
            {
                case TileCategory.Wall: return wallSprite;
                case TileCategory.Boss: return bossSprite;
                case TileCategory.Corruption: return corruptionSprite;
                default:
                    return _spriteByDefinition.TryGetValue(tile.Definition, out Sprite sprite) ? sprite : null;
            }
        }

        /// <summary>
        /// 타일 받침. 타일 SO가 전용 그림을 가지면 그것, 없으면 공용 그림,
        /// 둘 다 없는데 색만 정해져 있으면 사각형 — <b>그림 없이 색만 넣어도 받침이 생긴다.</b>
        /// </summary>
        private Sprite BackgroundFor(Tile tile)
        {
            if (tile.Category == TileCategory.Wall && !backgroundOnWalls)
                return null;

            if (_backgroundByDefinition.TryGetValue(tile.Definition, out Sprite own) && own != null)
                return own;

            if (tileBackgroundSprite != null)
                return tileBackgroundSprite;

            return BackgroundColorFor(tile).a > 0f ? PlaceholderSprite.Square : null;
        }

        private Color BackgroundColorFor(Tile tile)
        {
            if (_backgroundColorByDefinition.TryGetValue(tile.Definition, out Color color) && color.a > 0f)
                return color;

            return tileBackgroundColor;
        }

        private void BuildColorTable()
        {
            _colorByDefinition.Clear();
            _spriteByDefinition.Clear();
            _backgroundByDefinition.Clear();
            _backgroundColorByDefinition.Clear();
            foreach (TileDefinitionSO so in tileVisuals)
            {
                if (so == null)
                    continue;
                _colorByDefinition[so.ToDefinition()] = so.PlaceholderColor;
                if (so.Sprite != null)
                    _spriteByDefinition[so.ToDefinition()] = so.Sprite;
                if (so.BackgroundSprite != null)
                    _backgroundByDefinition[so.ToDefinition()] = so.BackgroundSprite;
                _backgroundColorByDefinition[so.ToDefinition()] = so.BackgroundColor;
            }
        }

        private Transform CreateRoot(string rootName)
        {
            var root = new GameObject(rootName).transform;
            root.SetParent(transform, false);
            return root;
        }

        private void CreateCellBackground(Transform parent, GridPos pos)
        {
            var go = new GameObject($"Cell_{pos.X}_{pos.Y}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = GridToLocal(pos);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = cellSprite != null ? cellSprite : PlaceholderSprite.Square;
            renderer.color = cellSprite != null
                ? Color.white
                : (pos.X + pos.Y) % 2 == 0 ? cellColorA : cellColorB;
            renderer.sortingOrder = -10;
        }

        private IEnumerator WhenAll(List<IEnumerator> routines)
        {
            int remaining = routines.Count;
            foreach (IEnumerator routine in routines)
                StartCoroutine(RunAndSignal(routine, () => remaining--));
            while (remaining > 0)
                yield return null;
        }

        private static IEnumerator RunAndSignal(IEnumerator inner, Action onDone)
        {
            yield return inner;
            onDone();
        }
    }
}
