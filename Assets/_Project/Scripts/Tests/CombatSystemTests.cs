using System;
using System.Collections.Generic;
using ChainRiposte.Core.Combat;
using ChainRiposte.Core.Stats;
using NUnit.Framework;

namespace ChainRiposte.Core.Tests
{
    public sealed class CombatSystemTests
    {
        private const float FirstDelay = 1f; // 기본: t=1에 패턴 시작

        /// <summary>테스트는 전부 60BPM으로 돌린다 — <b>1박 = 1초</b>라 시간 계산이 눈에 보인다.</summary>
        private const float Bpm = 60f;

        private static BossNoteConfig Note(float beat, float telegraph = 1f, float damage = 20f, float speed = 1f) =>
            new(beat, telegraph, damage, speed);

        private static BossConfig Boss(params BossNoteConfig[] notes)
        {
            var used = notes.Length > 0 ? notes : new[] { Note(1f) };
            var pattern = new BossPatternConfig("Test", Bpm, lengthBeats: 8f, used);

            return new BossConfig
            {
                MaxHp = 100f,
                MaxPosture = 100f,
                ParryPostureGain = 40f,
                AttackPostureFactor = 0.5f,
                PostureDecayPerSecond = 0f, // 회복은 전용 테스트에서만 켠다
                FirstAttackDelaySeconds = FirstDelay,
                PatternGapSeconds = 0f,
                Phases = new[] { new BossPhaseConfig(1f, new[] { new WeightedPattern(pattern) }) },
            };
        }

        private static PlayerStats Stats(float def = 0f, float atk = 10f, float lateGrace = 0f) =>
            new(new PlayerStatsConfig
            {
                BaseAttackDamage = atk,
                BaseDamageReduction = def,
                BaseParryWindowSeconds = 0.2f,
                AttackCommitSeconds = 0.4f,
                ParryWhiffLockSeconds = 0.25f,
                // 대부분의 테스트는 유예를 꺼서 타격 시점을 정확히 관찰한다
                ParryLateGraceSeconds = lateGrace,
            });

        [Test]
        public void 패링_성공시_피해없이_체간이_대폭_상승한다()
        {
            var health = new PlayerHealth(100);
            var combat = new CombatSystem(Boss(), Stats(), health);
            bool parried = false;
            combat.AttackParried += _ => parried = true;

            combat.Tick(1.9f);       // 예비동작 진행 중 (타격은 t=2)
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
            var combat = new CombatSystem(Boss(Note(1f, damage: 20f)), Stats(def: 5f), health);
            int hitDamage = -1;
            combat.PlayerHit += (_, dmg) => hitDamage = dmg;

            combat.Tick(2f); // t=2 타격, 패링 없음

            Assert.That(hitDamage, Is.EqualTo(15), "20 − DEF 5");
            Assert.That(health.Current, Is.EqualTo(85));
        }

        [Test]
        public void 판정_밖에서_미리_누르면_헛침이고_그_타격은_맞는다()
        {
            var health = new PlayerHealth(100);
            var combat = new CombatSystem(Boss(), Stats(), health);

            // 타격은 t=2, 윈도우는 0.2초 — t=0에 누르는 것은 한참 이르다
            combat.PressParry();

            Assert.That(combat.PlayerState, Is.EqualTo(PlayerActionState.ParryRecovering),
                "판정 밖 입력은 그 자리에서 헛침으로 확정된다");

            combat.Tick(2f);

            Assert.That(health.Current, Is.EqualTo(80), "미리 눌러 둔 것은 타격을 막아 주지 않는다");
        }

