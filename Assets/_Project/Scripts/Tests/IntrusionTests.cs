using ChainRiposte.Core.Board;
using ChainRiposte.Core.Intrusion;
using ChainRiposte.Core.Stage;
using NUnit.Framework;

namespace ChainRiposte.Core.Tests
{
    public sealed class IntrusionTests
    {
        private static StageConfig IntrusionConfig(float bossChance)
        {
            StageConfig config = TestUtils.Config(new[] { "O", "O", "O" });
            config.SpawnWeights = new[] { new TileSpawnWeight(TestUtils.Skull, 1f) };
            config.BossChanceByScore = _ => bossChance;
            config.BossChanceBySeconds = _ => 0f;
            config.BossCountdownSeconds = 10f;
            config.BossCountdownTurns = 3;
            return config;
        }

        [Test]
        public void 스포너는_확률에_따라_보스_타일을_내놓는다()
        {
            var always = new IntrusionSystem(IntrusionConfig(1f), () => 0f);
            Assert.That(always.Spawner.NextDefinition(), Is.SameAs(always.BossDefinition));

            var never = new IntrusionSystem(IntrusionConfig(0f), () => 0f);
            Assert.That(never.Spawner.NextDefinition(), Is.SameAs(TestUtils.Skull));
        }

        [Test]
        public void 보스_타일이_바닥에_도달하면_정상_돌입한다()
        {
            var system = new IntrusionSystem(IntrusionConfig(0f), () => 0f);
            var board = new BoardGrid(TestUtils.ParseRows("O", "O", "O").mask);
            board.PlaceTile(new GridPos(0, 0), new Tile(system.BossDefinition)); // 최하단
            system.AttachBoard(board);

            Tile engaged = null;
            bool? wasAmbush = null;
            system.Engage += (tile, ambush) => { engaged = tile; wasAmbush = ambush; };

            system.OnBoardSettled();

            Assert.That(engaged, Is.Not.Null);
            Assert.That(wasAmbush, Is.False, "바닥 도달은 정상 돌입");
            Assert.That(system.Engaged, Is.True);
        }

        [Test]
        public void 시간이_만료되면_기습_돌입한다()
        {
            var system = new IntrusionSystem(IntrusionConfig(0f), () => 0f);
            var board = new BoardGrid(TestUtils.ParseRows("O", "O", "O").mask);
            board.PlaceTile(new GridPos(0, 1), new Tile(system.BossDefinition)); // 바닥 아님
            system.AttachBoard(board);

            bool? wasAmbush = null;
            system.Engage += (tile, ambush) => wasAmbush = ambush;

            system.OnBoardSettled(); // 카운트다운 시작
            Assert.That(system.Engaged, Is.False, "추적만 시작, 아직 돌입 아님");

            system.Tick(9.9f);
            Assert.That(system.Engaged, Is.False);

            system.Tick(0.2f); // 10초 초과
            Assert.That(wasAmbush, Is.True, "시간 만료는 기습 돌입");
        }

        [Test]
        public void 턴이_만료되면_기습_돌입한다()
        {
            var system = new IntrusionSystem(IntrusionConfig(0f), () => 0f);
            var board = new BoardGrid(TestUtils.ParseRows("O", "O", "O").mask);
            board.PlaceTile(new GridPos(0, 2), new Tile(system.BossDefinition));
            system.AttachBoard(board);

            bool? wasAmbush = null;
            system.Engage += (tile, ambush) => wasAmbush = ambush;

            system.OnBoardSettled();
            system.OnTurnConsumed(29);
            system.OnTurnConsumed(28);
            Assert.That(system.Engaged, Is.False, "3턴 중 2턴 소모");

            system.OnTurnConsumed(27);
            Assert.That(wasAmbush, Is.True, "턴 만료는 기습 돌입");
        }
    }
}
