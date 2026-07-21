using System;
using System.Collections.Generic;
using ChainRiposte.Core.Board;
using ChainRiposte.Core.Match;
using ChainRiposte.Core.Stage;
using ChainRiposte.Core.Stage.Gimmicks;
using NUnit.Framework;

namespace ChainRiposte.Core.Tests
{
    /// <summary>스테이지 기믹 3종 (GDD §3.6) — 전염 / 시한폭탄 / 사슬 결박.</summary>
    public sealed class GimmickTests
    {
        private static readonly TileDefinition S = TestUtils.Skull;
        private static readonly TileDefinition R = TestUtils.Rat;
        private static readonly TileDefinition P = TestUtils.Potion;

        private static readonly string[] Plain3x3 = { "OOO", "OOO", "OOO" };

        /// <summary>PuzzleEngineTests와 같은 배치 — (2,0)↔(2,1) 스왑이면 y0가 해골 3개가 된다.</summary>
        private static PuzzleEngine CreateEngine(StageConfig config)
        {
            var seq = new List<TileDefinition>
            {
                S, S, R,
                P, R, S,
                R, S, P,
            };
            return new PuzzleEngine(config, new SequenceSpawner(seq), new Random(1));
        }

        private static StageConfig ConfigWith(GimmickType gimmick, GimmickSettings settings)
        {
            StageConfig config = TestUtils.Config(Plain3x3);
            config.Gimmicks = new[] { gimmick };
            config.GimmickSettings = settings;
            return config;
        }

        private static GimmickContext Context(BoardGrid board, GimmickSettings settings) =>
            new(board, new Random(1), settings);

        private static BoardGrid FilledBoard(string[] rows, TileDefinition definition)
        {
            (bool[,] mask, _) = TestUtils.ParseRows(rows);
            var board = new BoardGrid(mask);
            foreach (GridPos pos in board.ActivePositions())
                board.PlaceTile(pos, new Tile(definition));
            return board;
        }

        // ── 사슬 결박 ──

        [Test]
        public void 결박된_타일은_낙하하지_않는다()
        {
            (bool[,] mask, _) = TestUtils.ParseRows("O", "O", "O", "O");
            var board = new BoardGrid(mask);
            var chained = new Tile(S);
            chained.Status.Chained = true;
            board.PlaceTile(new GridPos(0, 3), chained);

            GravityResolver.Collapse(board);

            Assert.That(board.GetTile(new GridPos(0, 3)), Is.SameAs(chained), "결박 타일은 벽처럼 제자리");
            Assert.That(board.GetTile(new GridPos(0, 0)), Is.Null);
        }

        [Test]
        public void 결박된_타일은_스왑할_수_없다()
        {
            PuzzleEngine engine = CreateEngine(ConfigWith(GimmickType.LockedTiles,
                new GimmickSettings { ChainInitialCount = 0, ChainChance = 0f }));
            engine.Board.GetTile(new GridPos(2, 0)).Status.Chained = true;

            SwapResult result = engine.TrySwap(new GridPos(2, 0), new GridPos(2, 1));

            Assert.That(result.Success, Is.False);
            Assert.That(engine.TurnsRemaining, Is.EqualTo(30), "턴도 소모하지 않는다");
        }

        [Test]
        public void 매치에_걸린_결박_타일은_파괴_대신_사슬만_풀린다()
        {
            PuzzleEngine engine = CreateEngine(ConfigWith(GimmickType.LockedTiles,
                new GimmickSettings { ChainInitialCount = 0, ChainChance = 0f }));
            Tile chained = engine.Board.GetTile(new GridPos(1, 0));
            chained.Status.Chained = true;

            SwapResult result = engine.TrySwap(new GridPos(2, 0), new GridPos(2, 1));
            CascadeStep first = result.Steps[0];

            Assert.That(result.Success, Is.True);
            Assert.That(first.ClearedPositions, Has.Count.EqualTo(2), "결박 타일은 살아남아 2칸만 파괴");
            Assert.That(first.SoulsEarned, Is.EqualTo(20), "살아남은 타일은 영혼석을 주지 않는다");
            Assert.That(engine.Board.GetTile(new GridPos(1, 0)), Is.SameAs(chained), "타일은 그 자리에 남는다");
            Assert.That(chained.Status.Chained, Is.False, "사슬은 풀렸다");
            Assert.That(first.GimmickEvents,
                Has.Some.Matches<GimmickEvent>(e => e.Type == GimmickEventType.ChainBroken));
        }

