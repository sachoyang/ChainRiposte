using ChainRiposte.Core.Combat;
using ChainRiposte.Core.Stats;
using NUnit.Framework;

namespace ChainRiposte.Core.Tests
{
    /// <summary>
    /// 난이도 곡선(<c>Docs/PROGRESSION.md</c> §2.5) — 고리 깊이 → 배수, 배수 → 보스 config,
    /// 그리고 실패 처벌(헛침 시 보스 체간 회복).
    /// </summary>
    public sealed class DifficultyTests
    {
        private const float Bpm = 60f;

        private static DifficultyCurve Curve() => new()
        {
            HpPerLink = 0.04f,
            PosturePerLink = 0.08f,
            DamagePerLink = 0.06f,
            TempoPerLink = 0.03f,
            HpPerNewGamePlus = 0.25f,
            PosturePerNewGamePlus = 0.25f,
            DamagePerNewGamePlus = 0.3f,
            TempoPerNewGamePlus = 0.08f,
            MaxTempoMultiplier = 1.5f,
        };

        private static BossConfig Boss(float damage = 20f, float speed = 1f)
        {
            var pattern = new BossPatternConfig(
                "Test", Bpm, lengthBeats: 8f, new[] { new BossNoteConfig(1f, 1f, damage) }, speed);
            var phase = new BossPhaseConfig(1f, new[] { new WeightedPattern(pattern) });

            return new BossConfig
            {
                MaxHp = 100f,
                MaxPosture = 100f,
                ParryPostureGain = 40f,
                PostureDecayPerSecond = 6f,
                WhiffPostureRecovery = 8f,
                Phases = new[] { phase },
            };
        }

        // ── 커브 ────────────────────────────────────────────────────────────────

        [Test]
        public void 첫_고리는_배수가_전부_1이다()
        {
            DifficultyConfig scale = Curve().Evaluate(linkDepth: 0);

            Assert.That(scale.IsIdentity, Is.True, "첫 판은 보스 에셋 값 그대로여야 한다");
        }

        [Test]
        public void 고리가_깊어질수록_등차로_부푼다()
        {
            DifficultyConfig scale = Curve().Evaluate(linkDepth: 5);

            Assert.That(scale.Hp, Is.EqualTo(1.20f).Within(0.001f));
            Assert.That(scale.Posture, Is.EqualTo(1.40f).Within(0.001f));
            Assert.That(scale.Damage, Is.EqualTo(1.30f).Within(0.001f));
            Assert.That(scale.Tempo, Is.EqualTo(1.15f).Within(0.001f));
        }

        [Test]
        public void NG플러스는_고리_배수_위에_곱해진다()
        {
            DifficultyConfig scale = Curve().Evaluate(linkDepth: 5, newGamePlusCount: 1);

            // (1 + 0.08×5) × (1 + 0.25) — 더하는 게 아니라 곱한다
            Assert.That(scale.Posture, Is.EqualTo(1.40f * 1.25f).Within(0.001f));
        }

