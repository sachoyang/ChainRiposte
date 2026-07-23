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

        [Header("타일 배경판 — 아이콘만 있으면 타일 경계가 안 읽힌다")]
        [Tooltip("타일 SO에 전용 배경이 없을 때 쓰는 공용 받침. 비워도 SO의 배경 색만 넣으면 사각 받침이 깔린다.")]
        [SerializeField] private Sprite tileBackgroundSprite;
        [Tooltip("받침 크기 (1 = 아이콘과 같은 크기). 살짝 커야 받침으로 읽힌다.")]
        [SerializeField, Min(0.1f)] private float tileBackgroundScale = 1.05f;
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
        private IEnumerator PlayGimmickPhase(GimmickPhase phase)
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
            }
        }

        private IEnumerator PlayClear(CascadeStep step)
        {
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

        /// <summary>보드 위 보스 타일의 카운트다운 표시를 갱신한다.</summary>
        public void UpdateBossCountdown(long tileId, float seconds, int turns)
        {
            if (TryFindView(tileId, out TileView view))
                view.SetCountdown(seconds, turns);
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
                BackgroundScale = tileBackgroundScale,
            });
            if (tile.Category == TileCategory.Wall && wallDamageSprites.Length > 0)
                view.SetWallStages(wallDamageSprites);
            view.transform.localPosition = GridToLocal(pos);
            view.ApplyStatus(tile, chainSprite); // 사슬/폭탄 뱃지 (GDD §3.6)
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