        [Test]
        public void 인접_매치도_사슬을_푼다()
        {
            BoardGrid board = FilledBoard(Plain3x3, S);
            Tile chained = board.GetTile(new GridPos(1, 1));
            chained.Status.Chained = true;
            GimmickContext context = Context(board, new GimmickSettings());
            var cleared = new HashSet<GridPos> { new(1, 0) }; // 바로 아래 칸이 매치로 사라짐

            new ChainedTilesGimmick().OnMatchesResolving(context, cleared);

            Assert.That(chained.Status.Chained, Is.False);
            Assert.That(cleared, Has.Count.EqualTo(1), "인접 해제는 파괴 목록을 늘리지 않는다");
        }

        // ── 시한폭탄 ──

        [Test]
        public void 폭탄은_스폰된_턴에는_줄지_않고_다음_턴부터_카운트다운한다()
        {
            BoardGrid board = FilledBoard(new[] { "O", "O", "O" }, S);
            GimmickContext context = Context(board,
                new GimmickSettings { BombChance = 1f, BombTurns = 2, BombDamage = 7 });
            var gimmick = new TickingDeathGimmick();
            var pos = new GridPos(0, 0);
            Tile tile = board.GetTile(pos);

            gimmick.OnTilesSpawned(context, new[] { new TileSpawn(tile, pos) });
            Assert.That(tile.Status.BombTurnsRemaining, Is.EqualTo(2), "스폰 시 장전");

            gimmick.OnTurnEnded(context);
            Assert.That(tile.Status.BombTurnsRemaining, Is.EqualTo(2), "스폰된 턴은 유예");

            gimmick.OnTurnEnded(context);
            Assert.That(tile.Status.BombTurnsRemaining, Is.EqualTo(1));
        }

        [Test]
        public void 카운트가_0이_되면_폭발해_타일이_사라지고_피해를_남긴다()
        {
            BoardGrid board = FilledBoard(new[] { "O", "O", "O" }, S);
            GimmickContext context = Context(board,
                new GimmickSettings { BombTurns = 1, BombDamage = 7 });
            var pos = new GridPos(0, 0);
            board.GetTile(pos).Status.BombTurnsRemaining = 1;

            new TickingDeathGimmick().OnTurnEnded(context);

            Assert.That(board.GetTile(pos), Is.Null, "폭탄 타일은 사라진다");
            Assert.That(context.PlayerDamage, Is.EqualTo(7));
            Assert.That(context.BoardChanged, Is.True, "보드가 바뀌었으니 재정착이 필요하다");
        }

        [Test]
        public void 폭발_피해는_스왑_결과에_실린다()
        {
            PuzzleEngine engine = CreateEngine(ConfigWith(GimmickType.TickingDeath,
                new GimmickSettings { BombChance = 0f, BombTurns = 1, BombDamage = 9 }));
            // 매치와 무관한 칸의 타일을 폭발 직전 상태로 만든다
            engine.Board.GetTile(new GridPos(0, 2)).Status.BombTurnsRemaining = 1;

            SwapResult result = engine.TrySwap(new GridPos(2, 0), new GridPos(2, 1));

            Assert.That(result.Success, Is.True);
            Assert.That(result.Gimmicks.PlayerDamage, Is.EqualTo(9));
            Assert.That(result.Gimmicks.Events,
                Has.Some.Matches<GimmickEvent>(e => e.Type == GimmickEventType.BombExploded));
        }

