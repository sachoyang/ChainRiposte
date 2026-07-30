using ChainRiposte.Core.Combat;
using ChainRiposte.Core.Stats;
using NUnit.Framework;

namespace ChainRiposte.Core.Tests
{
    /// <summary>
    /// 보스의 기억 — 효과 합산(<see cref="BossMemoryConfig"/>)과 전투 반영
    /// (<c>Docs/PROGRESSION.md</c> §2.2).
    /// </summary>
    public sealed class BossMemoryTests
    {
        private const float Bpm = 60f;      // 1박 = 1초
        private const float FirstDelay = 1f;

        private static BossNoteConfig Note(float beat, float damage = 20f) => new(beat, 1f, damage);

        /// <summary>타격이 1초 간격으로 이어지는 보스 — 연속 패링을 세는 테스트용.</summary>
        private static BossConfig Boss(params BossNoteConfig[] notes)
        {
            var pattern = new BossPatternConfig("Test", Bpm, lengthBeats: 12f, notes);
            return new BossConfig
            {
                MaxHp = 1000f,          // 이 테스트에서 보스가 먼저 눕지 않게
                MaxPosture = 1000f,
                ParryPostureGain = 10f,
                PostureDecayPerSecond = 0f,
                FirstAttackDelaySeconds = FirstDelay,
                PatternGapSeconds = 0f,
                Phases = new[] { new BossPhaseConfig(1f, new[] { new WeightedPattern(pattern) }) },
            };
        }

        private static PlayerStats Stats() =>
            new(new PlayerStatsConfig
            {
                BaseAttackDamage = 10f,
                BaseDamageReduction = 0f,
                BaseParryWindowSeconds = 0.2f,
                AttackCommitSeconds = 0.4f,
                ParryWhiffLockSeconds = 0.5f,
                ParryLateGraceSeconds = 0f,
            });

        // ── 합산 규칙 ──

        [Test]
        public void 기억이_없으면_기본값이다()
        {
            BossMemoryConfig total = BossMemoryConfig.Combine(null);

            Assert.That(total.BonusParryPostureGain, Is.Zero);
            Assert.That(total.WhiffLockMultiplier, Is.EqualTo(1f));
            Assert.That(total.PerfectStreakGuard, Is.Zero);
            Assert.That(total.HasEffect, Is.False);
        }

        [Test]
        public void 가산은_더하고_배수는_곱한다()
        {
            BossMemoryConfig total = BossMemoryConfig.Combine(new[]
            {
                new BossMemoryConfig { BonusParryPostureGain = 5f, WhiffLockMultiplier = 0.8f },
                new BossMemoryConfig { BonusParryPostureGain = 3f, WhiffLockMultiplier = 0.9f },
            });

            Assert.That(total.BonusParryPostureGain, Is.EqualTo(8f));
            Assert.That(total.WhiffLockMultiplier, Is.EqualTo(0.72f).Within(1e-5f));
        }

        [Test]
        public void 헛침_잠금은_아무리_모아도_하한_아래로_안_내려간다()
        {
            BossMemoryConfig total = BossMemoryConfig.Combine(new[]
            {
                new BossMemoryConfig { WhiffLockMultiplier = 0.5f },
                new BossMemoryConfig { WhiffLockMultiplier = 0.5f },
                new BossMemoryConfig { WhiffLockMultiplier = 0.5f },
            });

            Assert.That(total.WhiffLockMultiplier, Is.EqualTo(BossMemoryConfig.MinWhiffLockMultiplier),
                "기억을 다 모아도 헛침 처벌이 사라지면 연타 게임이 된다");
        }

