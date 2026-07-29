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
        private const int MaxShuffles = 32;   // 데드락 해소 리롤 시도 상한

        private readonly ITileSpawner _spawner;
        private readonly Random _rng;
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
            _rng = rng ?? new Random();
            _comboMultiplierStep = config.ComboSoulMultiplierStep;
            TurnsRemaining = config.TurnLimit;
            Board = config.CreateBoard();

            var wallDefinition = new TileDefinition("Wall", TileCategory.Wall, maxHp: config.WallHp);
            foreach (GridPos wallPos in config.WallPositions)
                Board.PlaceTile(wallPos, new Tile(wallDefinition));

            FillInitialBoard();

            _gimmicks = GimmickFactory.CreateAll(config.Gimmicks);
            if (_gimmicks.Count > 0)
            {
                _gimmickContext = new GimmickContext(Board, _rng, config.GimmickSettings);
                foreach (IStageGimmick gimmick in _gimmicks)
                    gimmick.OnBoardInitialized(_gimmickContext);
                _gimmickContext.BeginTurn(); // 초기 배치 기록은 연출 대상이 아니다
            }

            // 사슬·부패가 깔린 뒤에 판정해야 한다 — 기믹이 보드를 조각내 시작부터 데드락일 수 있다.
            // 아직 아무것도 그려지지 않았으므로 이동 기록은 버린다.
            ShuffleIfDeadlocked();
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

            GimmickPhase gimmickPhase = RunTurnEndGimmicks();

            // 판이 굳었으면 여기서 푼다 — 이 게임은 매치 없는 스왑이 턴을 소모하지 않으므로
            // 수가 없으면 턴이 영영 안 넘어가고, 보스 카운트다운의 실시간 축만 흘러간다.
            var result = SwapResult.Resolved(a, b, steps, gimmickPhase, ShuffleIfDeadlocked());
            SwapResolved?.Invoke(result);
            return result;
        }

        private bool AreSwappable(GridPos a, GridPos b) => MoveFinder.AreSwappable(Board, a, b);

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

        /// <summary>
        /// 턴과 무관하게 흐르는 시간을 기믹에게 알린다 — <b>플레이어가 아무것도 안 해도</b>
        /// 자라는 위협(성난 몬스터)이 여기서 돈다. 일어난 일이 없으면 <see cref="GimmickPhase.Empty"/>.
        ///
        /// <para>부르는 쪽(Game)은 연출이 도는 동안에는 부르지 않는다 — 애니메이션 중에 보드가 바뀌면
        /// 화면과 모델이 어긋난다. 그래서 흐른 시간을 <b>엔진이 아니라 부르는 쪽이</b> 정한다.</para>
        /// </summary>
        public GimmickPhase TickTime(float deltaSeconds)
        {
            if (_gimmicks.Count == 0 || deltaSeconds <= 0f)
                return GimmickPhase.Empty;

            _gimmickContext.BeginTurn(); // 이번 호출의 기록만 담기게 비운다
            foreach (IStageGimmick gimmick in _gimmicks)
                gimmick.OnTimeElapsed(_gimmickContext, deltaSeconds);

            return CollectGimmickPhase();
        }

        /// <summary>턴 종료 기믹(확산·폭발)을 돌리고, 그 여파(낙하·새 연쇄)까지 해석한다.</summary>
        private GimmickPhase RunTurnEndGimmicks()
        {
            if (_gimmicks.Count == 0)
                return GimmickPhase.Empty;

            foreach (IStageGimmick gimmick in _gimmicks)
                gimmick.OnTurnEnded(_gimmickContext);

            return CollectGimmickPhase();
        }

        /// <summary>기믹이 기록한 것을 걷고, 보드를 건드렸다면 그 여파(낙하·새 연쇄)까지 해석한다.</summary>
        private GimmickPhase CollectGimmickPhase()
        {
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

        /// <summary>
        /// 둘 수 있는 수가 하나도 없으면 보드를 섞는다. 턴은 소모하지 않는다.
        /// 섞는 대상은 '움직일 수 있는 타일'뿐 — 벽·보스(하강 위치가 곧 게임플레이)·부패·사슬은 제자리다.
        /// 정의값이 아니라 Tile 객체째로 자리를 바꾸므로 폭탄 카운트 같은 상태는 따라간다.
        /// </summary>
        /// <returns>뷰가 재생할 이동 기록. 데드락이 아니었으면 비어 있다.</returns>
        private IReadOnlyList<TileMove> ShuffleIfDeadlocked()
        {
            if (MoveFinder.HasAnyValidMove(Board))
                return Array.Empty<TileMove>();

            var slots = new List<GridPos>();
            foreach (GridPos pos in Board.ActivePositions())
                if (MoveFinder.IsSwappable(Board.GetTile(pos)))
                    slots.Add(pos);

            if (slots.Count < 3)
                return Array.Empty<TileMove>(); // 섞을 것이 없다 — 어떻게 배치해도 매치가 안 나온다

            var tiles = new List<Tile>(slots.Count);
            var originBySlot = new Dictionary<long, GridPos>(slots.Count);
            foreach (GridPos pos in slots)
            {
                Tile tile = Board.GetTile(pos);
                tiles.Add(tile);
                originBySlot[tile.InstanceId] = pos;
            }

            for (int attempt = 0; attempt < MaxShuffles; attempt++)
            {
                ShuffleInPlace(tiles);
                Rearrange(slots, tiles);

                // 섞자마자 매치가 터져 있으면 공짜 콤보가 되므로 다시 뽑는다
                if (MatchFinder.FindAll(Board).Count == 0 && MoveFinder.HasAnyValidMove(Board))
                    break;
            }

            // 상한까지 실패했더라도 마지막 배치를 그대로 둔다 — 뷰와 모델은 어긋나지 않는다.
            // (자유 타일이 극단적으로 적은 판에서만 일어나며, 그건 스테이지 설계 쪽 문제다)
            var moves = new List<TileMove>();
            for (int i = 0; i < slots.Count; i++)
            {
                GridPos origin = originBySlot[tiles[i].InstanceId];
                if (!origin.Equals(slots[i]))
                    moves.Add(new TileMove(tiles[i], origin, slots[i]));
            }

            return moves;
        }

        /// <summary>Fisher–Yates. 주입된 Random을 쓰므로 테스트에서 결정적으로 재현된다.</summary>
        private void ShuffleInPlace(List<Tile> tiles)
        {
            for (int i = tiles.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (tiles[i], tiles[j]) = (tiles[j], tiles[i]);
            }
        }

        private void Rearrange(List<GridPos> slots, List<Tile> tiles)
        {
            foreach (GridPos pos in slots)
                Board.RemoveTile(pos);

            for (int i = 0; i < slots.Count; i++)
                Board.PlaceTile(slots[i], tiles[i]);
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