        // ── 전염되는 타일 ──

        [Test]
        public void 부패는_시작_시_지정한_개수만큼_심긴다()
        {
            BoardGrid board = FilledBoard(Plain3x3, S);
            GimmickContext context = Context(board, new GimmickSettings { CorruptionSeeds = 2 });

            new SpreadingCorruptionGimmick().OnBoardInitialized(context);

            Assert.That(CountCorruption(board), Is.EqualTo(2));
        }

        [Test]
        public void 부패는_턴이_끝나면_인접_몬스터를_감염시킨다()
        {
            BoardGrid board = FilledBoard(Plain3x3, S);
            GimmickContext context = Context(board,
                new GimmickSettings { CorruptionSeeds = 1, MaxCorruptionRatio = 1f });
            var gimmick = new SpreadingCorruptionGimmick();
            gimmick.OnBoardInitialized(context);

            gimmick.OnTurnEnded(context);

            Assert.That(CountCorruption(board), Is.EqualTo(2));
            Assert.That(context.Events,
                Has.Some.Matches<GimmickEvent>(e => e.Type == GimmickEventType.CorruptionSpread));
        }

        [Test]
        public void 부패는_상한_비율을_넘으면_더_퍼지지_않는다()
        {
            BoardGrid board = FilledBoard(Plain3x3, S);
            // 9칸 × 0.2 = 1.8 → 상한 1개. 씨앗 1개가 이미 상한이다
            GimmickContext context = Context(board,
                new GimmickSettings { CorruptionSeeds = 1, MaxCorruptionRatio = 0.2f });
            var gimmick = new SpreadingCorruptionGimmick();
            gimmick.OnBoardInitialized(context);

            gimmick.OnTurnEnded(context);

            Assert.That(CountCorruption(board), Is.EqualTo(1), "완전 데드락 방지");
        }

        [Test]
        public void 인접_매치는_부패_타일을_함께_태운다()
        {
            BoardGrid board = FilledBoard(Plain3x3, S);
            var corruptionPos = new GridPos(1, 1);
            board.RemoveTile(corruptionPos);
            board.PlaceTile(corruptionPos, new Tile(SpreadingCorruptionGimmick.CorruptionDefinition));
            GimmickContext context = Context(board, new GimmickSettings());
            var cleared = new HashSet<GridPos> { new(1, 0) };

            new SpreadingCorruptionGimmick().OnMatchesResolving(context, cleared);

            Assert.That(cleared, Contains.Item(corruptionPos), "부패도 파괴 목록에 들어간다");
            Assert.That(context.Events,
                Has.Some.Matches<GimmickEvent>(e => e.Type == GimmickEventType.CorruptionCleared));
        }

        [Test]
        public void 부패_타일은_매치에도_스왑에도_쓰이지_않는다()
        {
            var corruption = new Tile(SpreadingCorruptionGimmick.CorruptionDefinition);

            Assert.That(MatchFinder.IsMatchable(corruption), Is.False);
            Assert.That(corruption.IsFixed, Is.False, "낙하는 한다");
        }

        // ── 회귀 ──

        [Test]
        public void 기믹이_없으면_결과에_기믹_기록이_없다()
        {
            PuzzleEngine engine = CreateEngine(TestUtils.Config(Plain3x3));

            SwapResult result = engine.TrySwap(new GridPos(2, 0), new GridPos(2, 1));

            Assert.That(result.Success, Is.True);
            Assert.That(result.Gimmicks.IsEmpty, Is.True);
            Assert.That(result.Steps[0].ClearedPositions, Has.Count.EqualTo(3), "매치 3칸이 그대로 파괴");
        }

        private static int CountCorruption(BoardGrid board)
        {
            int count = 0;
            foreach (GridPos pos in board.ActivePositions())
            {
                Tile tile = board.GetTile(pos);
                if (tile != null && tile.Category == TileCategory.Corruption)
                    count++;
            }
            return count;
        }
    }
}
