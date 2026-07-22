using System;
using ChainRiposte.Core.Board;
using ChainRiposte.Core.Match;
using ChainRiposte.Core.Stage;
using NUnit.Framework;

namespace ChainRiposte.Core.Tests
{
    /// <summary>
    /// 비정형 보드(해골 — 눈이 뚫림, 하트 — 아래로 좁아짐)에서 낙하·리필·데드락 리롤이
    /// 끝까지 성립하는지 확인하는 퍼즈 테스트. 실제로 수를 찾아 두는 것을 수백 턴 반복한다.
    /// </summary>
    public sealed class IrregularBoardTests
    {
        /// <summary>해골 — 눈 두 개가 비활성(X)으로 뚫려 있다.</summary>
        private static readonly string[] Skull =
        {
            "XXOOOOOXX",
            "XOOOOOOOX",
            "OOOOOOOOO",
            "OOXOOOXOO",
            "OOXOOOXOO",
            "OOOOOOOOO",
            "OOOOOOOOO",
            "XOOOOOOOX",
            "XXOOOOOXX",
        };

        /// <summary>하트 — 위가 갈라지고 아래로 갈수록 좁아져 마지막 줄은 한 칸뿐.</summary>
        private static readonly string[] Heart =
        {
            "XOOOXOOOX",
            "OOOOOOOOO",
            "OOOOOOOOO",
            "OOOOOOOOO",
            "XOOOOOOOX",
            "XXOOOOOXX",
            "XXXOOOXXX",
            "XXXXOXXXX",
        };

        /// <summary>해골 + 벽 — 벽 그늘과 구멍이 겹치는 최악의 조합.</summary>
        private static readonly string[] SkullWithWalls =
        {
            "XXOOOOOXX",
            "XOOOOOOOX",
            "OOOOOOOOO",
            "OOXOWOXOO",
            "OOXOOOXOO",
            "OWOOOOOWO",
            "OOOOOOOOO",
            "XOOOOOOOX",
            "XXOOOOOXX",
        };

        [Test]
        public void 해골_보드는_수백턴을_돌려도_불변식을_지킨다() => PlayOut(Skull, seed: 11);

        [Test]
        public void 하트_보드는_수백턴을_돌려도_불변식을_지킨다() => PlayOut(Heart, seed: 22);

        [Test]
        public void 해골에_벽을_섞어도_불변식을_지킨다() => PlayOut(SkullWithWalls, seed: 33, expectFilled: true);

        [Test]
        public void 구멍은_매치를_끊는다()
        {
            // 눈 구멍을 사이에 둔 같은 종류 3개는 매치가 아니다 (MatchFinder와 MoveFinder가 같은 규칙이어야 한다)
            (bool[,] mask, _) = TestUtils.ParseRows("OXOO");
            var board = new BoardGrid(mask);
            foreach (GridPos pos in board.ActivePositions())
                board.PlaceTile(pos, new Tile(TestUtils.Skull));

            Assert.That(MatchFinder.FindAll(board), Is.Empty, "구멍 건너 3개는 매치가 아니다");
        }

        /// <param name="expectFilled">
        /// 벽·구멍과 무관하게 활성 칸이 항상 꽉 차야 한다 (구멍은 낙하도 대각선 슬라이드도 끊지 않는다).
        /// 예외는 대각선 경로가 보드 밖으로만 열린 칸(맨 윗줄 모서리에 벽을 둔 경우 등)뿐이다.
        /// </param>
        private static void PlayOut(string[] rows, int seed, bool expectFilled = true)
        {
            StageConfig config = TestUtils.Config(rows, turnLimit: 100000, wallHp: 3);
            var rng = new Random(seed);
            var engine = new PuzzleEngine(config, new RandomSpawner(rng), rng);

            AssertInvariants(engine.Board, "초기 배치", expectFilled);

            for (int turn = 0; turn < 300; turn++)
            {
                if (!MoveFinder.TryFindMove(engine.Board, out GridPos a, out GridPos b))
                    Assert.Fail($"{turn}턴: 둘 수 있는 수가 없다 — 리롤이 데드락을 못 풀었다");

                SwapResult result = engine.TrySwap(a, b);
                Assert.That(result.Success, Is.True, $"{turn}턴: {a}↔{b} 는 매치가 나야 하는 수인데 실패했다");

                AssertInvariants(engine.Board, $"{turn}턴", expectFilled);
            }
        }

        private static void AssertInvariants(BoardGrid board, string context, bool expectFilled)
        {
            Assert.That(MatchFinder.FindAll(board), Is.Empty, $"{context}: 해석이 끝났는데 매치가 남아 있다");
            Assert.That(MoveFinder.HasAnyValidMove(board), Is.True, $"{context}: 데드락인 채로 턴이 끝났다");

            if (!expectFilled)
                return;

            foreach (GridPos pos in board.ActivePositions())
                Assert.That(board.GetTile(pos), Is.Not.Null, $"{context}: {pos} 칸이 비어 있다");
        }

        /// <summary>3종을 균등 무작위로 내놓는 스포너 — 시드로 재현된다.</summary>
        private sealed class RandomSpawner : ITileSpawner
        {
            private static readonly TileDefinition[] Palette = { TestUtils.Skull, TestUtils.Rat, TestUtils.Potion };
            private readonly Random _rng;

            public RandomSpawner(Random rng) => _rng = rng;

            public TileDefinition NextDefinition() => Palette[_rng.Next(Palette.Length)];
        }
    }
}
