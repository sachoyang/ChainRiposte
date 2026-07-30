using ChainRiposte.Core.Progress;
using ChainRiposte.Core.Stats;
using NUnit.Framework;

namespace ChainRiposte.Core.Tests
{
    /// <summary>
    /// 성장 캐리(<c>Docs/PROGRESSION.md</c>)의 기반 — 런 상태 왕복과 PlayerStats 복원/캡처.
    /// </summary>
    public sealed class RunStateTests
    {
        private static PlayerStatsConfig Config() => new()
        {
            BaseAttackDamage = 10f,
            AttackDamagePerLevel = 3f,
            BaseParryWindowSeconds = 0.13f,
            ParryWindowPerLevelSeconds = 0.024f,
            AttackPointCost = 1,
            DefensePointCost = 1,
            ParryPointCost = 2,
        };

        [Test]
        public void 스냅샷_복원은_레벨과_소울과_스탯을_그대로_되살린다()
        {
            PlayerStats grown = new(Config());
            grown.AddSouls(400);              // 레벨·포인트 적립
            grown.Allocate(StatType.Attack);  // ATK 1
            grown.Allocate(StatType.Attack);  // ATK 2

            PlayerStats restored = new(Config(), grown.Capture());

            Assert.That(restored.Level, Is.EqualTo(grown.Level));
            Assert.That(restored.Souls, Is.EqualTo(grown.Souls));
            Assert.That(restored.PendingPoints, Is.EqualTo(grown.PendingPoints));
            Assert.That(restored.GetStatLevel(StatType.Attack), Is.EqualTo(2));
            Assert.That(restored.AttackDamage, Is.EqualTo(grown.AttackDamage), "복원된 파생 수치도 같아야 한다");
        }

        [Test]
        public void 복원은_이벤트를_발행하지_않는다()
        {
            PlayerStats grown = new(Config());
            grown.AddSouls(100);
            bool fired = false;

            PlayerStats restored = new(Config(), grown.Capture());
            restored.SoulsChanged += (_, __) => fired = true;
            restored.LeveledUp += _ => fired = true;

            Assert.That(fired, Is.False, "생성만으로는 아무 이벤트도 안 나가야 한다");
        }

        [Test]
        public void 런상태_직렬화_왕복이_전부를_보존한다()
        {
            PlayerStats grown = new(Config());
            grown.AddSouls(500);
            grown.Allocate(StatType.Defense);
            grown.Allocate(StatType.Parry);

            RunState original = new(grown.Capture(), new[] { "memory_a", "memory_b" }, chainStep: 3, newGamePlusCount: 1);

            RunState round = RunState.Deserialize(original.Serialize());

            Assert.That(round.Stats.Level, Is.EqualTo(original.Stats.Level));
            Assert.That(round.Stats.Souls, Is.EqualTo(original.Stats.Souls));
            Assert.That(round.Stats.PendingPoints, Is.EqualTo(original.Stats.PendingPoints));
            Assert.That(round.Stats.StatLevels, Is.EqualTo(original.Stats.StatLevels));
            Assert.That(round.AcquiredMemoryIds, Is.EqualTo(new[] { "memory_a", "memory_b" }), "먹은 순서까지 보존");
            Assert.That(round.ChainStep, Is.EqualTo(3));
            Assert.That(round.NewGamePlusCount, Is.EqualTo(1));
        }

        [Test]
        public void 빈_문자열이나_깨진_세이브는_기본_런으로_떨어진다()
        {
            Assert.That(RunState.Deserialize(null).Stats.Level, Is.EqualTo(1));
            Assert.That(RunState.Deserialize("").ChainStep, Is.EqualTo(0));
            Assert.That(RunState.Deserialize("쓰레기값").AcquiredMemoryIds, Is.Empty);
            Assert.That(RunState.Deserialize("v1|불완전").Stats.Level, Is.EqualTo(1), "섹션이 모자라면 기본값");
        }

        [Test]
        public void 기억은_중복으로_삼켜지지_않는다()
        {
            RunState run = new();

            Assert.That(run.AddMemory("memory_a"), Is.True);
            Assert.That(run.AddMemory("memory_a"), Is.False, "같은 보스의 기억 재흡수 무시");
            Assert.That(run.HasMemory("memory_a"), Is.True);
            Assert.That(run.AcquiredMemoryIds.Count, Is.EqualTo(1));
        }

        [Test]
        public void 스탯_갱신은_기억과_사슬을_건드리지_않는다()
        {
            RunState run = new(memoryIds: new[] { "memory_a" }, chainStep: 2);

            PlayerStats grown = new(Config());
            grown.AddSouls(200);
            run.UpdateStats(grown.Capture());

            Assert.That(run.Stats.Level, Is.EqualTo(grown.Level), "성장은 이어진다");
            Assert.That(run.AcquiredMemoryIds, Is.EquivalentTo(new[] { "memory_a" }), "기억은 유지");
            Assert.That(run.ChainStep, Is.EqualTo(2), "사슬은 유지");
        }

        [Test]
        public void 죽으면_사슬만_끊기고_이어가면_한칸_오른다()
        {
            RunState run = new(chainStep: 4);

            run.BreakChain();
            Assert.That(run.ChainStep, Is.EqualTo(0));

            run.AdvanceChain();
            run.AdvanceChain();
            Assert.That(run.ChainStep, Is.EqualTo(2));
        }

        [Test]
        public void 채굴량은_스테이지별로_쌓인다()
        {
            RunState run = new();

            Assert.That(run.GetHarvested("Stage_1_1"), Is.EqualTo(0), "안 가본 땅은 0");

            run.Harvest("Stage_1_1", 120);
            run.Harvest("Stage_1_1", 30);
            run.Harvest("Stage_1_2", 50);

            Assert.That(run.GetHarvested("Stage_1_1"), Is.EqualTo(150), "같은 스테이지는 누적");
            Assert.That(run.GetHarvested("Stage_1_2"), Is.EqualTo(50), "다른 스테이지는 따로");
        }

        [Test]
        public void 채굴량도_세이브를_왕복한다()
        {
            RunState run = new(chainStep: 3);
            run.Harvest("Stage_1_1", 200);
            run.Harvest("Stage_2_3", 75);

            RunState restored = RunState.Deserialize(run.Serialize());

            Assert.That(restored.GetHarvested("Stage_1_1"), Is.EqualTo(200));
            Assert.That(restored.GetHarvested("Stage_2_3"), Is.EqualTo(75));
            Assert.That(restored.ChainStep, Is.EqualTo(3), "기존 칸도 그대로");
        }

        [Test]
        public void 광맥은_남은_만큼만_준다()
        {
            RunEconomyConfig economy = new() { DefaultStageSoulBudget = 300 };

            Assert.That(economy.RemainingSouls(budget: 0, harvested: 0), Is.EqualTo(300), "기본값 폴백");
            Assert.That(economy.RemainingSouls(budget: 500, harvested: 120), Is.EqualTo(380), "스테이지 값 우선");
            Assert.That(economy.RemainingSouls(budget: 100, harvested: 250), Is.EqualTo(0), "넘게 캤어도 음수 아님");
        }

        [Test]
        public void 매장량을_안_정하면_무제한이다()
        {
            RunEconomyConfig economy = new(); // 기본값 0 = 광맥 개념 끔

            Assert.That(economy.ResolveBudget(0), Is.EqualTo(0), "0이면 무제한");
            Assert.That(economy.RemainingSouls(budget: 0, harvested: 9999), Is.EqualTo(int.MaxValue));
        }
    }
}
