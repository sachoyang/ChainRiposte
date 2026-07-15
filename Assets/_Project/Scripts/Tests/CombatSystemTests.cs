using System;
using System.Collections.Generic;
using ChainRiposte.Core.Combat;
using ChainRiposte.Core.Stats;
using NUnit.Framework;

namespace ChainRiposte.Core.Tests
{
    public sealed class CombatSystemTests
    {
        private const float FirstDelay = 1f; // 기본: t=1에 첫 텔레그래프 시작

        private static BossAttackConfig Attack(
            float telegraph = 1f, float damage = 20f, bool parryable = true, float recovery = 1f) =>
            new(telegraph, damage, parryable, recovery);

        private static BossConfig Boss(params BossAttackConfig[] pattern) => new()
        {
            MaxHp = 100f,
            MaxPosture = 100f,
            ParryPostureGain = 40f,
            AttackPostureFactor = 0.5f,
            PostureDecayPerSecond = 0f, // 회복은 전용 테스트에서만 켠다
            FirstAttackDelaySeconds = FirstDelay,
            Pattern = pattern.Length > 0 ? pattern : new[] { Attack() },
        };

        private static PlayerStats Stats(float def = 0f, float atk = 10f) =>
            new(new PlayerStatsConfig
            {
                BaseAttackDamage = atk,
                BaseDamageReduction = def,
                BaseParryWindowSeconds = 0.2f,
                AttackCommitSeconds = 0.4f,
                ParryWhiffLockSeconds = 0.25f,
            });

        [Test]
        public void 패링_성공시_피해없이_체간이_대폭_상승한다()
        {
            var health = new PlayerHealth(100);
            var combat = new CombatSystem(Boss(), Stats(), health);
            bool parried = false;
            combat.AttackParried += _ => parried = true;

            combat.Tick(1.9f);       // 텔레그래프 진행 중 (타격은 t=2)
            combat.PressParry();     // 윈도우 0.2초 → t=2.1까지 유효
            combat.Tick(0.2f);       // t=2 타격 → 패링

            Assert.That(parried, Is.True);
            Assert.That(health.Current, Is.EqualTo(100), "패링 성공은 피해 0");
            Assert.That(combat.Posture, Is.EqualTo(40f), "체간 대폭 상승");
            Assert.That(combat.PlayerState, Is.EqualTo(PlayerActionState.Ready), "성공 시 후딜레이 없이 복귀");
        }

        [Test]
        public void 패링_미입력시_방어력을_뺀_피해를_입는다()
        {
            var health = new PlayerHealth(100);
            var combat = new CombatSystem(Boss(Attack(damage: 20f)), Stats(def: 5f), health);
            int hitDamage = -1;
            combat.PlayerHit += (_, dmg) => hitDamage = dmg;

            combat.Tick(2f); // t=2 타격, 패링 없음

            Assert.That(hitDamage, Is.EqualTo(15), "20 − DEF 5");
            Assert.That(health.Current, Is.EqualTo(85));
        }

        [Test]
        public void 판정_윈도우가_지난_뒤의_타격은_맞는다()
        {
            var health = new PlayerHealth(100);
            var combat = new CombatSystem(Boss(), Stats(), health);

            combat.PressParry(); // t=0에 헛침 — 윈도우는 t=0.2에 종료
            combat.Tick(2f);     // 타격은 t=2

            Assert.That(health.Current, Is.EqualTo(80), "윈도우 밖 타격은 피격");
        }

        [Test]
        public void 공격_커밋_중에는_패링_입력이_무시된다()
        {
            var health = new PlayerHealth(100);
            var combat = new CombatSystem(Boss(), Stats(), health);

            combat.Tick(1.8f);   // 타격(t=2) 직전
            combat.PressAttack(); // 커밋 t=1.8~2.2 — 이 동안 무방비
            combat.PressParry();  // 무시되어야 한다

            Assert.That(combat.PlayerState, Is.EqualTo(PlayerActionState.Attacking));

            combat.Tick(0.5f); // t=2 피격 → t=2.2 공격 적중

            Assert.That(health.Current, Is.EqualTo(80), "커밋 중 타격은 그대로 맞는다");
            Assert.That(combat.BossHp, Is.EqualTo(90f), "커밋이 끝나면 공격은 적중한다");
        }

        [Test]
        public void 공격은_보스HP와_체간을_소폭_깎는다()
        {
            var combat = new CombatSystem(Boss(), Stats(atk: 10f), new PlayerHealth(100));

            combat.PressAttack();
            combat.Tick(0.4f); // 커밋 종료 → 적중

            Assert.That(combat.BossHp, Is.EqualTo(90f));
            Assert.That(combat.Posture, Is.EqualTo(5f), "ATK 10 × 배율 0.5");
        }

