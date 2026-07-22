using System;
using System.Collections.Generic;
using ChainRiposte.Core.Board;
using ChainRiposte.Core.Match;
using ChainRiposte.Core.Stage;
using NUnit.Framework;

namespace ChainRiposte.Core.Tests
{
    /// <summary>데드락 검출(둘 수 있는 수가 있는가)과 보드 리롤 테스트.</summary>
    public sealed class MoveFinderTests
    {
        private static readonly TileDefinition[] Palette = { TestUtils.Skull, TestUtils.Rat, TestUtils.Potion };

        /// <summary>
        /// 대각 3색 패턴 c = (x + 2y) % 3.
        /// 가로는 한 칸에 +1, 세로는 +2씩 도니 <b>어떤 3연속 창에도 같은 색이 없다</b> →
        /// 한 번의 스왑으로는 절대 3개를 못 맞춘다 (수학적으로 보장된 데드락 보드).
        /// </summary>
        private static TileDefinition Diagonal(int x, int y) => Palette[(x + 2 * y) % 3];

        [Test]
        public void 한_번의_스왑으로_매치가_나면_수가_있다()
        {
            BoardGrid board = Board3x3(new[]
            {
                //  x=0        x=1          x=2
                "S", "S", "R", // y=0 — (2,0)과 (2,1)을 바꾸면 S S S
                "P", "R", "S", // y=1
                "R", "P", "P", // y=2
            });

            Assert.That(MoveFinder.HasAnyValidMove(board), Is.True);
        }

        [Test]
        public void 대각_3색_패턴은_데드락이다()
        {
            BoardGrid board = Filled(4, 4, Diagonal);

            Assert.That(MatchFinder.FindAll(board), Is.Empty, "이미 매치가 있으면 데드락 판정의 전제가 깨진다");
            Assert.That(MoveFinder.HasAnyValidMove(board), Is.False);
        }

        [Test]
        public void 판정은_보드를_원상태로_되돌린다()
        {
            BoardGrid board = Filled(4, 4, Diagonal);
            var before = new Dictionary<GridPos, Tile>();
            foreach (GridPos pos in board.ActivePositions())
                before[pos] = board.GetTile(pos);

            MoveFinder.HasAnyValidMove(board);

            foreach (KeyValuePair<GridPos, Tile> entry in before)
                Assert.That(board.GetTile(entry.Key), Is.SameAs(entry.Value), $"{entry.Key} 타일이 뒤바뀐 채로 남음");
        }

        [Test]
        public void 사슬에_결박된_타일로는_수를_만들_수_없다()
        {
            // 유일하게 매치를 만드는 스왑이 (2,0)↔(2,1) 뿐인 보드
            (bool[,] mask, _) = TestUtils.ParseRows("OOO", "OOO");
            var board = new BoardGrid(mask);
            Place(board, 0, 0, TestUtils.Skull);
            Place(board, 1, 0, TestUtils.Skull);
            Place(board, 2, 0, TestUtils.Rat);
            Place(board, 0, 1, TestUtils.Rat);
            Place(board, 1, 1, TestUtils.Potion);
            Tile chainTarget = Place(board, 2, 1, TestUtils.Skull);

            Assert.That(MoveFinder.HasAnyValidMove(board), Is.True, "결박 전에는 수가 있어야 한다");

            chainTarget.Status.Chained = true;

            Assert.That(MoveFinder.HasAnyValidMove(board), Is.False, "결박된 타일은 스왑 불가 — 유령 수로 세면 안 된다");
        }

        [Test]
        public void 데드락으로_시작하면_생성자가_보드를_섞는다()
        {
            var engine = new PuzzleEngine(
                TestUtils.Config(new[] { "OOOO", "OOOO", "OOOO", "OOOO" }),
                DiagonalSpawner(4, 4),
                new Random(1));

            Assert.That(MoveFinder.HasAnyValidMove(engine.Board), Is.True, "리롤로 데드락이 풀려야 한다");
            Assert.That(MatchFinder.FindAll(engine.Board), Is.Empty, "리롤 직후 공짜 콤보가 터지면 안 된다");
        }

        [Test]
        public void 리롤은_벽을_건드리지_않고_타일도_잃지_않는다()
        {
            var engine = new PuzzleEngine(
                TestUtils.Config(new[] { "OOOO", "OOOO", "OOOO", "WOOO" }),
                DiagonalSpawner(4, 4, skip: new GridPos(0, 0)),
                new Random(7));

            Tile wall = engine.Board.GetTile(new GridPos(0, 0));
            Assert.That(wall, Is.Not.Null);
            Assert.That(wall.Category, Is.EqualTo(TileCategory.Wall), "벽은 리롤 대상이 아니다");

            var ids = new HashSet<long>();
            foreach (GridPos pos in engine.Board.ActivePositions())
            {
                Tile tile = engine.Board.GetTile(pos);
                Assert.That(tile, Is.Not.Null, $"{pos} 칸이 비었다");
                Assert.That(ids.Add(tile.InstanceId), Is.True, $"{pos}에 같은 타일이 두 번 놓였다");
            }
        }

        // --- 헬퍼 ---

        /// <summary>대각 3색 패턴을 그대로 내놓는 스포너 — FillInitialBoard의 순회(아래 행부터, 왼쪽부터)에 맞춘다.</summary>
        private static SequenceSpawner DiagonalSpawner(int width, int height, GridPos? skip = null)
        {
            var sequence = new List<TileDefinition>();
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (skip == null || !skip.Value.Equals(new GridPos(x, y)))
                        sequence.Add(Diagonal(x, y));

            return new SequenceSpawner(sequence);
        }

        private static BoardGrid Filled(int width, int height, Func<int, int, TileDefinition> pick)
        {
            var mask = new bool[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    mask[x, y] = true;

            var board = new BoardGrid(mask);
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    Place(board, x, y, pick(x, y));

            return board;
        }

        /// <summary>행 문자열(아래 행부터, S/R/P)로 3×3 보드를 만든다.</summary>
        private static BoardGrid Board3x3(string[] cellsBottomUp)
        {
            (bool[,] mask, _) = TestUtils.ParseRows("OOO", "OOO", "OOO");
            var board = new BoardGrid(mask);

            for (int i = 0; i < cellsBottomUp.Length; i++)
            {
                TileDefinition def = cellsBottomUp[i] switch
                {
                    "S" => TestUtils.Skull,
                    "R" => TestUtils.Rat,
                    _ => TestUtils.Potion,
                };
                Place(board, i % 3, i / 3, def);
            }

            return board;
        }

        private static Tile Place(BoardGrid board, int x, int y, TileDefinition def)
        {
            var tile = new Tile(def);
            board.PlaceTile(new GridPos(x, y), tile);
            return tile;
        }
    }
}
