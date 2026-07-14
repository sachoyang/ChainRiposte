using System;
using ChainRiposte.Core.Board;
using ChainRiposte.Core.Match;
using ChainRiposte.Core.Stage;
using NUnit.Framework;

namespace ChainRiposte.Core.Tests
{
    public sealed class PuzzleEngineTests
    {
        private static readonly string[] Plain3x3 = { "OOO", "OOO", "OOO" };

        private static readonly TileDefinition S = TestUtils.Skull;
        private static readonly TileDefinition R = TestUtils.Rat;
        private static readonly TileDefinition P = TestUtils.Potion;

        /// <summary>
        /// 초기 배치 (아래 행부터, 왼쪽부터):
        /// y2: R S P
        /// y1: P R S
        /// y0: S S R   ← (2,0)↔(2,1) 스왑 시 y0가 SSS
        /// </summary>
        private static PuzzleEngine CreateBasicEngine(params TileDefinition[] refillScript)
        {
            var seq = new System.Collections.Generic.List<TileDefinition>
            {
                S, S, R,
                P, R, S,
                R, S, P,
            };
            seq.AddRange(refillScript);
            return new PuzzleEngine(TestUtils.Config(Plain3x3), new SequenceSpawner(seq));
        }

        [Test]
        public void 성공한_스왑은_영혼석을_계산하고_턴을_소모한다()
        {
            PuzzleEngine engine = CreateBasicEngine();

            SwapResult result = engine.TrySwap(new GridPos(2, 0), new GridPos(2, 1));

            Assert.That(result.Success, Is.True);
            Assert.That(result.ComboCount, Is.EqualTo(1));
            Assert.That(result.TotalSouls, Is.EqualTo(30), "해골 3개 × 10 영혼석");
            Assert.That(engine.TurnsRemaining, Is.EqualTo(29));
        }

        [Test]
        public void 매치가_없는_스왑은_롤백되고_턴을_소모하지_않는다()
        {
            PuzzleEngine engine = CreateBasicEngine();

            SwapResult result = engine.TrySwap(new GridPos(0, 0), new GridPos(0, 1));

            Assert.That(result.Success, Is.False);
            Assert.That(engine.TurnsRemaining, Is.EqualTo(30));
            Assert.That(engine.Board.GetTile(new GridPos(0, 0)).Definition, Is.SameAs(S), "보드 원상 복구");
            Assert.That(engine.Board.GetTile(new GridPos(0, 1)).Definition, Is.SameAs(P));
        }

        [Test]
        public void 인접하지_않은_스왑은_거부된다()
        {
            PuzzleEngine engine = CreateBasicEngine();

            Assert.That(engine.TrySwap(new GridPos(0, 0), new GridPos(2, 0)).Success, Is.False);
            Assert.That(engine.TrySwap(new GridPos(0, 0), new GridPos(1, 1)).Success, Is.False, "대각선 금지");
        }

        [Test]
        public void 연쇄_콤보는_영혼석_배수가_증가한다()
        {
            // 1차 클리어 후 리필 3칸(y2)에 해골 3개를 심어 2차 연쇄를 강제한다
            PuzzleEngine engine = CreateBasicEngine(S, S, S);

            SwapResult result = engine.TrySwap(new GridPos(2, 0), new GridPos(2, 1));

            Assert.That(result.ComboCount, Is.EqualTo(2));
            Assert.That(result.Steps[0].SoulsEarned, Is.EqualTo(30), "콤보1: 배수 1.0");
            Assert.That(result.Steps[1].SoulsEarned, Is.EqualTo(45), "콤보2: 배수 1.5");
            Assert.That(result.TotalSouls, Is.EqualTo(75));
        }

        [Test]
        public void 매치_인접_벽은_피해를_입고_벽_아래는_대각선_낙하로_채워진다()
        {
            // y2: R R P / y1: W P S / y0: S S R — 벽 (0,1), 내구도 2
            string[] rows = { "OOO", "WOO", "OOO" };
            var seq = new[] { S, S, R, /* (0,1)=벽 스킵 */ P, S, R, R, P };
            var engine = new PuzzleEngine(TestUtils.Config(rows, wallHp: 2), new SequenceSpawner(seq));

            SwapResult result = engine.TrySwap(new GridPos(2, 0), new GridPos(2, 1));

            Assert.That(result.Success, Is.True);
            Assert.That(result.Steps[0].WallHits, Has.Count.EqualTo(1));
            WallHit hit = result.Steps[0].WallHits[0];
            Assert.That(hit.Position, Is.EqualTo(new GridPos(0, 1)));
            Assert.That(hit.Damage, Is.EqualTo(1), "인접 파괴 타일 (0,0) 하나만");
            Assert.That(hit.Destroyed, Is.False);
            Assert.That(engine.Board.GetTile(new GridPos(0, 1)).RemainingHp, Is.EqualTo(1));

            // 대각선 낙하: (1,1)에 있던 Rat이 벽 아래 (0,0)으로 미끄러져 들어온다
            Assert.That(engine.Board.GetTile(new GridPos(0, 0)), Is.Not.Null, "벽 아래가 대각선 낙하로 채워져야 함");
            Assert.That(engine.Board.GetTile(new GridPos(0, 0)).Definition, Is.SameAs(R));

            foreach (GridPos pos in engine.Board.ActivePositions())
                Assert.That(engine.Board.IsOccupied(pos), Is.True, $"{pos} 빈 칸 없이 가득 차야 함");
        }

        [Test]
        public void 초기_보드는_가득_차고_매치가_없다()
        {
            string[] rows = { "OOOOOOO", "OOOOOOO", "OOXXXOO", "OOOOOOO", "OOOOOOO", "OOOOOOO" };
            var weights = new[]
            {
                new TileSpawnWeight(S, 1f),
                new TileSpawnWeight(R, 1f),
                new TileSpawnWeight(P, 0.5f),
            };
            StageConfig config = TestUtils.Config(rows);
            config.SpawnWeights = weights;

            // 시드 고정 랜덤으로 여러 판 검증
            for (int seed = 0; seed < 20; seed++)
            {
                var engine = new PuzzleEngine(config, new WeightedTileSpawner(weights, new Random(seed)));

                foreach (GridPos pos in engine.Board.ActivePositions())
                    Assert.That(engine.Board.IsOccupied(pos), Is.True, $"seed {seed}: {pos} 빈 칸");

                Assert.That(MatchFinder.FindAll(engine.Board), Is.Empty, $"seed {seed}: 초기 매치 존재");
            }
        }
    }
}
