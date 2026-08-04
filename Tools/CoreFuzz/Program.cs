using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using ChainRiposte.Core.Board;
using ChainRiposte.Core.Intrusion;
using ChainRiposte.Core.Match;
using ChainRiposte.Core.Stage;

namespace ChainRiposte.Tools.CoreFuzz
{
    /// <summary>
    /// ChainRiposte.Core 퍼즈 하네스.
    ///
    /// <para><b>Unity를 켜지 않는다.</b> Core에 UnityEngine 참조가 하나도 없기 때문에
    /// Core의 .cs를 그대로 콘솔 앱에 넣고 컴파일할 수 있다. 에디터 실행·도메인 리로드·
    /// 재생 모드가 전부 빠지므로 보드 수천 개를 초 단위로 돌린다.</para>
    ///
    /// <para>손으로 짠 테스트는 "내가 생각한 상황"만 확인한다. 퍼즈는 반대로
    /// <b>생각해 본 적 없는 보드</b>를 무작위로 만들어 실제로 플레이하고,
    /// 매 스텝마다 "이건 언제나 참이어야 한다"(불변식)만 검사한다.</para>
    /// </summary>
    internal static class Program
    {
        private const int DefaultBoards = 6000;
        private const int MaxTurnsPerBoard = 40;

        /// <summary>
        /// 벽 비율 고정값 (0~1). 음수면 보드마다 무작위로 뽑는다(평소).
        /// <b>갇힌 칸이 생기는 빈도를 사실상 이 값 하나가 정한다</b> — 벽이 없으면 갇힐 일도 없다.
        /// </summary>
        private static double _wallRatio = -1;

        /// <summary>구멍(비활성 칸) 비율 고정값 (0~1). 음수면 보드마다 무작위로 뽑는다(평소).</summary>
        private static double _holeRatio = -1;

        // ── 타일 팔레트 (Game 레이어의 TileDefinitionSO 대신 여기서 직접 만든다) ──
        private static readonly TileDefinition[] Monsters =
        {
            new("Rat",    TileCategory.Monster, baseSouls: 10),
            new("Bat",    TileCategory.Monster, baseSouls: 10),
            new("Skull",  TileCategory.Monster, baseSouls: 12),
            new("Ghoul",  TileCategory.Monster, baseSouls: 12),
            new("Imp",    TileCategory.Monster, baseSouls: 8),
        };

        private static readonly TileDefinition Potion = new("Potion", TileCategory.Potion);
        private static readonly TileDefinition BossTile = new("BossTile", TileCategory.Boss);