        [Test]
        public void 판정_안에서_누르면_타격을_기다리지_않고_그_자리에서_막힌다()
        {
            var health = new PlayerHealth(100);
            var combat = new CombatSystem(Boss(), Stats(), health);
            bool parried = false;
            combat.AttackParried += _ => parried = true;

            combat.Tick(1.9f);   // 타격 t=2 까지 0.1초 — 윈도우(0.2) 안
            combat.PressParry();

            Assert.That(parried, Is.True, "누른 즉시 결판난다 — 원이 다 줄어들 때까지 기다리지 않는다");
            Assert.That(combat.ActiveNotes.Count, Is.Zero, "막은 노트는 그 자리에서 화면에서 빠진다");
            Assert.That(combat.Posture, Is.EqualTo(40f));
            Assert.That(combat.PlayerState, Is.EqualTo(PlayerActionState.Ready));

            combat.Tick(0.2f);
            Assert.That(health.Current, Is.EqualTo(100), "이미 막은 노트가 다시 때리지 않는다");
        }

        [Test]
        public void 연속기는_노트마다_따로_패링해야_한다()
        {
            // 1박과 1.5박 — 0.5초 간격의 2연타
            var health = new PlayerHealth(100);
            var combat = new CombatSystem(
                Boss(Note(1f, telegraph: 1f, damage: 20f), Note(1.5f, telegraph: 1f, damage: 20f)),
                Stats(), health);
            int parries = 0;
            combat.AttackParried += _ => parries++;

            combat.Tick(1.9f);
            combat.PressParry();
            combat.Tick(0.2f);  // t=2.0 첫 타격 → 패링 성공, 즉시 Ready

            Assert.That(parries, Is.EqualTo(1));
            Assert.That(combat.PlayerState, Is.EqualTo(PlayerActionState.Ready));

            // 윈도우가 0.2초뿐이라 두 번째 타격(t=2.5)에 맞춰 다시 눌러야 한다 — 바로 누르면 일찍 끝난다
            combat.Tick(0.25f);  // t=2.35
            combat.PressParry();
            combat.Tick(0.2f);   // t=2.55 — 그 사이 t=2.5 타격

            Assert.That(parries, Is.EqualTo(2), "연속기는 노트 수만큼 눌러야 한다");
            Assert.That(health.Current, Is.EqualTo(100));
        }

        [Test]
        public void 연속기_중_하나를_놓쳐도_나머지는_계속된다()
        {
            var health = new PlayerHealth(100);
            var combat = new CombatSystem(
                Boss(Note(1f, damage: 20f), Note(1.5f, damage: 20f)),
                Stats(), health);

            combat.Tick(2.1f);   // 첫 타격 놓침 (t=2)
            Assert.That(health.Current, Is.EqualTo(80));

            combat.Tick(0.25f);  // t=2.35 — 두 번째 타격(t=2.5)에 맞춰
            combat.PressParry();
            combat.Tick(0.2f);   // t=2.55

            Assert.That(health.Current, Is.EqualTo(80), "놓친 한 대만 맞고 나머지는 살아난다");
            Assert.That(combat.Posture, Is.EqualTo(40f));
        }

        [Test]
        public void 타격_직후_유예_안에_누르면_늦은_패링이_인정된다()
        {
            var health = new PlayerHealth(100);
            var combat = new CombatSystem(Boss(), Stats(lateGrace: 0.12f), health);
            bool parried = false;
            combat.AttackParried += _ => parried = true;

            combat.Tick(2.05f);  // t=2 타격이 이미 지났다 — 유예 중
            Assert.That(health.Current, Is.EqualTo(100), "유예 동안은 아직 피해가 확정되지 않는다");

            combat.PressParry(); // 늦게 눌렀지만 유예 안

            Assert.That(parried, Is.True);
            Assert.That(health.Current, Is.EqualTo(100));
            Assert.That(combat.Posture, Is.EqualTo(40f));
        }

