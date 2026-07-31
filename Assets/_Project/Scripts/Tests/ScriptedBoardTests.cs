using System.Collections.Generic;
using ChainRiposte.Core.Board;
using ChainRiposte.Core.Match;
using ChainRiposte.Core.Stage;
using NUnit.Framework;

namespace ChainRiposte.Core.Tests
{
    /// <summary>
    /// 튜토리얼의 <b>고정 보드</b> (<c>Docs/TUTORIAL.md</c> §4.3) — 대본 스포너 + 재추첨/리롤 차단.
    /// </summary>
    public sealed class ScriptedBoardTests
    {
        private static readonly string[] SmallBoard =
        {
            "OOO",
            "OOO",
            "OOO",
        };

        [Test]
        public void 대본이_있으면_그_순서대로_뱉는다()
        {
            var spawner = new ScriptedTileSpawner(
                new List<TileDefinition> { TestUtils.Rat, TestUtils.Skull },
                new SequenceSpawner(new List<TileDefinition>(), TestUtils.Potion));

            Assert.AreEqual(TestUtils.Rat, spawner.NextDefinition());
            Assert.AreEqual(TestUtils.Skull, spawner.NextDefinition());
            Assert.IsTrue(spawner.Exhausted);
            Assert.AreEqual(2, spawner.Consumed);
        }

        [Test]
        public void 대본이_떨어지면_평소_스포너에_넘긴다()
        {
            var spawner = new ScriptedTileSpawner(
                new List<TileDefinition> { TestUtils.Rat },
                new SequenceSpawner(new List<TileDefinition>(), TestUtils.Potion));

            spawner.NextDefinition();
            Assert.AreEqual(TestUtils.Potion, spawner.NextDefinition());
            Assert.AreEqual(TestUtils.Potion, spawner.NextDefinition());
        }

        [Test]
        public void 대본이_비어_있어도_평소_스포너로_돈다()
        {
            var spawner = new ScriptedTileSpawner(
                null, new SequenceSpawner(new List<TileDefinition>(), TestUtils.Skull));

            Assert.IsTrue(spawner.Exhausted);
            Assert.AreEqual(TestUtils.Skull, spawner.NextDefinition());
        }

        /// <summary>
        /// 고정 보드가 아니면 엔진이 즉시 매치를 피하려고 <b>재추첨</b>한다 — 그 과정에서 대본의
        /// 다음 칸을 몰래 당겨 쓰므로 짠 대로 안 깔린다. 이 테스트가 그 사실을 못 박아 둔다.
        /// </summary>
        [Test]
        public void 보통_판은_재추첨이_대본을_당겨_쓴다()
        {
            var script = new List<TileDefinition>();
            for (int i = 0; i < 9; i++)
                script.Add(TestUtils.Rat); // 전부 같은 종류 = 반드시 즉시 매치가 난다

            var spawner = new ScriptedTileSpawner(script, new SequenceSpawner(new List<TileDefinition>(), TestUtils.Potion));
            StageConfig config = TestUtils.Config(SmallBoard);
            _ = new PuzzleEngine(config, spawner, new System.Random(1));

            // 9칸인데 재추첨 때문에 대본을 9개보다 많이(=전부) 읽어 버렸다
            Assert.IsTrue(spawner.Exhausted);
        }

        [Test]
        public void 고정_보드는_대본_그대로_깔린다()
        {
            var script = new List<TileDefinition>();
            for (int i = 0; i < 9; i++)
                script.Add(TestUtils.Rat);

            var spawner = new ScriptedTileSpawner(script, new SequenceSpawner(new List<TileDefinition>(), TestUtils.Potion));
            StageConfig config = TestUtils.Config(SmallBoard);
            config.FixedBoard = true;

            var engine = new PuzzleEngine(config, spawner, new System.Random(1));

            // 한 칸에 정확히 하나씩만 썼다 — 재추첨이 없었다는 뜻이다
            Assert.AreEqual(9, spawner.Consumed);
            foreach (GridPos pos in engine.Board.ActivePositions())
                Assert.AreEqual(TestUtils.Rat, engine.Board.GetTile(pos).Definition);
        }

        /// <summary>
        /// 같은 씨앗 + 같은 대본이면 같은 판. 튜토리얼이 매번 같은 수를 요구할 수 있는 근거다.
        /// </summary>
        [Test]
        public void 같은_씨앗이면_같은_판이_나온다()
        {
            var a = Build(seed: 7);
            var b = Build(seed: 7);
            var c = Build(seed: 8);

            Assert.AreEqual(a, b);
            Assert.AreNotEqual(a, c);
        }

        private static string Build(int seed)
        {
            StageConfig config = TestUtils.Config(new[] { "OOOO", "OOOO", "OOOO", "OOOO" });
            config.SpawnWeights = new List<TileSpawnWeight>
            {
                new(TestUtils.Rat, 1f), new(TestUtils.Skull, 1f), new(TestUtils.Potion, 1f),
            };

            // 대본이 떨어진 뒤의 리필은 이 추첨에 걸린다 — 그래서 씨앗을 스포너와 엔진 둘 다에 넣는다.
            var rng = new System.Random(seed);
            var spawner = new ScriptedTileSpawner(null, new WeightedTileSpawner(config.SpawnWeights, rng));
            var engine = new PuzzleEngine(config, spawner, rng);

            var text = new System.Text.StringBuilder();
            foreach (GridPos pos in engine.Board.ActivePositions())
                text.Append(engine.Board.GetTile(pos).Definition.Id).Append(',');

            return text.ToString();
        }
    }
}
