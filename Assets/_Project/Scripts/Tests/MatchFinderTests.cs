using System.Linq;
using ChainRiposte.Core.Board;
using ChainRiposte.Core.Match;
using NUnit.Framework;

namespace ChainRiposte.Core.Tests
{
    public sealed class MatchFinderTests
    {
        private static BoardGrid EmptyBoard(int w, int h)
        {
            var mask = new bool[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    mask[x, y] = true;
            return new BoardGrid(mask);
        }

        private static void Place(BoardGrid board, int x, int y, TileDefinition def) =>
            board.PlaceTile(new GridPos(x, y), new Tile(def));

        [Test]
        public void 가로_3개_매치를_찾는다()
        {
            BoardGrid board = EmptyBoard(4, 1);
            Place(board, 0, 0, TestUtils.Skull);
            Place(board, 1, 0, TestUtils.Skull);
            Place(board, 2, 0, TestUtils.Skull);
            Place(board, 3, 0, TestUtils.Rat);

            var matches = MatchFinder.FindAll(board);

            Assert.That(matches, Has.Count.EqualTo(1));
            Assert.That(matches[0].Positions, Has.Count.EqualTo(3));
            Assert.That(matches[0].Definition, Is.SameAs(TestUtils.Skull));
        }

        [Test]
        public void 두_개는_매치가_아니다()
        {
            BoardGrid board = EmptyBoard(3, 1);
            Place(board, 0, 0, TestUtils.Skull);
            Place(board, 1, 0, TestUtils.Skull);
            Place(board, 2, 0, TestUtils.Rat);

            Assert.That(MatchFinder.FindAll(board), Is.Empty);
        }

        [Test]
        public void L자로_겹친_가로세로_런은_한_그룹으로_병합된다()
        {
            BoardGrid board = EmptyBoard(3, 3);
            // 가로: (0,0)(1,0)(2,0) + 세로: (0,0)(0,1)(0,2) — (0,0) 공유
            Place(board, 0, 0, TestUtils.Skull);
            Place(board, 1, 0, TestUtils.Skull);
            Place(board, 2, 0, TestUtils.Skull);
            Place(board, 0, 1, TestUtils.Skull);
            Place(board, 0, 2, TestUtils.Skull);

            var matches = MatchFinder.FindAll(board);

            Assert.That(matches, Has.Count.EqualTo(1));
            Assert.That(matches[0].Positions, Has.Count.EqualTo(5));
        }

        [Test]
        public void 비활성_구멍은_런을_끊는다()
        {
            (bool[,] mask, _) = TestUtils.ParseRows("OXO", "OOO", "OOO");
            var board = new BoardGrid(mask); // (1,2)만 비활성
            // 최상단 행 y=2: (0,2) O, (1,2) X, (2,2) O
            Place(board, 0, 2, TestUtils.Skull);
            Place(board, 2, 2, TestUtils.Skull);
            Place(board, 0, 1, TestUtils.Rat);
            Place(board, 1, 1, TestUtils.Skull);
            Place(board, 2, 1, TestUtils.Rat);

            Assert.That(MatchFinder.FindAll(board), Is.Empty);
        }

        [Test]
        public void 서로_다른_종류가_겹치지_않으면_별도_그룹이다()
        {
            BoardGrid board = EmptyBoard(3, 2);
            Place(board, 0, 0, TestUtils.Skull);
            Place(board, 1, 0, TestUtils.Skull);
            Place(board, 2, 0, TestUtils.Skull);
            Place(board, 0, 1, TestUtils.Rat);
            Place(board, 1, 1, TestUtils.Rat);
            Place(board, 2, 1, TestUtils.Rat);

            var matches = MatchFinder.FindAll(board);

            Assert.That(matches, Has.Count.EqualTo(2));
            Assert.That(matches.Select(m => m.Definition),
                Is.EquivalentTo(new[] { TestUtils.Skull, TestUtils.Rat }));
        }
    }
}
