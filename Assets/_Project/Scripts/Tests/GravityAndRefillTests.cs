using ChainRiposte.Core.Board;
using ChainRiposte.Core.Match;
using NUnit.Framework;

namespace ChainRiposte.Core.Tests
{
    public sealed class GravityAndRefillTests
    {
        [Test]
        public void 타일은_비활성_구멍을_통과해_떨어진다()
        {
            // 1열 높이 4, 위에서 두 번째 칸(y=2)이 구멍
            (bool[,] mask, _) = TestUtils.ParseRows("O", "X", "O", "O");
            var board = new BoardGrid(mask);
            var tile = new Tile(TestUtils.Skull);
            board.PlaceTile(new GridPos(0, 3), tile);

            var moves = GravityResolver.Collapse(board);

            Assert.That(moves, Has.Count.EqualTo(1));
            Assert.That(moves[0].From, Is.EqualTo(new GridPos(0, 3)));
            Assert.That(moves[0].To, Is.EqualTo(new GridPos(0, 0)));
            Assert.That(board.GetTile(new GridPos(0, 0)), Is.SameAs(tile));
        }

        [Test]
        public void 벽은_움직이지_않고_낙하를_막는다()
        {
            (bool[,] mask, _) = TestUtils.ParseRows("O", "O", "O", "O");
            var board = new BoardGrid(mask);
            var wall = new Tile(new TileDefinition("Wall", TileCategory.Wall, maxHp: 2));
            var tile = new Tile(TestUtils.Skull);
            board.PlaceTile(new GridPos(0, 1), wall);
            board.PlaceTile(new GridPos(0, 3), tile);

            GravityResolver.Collapse(board);

            Assert.That(board.GetTile(new GridPos(0, 1)), Is.SameAs(wall), "벽은 제자리");
            Assert.That(board.GetTile(new GridPos(0, 2)), Is.SameAs(tile), "타일은 벽 위에 안착");
            Assert.That(board.GetTile(new GridPos(0, 0)), Is.Null, "벽 아래는 비어 있어야 함");
        }

        [Test]
        public void 리필은_벽_아래_구간을_채우지_않는다()
        {
            (bool[,] mask, _) = TestUtils.ParseRows("O", "O", "O", "O");
            var board = new BoardGrid(mask);
            board.PlaceTile(new GridPos(0, 1), new Tile(new TileDefinition("Wall", TileCategory.Wall, maxHp: 2)));

            var spawns = BoardRefiller.Refill(board, new SequenceSpawner(new TileDefinition[0]));

            Assert.That(spawns, Has.Count.EqualTo(2), "벽 위 두 칸(y=2,3)만 리필");
            Assert.That(board.GetTile(new GridPos(0, 3)), Is.Not.Null);
            Assert.That(board.GetTile(new GridPos(0, 2)), Is.Not.Null);
            Assert.That(board.GetTile(new GridPos(0, 0)), Is.Null, "벽 아래는 리필 금지");
        }

        [Test]
        public void 대각선_슬라이드로_벽_아래가_채워진다()
        {
            // 2×2: 벽 (0,1), 타일 A (1,1). (0,0)은 직선 낙하/리필로는 못 채우는 칸
            (bool[,] mask, _) = TestUtils.ParseRows("OO", "OO");
            var board = new BoardGrid(mask);
            board.PlaceTile(new GridPos(0, 1), new Tile(new TileDefinition("Wall", TileCategory.Wall, maxHp: 2)));
            var tileA = new Tile(TestUtils.Skull);
            board.PlaceTile(new GridPos(1, 1), tileA);

            var spawner = new SequenceSpawner(new[] { TestUtils.Potion, TestUtils.Rat });
            var phases = GravityResolver.Settle(board, spawner);

            // 웨이브1: A 직선 낙하 (1,1)→(1,0), 리필 P@(1,1)
            // 웨이브2: P 대각선 슬라이드 (1,1)→(0,0), 리필 R@(1,1)
            Assert.That(phases, Has.Count.EqualTo(2));
            Assert.That(board.GetTile(new GridPos(1, 0)), Is.SameAs(tileA));
            Assert.That(board.GetTile(new GridPos(0, 0)).Definition, Is.SameAs(TestUtils.Potion), "벽 아래로 미끄러진 타일");
            Assert.That(board.GetTile(new GridPos(1, 1)).Definition, Is.SameAs(TestUtils.Rat));
        }

        [Test]
        public void 벽이_없으면_대각선_슬라이드가_일어나지_않는다()
        {
            (bool[,] mask, _) = TestUtils.ParseRows("OO", "OO");
            var board = new BoardGrid(mask);

            var spawner = new SequenceSpawner(new TileDefinition[0]);
            var phases = GravityResolver.Settle(board, spawner);

            Assert.That(phases, Has.Count.EqualTo(1), "리필 한 웨이브로 안정");
            foreach (FallPhase phase in phases)
                Assert.That(phase.Moves, Is.Empty, "이동 없이 리필만 발생해야 함");
        }

        [Test]
        public void 리필은_비활성_셀을_건너뛴다()
        {
            (bool[,] mask, _) = TestUtils.ParseRows("O", "X", "O");
            var board = new BoardGrid(mask);

            var spawns = BoardRefiller.Refill(board, new SequenceSpawner(new TileDefinition[0]));

            Assert.That(spawns, Has.Count.EqualTo(2), "활성 셀 2개만 리필");
            Assert.That(board.GetTile(new GridPos(0, 2)), Is.Not.Null);
            Assert.That(board.GetTile(new GridPos(0, 0)), Is.Not.Null);
        }
    }
}