        [Test]
        public void 안_채운_배수_칸은_효과_없음으로_읽는다()
        {
            // 인스펙터에서 안 건드린 float은 0이다. 그것을 곧이곧대로 곱하면
            // "아무 효과도 안 적은 기억"이 헛침 처벌을 통째로 없애 버린다.
            BossMemoryConfig total = BossMemoryConfig.Combine(new[]
            {
                new BossMemoryConfig { WhiffLockMultiplier = 0f },
                new BossMemoryConfig { WhiffLockMultiplier = 2f },
            });

            Assert.That(total.WhiffLockMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void 보호막_조건은_가장_관대한_쪽이_이긴다()
        {
            BossMemoryConfig total = BossMemoryConfig.Combine(new[]
            {
                new BossMemoryConfig { PerfectStreakGuard = 5 },
                new BossMemoryConfig { PerfectStreakGuard = 3 },
            });

            Assert.That(total.PerfectStreakGuard, Is.EqualTo(3), "조건을 두 벌 세면 어느 쪽이 찼는지 못 읽는다");
        }

        // ── 전투 반영 ──

        [Test]
        public void 패링_체간_가산이_보스_값에_더해진다()
        {
            var combat = new CombatSystem(
                Boss(Note(1f)), Stats(), new PlayerHealth(100),
                memories: new BossMemoryConfig { BonusParryPostureGain = 6f });

            combat.Tick(1.9f);
            combat.PressParry();

            Assert.That(combat.Posture, Is.EqualTo(16f), "보스의 10 + 기억의 6");
        }

        [Test]
        public void 헛침_잠금이_배수만큼_짧아진다()
        {
            var combat = new CombatSystem(
                Boss(Note(4f)), Stats(), new PlayerHealth(100),
                memories: new BossMemoryConfig { WhiffLockMultiplier = 0.8f });

            combat.PressParry(); // 판정 밖 — 헛침. 잠금 0.5 × 0.8 = 0.4초
            Assert.That(combat.PlayerState, Is.EqualTo(PlayerActionState.ParryRecovering));

            combat.Tick(0.41f);
            Assert.That(combat.PlayerState, Is.EqualTo(PlayerActionState.Ready), "0.4초면 풀려야 한다");
        }

        [Test]
        public void 연속_패링_3회를_채우면_다음_피격_한_번이_무효가_된다()
        {
            var health = new PlayerHealth(100);
            // 타격 t=2,3,4 를 다 막고, t=5 는 일부러 안 막는다
            var combat = new CombatSystem(
                Boss(Note(1f), Note(2f), Note(3f), Note(4f)), Stats(), health,
                memories: new BossMemoryConfig { PerfectStreakGuard = 3 });

            bool nullified = false;
            combat.HitNullifiedByMemory += _ => nullified = true;

            // 패턴은 t=1에 시작하므로 타격은 t=2·3·4·5 다. 매번 타격 0.1초 전에 누른다.
            combat.Tick(1.9f);
            combat.PressParry();
            for (int i = 0; i < 2; i++)
            {
                combat.Tick(1f);
                combat.PressParry();
            }

            Assert.That(combat.MemoryGuardReady, Is.True, "3연속 패링으로 보호막이 찼다");
            Assert.That(combat.ParryStreak, Is.Zero, "채운 순간 0으로 되돌아간다 — 안 그러면 사실상 무적이다");

            combat.Tick(1.2f); // t=5 타격을 그냥 맞아 본다 (지금 t=3.9)

            Assert.That(nullified, Is.True);
            Assert.That(health.Current, Is.EqualTo(100), "보호막이 한 대를 통째로 지웠다");
            Assert.That(combat.MemoryGuardReady, Is.False, "쓰면 사라진다");
        }

        [Test]
        public void 보호막이_없으면_그냥_맞는다()
        {
            var health = new PlayerHealth(100);
            var combat = new CombatSystem(
                Boss(Note(1f), Note(2f)), Stats(), health,
                memories: new BossMemoryConfig { PerfectStreakGuard = 3 });

            combat.Tick(2.1f); // t=2 첫 타격을 안 막는다 (연속 0)

            Assert.That(health.Current, Is.EqualTo(80));
            Assert.That(combat.MemoryGuardReady, Is.False);
        }

        [Test]
        public void 헛치면_연속이_끊긴다()
        {
            var combat = new CombatSystem(
                Boss(Note(1f), Note(3f)), Stats(), new PlayerHealth(100),
                memories: new BossMemoryConfig { PerfectStreakGuard = 3 });

            combat.Tick(1.9f);
            combat.PressParry();      // 1연속
            Assert.That(combat.ParryStreak, Is.EqualTo(1));

            combat.Tick(0.2f);
            combat.PressParry();      // 판정 밖 — 헛침

            Assert.That(combat.ParryStreak, Is.Zero, "막 눌러서 연속을 쌓을 수는 없다");
        }

        [Test]
        public void 기억을_안_가진_판에서는_연속을_세지_않는다()
        {
            var combat = new CombatSystem(Boss(Note(1f)), Stats(), new PlayerHealth(100));

            combat.Tick(1.9f);
            combat.PressParry();

            Assert.That(combat.ParryStreak, Is.Zero);
            Assert.That(combat.MemoryGuardStreakRequired, Is.Zero);
            Assert.That(combat.MemoryGuardReady, Is.False);
        }
    }
}
