using System;
using ChainRiposte.Core.Stats;
using NUnit.Framework;

namespace ChainRiposte.Core.Tests
{
    public sealed class PlayerStatsTests
    {
        // 밸런스 기본값이 바뀌어도 테스트가 깨지지 않도록 여기서 쓰는 값은 명시한다
        private static PlayerStats CreateStats(int parryCap = 5) =>
            new(new PlayerStatsConfig
            {
                ParryLevelHardCap = parryCap,
                BaseParryWindowSeconds = 0.15f,
                ParryWindowPerLevelSeconds = 0.03f,
            });

        [Test]
        public void 영혼석이_요구량에_도달하면_레벨업하고_포인트를_적립한다()
        {
            PlayerStats stats = CreateStats();

            stats.AddSouls(30); // 첫 요구량 30

            Assert.That(stats.Level, Is.EqualTo(2));
            Assert.That(stats.PendingPoints, Is.EqualTo(1));
            Assert.That(stats.Souls, Is.EqualTo(0));
            Assert.That(stats.SoulsToNextLevel, Is.EqualTo(45), "요구량 30 + 15");
        }

        [Test]
        public void 한_번에_큰_영혼석은_연속_레벨업된다()
        {
            PlayerStats stats = CreateStats();

            stats.AddSouls(80); // 30 소모 → 레벨2, 45 소모 → 레벨3, 잔여 5

            Assert.That(stats.Level, Is.EqualTo(3));
            Assert.That(stats.PendingPoints, Is.EqualTo(2));
            Assert.That(stats.Souls, Is.EqualTo(5));
        }

        [Test]
        public void 포인트가_없으면_할당할_수_없다()
        {
            PlayerStats stats = CreateStats();

            Assert.That(stats.CanAllocate(StatType.Attack), Is.False);
            Assert.Throws<InvalidOperationException>(() => stats.Allocate(StatType.Attack));
        }

        [Test]
        public void 스탯_할당은_파생_수치에_반영된다()
        {
            PlayerStats stats = CreateStats();
            stats.AddSouls(200);

            stats.Allocate(StatType.Attack);
            stats.Allocate(StatType.Defense);
            stats.Allocate(StatType.Parry);

            Assert.That(stats.AttackDamage, Is.EqualTo(13f), "기본 10 + 3");
            Assert.That(stats.DamageReduction, Is.EqualTo(2f), "기본 0 + 2");
            Assert.That(stats.ParryWindowSeconds, Is.EqualTo(0.18f).Within(1e-5), "기본 0.15 + 0.03");
        }

        [Test]
        public void 판정치는_하드_캡_이후_할당할_수_없다()
        {
            PlayerStats stats = CreateStats(parryCap: 2);
            stats.AddSouls(1000); // 포인트 충분히 적립

            stats.Allocate(StatType.Parry);
            stats.Allocate(StatType.Parry);

            Assert.That(stats.GetStatLevel(StatType.Parry), Is.EqualTo(2));
            Assert.That(stats.CanAllocate(StatType.Parry), Is.False, "하드 캡 도달");
            Assert.That(stats.CanAllocate(StatType.Attack), Is.True, "다른 스탯은 가능");
            Assert.Throws<InvalidOperationException>(() => stats.Allocate(StatType.Parry));
        }
    }
}