        [Test]
        public void 체간_한계치_도달시_인살_대기가_되고_공격버튼으로_승리한다()
        {
            BossConfig config = Boss();
            config.ParryPostureGain = 100f; // 패링 1회로 파괴
            var combat = new CombatSystem(config, Stats(), new PlayerHealth(100));
            bool broken = false, executed = false;
            bool? victory = null;
            combat.BossBroken += () => broken = true;
            combat.ExecutionPerformed += () => executed = true;
            combat.Ended += v => victory = v;

            combat.Tick(1.9f);
            combat.PressParry();
            combat.Tick(0.2f); // 패링 → 체간 100 → 파괴

            Assert.That(broken, Is.True);
            Assert.That(combat.ExecutionReady, Is.True);
            Assert.That(combat.BossState, Is.EqualTo(BossActionState.Broken));

            combat.Tick(5f); // 파괴 상태에서는 더 이상 공격받지 않는다
            Assert.That(combat.Finished, Is.False);

            combat.PressAttack(); // 인살

            Assert.That(executed, Is.True);
            Assert.That(victory, Is.True);
            Assert.That(combat.Finished, Is.True);
        }

        [Test]
        public void 보스HP_소진시_체간이_즉시_파괴된다()
        {
            BossConfig config = Boss();
            config.MaxHp = 10f;
            var combat = new CombatSystem(config, Stats(atk: 10f), new PlayerHealth(100));

            combat.PressAttack();
            combat.Tick(0.4f);

            Assert.That(combat.BossHp, Is.EqualTo(0f));
            Assert.That(combat.ExecutionReady, Is.True, "HP 소진 = 인살로만 마무리");
        }

        [Test]
        public void 플레이어HP_소진시_패배로_끝난다()
        {
            var combat = new CombatSystem(Boss(Attack(damage: 20f)), Stats(), new PlayerHealth(10));
            bool? victory = null;
            combat.Ended += v => victory = v;

            combat.Tick(2f);

            Assert.That(victory, Is.False);
            Assert.That(combat.Finished, Is.True);
        }

        [Test]
        public void 패링불가_공격은_윈도우_안이어도_맞는다()
        {
            var health = new PlayerHealth(100);
            var combat = new CombatSystem(Boss(Attack(parryable: false)), Stats(), health);

            combat.Tick(1.9f);
            combat.PressParry();
            combat.Tick(0.2f);

            Assert.That(health.Current, Is.EqualTo(80));
            Assert.That(combat.Posture, Is.EqualTo(0f));
        }

        [Test]
        public void 체간은_시간이_지나면_회복된다()
        {
            BossConfig config = Boss();
            config.FirstAttackDelaySeconds = 10f; // 회복만 관찰
            config.PostureDecayPerSecond = 10f;
            config.ScaleDecayWithHp = false;
            config.AttackPostureFactor = 1f;
            var combat = new CombatSystem(config, Stats(atk: 10f), new PlayerHealth(100));

            combat.PressAttack();
            combat.Tick(0.4f); // 적중 → 체간 10

            combat.Tick(0.5f); // 10 − 10/s × 0.5s

            Assert.That(combat.Posture, Is.EqualTo(5f).Within(0.001f));
        }

        [Test]
        public void 패턴은_순서대로_반복되며_큰_틱에도_결정적이다()
        {
            var combat = new CombatSystem(
                Boss(
                    Attack(telegraph: 1f, damage: 1f, recovery: 1f),
                    Attack(telegraph: 0.5f, damage: 1f, recovery: 0.5f)),
                Stats(),
                new PlayerHealth(100));
            var indices = new List<int>();
            combat.AttackTelegraphed += (i, _) => indices.Add(i);

            combat.Tick(4.1f); // t=1 공격0, t=3 공격1, t=4 공격0 — 한 번의 큰 틱으로 진행

            Assert.That(indices, Is.EqualTo(new[] { 0, 1, 0 }));
        }

        [Test]
        public void 패링_헛침후_잠금시간_동안_재입력이_불가하다()
        {
            var combat = new CombatSystem(Boss(), Stats(), new PlayerHealth(100));

            combat.PressParry();  // 윈도우 t=0~0.2, 잠금 t=0.2~0.45
            combat.Tick(0.3f);

            Assert.That(combat.PlayerState, Is.EqualTo(PlayerActionState.ParryRecovering));
            combat.PressParry(); // 무시
            Assert.That(combat.PlayerState, Is.EqualTo(PlayerActionState.ParryRecovering));

            combat.Tick(0.2f); // t=0.5 — 잠금 해제

            Assert.That(combat.PlayerState, Is.EqualTo(PlayerActionState.Ready));
        }

        [Test]
        public void 빈_공격_패턴은_예외를_던진다()
        {
            var config = new BossConfig { Pattern = Array.Empty<BossAttackConfig>() };

            Assert.Throws<ArgumentException>(() =>
                _ = new CombatSystem(config, Stats(), new PlayerHealth(100)));
        }
    }
}
