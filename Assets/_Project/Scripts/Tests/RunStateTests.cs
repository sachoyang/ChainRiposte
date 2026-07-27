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

            RunState original = new(grown.Capture(), new[] { "relic_a", "relic_b" }, chainStep: 3, newGamePlusCount: 1);

            RunState round = RunState.Deserialize(original.Serialize());

            Assert.That(round.Stats.Level, Is.EqualTo(original.Stats.Level));
            Assert.That(round.Stats.Souls, Is.EqualTo(original.Stats.Souls));
            Assert.That(round.Stats.PendingPoints, Is.EqualTo(original.Stats.PendingPoints));
            Assert.That(round.Stats.StatLevels, Is.EqualTo(original.Stats.StatLevels));
            Assert.That(round.AcquiredRelicIds, Is.EquivalentTo(new[] { "relic_a", "relic_b" }));
            Assert.That(round.ChainStep, Is.EqualTo(3));
            Assert.That(round.NewGamePlusCount, Is.EqualTo(1));
        }

        [Test]
        public void 빈_문자열이나_깨진_세이브는_기본_런으로_떨어진다()
        {
            Assert.That(RunState.Deserialize(null).Stats.Level, Is.EqualTo(1));
            Assert.That(RunState.Deserialize("").ChainStep, Is.EqualTo(0));
            Assert.That(RunState.Deserialize("쓰레기값").AcquiredRelicIds, Is.Empty);
            Assert.That(RunState.Deserialize("v1|불완전").Stats.Level, Is.EqualTo(1), "섹션이 모자라면 기본값");
        }

        [Test]
        public void 넋은_중복으로_흡수되지_않는다()
        {
            RunState run = new();

            Assert.That(run.AddRelic("relic_a"), Is.True);
            Assert.That(run.AddRelic("relic_a"), Is.False, "같은 넋 재흡수 무시");
            Assert.That(run.HasRelic("relic_a"), Is.True);
            Assert.That(run.AcquiredRelicIds.Count, Is.EqualTo(1));
        }

        [Test]
        public void 스탯_갱신은_넋과_사슬을_건드리지_않는다()
        {
            RunState run = new(relicIds: new[] { "relic_a" }, chainStep: 2);

            PlayerStats grown = new(Config());
            grown.AddSouls(200);
            run.UpdateStats(grown.Capture());

            Assert.That(run.Stats.Level, Is.EqualTo(grown.Level), "성장은 이어진다");
            Assert.That(run.AcquiredRelicIds, Is.EquivalentTo(new[] { "relic_a" }), "넋은 유지");
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
    }
}