        private static int Main(string[] args)
        {
            TrySetUtf8();

            int boards = ParseBoards(args);
            var stats = new Stats();
            var invariants = Invariant.All();

            Header(boards);

            // ── 워밍업 ── JIT가 데워지지 않은 첫 판이 통계를 왜곡하지 않게 한다.
            Step(1, 3, "워밍업 (JIT)");
            for (int i = 0; i < 30; i++)
                RunBoard(-1 - i, new Stats(), Invariant.All());
            Done();

            Step(2, 3, "퍼즈 실행");
            Console.WriteLine();
            var clock = Stopwatch.StartNew();
            var failures = new List<Failure>();

            for (int board = 0; board < boards; board++)
            {
                Failure failure = RunBoard(board, stats, invariants);
                if (failure != null && failures.Count < 8)
                    failures.Add(failure);

                if (board % 64 == 0 || board == boards - 1)
                    Progress(board + 1, boards, clock.Elapsed);
            }

            clock.Stop();
            Progress(boards, boards, clock.Elapsed);
            Console.WriteLine();
            Console.WriteLine();

            Step(3, 3, "불변식 판정");
            Console.WriteLine();
            InvariantTable(invariants);
            Console.WriteLine();
            StatsBlock(stats, clock.Elapsed, boards);
            Console.WriteLine();
            SampleBoard(stats.SampleRows);
            Console.WriteLine();
            Caught();

            bool passed = failures.Count == 0;
            if (!passed)
                FailureBlock(failures);

            Verdict(passed, invariants, clock.Elapsed);
            return passed ? 0 : 1;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  한 판 = 무작위 보드 하나를 만들어 실제로 플레이한다
        // ═══════════════════════════════════════════════════════════════════

        /// <param name="seed">보드 번호가 곧 씨앗이다 — 실패하면 그 번호 하나로 똑같이 재현된다.</param>
        private static Failure RunBoard(int seed, Stats stats, Invariant[] invariants)
        {
            var rng = new Random(seed);
            BoardShape shape = BoardShape.Random(rng);
            StageConfig config = BuildConfig(shape, rng);

            PuzzleEngine engine;
            BoardGrid board;
            var spawnerRng = new Random(seed ^ 0x5EED);

            try
            {
                // 스포너를 보스 타일 데코레이터로 감싼다 — 갇힌 칸에 보스가 들어가는 사고를
                // 실제로 유발할 수 있는 유일한 구성이다.
                var live = new LiveBossCounter();
                var spawner = new BossTileSpawner(
                    new WeightedTileSpawner(config.SpawnWeights, spawnerRng),
                    BossTile,
                    config.BossChanceByScore,
                    config.BossChanceBySeconds,
                    scoreGetter: () => 0f,
                    secondsGetter: () => 30f,
                    liveBossCount: live.Count,
                    maxLive: config.MaxLiveBossTiles,
                    rng: spawnerRng);

                engine = new PuzzleEngine(config, spawner, new Random(seed ^ 0x1234));
                board = engine.Board;
                live.Bind(board);
            }
            catch (Exception ex)
            {
                return Failure.Crash(seed, "보드 생성", ex);
            }

            var context = new CheckContext(board, shape, seed);
            context.BeginStep();
            Failure initial = Check(invariants, context, stats, "초기 배치", turn: 0);
            if (initial != null)
                return initial;

            for (int turn = 1; turn <= MaxTurnsPerBoard; turn++)
            {
                try
                {
                    // 턴과 무관하게 흐르는 시간 — 성난 몬스터가 여기서 자란다.
                    if (rng.NextDouble() < 0.5)
                    {
                        context.BeginStep();
                        context.Collect(engine.TickTime(1.6f));

                        Failure ticked = Check(invariants, context, stats, "시간 경과", turn);
                        if (ticked != null)
                            return ticked;
                    }

                    if (!TryPickMove(board, rng, out GridPos a, out GridPos b))
                    {
                        stats.NoMoveBoards++;
                        break; // 둘 수 있는 수가 없다 — 이 판은 여기서 끝
                    }

                    context.BeginStep();
                    SwapResult result = engine.TrySwap(a, b);
                    context.Collect(result);
                    stats.Record(result);
                }
                catch (Exception ex)
                {
                    return Failure.Crash(seed, $"{turn}수째", ex);
                }

                Failure broke = Check(invariants, context, stats, $"{turn}수째", turn);
                if (broke != null)
                    return broke;
            }

            stats.Boards++;
            stats.RecordShape(shape);
            stats.SealedObserved += context.SealedObserved;
            stats.CaptureSample(board, shape, rng);
            return null;
        }

        /// <summary>
        /// 무작위로 둘 수 있는 수를 하나 고른다. 항상 같은 수(제일 왼쪽 아래)를 두면
        /// 보드가 늘 같은 방향으로만 무너져 커버리지가 좁아진다.
        /// </summary>
        private static bool TryPickMove(BoardGrid board, Random rng, out GridPos a, out GridPos b)
        {
            for (int attempt = 0; attempt < 60; attempt++)
            {
                var from = new GridPos(rng.Next(board.Width), rng.Next(board.Height));
                GridPos to = (rng.Next(2) == 0) ? from.Right : from.Up;

                if (CreatesMatch(board, from, to))
                {
                    (a, b) = (from, to);
                    return true;
                }
            }

            // 무작위로 못 찾았다고 수가 없는 것은 아니다 — 전수 조사로 확인한다.
            return MoveFinder.TryFindMove(board, out a, out b);
        }

        /// <summary>이 스왑이 매치를 만드는가. MoveFinder의 지역 검사와 같은 규칙.</summary>
        private static bool CreatesMatch(BoardGrid board, GridPos a, GridPos b)
        {
            if (!MoveFinder.AreSwappable(board, a, b))
                return false;

            Swap(board, a, b);
            bool matched = HasRunThrough(board, a) || HasRunThrough(board, b);
            Swap(board, a, b);
            return matched;
        }

        private static void Swap(BoardGrid board, GridPos a, GridPos b)
        {
            Tile tileA = board.RemoveTile(a);
            Tile tileB = board.RemoveTile(b);
            board.PlaceTile(a, tileB);
            board.PlaceTile(b, tileA);
        }

        private static bool HasRunThrough(BoardGrid board, GridPos pos)
        {
            Tile tile = board.GetTile(pos);
            if (!MatchFinder.IsMatchable(tile))
                return false;

            TileDefinition def = tile.Definition;
            return 1 + Run(board, pos, -1, 0, def) + Run(board, pos, 1, 0, def) >= 3
                || 1 + Run(board, pos, 0, -1, def) + Run(board, pos, 0, 1, def) >= 3;
        }

        private static int Run(BoardGrid board, GridPos from, int dx, int dy, TileDefinition def)
        {
            int count = 0;
            for (var pos = new GridPos(from.X + dx, from.Y + dy); ; pos = new GridPos(pos.X + dx, pos.Y + dy))
            {
                Tile tile = board.GetTile(pos);
                if (!MatchFinder.IsMatchable(tile) || tile.Definition != def)
                    return count;

                count++;
            }
        }

        private static Failure Check(Invariant[] invariants, CheckContext context, Stats stats, string where, int turn)
        {
            foreach (Invariant invariant in invariants)
            {
                invariant.Checks++;
                string broken = invariant.Rule(context);
                if (broken == null)
                    continue;

                invariant.Violations++;
                return Failure.Broken(context.Seed, where, turn, invariant, broken, context.Board);
            }

            stats.Steps++;
            return null;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  불변식 — "언제나 참이어야 하는 것". 깨지면 그 자리가 곧 버그다.
        // ═══════════════════════════════════════════════════════════════════

        private sealed class Invariant
        {
            public string Id;
            public string Title;
            public string Why;
            public Func<CheckContext, string> Rule; // null = 통과, 문자열 = 위반 설명
            public long Checks;
            public long Violations;

            public static Invariant[] All() => new[]
            {
                new Invariant
                {
                    Id = "I-1",
                    Title = "정착이 끝난 보드에 빈 칸이 없다",
                    Why = "빈 칸이 남으면 화면에 구멍으로 보인다",
                    Rule = ctx =>
                    {
                        foreach (GridPos pos in ctx.Board.ActivePositions())
                            if (!ctx.Board.IsOccupied(pos))
                                return $"({pos.X},{pos.Y})가 비어 있다";

                        return null;
                    },
                },
                new Invariant
                {
                    Id = "I-2",
                    Title = "한 타일이 두 칸에 동시에 있지 않다",
                    Why = "낙하 기록이 어긋나면 뷰가 타일을 복제한다",
                    Rule = ctx =>
                    {
                        ctx.Seen.Clear();
                        foreach (GridPos pos in ctx.Board.ActivePositions())
                        {
                            Tile tile = ctx.Board.GetTile(pos);
                            if (tile != null && !ctx.Seen.Add(tile.InstanceId))
                                return $"타일 #{tile.InstanceId}가 중복 배치됐다";
                        }

                        return null;
                    },
                },
                new Invariant
                {
                    Id = "I-3",
                    Title = "벽은 스스로 움직이지 않는다",
                    Why = "벽이 휩쓸리면 스테이지 설계가 무너진다",
                    Rule = ctx =>
                    {
                        foreach (GridPos pos in ctx.Board.ActivePositions())
                        {
                            Tile tile = ctx.Board.GetTile(pos);
                            if (tile != null && tile.Category == TileCategory.Wall && !ctx.Shape.Walls.Contains(pos))
                                return $"({pos.X},{pos.Y})에 없던 벽이 있다";
                        }

                        return null;
                    },
                },
                new Invariant
                {
                    Id = "I-4",
                    Title = "보스 타일이 동시 상한을 넘지 않는다",
                    Why = "상한이 없으면 한 웨이브에 보드를 도배한다",
                    Rule = ctx =>
                    {
                        int live = 0;
                        foreach (GridPos pos in ctx.Board.ActivePositions())
                        {
                            Tile tile = ctx.Board.GetTile(pos);
                            if (tile != null && tile.Category == TileCategory.Boss)
                                live++;
                        }

                        return live > ctx.Shape.MaxLiveBossTiles
                            ? $"보스 타일 {live}개 (상한 {ctx.Shape.MaxLiveBossTiles})"
                            : null;
                    },
                },
                new Invariant
                {
                    Id = "I-5",
                    Title = "갇힌 칸에는 평범한 타일만 생긴다",
                    Why = "중력이 닿지 않는 칸이라 손쓸 방법이 없다",
                    Rule = ctx =>
                    {
                        // 갇힌 칸 = 낙하·슬라이드·리필이 전부 끝났는데도 남은 빈 칸.
                        // 여기 보스·벽·부패가 들어가면 영영 안 없어지고 동시 상한만 잡아먹는다.
                        foreach (TileSpawn spawn in ctx.RecentSealed)
                            if (!MatchFinder.IsMatchable(spawn.Tile))
                                return $"({spawn.Position.X},{spawn.Position.Y})에 {spawn.Tile.Category} 타일이 생겼다";

                        return null;
                    },
                },
            };
        }

        /// <summary>불변식이 보는 것 한 벌. 판마다 새로 만들고 매 스텝 재사용한다.</summary>
        private sealed class CheckContext
        {
            public readonly BoardGrid Board;
            public readonly BoardShape Shape;
            public readonly int Seed;
            public readonly HashSet<long> Seen = new();

            /// <summary>직전 스텝에 '갇힌 칸'으로 생겨난 타일들 (I-5가 본다).</summary>
            public readonly List<TileSpawn> RecentSealed = new();

            public long SealedObserved;

            public CheckContext(BoardGrid board, BoardShape shape, int seed)
            {
                Board = board;
                Shape = shape;
                Seed = seed;
            }

            public void BeginStep() => RecentSealed.Clear();

            public void Collect(IReadOnlyList<FallPhase> phases)
            {
                foreach (FallPhase phase in phases)
                    foreach (TileSpawn spawn in phase.Spawns)
                        if (spawn.Sealed)
                        {
                            RecentSealed.Add(spawn);
                            SealedObserved++;
                        }
            }

            public void Collect(SwapResult result)
            {
                foreach (CascadeStep step in result.Steps)
                    Collect(step.FallPhases);

                Collect(result.Gimmicks);
            }

            public void Collect(GimmickPhase phase)
            {
                Collect(phase.FallPhases);
                foreach (CascadeStep step in phase.Cascades)
                    Collect(step.FallPhases);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  무작위 보드 생성
        // ═══════════════════════════════════════════════════════════════════

        private sealed class BoardShape
        {
            public bool[,] Mask;
            public HashSet<GridPos> Walls;
            public int Width;
            public int Height;
            public int ActiveCells;
            public int MaxLiveBossTiles;
            public GimmickType[] Gimmicks;

            public static BoardShape Random(Random rng)
            {
                int width = rng.Next(6, 10);   // 6~9
                int height = rng.Next(7, 11);  // 7~10
                var mask = new bool[width, height];
                var walls = new HashSet<GridPos>();

                // 구멍(비정형 보드) — 하트·해골 같은 손그림 모양을 흉내 낸다
                double holeChance = _holeRatio >= 0
                    ? _holeRatio
                    : (rng.Next(3) == 0 ? 0.0 : rng.NextDouble() * 0.18);
                int active = 0;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        bool hole = y > 0 && rng.NextDouble() < holeChance; // 바닥 행은 항상 남긴다
                        mask[x, y] = !hole;
                        if (!hole)
                            active++;
                    }
                }

                // 벽 — 부서지면 그 자리가 '갇힌 칸'이 되는 주범이라 넉넉히 뿌린다
                int wallCount = _wallRatio >= 0
                    ? (int)Math.Round(active * _wallRatio)
                    : rng.Next(0, Math.Max(1, active / 8));
                for (int i = 0; i < wallCount; i++)
                {
                    var pos = new GridPos(rng.Next(width), rng.Next(1, height));
                    if (mask[pos.X, pos.Y])
                        walls.Add(pos);
                }

                return new BoardShape
                {
                    Mask = mask,
                    Walls = walls,
                    Width = width,
                    Height = height,
                    ActiveCells = active,
                    MaxLiveBossTiles = rng.Next(1, 3),
                    Gimmicks = RandomGimmicks(rng),
                };
            }

            private static GimmickType[] RandomGimmicks(Random rng)
            {
                var list = new List<GimmickType>();
                if (rng.NextDouble() < 0.45) list.Add(GimmickType.LockedTiles);
                if (rng.NextDouble() < 0.40) list.Add(GimmickType.TickingDeath);
                if (rng.NextDouble() < 0.30) list.Add(GimmickType.SpreadingCorruption);
                return list.ToArray();
            }
        }

        private static StageConfig BuildConfig(BoardShape shape, Random rng)
        {
            var weights = new List<TileSpawnWeight>();
            foreach (TileDefinition monster in Monsters)
                weights.Add(new TileSpawnWeight(monster, 1f + (float)rng.NextDouble()));
            weights.Add(new TileSpawnWeight(Potion, 0.4f));

            float bossChance = 0.02f + (float)rng.NextDouble() * 0.10f;

            return new StageConfig
            {
                ActiveMask = shape.Mask,
                WallPositions = new List<GridPos>(shape.Walls),
                WallHp = rng.Next(1, 4),
                TurnLimit = 0,
                SpawnWeights = weights,
                Gimmicks = shape.Gimmicks,
                MaxLiveBossTiles = shape.MaxLiveBossTiles,
                BossChanceByScore = _ => bossChance,
                BossChanceBySeconds = _ => bossChance,
                GimmickSettings = new GimmickSettings
                {
                    CorruptionSeeds = rng.Next(1, 4),
                    ChainInitialCount = rng.Next(0, 5),
                    ChainChance = (float)rng.NextDouble() * 0.15f,
                    BombChance = (float)rng.NextDouble() * 0.20f,
                    EnrageChance = (float)rng.NextDouble() * 0.6f,
                },
            };
        }

        /// <summary>보드 위의 보스 타일 수를 세어 스포너의 동시 상한에 넘긴다.</summary>
        private sealed class LiveBossCounter
        {
            private BoardGrid _board;

            public void Bind(BoardGrid board) => _board = board;

            public int Count()
            {
                if (_board == null)
                    return 0;

                int live = 0;
                foreach (GridPos pos in _board.ActivePositions())
                {
                    Tile tile = _board.GetTile(pos);
                    if (tile != null && tile.Category == TileCategory.Boss)
                        live++;
                }

                return live;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  통계
        // ═══════════════════════════════════════════════════════════════════

        private sealed class Stats
        {
            public long Boards;
            public long Steps;
            public long Swaps;
            public long Cascades;
            public long TilesCleared;
            public long FallPhases;
            public long Spawns;
            public long SealedSpawns;   // 갇힌 칸에 제자리 생성된 타일 (TileSpawn.Sealed — 정확한 값)
            public long WallsDestroyed;
            public long Shuffles;
            public long NoMoveBoards;
            public long SealedObserved;

            // 실제로 만들어진 보드의 밀도 — 갇힌 칸 비율이 여기에 딸려 움직인다
            public long GridCells;
            public long ActiveCells;
            public long WallCells;

            public void RecordShape(BoardShape shape)
            {
                GridCells += shape.Width * shape.Height;
                ActiveCells += shape.ActiveCells;
                WallCells += shape.Walls.Count;
            }
            public int MaxCombo;
            public string[] SampleRows;

            public void Record(SwapResult result)
            {
                if (!result.Success)
                    return;

                Swaps++;
                Cascades += result.Steps.Count;
                if (result.ComboCount > MaxCombo)
                    MaxCombo = result.ComboCount;
                if (result.Shuffled)
                    Shuffles++;

                foreach (CascadeStep step in result.Steps)
                {
                    TilesCleared += step.ClearedPositions.Count;
                    foreach (WallHit hit in step.WallHits)
                        if (hit.Destroyed)
                            WallsDestroyed++;

                    CountFalls(step.FallPhases);
                }

                foreach (CascadeStep step in result.Gimmicks.Cascades)
                {
                    TilesCleared += step.ClearedPositions.Count;
                    CountFalls(step.FallPhases);
                }

                CountFalls(result.Gimmicks.FallPhases);
            }

            private void CountFalls(IReadOnlyList<FallPhase> phases)
            {
                foreach (FallPhase phase in phases)
                {
                    FallPhases++;

                    foreach (TileSpawn spawn in phase.Spawns)
                    {
                        Spawns++;
                        if (spawn.Sealed)
                            SealedSpawns++;
                    }
                }
            }

            /// <summary>PPT용 — 실제로 돌린 보드 하나를 그대로 찍어 둔다.</summary>
            public void CaptureSample(BoardGrid board, BoardShape shape, Random rng)
            {
                // 벽이 살아남고 구멍도 있는 판을 골라 남긴다 —
                // 이 하네스가 무엇을 흔드는지 한눈에 보이는 그림이라야 한다.
                if (shape.ActiveCells == shape.Width * shape.Height || !HasWall(board))
                    return;

                if (SampleRows != null && rng.Next(20) != 0)
                    return;

                var rows = new string[board.Height];
                for (int row = 0; row < board.Height; row++)
                {
                    int y = board.Height - 1 - row;
                    var sb = new StringBuilder();
                    for (int x = 0; x < board.Width; x++)
                    {
                        var pos = new GridPos(x, y);
                        sb.Append(!board.IsActive(pos) ? "  " : Glyph(board.GetTile(pos)) + " ");
                    }

                    rows[row] = sb.ToString().TrimEnd();
                }

                SampleRows = rows;
            }

            private static bool HasWall(BoardGrid board)
            {
                foreach (GridPos pos in board.ActivePositions())
                {
                    Tile tile = board.GetTile(pos);
                    if (tile != null && tile.Category == TileCategory.Wall)
                        return true;
                }

                return false;
            }

            private static string Glyph(Tile tile)
            {
                if (tile == null)
                    return "·";
                if (tile.Category == TileCategory.Wall)
                    return "▩";
                if (tile.Category == TileCategory.Boss)
                    return "◆";
                if (tile.Category == TileCategory.Corruption)
                    return "✳";
                if (tile.Category == TileCategory.Bomb)
                    return "◉";
                if (tile.Status.Chained)
                    return "⛓";
                if (tile.Status.IsEnraged)
                    return "!";
                if (tile.Category == TileCategory.Potion)
                    return "♥";

                return tile.Definition.Id.Substring(0, 1).ToLowerInvariant();
            }
        }

        private sealed class Failure
        {
            public int Seed;
            public string Where;
            public string What;

            public static Failure Crash(int seed, string where, Exception ex) =>
                new() { Seed = seed, Where = where, What = $"{ex.GetType().Name}: {ex.Message}" };

            public static Failure Broken(int seed, string where, int turn, Invariant invariant, string detail, BoardGrid board) =>
                new() { Seed = seed, Where = $"{where}", What = $"{invariant.Id} 위반 — {detail}" };
        }

        // ═══════════════════════════════════════════════════════════════════
        //  출력
        // ═══════════════════════════════════════════════════════════════════

        private const int Wide = 78;

        private static void Header(int boards)
        {
            Console.WriteLine();
            Color(ConsoleColor.Cyan);
            Console.WriteLine("╭" + new string('─', Wide) + "╮");
            Console.WriteLine("│" + Pad("  ChainRiposte · Core 퍼즈 하네스", Wide - 12) + "net8.0 콘솔  │");
            Console.WriteLine("╰" + new string('─', Wide) + "╯");
            Reset();

            Field("대상", "ChainRiposte.Core — .cs 59개 · UnityEngine 참조 0개");
            Field("실행", "Unity 에디터 없이 콘솔에서 단독 컴파일·실행 (재생 모드·도메인 리로드 없음)");
            Field("방식", "무작위 보드 생성 → 무작위 수로 실제 플레이 → 매 스텝 불변식 5종 검사");
            Field("규모", $"보드 {boards:N0}개 × 최대 {MaxTurnsPerBoard}수 · 씨앗 = 보드 번호(실패 시 그대로 재현)");
            Console.WriteLine();
        }

        private static void Field(string key, string value)
        {
            Color(ConsoleColor.DarkGray);
            Console.Write($" {key}   ");
            Reset();
            Console.WriteLine(value);
        }

        private static void Step(int index, int total, string title)
        {
            Color(ConsoleColor.Yellow);
            Console.Write($" [{index}/{total}] ");
            Reset();
            Console.Write(title);
        }

        private static void Done()
        {
            Color(ConsoleColor.DarkGray);
            Console.Write(" " + new string('.', Math.Max(1, 46 - 14)));
            Reset();
            Color(ConsoleColor.Green);
            Console.WriteLine(" 완료");
            Reset();
            Console.WriteLine();
        }

        private static void Progress(int done, int total, TimeSpan elapsed)
        {
            const int Cells = 44;
            int filled = (int)((double)done / total * Cells);
            double perSecond = elapsed.TotalSeconds > 0 ? done / elapsed.TotalSeconds : 0;

            Console.Write("\r   ");
            Color(ConsoleColor.Green);
            Console.Write(new string('█', filled));
            Color(ConsoleColor.DarkGray);
            Console.Write(new string('░', Cells - filled));
            Reset();
            Console.Write($"  {done,6:N0}/{total:N0} boards   {elapsed.TotalSeconds,5:F1}초   {perSecond,6:F0} boards/s");
        }

        private static void InvariantTable(Invariant[] invariants)
        {
            string top = "┌──────┬────────────────────────────────────────────────┬────────────┬────────┐";
            string mid = "├──────┼────────────────────────────────────────────────┼────────────┼────────┤";
            string bot = "└──────┴────────────────────────────────────────────────┴────────────┴────────┘";

            Color(ConsoleColor.DarkGray);
            Console.WriteLine(" " + top);
            Reset();
            Console.WriteLine($" │ {"ID",-4} │ {Pad("불변식", 46)} │ {PadLeft("검사", 10)} │ {Pad("결과", 6)} │");
            Color(ConsoleColor.DarkGray);
            Console.WriteLine(" " + mid);
            Reset();

            foreach (Invariant invariant in invariants)
            {
                bool ok = invariant.Violations == 0;
                Console.Write($" │ {invariant.Id,-4} │ {Pad(invariant.Title, 46)} │ {invariant.Checks,10:N0} │ ");
                Color(ok ? ConsoleColor.Green : ConsoleColor.Red);
                Console.Write(ok ? " PASS " : " FAIL ");
                Reset();
                Console.WriteLine(" │");

                Color(ConsoleColor.DarkGray);
                Console.WriteLine($" │ {"",-4} │ {Pad("↳ " + invariant.Why, 46)} │ {"",10} │ {"",6} │");
                Reset();
            }

            Color(ConsoleColor.DarkGray);
            Console.WriteLine(" " + bot);
            Reset();
        }

        private static void StatsBlock(Stats stats, TimeSpan elapsed, int boards)
        {
            long normalSpawns = stats.Spawns - stats.SealedSpawns;
            double normalRatio = stats.Spawns > 0 ? normalSpawns * 100.0 / stats.Spawns : 0;
            double sealedRatio = stats.Spawns > 0 ? stats.SealedSpawns * 100.0 / stats.Spawns : 0;

            Color(ConsoleColor.Cyan);
            Console.WriteLine(" 실측");
            Reset();
            Row("완주한 보드", $"{stats.Boards:N0}개", $"수 없음으로 조기 종료 {stats.NoMoveBoards:N0}");
            Row("해석한 스왑", $"{stats.Swaps:N0}회", $"최대 연쇄 {stats.MaxCombo}단 · 데드락 자동 리롤 {stats.Shuffles:N0}회");
            Row("사라진 타일", $"{stats.TilesCleared:N0}개", $"부서진 벽 {stats.WallsDestroyed:N0}개");
            Row("낙하 웨이브", $"{stats.FallPhases:N0}회", $"새로 생긴 타일 {stats.Spawns:N0}개");
            Row("불변식 검사", $"{stats.Steps:N0}스텝", $"{elapsed.TotalSeconds:F1}초 · 보드당 {elapsed.TotalMilliseconds / Math.Max(1, boards):F2}ms");

            double wallPercent = stats.ActiveCells > 0 ? stats.WallCells * 100.0 / stats.ActiveCells : 0;
            double holePercent = stats.GridCells > 0 ? (stats.GridCells - stats.ActiveCells) * 100.0 / stats.GridCells : 0;
            Row("보드 밀도", $"벽 {wallPercent:F1}%", $"구멍 {holePercent:F1}% · 활성 칸 평균 {(double)stats.ActiveCells / Math.Max(1, stats.Boards):F0}개");
            Console.WriteLine();

            Color(ConsoleColor.Cyan);
            Console.WriteLine($" 새 타일이 들어온 경로   (총 {stats.Spawns:N0}개)");
            Reset();
            Bar("위에서 떨어짐 (평범한 리필)", normalRatio, normalSpawns, ConsoleColor.Green);
            Bar("제자리 생성 (갇힌 칸 · 최후의 수단)", sealedRatio, stats.SealedSpawns, ConsoleColor.Yellow);
        }

        private static void Row(string key, string value, string note)
        {
            Color(ConsoleColor.DarkGray);
            Console.Write("   " + Pad(key, 14));
            Reset();
            Console.Write(Pad(value, 14));
            Color(ConsoleColor.DarkGray);
            Console.WriteLine(note);
            Reset();
        }

        private static void Bar(string label, double percent, long count, ConsoleColor color)
        {
            const int Cells = 30;
            int filled = (int)Math.Round(percent / 100.0 * Cells);

            Console.Write("   " + Pad(label, 36));
            Color(color);
            Console.Write(new string('█', filled));
            Color(ConsoleColor.DarkGray);
            Console.Write(new string('░', Cells - filled));
            Reset();
            Console.WriteLine($"  {percent,5:F2}%  ({count:N0}회)");
        }

        private static void SampleBoard(string[] rows)
        {
            if (rows == null)
                return;

            Color(ConsoleColor.Cyan);
            Console.WriteLine(" 실제로 돌린 보드 하나 (마지막 수까지 둔 상태)");
            Reset();

            foreach (string row in rows)
                Console.WriteLine("   " + row);

            Color(ConsoleColor.DarkGray);
            Console.WriteLine("   ▩ 벽   ◆ 보스   ⛓ 사슬   ◉ 폭탄   ✳ 부패   ♥ 물약   ! 성남   (빈 칸 = 의도한 구멍)");
            Reset();
        }

        /// <summary>
        /// 이 하네스가 실제로 잡아낸 것. EditMode 테스트 166개는 셋 다 통과시켰다 —
        /// 손으로 짠 테스트는 "내가 생각한 보드"만 보기 때문이다.
        /// </summary>
        private static void Caught()
        {
            Color(ConsoleColor.Cyan);
            Console.WriteLine(" 이 하네스가 잡은 것 (손으로 짠 테스트 166개는 셋 다 놓쳤다)");
            Reset();
            Caught("I-1", "끌어오기 경로 중간이 빈 칸이면 밀 타일이 없어 널 참조로 터졌다");
            Caught("I-1", "벽에 얹힌 타일을 슬라이드와 끌어오기가 서로 빼앗아 웨이브 상한을 소진했다");
            Caught("I-5", "갇힌 칸에 보스 타일이 들어가 난입이 통째로 막혔다 (16칸 중 8칸)");
            Console.WriteLine();
        }

        private static void Caught(string id, string what)
        {
            Color(ConsoleColor.DarkGray);
            Console.Write($"   {id}  ");
            Reset();
            Console.WriteLine(what);
        }

        private static void FailureBlock(List<Failure> failures)
        {
            Color(ConsoleColor.Red);
            Console.WriteLine(" 위반");
            Reset();
            foreach (Failure failure in failures)
                Console.WriteLine($"   보드 #{failure.Seed} · {failure.Where} — {failure.What}");

            Console.WriteLine();
        }

        private static void Verdict(bool passed, Invariant[] invariants, TimeSpan elapsed)
        {
            long checks = 0;
            foreach (Invariant invariant in invariants)
                checks += invariant.Checks;

            Console.WriteLine();
            Color(passed ? ConsoleColor.Green : ConsoleColor.Red);
            Console.WriteLine("╭" + new string('─', Wide) + "╮");

            string verdict = passed
                ? $"  ✔  불변식 {invariants.Length}종 · 검사 {checks:N0}회 — 위반 0건"
                : $"  ✘  불변식 위반이 있다 — 위 보드 번호로 재현할 것";

            Console.WriteLine("│" + Pad(verdict, Wide) + "│");
            Console.WriteLine("│" + Pad($"     Unity를 켜지 않고 {elapsed.TotalSeconds:F1}초 만에 끝났다 (Core에 UnityEngine 참조가 없어서)", Wide) + "│");
            Console.WriteLine("╰" + new string('─', Wide) + "╯");
            Reset();
            Console.WriteLine();
        }

        // ── 한글은 콘솔에서 두 칸을 먹는다. 표가 어긋나지 않게 폭을 직접 센다. ──
        private static string Pad(string text, int width)
        {
            int visible = 0;
            foreach (char c in text)
                visible += IsWide(c) ? 2 : 1;

            return visible >= width ? text : text + new string(' ', width - visible);
        }

        private static string PadLeft(string text, int width)
        {
            int visible = 0;
            foreach (char c in text)
                visible += IsWide(c) ? 2 : 1;

            return visible >= width ? text : new string(' ', width - visible) + text;
        }

        private static bool IsWide(char c) =>
            (c >= 0x1100 && c <= 0x115F) ||
            (c >= 0x2E80 && c <= 0xA4CF) ||
            (c >= 0xAC00 && c <= 0xD7A3) ||
            (c >= 0xF900 && c <= 0xFAFF) ||
            (c >= 0xFE30 && c <= 0xFE6F) ||
            (c >= 0xFF00 && c <= 0xFF60) ||
            (c >= 0xFFE0 && c <= 0xFFE6);

        private static void Color(ConsoleColor color) => Console.ForegroundColor = color;
        private static void Reset() => Console.ResetColor();

        private static void TrySetUtf8()
        {
            try
            {
                Console.OutputEncoding = new UTF8Encoding(false);
            }
            catch
            {
                // 인코딩을 못 바꾸는 콘솔이면 그냥 둔다 — 숫자는 어차피 읽힌다
            }
        }

        private static int ParseBoards(string[] args)
        {
            int boards = DefaultBoards;

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--boards" && int.TryParse(args[i + 1], out int value) && value > 0)
                    boards = value;

                if (args[i] == "--wall-ratio" &&
                    double.TryParse(args[i + 1], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double wall))
                    _wallRatio = wall;

                if (args[i] == "--hole-ratio" &&
                    double.TryParse(args[i + 1], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double hole))
                    _holeRatio = hole;
            }

            return boards;
        }
    }
}
