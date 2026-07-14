using System;
using System.Collections.Generic;
using ChainRiposte.Core.Board;
using ChainRiposte.Core.Stage;

namespace ChainRiposte.Core.Match
{
    /// <summary>
    /// 퍼즐 페이즈의 규칙 엔진. 보드 초기화와 스왑→매치→중력→리필의 연쇄 해석을 담당한다.
    /// 영혼석 적립(PlayerStats)·HP 회복·보스 난입은 이 엔진의 이벤트/결과를 구독하는 상위 레이어가 처리한다.
    /// </summary>
    public sealed class PuzzleEngine
    {
        private const int MaxRerolls = 16;    // 초기 배치 시 즉시 매치 회피 재추첨 상한
        private const int MaxCascades = 100;  // 연쇄 폭주 안전 상한

        private readonly ITileSpawner _spawner;
        private readonly float _comboMultiplierStep;

        public BoardGrid Board { get; }
        public int TurnsRemaining { get; private set; }
        public bool OutOfTurns => TurnsRemaining <= 0;

        public event Action<int> TurnsChanged;
        public event Action<SwapResult> SwapResolved;

        public PuzzleEngine(StageConfig config, ITileSpawner spawner)
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
        }

        /// <summary>
        /// 인접 두 타일의 스왑을 시도한다. 매치가 없으면 롤백되고 턴을 소모하지 않는다.
        /// 성공 시 연쇄 전체를 해석해 단계별 기록을 반환한다.
        /// </summary>
        public SwapResult TrySwap(GridPos a, GridPos b)
        {
            if (!AreSwappable(a, b))
                return SwapResult.Failed(a, b);

            SwapTiles(a, b);

            List<CascadeStep> steps = ResolveCascades();
            if (steps.Count == 0)
            {
                SwapTiles(a, b); // 롤백 — 턴 미소모
                return SwapResult.Failed(a, b);
            }

            TurnsRemaining--;
            TurnsChanged?.Invoke(TurnsRemaining);

            var result = SwapResult.Resolved(a, b, steps);
            SwapResolved?.Invoke(result);
            return result;
        }

        private bool AreSwappable(GridPos a, GridPos b)
        {
            bool adjacent = Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) == 1;
            return adjacent
                && MatchFinder.IsMatchable(Board.GetTile(a))
                && MatchFinder.IsMatchable(Board.GetTile(b));
        }

        private void SwapTiles(GridPos a, GridPos b)
        {
            Tile tileA = Board.RemoveTile(a);
            Tile tileB = Board.RemoveTile(b);
            Board.PlaceTile(a, tileB);
            Board.PlaceTile(b, tileA);
        }

        private List<CascadeStep> ResolveCascades()
        {
            var steps = new List<CascadeStep>();

            while (steps.Count < MaxCascades)
            {
                IReadOnlyList<MatchGroup> matches = MatchFinder.FindAll(Board);
                if (matches.Count == 0)
                    break;

                int combo = steps.Count + 1;
                int baseSouls = 0;
                int potionCount = 0;
                var clearedPositions = new HashSet<GridPos>();

                foreach (MatchGroup group in matches)
                {
                    foreach (GridPos pos in group.Positions)
                    {
                        if (!clearedPositions.Add(pos))
                            continue;

                        if (group.Definition.Category == TileCategory.Monster)
                            baseSouls += group.Definition.BaseSouls;
                        else if (group.Definition.Category == TileCategory.Potion)
                            potionCount++;
                    }
                }

                IReadOnlyList<WallHit> wallHits = ApplyWallDamage(clearedPositions);

                foreach (GridPos pos in clearedPositions)
                    Board.RemoveTile(pos);

                IReadOnlyList<FallPhase> fallPhases = GravityResolver.Settle(Board, _spawner);

                float multiplier = 1f + _comboMultiplierStep * (combo - 1);
                int soulsEarned = (int)Math.Round(baseSouls * multiplier);

                steps.Add(new CascadeStep(combo, matches, wallHits, soulsEarned, potionCount, fallPhases));
            }

            return steps;
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
