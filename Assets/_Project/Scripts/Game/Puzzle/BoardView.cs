using System;
using System.Collections;
using System.Collections.Generic;
using ChainRiposte.Core.Board;
using ChainRiposte.Core.Match;
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
        [Header("타일 비주얼 (TileDefinitionSO 색상 매핑)")]
        [SerializeField] private TileDefinitionSO[] tileVisuals = Array.Empty<TileDefinitionSO>();
        [SerializeField] private Color wallColor = new(0.20f, 0.17f, 0.15f);
        [SerializeField] private Color bossColor = new(0.62f, 0.08f, 0.12f);
        [SerializeField] private Color unknownColor = Color.magenta;

        [Header("배경 셀 (체커 2색)")]
        [SerializeField] private Color cellColorA = new(0.16f, 0.15f, 0.19f);
        [SerializeField] private Color cellColorB = new(0.13f, 0.12f, 0.16f);

        [Header("연출 타이밍(초)")]
        [SerializeField, Min(0.01f)] private float swapDuration = 0.15f;
        [SerializeField, Min(0.01f)] private float clearDuration = 0.18f;
        [SerializeField, Min(0.01f)] private float fallDurationPerCell = 0.07f;
        [SerializeField, Min(0f)] private float stepPause = 0.05f;

        private readonly Dictionary<GridPos, TileView> _views = new();
        private readonly Dictionary<TileDefinition, Color> _colorByDefinition = new();
        private BoardGrid _board;
        private Transform _tileRoot;
        private Vector2 _originLocal; // (0,0) 셀의 로컬 위치 — 보드를 중앙 정렬

        public Bounds WorldBounds { get; private set; }

        /// <summary>캐스케이드 한 단계의 파괴 연출이 시작되는 순간 — 타일 깨짐 SFX/VFX 훅 (콤보는 step.ComboIndex).</summary>
        public event Action<CascadeStep> StepCleared;

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
            {
                StepCleared?.Invoke(step);
                yield return PlayClear(step);
                yield return PlayFallAndSpawn(step);
                if (stepPause > 0f)
                    yield return new WaitForSeconds(stepPause);
            }
        }

        private IEnumerator PlayClear(CascadeStep step)
        {
            var anims = new List<IEnumerator>();

            foreach (MatchGroup group in step.Matches)
            {
                foreach (GridPos pos in group.Positions)
                {
                    if (_views.Remove(pos, out TileView view))
                        anims.Add(view.ClearAndDestroy(clearDuration));
                }
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
        private IEnumerator PlayFallAndSpawn(CascadeStep step)
        {
            foreach (FallPhase phase in step.FallPhases)
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
            foreach (TileView view in _views.Values)
            {
                if (view.TileId == tileId)
                {
                    view.SetCountdown(seconds, turns);
                    return;
                }
            }
        }

        private TileView CreateTileView(Tile tile, GridPos pos)
        {
            var view = TileView.Create(_tileRoot, tile, ColorFor(tile));
            view.transform.localPosition = GridToLocal(pos);
            _views[pos] = view;
            return view;
        }

        private Color ColorFor(Tile tile)
        {
            switch (tile.Category)
            {
                case TileCategory.Wall: return wallColor;
                case TileCategory.Boss: return bossColor;
                default:
                    return _colorByDefinition.TryGetValue(tile.Definition, out Color color) ? color : unknownColor;
            }
        }

        private void BuildColorTable()
        {
            _colorByDefinition.Clear();
            foreach (TileDefinitionSO so in tileVisuals)
            {
                if (so != null)
                    _colorByDefinition[so.ToDefinition()] = so.PlaceholderColor;
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
            renderer.sprite = PlaceholderSprite.Square;
            renderer.color = (pos.X + pos.Y) % 2 == 0 ? cellColorA : cellColorB;
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
