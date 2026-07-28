using ChainRiposte.Core.Board;
using ChainRiposte.Core.Intrusion;
using ChainRiposte.Core.Stage;
using NUnit.Framework;

namespace ChainRiposte.Core.Tests
{
    public sealed class IntrusionTests
    {
        private static StageConfig IntrusionConfig(float bossChance, int maxLiveBossTiles = 1)
        {
            StageConfig config = TestUtils.Config(new[] { "O", "O", "O" });
            config.SpawnWeights = new[] { new TileSpawnWeight(TestUtils.Skull, 1f) };
            config.BossChanceByScore = _ => bossChance;
            config.BossChanceBySeconds = _ => 0f;
            config.BossEngageSeconds = 10f;
            config.MaxLiveBossTiles = maxLiveBossTiles;
            return config;
        }

        private static BoardGrid EmptyBoard() => new(TestUtils.ParseRows("O", "O", "O").mask);

        [Test]
        public void 스포너는_확률에_따라_보스_타일을_내놓는다()
        {
            var always = new IntrusionSystem(IntrusionConfig(1f), () => 0f);
            always.AttachBoard(EmptyBoard());
            Assert.That(always.Spawner.NextDefinition(), Is.SameAs(always.BossDefinition));

            var never = new IntrusionSystem(IntrusionConfig(0f), () => 0f);
            never.AttachBoard(EmptyBoard());
            Assert.That(never.Spawner.NextDefinition(), Is.SameAs(TestUtils.Skull));
        }

        [Test]
        public void 한_웨이브에서_상한을_넘겨_내놓지_않는다()
        {
            // 확률 1이라도 상한이 1이면 웨이브 안에서 두 번째부터는 일반 타일이 나와야 한다.
            // (보드에 놓이기 전이라 개수를 셀 수 없는 구간 — 여기서 새는 것이 '보드 도배'의 원인이었다)
            var system = new IntrusionSystem(IntrusionConfig(1f), () => 0f);
            system.AttachBoard(EmptyBoard());

            Assert.That(system.Spawner.NextDefinition(), Is.SameAs(system.BossDefinition), "첫 개는 나온다");
            Assert.That(system.Spawner.NextDefinition(), Is.SameAs(TestUtils.Skull), "상한에 걸려 일반 타일");
            Assert.That(system.Spawner.NextDefinition(), Is.SameAs(TestUtils.Skull));
        }

        [Test]
        public void 보드_위_보스_타일이_상한을_채우면_더_안_나온다()
        {
            var system = new IntrusionSystem(IntrusionConfig(1f), () => 0f);
            BoardGrid board = EmptyBoard();
            board.PlaceTile(new GridPos(0, 1), new Tile(system.BossDefinition));
            system.AttachBoard(board);

            Assert.That(system.Spawner.NextDefinition(), Is.SameAs(TestUtils.Skull));
        }

        [Test]
        public void 보스_타일이_바닥에_도달하면_돌입한다()
        {
            var system = new IntrusionSystem(IntrusionConfig(0f), () => 0f);
            BoardGrid board = EmptyBoard();
            board.PlaceTile(new GridPos(0, 0), new Tile(system.BossDefinition)); // 최하단
            system.AttachBoard(board);

            Tile engaged = null;
            bool raised = false;
            system.Engage += tile => { engaged = tile; raised = true; };

            system.OnBoardSettled();

            Assert.That(raised, Is.True);
            Assert.That(engaged, Is.Not.Null, "계기가 된 보스 타일이 넘어온다");
            Assert.That(system.Engaged, Is.True);
        }

        [Test]
        public void 판_시계가_만료되면_보스_타일_없이도_돌입한다()
        {
            var system = new IntrusionSystem(IntrusionConfig(0f), () => 0f);
            system.AttachBoard(EmptyBoard()); // 보드에 보스 타일이 하나도 없다

            Tile engaged = null;
            bool raised = false;
            system.Engage += tile => { engaged = tile; raised = true; };

            system.Tick(9.9f);
            Assert.That(system.Engaged, Is.False);
            Assert.That(system.SecondsUntilEngage, Is.EqualTo(0.1f).Within(1e-3));

            system.Tick(0.2f); // 10초 초과
            Assert.That(raised, Is.True, "시계 만료만으로 돌입한다");
            Assert.That(engaged, Is.Null, "계기가 된 타일이 없다");
        }

        [Test]
        public void 시계를_끄면_보스_타일만_기다린다()
        {
            StageConfig config = IntrusionConfig(0f);
            config.BossEngageSeconds = 0f;
            var system = new IntrusionSystem(config, () => 0f);
            system.AttachBoard(EmptyBoard());

            system.Tick(999f);

            Assert.That(system.HasEngageTimer, Is.False);
            Assert.That(system.Engaged, Is.False, "시계가 꺼져 있으면 시간만으로는 안 온다");
        }
    }
}