        [Test]
        public void 템포는_천장을_넘지_않는다()
        {
            // 회차를 아무리 돌아도 예비동작이 반응 시간보다 짧아지면 실력이 아니라 운이 된다
            DifficultyConfig scale = Curve().Evaluate(linkDepth: 5, newGamePlusCount: 9);

            Assert.That(scale.Tempo, Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(scale.Hp, Is.GreaterThan(2f), "천장은 템포에만 있다");
        }

        [Test]
        public void 지도를_안_거친_판은_깊이가_0으로_취급된다()
        {
            Assert.That(Curve().Evaluate(linkDepth: -3, newGamePlusCount: -1).IsIdentity, Is.True);
        }

        // ── 스케일러 ────────────────────────────────────────────────────────────

        [Test]
        public void 배수가_HP_체간_피해_템포에_곱해진다()
        {
            BossConfig scaled = BossConfigScaler.Scale(Boss(damage: 20f), Curve().Evaluate(linkDepth: 5));

            Assert.That(scaled.MaxHp, Is.EqualTo(120f).Within(0.01f));
            Assert.That(scaled.MaxPosture, Is.EqualTo(140f).Within(0.01f));
            Assert.That(scaled.Phases[0].Patterns[0].Pattern.Notes[0].Damage, Is.EqualTo(26f).Within(0.01f));
            Assert.That(scaled.Phases[0].Patterns[0].Pattern.SpeedMultiplier, Is.EqualTo(1.15f).Within(0.01f));
        }

        [Test]
        public void 체간_경제의_절대값은_보스_에셋이_계속_주인이다()
        {
            // 상승량까지 부풀리면 체간 배수가 아무 일도 안 한 것이 되고,
            // 회복만 부풀리면 배수 하나가 두 번 먹혀 예측이 안 된다.
            BossConfig scaled = BossConfigScaler.Scale(Boss(), Curve().Evaluate(linkDepth: 5));

            Assert.That(scaled.ParryPostureGain, Is.EqualTo(40f));
            Assert.That(scaled.PostureDecayPerSecond, Is.EqualTo(6f));
            Assert.That(scaled.WhiffPostureRecovery, Is.EqualTo(8f));
        }

        [Test]
        public void 원본_config는_안_바뀐다()
        {
            // 같은 보스 에셋을 다음 판이 또 쓴다 — 제자리에서 고치면 이미 부푼 값을 또 부풀린다
            BossConfig source = Boss(damage: 20f);
            BossConfigScaler.Scale(source, Curve().Evaluate(linkDepth: 5));

            Assert.That(source.MaxHp, Is.EqualTo(100f));
            Assert.That(source.Phases[0].Patterns[0].Pattern.Notes[0].Damage, Is.EqualTo(20f));
        }

        [Test]
        public void 인살_페이즈도_같이_부푼다()
        {
            BossConfig source = Boss();
            source.BattlePhases = new[]
            {
                new BossBattlePhase(80f, 60f, source.Phases),
                new BossBattlePhase(120f, 100f, source.Phases),
            };

            BossConfig scaled = BossConfigScaler.Scale(source, Curve().Evaluate(linkDepth: 5));

            Assert.That(scaled.BattlePhases[0].MaxHp, Is.EqualTo(96f).Within(0.01f));
            Assert.That(scaled.BattlePhases[1].MaxPosture, Is.EqualTo(140f).Within(0.01f));
        }

        [Test]
        public void 페이즈들이_공유하던_채보는_스케일_후에도_한_벌이다()
        {
            BossConfig source = Boss();
            source.BattlePhases = new[]
            {
                new BossBattlePhase(80f, 60f, source.Phases),
                new BossBattlePhase(120f, 100f, source.Phases),
            };

            BossConfig scaled = BossConfigScaler.Scale(source, Curve().Evaluate(linkDepth: 5));

            Assert.That(
                scaled.BattlePhases[0].HpPhases[0],
                Is.SameAs(scaled.BattlePhases[1].HpPhases[0]),
                "공용 채보 풀이 페이즈 수만큼 복제되면 안 된다");
        }

        [Test]
        public void 배수가_1이면_원본을_그대로_돌려준다()
        {
            BossConfig source = Boss();

            Assert.That(BossConfigScaler.Scale(source, Curve().Evaluate(linkDepth: 0)), Is.SameAs(source));
            Assert.That(BossConfigScaler.Scale(source, null), Is.SameAs(source));
        }

        // ── 실패 처벌 (헛침) ────────────────────────────────────────────────────

        private static PlayerStats Stats() => new(new PlayerStatsConfig
        {
            BaseAttackDamage = 10f,
            BaseParryWindowSeconds = 0.2f,
            AttackCommitSeconds = 0.4f,
            ParryWhiffLockSeconds = 0.25f,
            ParryLateGraceSeconds = 0f,
        });

        /// <summary>t=2의 노트를 패링해 체간을 쌓아 둔 상태로 만든다(회복은 꺼 둔다).</summary>
        private static CombatSystem AfterOneParry(float whiffRecovery)
        {
            BossConfig config = Boss();
            config.PostureDecayPerSecond = 0f;
            config.WhiffPostureRecovery = whiffRecovery;
            config.FirstAttackDelaySeconds = 1f;

            var combat = new CombatSystem(config, Stats(), new PlayerHealth(100));
            combat.Tick(1.9f);
            combat.PressParry();

            Assert.That(combat.Posture, Is.EqualTo(40f).Within(0.01f), "전제: 패링 1회로 체간 40");
            return combat;
        }

        [Test]
        public void 헛치면_보스가_체간을_되찾는다()
        {
            CombatSystem combat = AfterOneParry(whiffRecovery: 8f);

            combat.Tick(1f);       // 잠금 해제 + 노트 없는 구간
            combat.PressParry();   // 판정 밖 — 헛침

            Assert.That(combat.Posture, Is.EqualTo(32f).Within(0.01f));
        }

        [Test]
        public void 회복량이_0이면_옛_동작대로_잠금뿐이다()
        {
            CombatSystem combat = AfterOneParry(whiffRecovery: 0f);

            combat.Tick(1f);
            combat.PressParry();

            Assert.That(combat.Posture, Is.EqualTo(40f).Within(0.01f));
        }

        [Test]
        public void 무너진_보스는_헛쳐도_일어서지_않는다()
        {
            // 인살을 기다리는 동안 헛쳤다고 체간이 내려가면 인살 자체를 놓칠 수 있다
            BossConfig config = Boss();
            config.MaxPosture = 40f;
            config.PostureDecayPerSecond = 0f;
            config.WhiffPostureRecovery = 8f;
            config.FirstAttackDelaySeconds = 1f;

            var combat = new CombatSystem(config, Stats(), new PlayerHealth(100));
            combat.Tick(1.9f);
            combat.PressParry();   // 체간 40/40 → 무너짐
            Assert.That(combat.ExecutionReady, Is.True, "전제: 체간이 무너져 인살 대기");

            combat.Tick(1f);
            combat.PressParry();   // 노트가 없으니 헛침

            Assert.That(combat.Posture, Is.EqualTo(40f).Within(0.01f));
            Assert.That(combat.ExecutionReady, Is.True);
        }
    }
}
