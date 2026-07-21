using System;
using System.Collections.Generic;
using ChainRiposte.Core.Board;
using ChainRiposte.Core.Stage;
using ChainRiposte.Core.Stage.Gimmicks;

namespace ChainRiposte.Core.Match
{
    /// <summary>
    /// 퍼즐 페이즈의 규칙 엔진. 보드 초기화와 스왑→매치→중력→리필의 연쇄 해석을 담당한다.
    /// 스테이지 기믹(GDD §3.6)은 조합형 모듈로 붙어 정해진 훅에서 보드를 변형한다.
    /// 영혼석 적립(PlayerStats)·HP 회복/피해·보스 난입은 이 엔진의 이벤트/결과를 구독하는 상위 레이어가 처리한다.
    /// </summary>
    public sealed class PuzzleEngine
    {
        private const int MaxRerolls = 16;    // 초기 배치 시 즉시 매치 회피 재추첨 상한
        private const int MaxCascades = 100;  // 연쇄 폭주 안전 상한

        private readonly ITileSpawner _spawner;
        private readonly float _comboMultiplierStep;
        private readonly IReadOnlyList<IStageGimmick> _gimmicks;
        private readonly GimmickContext _gimmickContext;

        public BoardGrid Board { get; }
        public int TurnsRemaining { get; private set; }
        public bool OutOfTurns => TurnsRemaining <= 0;

        public event Action<int> TurnsChanged;
        public event Action<SwapResult> SwapResolved;

        public PuzzleEngine(StageConfig config, ITileSpawner spawner, Random rng = null)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            _spawner = spawner ?? throw new ArgumentNullException(nameof(spawner));
            _comboMultiplierStep = config.ComboSoulMultiplierStep;
            TurnsRemaining = config.TurnLimit;
            Board = config.CreateBoard();

            var wallDefinition = new TileDefinition("Wall", TileCategory.Wall, maxHp: config.WallHp);
            foreach (GridPos wallPos in config.WallPositions)
                Board.PlaceTile(wallPos, new Tile(wallDefinition));

            FillInitialBoard();

            _gimmicks = GimmickFactory.CreateAll(config.Gimmicks);
            if (_gimmicks.Count == 0)
                return;

            _gimmickContext = new GimmickContext(Board, rng ?? new Random(), config.GimmickSettings);
            foreach (IStageGimmick gimmick in _gimmicks)
                gimmick.OnBoardInitialized(_gimmickContext);
            _gimmickContext.BeginTurn(); // 초기 배치 기록은 연출 대상이 아니다
        }

        /// <summary>
        /// 인접 두 타일의 스왑을 시도한다. 매치가 없으면 롤백되고 턴을 소모하지 않는다.
        /// 성공 시 연쇄 전체와 턴 종료 기믹 처리를 해석해 기록을 반환한다.
        /// </summary>
        public SwapResult TrySwap(GridPos a, GridPos b)
        {
            if (!AreSwappable(a, b))
                return SwapResult.Failed(a, b);

            SwapTiles(a, b);
            _gimmickContext?.BeginTurn();

            var steps = new List<CascadeStep>();
            ResolveCascadesInto(steps);
            if (steps.Count == 0)
            {
                SwapTiles(a, b); // 롤백 — 턴 미소모
                return SwapResult.Failed(a, b);
            }

            TurnsRemaining--;
            TurnsChanged?.Invoke(TurnsRemaining);

            var result = SwapResult.Resolved(a, b, steps, RunTurnEndGimmicks());
            SwapResolved?.Invoke(result);
            return result;
        }

