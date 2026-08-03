using System;
using System.Collections.Generic;
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
        public void 해골에_벽을_섞어도_불변식을_지킨다() => PlayOut(SkullWithWalls, seed: 33);

        /// <summary>
        /// 양쪽 대각선이 <b>둘 다 벽</b>이라 어떤 타일도 슬라이드로 닿을 수 없는 칸.
        /// 퍼즈로 찾아낸 실제 반례다 — 예전에는 이 칸이 영영 비어 있었다.
        /// </summary>
        private static readonly string[] SealedUnderWalls =
        {
            "OOOOO",
            "OOWOO",
            "OWXWO",
            "OOOOO",
        };

        /// <summary>
        /// 벽이 맨 위를 막고 대각선 한쪽은 보드 밖, 다른 쪽은 위가 빈 구멍인 칸.
        /// 리필도 슬라이드도 닿지 않는다.
        /// </summary>
        private static readonly string[] SealedAtEdge =
        {
            "OOOXW",
            "OOOOO",
            "OOOOO",
        };

        [Test]
        public void 대각선이_양쪽_다_벽이어도_빈칸이_안_남는다() =>
            AssertNoEmptyCellAfterSettle(SealedUnderWalls, seed: 101);

        [Test]
        public void 보드_가장자리의_벽_그늘도_채워진다() =>
            AssertNoEmptyCellAfterSettle(SealedAtEdge, seed: 202);

        /// <summary>
        /// 정착이 끝난 판에 <b>빈 활성 칸이 하나도 없어야</b> 한다.
        ///
        /// <para>대각선 슬라이드는 한 칸짜리 이동이라 출처가 정확히 대각선 위여야 하는데,
        /// 그 자리가 벽·보드 밖·빈 구멍이면 어떤 타일도 닿지 못한다. 중력만으로는 못 채우므로
        /// <see cref="BoardRefiller.FillSealedPockets"/>가 마지막에 그 자리에서 채운다.</para>
        /// </summary>
        private static void AssertNoEmptyCellAfterSettle(string[] rows, int seed)
        {
            StageConfig config = TestUtils.Config(rows, turnLimit: 0, wallHp: 3);
            var rng = new Random(seed);
            var engine = new PuzzleEngine(config, new RandomSpawner(rng), rng);

            AssertNoEmptyCell(engine.Board, "초기 배치");

            // 벽이 부서지면 그 자리도 '갇힌 칸'이 되므로, 실제로 수를 두며 계속 확인한다.
            for (int turn = 0; turn < 200; turn++)
            {
                if (!MoveFinder.TryFindMove(engine.Board, out GridPos a, out GridPos b))
                    break;

                engine.TrySwap(a, b);
                AssertNoEmptyCell(engine.Board, $"{turn}턴");
            }
        }

        private static void AssertNoEmptyCell(BoardGrid board, string context)
        {
            foreach (GridPos pos in board.ActivePositions())
                Assert.That(board.GetTile(pos), Is.Not.Null,
                    $"{context}: {pos} 가 비어 있다 — 활성 칸은 벽이든 타일이든 무언가로 차 있어야 한다");
        }

        /// <summary>
        /// 갇힌 칸을 메울 때 <b>보스 타일이 들어가면 안 된다.</b>
        ///
        /// <para>보스 타일은 바닥에 닿아야 난입하는데 갇힌 칸은 중력이 닿지 않는 곳이다.
        /// 거기 생기면 영영 안 내려오면서 동시 상한만 차지해 <b>난입이 통째로 막히고</b>,
        /// 하필 그 칸이 바닥이면 반대로 예고 없이 즉시 난입한다.</para>
        ///
        /// <para>스포너가 <b>보스만</b> 내놓는 최악의 경우로 못 박는다 — 재추첨만으로는
        /// 확률만 낮아질 뿐 길이 남아서, 판 위의 평범한 종류를 빌려오는 폴백이 있어야 한다.</para>
        /// </summary>
        [Test]
        public void 갇힌_칸에는_보스_타일이_들어가지_않는다()
        {
            (bool[,] mask, List<GridPos> walls) =
                TestUtils.ParseRows("OOOOO", "OOWOO", "OWXWO", "OOOOO");
            var board = new BoardGrid(mask);
            var wallDef = new TileDefinition("Wall", TileCategory.Wall, maxHp: 3);
            foreach (GridPos w in walls)
                board.PlaceTile(w, new Tile(wallDef));

            // (2,0) 하나만 비워 둔다 — 위는 구멍 너머 벽, 양쪽 대각선은 둘 다 벽이라 아무도 못 온다.
            var pocket = new GridPos(2, 0);
            foreach (GridPos pos in board.ActivePositions())
                if (!board.IsOccupied(pos) && !pos.Equals(pocket))
                    board.PlaceTile(pos, new Tile(TestUtils.Skull));

            IReadOnlyList<TileSpawn> spawns = BoardRefiller.FillSealedPockets(board, new BossOnlySpawner());

            Assert.That(spawns.Count, Is.EqualTo(1), "갇힌 칸 하나가 채워져야 한다");
            Assert.That(spawns[0].Sealed, Is.True, "뷰가 낙하로 그리지 않도록 표시가 붙어야 한다");
            Assert.That(MatchFinder.IsMatchable(spawns[0].Tile), Is.True,
                "갇힌 칸에 보스 타일이 들어갔다 — 영영 안 내려오면서 난입 상한만 차지한다");
            Assert.That(board.GetTile(pocket), Is.Not.Null, "갇힌 칸이 여전히 비어 있다");
        }

        /// <summary>보스 타일만 내놓는 스포너 — 재추첨으로는 절대 못 빠져나간다.</summary>
        private sealed class BossOnlySpawner : ITileSpawner
        {
            private static readonly TileDefinition Boss = new("Boss", TileCategory.Boss);

            public TileDefinition NextDefinition() => Boss;
        }

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
