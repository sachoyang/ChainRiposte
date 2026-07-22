using System.Linq;
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
        public void 리필은_기둥_중간_빈칸에_스폰하지_않는다()
        {
            // 최상단에 타일이 떠 있고 아래가 빈 기둥 — 중간 빈칸은 낙하 몫, 리필이 가로채면 안 된다
            (bool[,] mask, _) = TestUtils.ParseRows("O", "O", "O");
            var board = new BoardGrid(mask);
            board.PlaceTile(new GridPos(0, 2), new Tile(TestUtils.Skull));

            var spawns = BoardRefiller.Refill(board, new SequenceSpawner(new TileDefinition[0]));

            Assert.That(spawns, Is.Empty, "첫 타일 아래로는 리필 금지");
            Assert.That(board.GetTile(new GridPos(0, 1)), Is.Null);
            Assert.That(board.GetTile(new GridPos(0, 0)), Is.Null);
        }

        [Test]
        public void 벽에_얹힌_타일은_대각선_아래_빈칸으로_미끄러진다()
        {
            // 보스 하강 보장의 핵심 — 벽 위에서 직선 낙하가 막힌 타일은 옆으로 미끄러져 내려간다
            (bool[,] mask, _) = TestUtils.ParseRows("OO", "OO");
            var board = new BoardGrid(mask);
            board.PlaceTile(new GridPos(0, 0), new Tile(new TileDefinition("Wall", TileCategory.Wall, maxHp: 2)));
            var boss = new Tile(new TileDefinition("Boss", TileCategory.Boss));
            board.PlaceTile(new GridPos(0, 1), boss); // 벽 위에 얹힘

            GravityResolver.Settle(board, new SequenceSpawner(new TileDefinition[0]));

            Assert.That(board.GetTile(new GridPos(1, 0)), Is.SameAs(boss), "벽에서 미끄러져 바닥 도달");
        }

        [Test]
        public void 슬라이드가_만든_기둥_중간_구멍은_위_타일이_내려와_채운다()
        {
            // 3x3, 벽 (1,1). 그늘 칸 (1,0)으로 A가 미끄러지면 (0,1) 구멍은
            // 새 스폰이 아니라 위 타일 B가 낙하해 채워야 한다 (보스 미하강 버그의 원형)
            (bool[,] mask, _) = TestUtils.ParseRows("OOO", "OWO", "OOO");
            var board = new BoardGrid(mask);
            board.PlaceTile(new GridPos(1, 1), new Tile(new TileDefinition("Wall", TileCategory.Wall, maxHp: 2)));
            var bottom = new Tile(TestUtils.Skull);
            var tileA = new Tile(TestUtils.Rat);
            var tileB = new Tile(TestUtils.Potion);
            board.PlaceTile(new GridPos(0, 0), bottom);
            board.PlaceTile(new GridPos(0, 1), tileA);
            board.PlaceTile(new GridPos(0, 2), tileB);

            GravityResolver.Settle(board, new SequenceSpawner(new TileDefinition[0]));

            Assert.That(board.GetTile(new GridPos(1, 0)), Is.SameAs(tileA), "A는 그늘 칸으로 슬라이드");
            Assert.That(board.GetTile(new GridPos(0, 1)), Is.SameAs(tileB), "B는 낙하로 구멍을 채움 (새 스폰 금지)");

            // 벽/구멍 제외 전 칸이 채워졌는지 — "모두 채워져야 함" 요구사항
            foreach (GridPos pos in board.ActivePositions())
                Assert.That(board.GetTile(pos), Is.Not.Null, $"{pos} 칸이 비어 있음");
        }

        [Test]
        public void 한_웨이브에서_낙하와_슬라이드가_겹쳐도_이동기록은_타일당_하나다()
        {
            // 3x3, 벽 (0,0). T는 같은 웨이브에서 (0,2)→(0,1) 낙하 후 (0,1)→(1,0) 슬라이드한다.
            // 기록이 둘로 남으면 뷰가 중간 좌표에 뷰를 남겨 '빈 칸 + 겹친 타일'이 생긴다.
            (bool[,] mask, _) = TestUtils.ParseRows("OOO", "OOO", "WOO");
            var board = new BoardGrid(mask);
            board.PlaceTile(new GridPos(0, 0), new Tile(new TileDefinition("Wall", TileCategory.Wall, maxHp: 2)));
            var tile = new Tile(TestUtils.Skull);
            board.PlaceTile(new GridPos(0, 2), tile);

            var phases = GravityResolver.Settle(board, new SequenceSpawner(new TileDefinition[0]));

            var first = phases[0].Moves;
            Assert.That(first.Where(m => m.Tile == tile).ToList(), Has.Count.EqualTo(1), "타일당 이동 기록 1개");
            Assert.That(first.Single(m => m.Tile == tile).From, Is.EqualTo(new GridPos(0, 2)));
            Assert.That(first.Single(m => m.Tile == tile).To, Is.EqualTo(new GridPos(1, 0)), "출발지와 최종 도착지로 합쳐짐");

            foreach (FallPhase phase in phases)
            {
                var ids = phase.Moves.Select(m => m.Tile.InstanceId).ToList();
                Assert.That(ids, Is.Unique, "한 웨이브에 같은 타일이 두 번 등장하면 안 됨");
            }
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