        [Test]
        public void 유예가_지나면_피해가_확정된다()
        {
            var health = new PlayerHealth(100);
            var combat = new CombatSystem(Boss(Note(1f, damage: 20f)), Stats(lateGrace: 0.12f), health);

            combat.Tick(2.2f);   // 타격 t=2 + 유예 0.12 를 넘겼다
            Assert.That(health.Current, Is.EqualTo(80));

            combat.PressParry(); // 이제 눌러도 소용없다
            Assert.That(health.Current, Is.EqualTo(80));
        }

        [Test]
        public void 여러_노트가_동시에_날아오면_전부_보인다()
        {
            // 예비동작 2박짜리 노트 세 개가 겹친다
            var combat = new CombatSystem(
                Boss(Note(2f, telegraph: 2f), Note(2.5f, telegraph: 2f), Note(3f, telegraph: 2f)),
                Stats(), new PlayerHealth(100));

            // 예비동작 시작은 각각 0 / 0.5 / 1박. 패턴 시작 t=1 기준 1.1박 지점이면 셋 다 감기고 있다
            combat.Tick(2.1f);

            Assert.That(combat.ActiveNotes.Count, Is.EqualTo(3));
            Assert.That(combat.ActiveNotes[0].SecondsUntilHit,
                Is.LessThan(combat.ActiveNotes[1].SecondsUntilHit), "임박한 순으로 정렬된다");
            Assert.That(combat.ActiveNotes[0].Progress, Is.GreaterThan(0f).And.LessThan(1f));
        }

        [Test]
        public void 공격은_빈_박에서만_들어간다()
        {
            var combat = new CombatSystem(Boss(), Stats(atk: 10f), new PlayerHealth(100));

            combat.Tick(1.5f);    // 노트가 날아오는 중 (타격 t=2)
            Assert.That(combat.BossState, Is.EqualTo(BossActionState.Telegraphing));

            combat.PressAttack(); // 빈 박이 아니다 → 헛짓으로 잠긴다
            Assert.That(combat.PlayerState, Is.EqualTo(PlayerActionState.ParryRecovering));
            Assert.That(combat.BossHp, Is.EqualTo(100f), "공격이 나가지 않았다");
        }

        [Test]
        public void 공격은_보스HP와_체간을_소폭_깎는다()
        {
            var combat = new CombatSystem(Boss(), Stats(atk: 10f), new PlayerHealth(100));

            combat.PressAttack(); // t=0 — 아직 패턴 전이라 빈 박
            combat.Tick(0.4f);    // 커밋 종료 → 적중

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
            var combat = new CombatSystem(Boss(Note(1f, damage: 20f)), Stats(), new PlayerHealth(10));
            bool? victory = null;
            combat.Ended += v => victory = v;

            combat.Tick(2f);

            Assert.That(victory, Is.False);
            Assert.That(combat.Finished, Is.True);
        }