        private bool AreSwappable(GridPos a, GridPos b)
        {
            bool adjacent = Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) == 1;
            return adjacent && IsSwappable(Board.GetTile(a)) && IsSwappable(Board.GetTile(b));
        }

        /// <summary>사슬에 결박된 타일은 그 자리에 고정 — 스왑 불가 (GDD §3.6).</summary>
        private static bool IsSwappable(Tile tile) => MatchFinder.IsMatchable(tile) && !tile.Status.Chained;

        private void SwapTiles(GridPos a, GridPos b)
        {
            Tile tileA = Board.RemoveTile(a);
            Tile tileB = Board.RemoveTile(b);
            Board.PlaceTile(a, tileB);
            Board.PlaceTile(b, tileA);
        }

        /// <summary>매치가 없어질 때까지 연쇄를 해석해 steps에 이어 붙인다 (콤보는 목록 길이 기준).</summary>
        private void ResolveCascadesInto(List<CascadeStep> steps)
        {
            while (steps.Count < MaxCascades)
            {
                IReadOnlyList<MatchGroup> matches = MatchFinder.FindAll(Board);
                if (matches.Count == 0)
                    break;

                var cleared = new HashSet<GridPos>();
                foreach (MatchGroup group in matches)
                    foreach (GridPos pos in group.Positions)
                        cleared.Add(pos);

                // 기믹이 파괴 대상을 고칠 수 있다 — 사슬은 살아남고, 인접 부패는 함께 탄다
                foreach (IStageGimmick gimmick in _gimmicks)
                    gimmick.OnMatchesResolving(_gimmickContext, cleared);

                int combo = steps.Count + 1;
                if (cleared.Count == 0)
                {
                    // 매치가 통째로 사슬에 막혔다 — 사슬만 풀고 이번 스왑은 여기서 마감한다
                    // (그대로 두면 같은 매치를 무한히 다시 찾는다)
                    steps.Add(new CascadeStep(
                        combo, matches, Array.Empty<GridPos>(), Array.Empty<WallHit>(), TakeGimmickEvents(),
                        soulsEarned: 0, potionCount: 0, Array.Empty<FallPhase>()));
                    break;
                }

                (int baseSouls, int potionCount) = Collect(cleared);
                IReadOnlyList<WallHit> wallHits = ApplyWallDamage(cleared);

                foreach (GridPos pos in cleared)
                    Board.RemoveTile(pos);

                IReadOnlyList<FallPhase> fallPhases = SettleAndNotify();

                float multiplier = 1f + _comboMultiplierStep * (combo - 1);
                int soulsEarned = (int)Math.Round(baseSouls * multiplier);

                // 이벤트는 리필까지 끝난 뒤에 걷는다 — 새로 스폰된 폭탄/사슬도 이 단계의 기록에 들어간다
                steps.Add(new CascadeStep(
                    combo, matches, new List<GridPos>(cleared), wallHits, TakeGimmickEvents(),
                    soulsEarned, potionCount, fallPhases));
            }
        }

        private IReadOnlyList<GimmickEvent> TakeGimmickEvents() =>
            _gimmickContext?.TakeEvents() ?? Array.Empty<GimmickEvent>();

        /// <summary>파괴 확정된 칸에서 영혼석/물약 수를 집계한다 (기믹 필터가 끝난 뒤에 부른다).</summary>
        private (int souls, int potions) Collect(HashSet<GridPos> cleared)
        {
            int souls = 0;
            int potions = 0;

            foreach (GridPos pos in cleared)
            {
                Tile tile = Board.GetTile(pos);
                if (tile == null)
                    continue;

                if (tile.Category == TileCategory.Monster)
                    souls += tile.Definition.BaseSouls;
                else if (tile.Category == TileCategory.Potion)
                    potions++;
            }

            return (souls, potions);
        }

        /// <summary>턴 종료 기믹(확산·폭발)을 돌리고, 그 여파(낙하·새 연쇄)까지 해석한다.</summary>
        private GimmickPhase RunTurnEndGimmicks()
        {
            if (_gimmicks.Count == 0)
                return GimmickPhase.Empty;

            foreach (IStageGimmick gimmick in _gimmicks)
                gimmick.OnTurnEnded(_gimmickContext);

            IReadOnlyList<GimmickEvent> events = _gimmickContext.TakeEvents();
            int damage = _gimmickContext.PlayerDamage;

            if (!_gimmickContext.BoardChanged)
                return new GimmickPhase(events, damage, Array.Empty<FallPhase>(), Array.Empty<CascadeStep>());

            IReadOnlyList<FallPhase> fallPhases = SettleAndNotify();
            var cascades = new List<CascadeStep>();
            ResolveCascadesInto(cascades);

            return new GimmickPhase(events, damage, fallPhases, cascades);
        }

        /// <summary>보드를 정착시키고, 새로 스폰된 타일을 기믹에게 알린다 (폭탄/사슬 부착 훅).</summary>
        private IReadOnlyList<FallPhase> SettleAndNotify()
        {
            IReadOnlyList<FallPhase> phases = GravityResolver.Settle(Board, _spawner);
            if (_gimmicks == null || _gimmicks.Count == 0 || phases.Count == 0)
                return phases;

            var spawns = new List<TileSpawn>();
            foreach (FallPhase phase in phases)
                spawns.AddRange(phase.Spawns);

            if (spawns.Count == 0)
                return phases;

            foreach (IStageGimmick gimmick in _gimmicks)
                gimmick.OnTilesSpawned(_gimmickContext, spawns);

            return phases;
        }

        /// <summary>매치로 파괴된 타일 1개당 인접한 벽에 피해 1을 준다.</summary>
        private IReadOnlyList<WallHit> ApplyWallDamage(HashSet<GridPos> clearedPositions)
        {
            var damagePerWall = new Dictionary<GridPos, int>();
            foreach (GridPos cleared in clearedPositions)
            {
                foreach (GridPos neighbor in Board.ActiveNeighbors4(cleared))
                {
                    Tile tile = Board.GetTile(neighbor);
                    if (tile != null && tile.Category == TileCategory.Wall)
                        damagePerWall[neighbor] = damagePerWall.TryGetValue(neighbor, out int d) ? d + 1 : 1;
                }
            }

            if (damagePerWall.Count == 0)
                return Array.Empty<WallHit>();

            var hits = new List<WallHit>(damagePerWall.Count);
            foreach (KeyValuePair<GridPos, int> entry in damagePerWall)
            {
                Tile wall = Board.GetTile(entry.Key);
                bool destroyed = wall.ApplyDamage(entry.Value);
                if (destroyed)
                    Board.RemoveTile(entry.Key);
                hits.Add(new WallHit(entry.Key, entry.Value, destroyed));
            }

            return hits;
        }

        /// <summary>초기 배치. 시작부터 매치가 존재하지 않도록 즉시 매치를 만드는 종류는 재추첨한다.</summary>
        private void FillInitialBoard()
        {
            foreach (GridPos pos in Board.ActivePositions())
            {
                if (Board.IsOccupied(pos))
                    continue; // 초기 벽

                TileDefinition def = _spawner.NextDefinition();
                for (int attempt = 0; attempt < MaxRerolls && CreatesImmediateMatch(pos, def); attempt++)
                    def = _spawner.NextDefinition();

                Board.PlaceTile(pos, new Tile(def));
            }
        }

        /// <summary>ActivePositions는 아래 행부터 채우므로 왼쪽·아래 두 칸만 검사하면 된다.</summary>
        private bool CreatesImmediateMatch(GridPos pos, TileDefinition def) =>
            (HasSameDefinition(pos.Left, def) && HasSameDefinition(pos.Left.Left, def)) ||
            (HasSameDefinition(pos.Down, def) && HasSameDefinition(pos.Down.Down, def));

        private bool HasSameDefinition(GridPos pos, TileDefinition def)
        {
            Tile tile = Board.GetTile(pos);
            return tile != null && MatchFinder.IsMatchable(tile) && tile.Definition == def;
        }
    }
}