        [Test]
        public void 노트_속도_배율은_예비동작만_짧게_만든다()
        {
            // 타격 시점(2박=t=3)은 그대로고 예비동작만 1박 → 0.5박으로 줄어든다
            var combat = new CombatSystem(
                Boss(Note(2f, telegraph: 1f, speed: 2f)), Stats(), new PlayerHealth(100));

            combat.Tick(2.4f); // t=1 패턴 시작 기준 1.4박 — 아직 예비동작 전(1.5박부터)
            Assert.That(combat.ActiveNotes.Count, Is.EqualTo(0), "배율만큼 늦게 감기 시작한다");

            combat.Tick(0.2f); // 1.6박 — 예비동작 시작됨
            Assert.That(combat.ActiveNotes.Count, Is.EqualTo(1));
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
        public void 채보는_큰_틱에도_결정적으로_재생된다()
        {
            var combat = new CombatSystem(
                Boss(Note(1f, damage: 1f), Note(3f, damage: 1f), Note(5f, damage: 1f)),
                Stats(), new PlayerHealth(100));
            var hits = new List<float>();
            combat.NoteTelegraphed += note => hits.Add(note.Beat);

            combat.Tick(6.1f); // 한 번의 큰 틱으로 t=0~6.1 진행

            Assert.That(hits, Is.EqualTo(new[] { 1f, 3f, 5f }), "예비동작 시작 순서가 박 순서와 같다");
        }

        [Test]
        public void 패링_헛침후_잠금시간_동안_재입력이_불가하다()
        {
            var combat = new CombatSystem(Boss(), Stats(), new PlayerHealth(100));

            combat.PressParry();  // 판정 밖 — 그 자리에서 헛침, 잠금 t=0~0.25
            combat.Tick(0.1f);

            Assert.That(combat.PlayerState, Is.EqualTo(PlayerActionState.ParryRecovering));
            combat.PressParry(); // 무시 — 연타로 판정을 도배할 수 없다
            Assert.That(combat.PlayerState, Is.EqualTo(PlayerActionState.ParryRecovering));

            combat.Tick(0.2f); // t=0.3 — 잠금 해제

            Assert.That(combat.PlayerState, Is.EqualTo(PlayerActionState.Ready));
        }

        [Test]
        public void HP_페이즈에_따라_다른_패턴_풀을_쓴다()
        {
            var easy = new BossPatternConfig("Easy", Bpm, 8f, new[] { Note(1f, damage: 1f) });
            var hard = new BossPatternConfig("Hard", Bpm, 8f, new[] { Note(1f, damage: 99f) });

            var config = Boss();
            config.MaxHp = 100f;
            config.Phases = new[]
            {
                new BossPhaseConfig(1f, new[] { new WeightedPattern(easy) }),
                new BossPhaseConfig(0.5f, new[] { new WeightedPattern(hard) }),
            };

            // 인살 페이즈를 안 짠 보스라 1페이즈짜리로 감싸진다 — HP 구간 풀은 그 안에서 돈다
            BossBattlePhase battle = config.ResolveBattlePhases()[0];

            Assert.That(battle.ResolveHpPhase(1f).Patterns[0].Pattern.Name, Is.EqualTo("Easy"));
            Assert.That(battle.ResolveHpPhase(0.8f).Patterns[0].Pattern.Name, Is.EqualTo("Easy"));
            Assert.That(battle.ResolveHpPhase(0.5f).Patterns[0].Pattern.Name, Is.EqualTo("Hard"), "체력이 깎이면 험한 풀로");
            Assert.That(battle.ResolveHpPhase(0.1f).Patterns[0].Pattern.Name, Is.EqualTo("Hard"));
        }

        // ── 인살 페이즈 (2페이즈 보스) ──

        /// <summary>인살 페이즈 2개짜리 보스. 페이즈마다 HP·체간을 따로 준다.</summary>
        private static BossConfig TwoPhaseBoss()
        {
            var pattern = new BossPatternConfig("Test", Bpm, lengthBeats: 8f, new[] { Note(1f) });
            var pool = new[] { new BossPhaseConfig(1f, new[] { new WeightedPattern(pattern) }) };

            BossConfig config = Boss();
            config.BattlePhases = new[]
            {
                new BossBattlePhase(maxHp: 100f, maxPosture: 100f, pool),
                new BossBattlePhase(maxHp: 200f, maxPosture: 140f, pool),
            };
            return config;
        }

        /// <summary>체간을 채워 인살 가능 상태로 만든다 (패링 없이 직접 때려서).</summary>
        private static void BreakPosture(CombatSystem combat)
        {
            for (int i = 0; i < 200 && !combat.ExecutionReady; i++)
            {
                combat.PressAttack();
                combat.Tick(0.5f);
            }
        }

        [Test]
        public void 인살_페이즈가_남아_있으면_승리가_아니라_전환이다()
        {
            var combat = new CombatSystem(TwoPhaseBoss(), Stats(atk: 200f), new PlayerHealth(1000));
            bool ended = false;
            int clearedPhase = -1;
            combat.Ended += _ => ended = true;
            combat.PhaseCleared += phase => clearedPhase = phase;

            BreakPosture(combat);
            combat.PressAttack(); // 인살

            Assert.That(ended, Is.False, "아직 페이즈가 남았으므로 전투가 끝나면 안 된다");
            Assert.That(clearedPhase, Is.EqualTo(0));
            Assert.That(combat.AwaitingPhaseTransition, Is.True);
            Assert.That(combat.RemainingDeathblows, Is.EqualTo(1), "인살 마크 하나가 남는다");
        }

        [Test]
        public void 전환_대기_중에는_시간도_입력도_멈춘다()
        {
            var health = new PlayerHealth(1000);
            var combat = new CombatSystem(TwoPhaseBoss(), Stats(atk: 200f), health);

            BreakPosture(combat);
            combat.PressAttack();

            float hpBefore = combat.BossHp;
            combat.Tick(10f);       // 컷씬이 도는 동안 — 흘러가면 안 된다
            combat.PressParry();    // 이 입력도 없던 일이어야 한다

            Assert.That(health.Current, Is.EqualTo(1000), "멈춰 있는 동안 맞으면 안 된다");
            Assert.That(combat.BossHp, Is.EqualTo(hpBefore));
            Assert.That(combat.PlayerState, Is.EqualTo(PlayerActionState.Ready), "헛침 잠금도 걸리면 안 된다");
        }

        [Test]
        public void 다음_페이즈는_HP와_체간이_만땅으로_새로_시작한다()
        {
            var combat = new CombatSystem(TwoPhaseBoss(), Stats(atk: 200f), new PlayerHealth(1000));
            int startedPhase = -1;
            combat.PhaseStarted += phase => startedPhase = phase;

            BreakPosture(combat);
            combat.PressAttack();
            combat.BeginNextPhase(); // 컷씬 종료를 Game 레이어가 알린다

            Assert.That(startedPhase, Is.EqualTo(1));
            Assert.That(combat.BattlePhaseIndex, Is.EqualTo(1));
            Assert.That(combat.BossMaxHp, Is.EqualTo(200f), "2페이즈는 자기 HP를 쓴다");
            Assert.That(combat.BossHp, Is.EqualTo(200f), "만땅으로 새로");
            Assert.That(combat.MaxPosture, Is.EqualTo(140f));
            Assert.That(combat.Posture, Is.EqualTo(0f), "체간도 비운 채 시작");
            Assert.That(combat.ExecutionReady, Is.False);
            Assert.That(combat.AwaitingPhaseTransition, Is.False);
        }

        [Test]
        public void 마지막_페이즈를_인살하면_승리한다()
        {
            var combat = new CombatSystem(TwoPhaseBoss(), Stats(atk: 200f), new PlayerHealth(1000));
            bool? victory = null;
            combat.Ended += result => victory = result;

            BreakPosture(combat);
            combat.PressAttack();
            combat.BeginNextPhase();

            BreakPosture(combat);
            combat.PressAttack();

            Assert.That(victory, Is.True);
            Assert.That(combat.Finished, Is.True);
        }

        [Test]
        public void 인살_페이즈를_안_짠_보스는_한_번으로_끝난다()
        {
            var combat = new CombatSystem(Boss(), Stats(atk: 200f), new PlayerHealth(1000));
            bool? victory = null;
            combat.Ended += result => victory = result;

            Assert.That(combat.BattlePhaseCount, Is.EqualTo(1), "안 짜면 1페이즈로 감싸진다");

            BreakPosture(combat);
            combat.PressAttack();

            Assert.That(victory, Is.True);
        }

        [Test]
        public void 페이즈가_비면_예외를_던진다()
        {
            var config = new BossConfig { Phases = Array.Empty<BossPhaseConfig>() };

            Assert.Throws<ArgumentException>(() =>
                _ = new CombatSystem(config, Stats(), new PlayerHealth(100)));
        }
    }
}
