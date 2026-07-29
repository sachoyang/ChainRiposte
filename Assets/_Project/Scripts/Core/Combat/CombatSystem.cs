using System;
using System.Collections.Generic;
using ChainRiposte.Core.Stats;

namespace ChainRiposte.Core.Combat
{
    /// <summary>
    /// 2버튼 패링 전투의 규칙 엔진 (GDD §5).
    /// 보스는 채보(<see cref="BossPatternConfig"/>)를 한 마디씩 재생하고, 끝나면 현재 페이즈의 풀에서 다음 패턴을 뽑는다.
    ///
    /// 채보는 <b>여러 노트가 동시에 살아 있을 수 있다</b> — 연속기는 별도 타입이 아니라 촘촘히 찍힌 노트다.
    /// 뷰는 <see cref="ActiveNotes"/>를 읽어 수축하는 원을 여러 개 그린다.
    ///
    /// 시간은 Tick(deltaSeconds)으로만 흐른다 — UnityEngine 비의존, 테스트에서 임의 시간 진행 가능.
    /// </summary>
    public sealed class CombatSystem
    {
        /// <summary>경계 분할 진행에서 생기는 부동소수점 잔차 허용치 — 이보다 적게 남은 타이머는 만료로 본다.</summary>
        private const float TimeEpsilon = 1e-4f;

        /// <summary>노트 하나의 진행 상태. 패턴을 뽑을 때마다 새로 만든다.</summary>
        private sealed class RuntimeNote
        {
            public BossNoteConfig Note;
            public float TelegraphStartSeconds;
            public float HitSeconds;
            public bool Telegraphed;
            public bool Resolved;

            /// <summary>타격 시점은 지났지만 아직 늦은 패링을 받아 주는 중.</summary>
            public bool InGrace;
            public float GraceEndsAt;
        }

        private readonly BossConfig _config;
        private readonly PlayerStats _stats;
        private readonly PlayerHealth _health;
        private readonly Random _rng;

        private readonly List<RuntimeNote> _notes = new();
        private readonly List<ActiveNote> _activeNotes = new();

        private readonly IReadOnlyList<BossBattlePhase> _battlePhases;
        private BossBattlePhase _battle;
        private int _battleIndex;
        private int _deathblowsDone;

        private BossPatternConfig _pattern;
        private float _patternTime;   // 현재 패턴의 경과 시간(초). 리드인 동안은 음수.
        private float _gapTimer;      // 패턴 사이 대기. 0보다 크면 패턴이 아직 안 돌고 있다.
        private float _playerTimer;   // 현재 플레이어 상태(패링/후딜/커밋)의 잔여 시간

        public BossActionState BossState { get; private set; } = BossActionState.Recovering;
        public PlayerActionState PlayerState { get; private set; } = PlayerActionState.Ready;

        public float BossHp { get; private set; }
        public float BossMaxHp => _battle.MaxHp;

        /// <summary>체간 게이지 — MaxPosture 도달 시 인살 가능.</summary>
        public float Posture { get; private set; }
        public float MaxPosture => _battle.MaxPosture;

        /// <summary>지금 몇 번째 인살 페이즈인가 (0부터). 겉모습·채보가 여기에 딸려 간다.</summary>
        public int BattlePhaseIndex => _battleIndex;

        /// <summary>인살 페이즈 수 = 이 보스를 눕히는 데 필요한 <b>인살 횟수</b>. 화면의 ◆ 개수다.</summary>
        public int BattlePhaseCount => _battlePhases.Count;

        /// <summary>
        /// 앞으로 <b>몇 번 더 인살해야</b> 이 보스가 눕는가. 화면의 ◆ 개수가 이것이다.
        /// 페이즈 번호로 계산하지 않는 이유: 인살 직후~다음 페이즈 시작 사이(컷씬)에는 번호가 아직 안 올라가서
        /// 방금 인살한 몫이 계속 남아 있는 것처럼 보인다.
        /// </summary>
        public int RemainingDeathblows => _battlePhases.Count - _deathblowsDone;

        /// <summary>
        /// 인살은 끝났고 <b>다음 페이즈가 시작되기를 기다리는</b> 중 — 전환 컷씬이 도는 구간이다.
        /// 이 동안은 시간이 흐르지 않고 입력도 받지 않는다.
        /// 컷씬이 끝나면 Game 레이어가 <see cref="BeginNextPhase"/>를 불러 재개시킨다.
        /// (Core는 컷씬이 몇 초인지 알 필요가 없다 — 알게 하면 연출을 바꿀 때마다 규칙을 고쳐야 한다.)
        /// </summary>
        public bool AwaitingPhaseTransition { get; private set; }

        /// <summary>재생 중인 패턴 (패턴 사이 대기 중에는 직전 패턴이 남아 있다).</summary>
        public BossPatternConfig CurrentPattern => _pattern;

        /// <summary>지금 그려야 할 노트들 — 예비동작이 시작됐고 아직 타격되지 않은 것. 타격이 임박한 순.</summary>
        public IReadOnlyList<ActiveNote> ActiveNotes => _activeNotes;

        /// <summary>
        /// 타격 <b>이전</b>으로 열려 있는 패링 판정 폭(초). PARRY 스탯을 올리면 넓어진다.
        /// 뷰는 이것과 <see cref="ParryLateGraceSeconds"/>로 회색 띠의 위치·두께를 정한다.
        /// </summary>
        public float ParryWindowSeconds => _stats.ParryWindowSeconds;

        /// <summary>타격 <b>이후</b>로 열려 있는 유예(초).</summary>
        public float ParryLateGraceSeconds => _stats.ParryLateGraceSeconds;

        /// <summary>무투자 상태의 판정 폭 — 뷰가 노트 원의 두께를 여기에 맞춘다.</summary>
        public float BaseParryWindowSeconds => _stats.BaseParryWindowSeconds;

        /// <summary>체간 파괴 — 화면에 붉은 인살 마크가 뜨는 상태. 공격 버튼이 인살로 바뀐다.</summary>
        public bool ExecutionReady => BossState == BossActionState.Broken;

        public bool Finished { get; private set; }

        /// <summary>새 패턴 시작 — 이름 표시/음악 동기화 훅.</summary>
        public event Action<BossPatternConfig> PatternStarted;

        /// <summary>노트의 예비동작 시작 — 원 생성 훅.</summary>
        public event Action<BossNoteConfig> NoteTelegraphed;

        /// <summary>패링 성공 — 쇳소리+기타 피드백 훅.</summary>
        public event Action<BossNoteConfig> AttackParried;

        /// <summary>(노트, 실제 피해량) — 패링 실패/미입력 피격. HP 반영은 PlayerHealth.Changed로 통지된다.</summary>
        public event Action<BossNoteConfig, int> PlayerHit;

        /// <summary>플레이어 공격 적중 (가한 피해량).</summary>
        public event Action<float> PlayerAttackLanded;

        /// <summary>(현재 HP, 최대 HP)</summary>
        public event Action<float, float> BossHpChanged;

        /// <summary>(현재 체간, 한계치)</summary>
        public event Action<float, float> PostureChanged;

        /// <summary>체간 파괴 — 인살 마크 표시 훅.</summary>
        public event Action BossBroken;

        /// <summary>인살 입력 성공 — 피니시 연출 훅. 마지막 페이즈였다면 직후 Ended(true)가 발행된다.</summary>
        public event Action ExecutionPerformed;

        /// <summary>
        /// (방금 끝낸 페이즈 번호) — 인살했지만 <b>다음 페이즈가 남아 있다</b>. 전환 컷씬을 재생할 훅.
        /// 이 뒤로 시간이 멈추므로, 연출이 끝나면 반드시 <see cref="BeginNextPhase"/>를 불러야 전투가 재개된다.
        /// </summary>
        public event Action<int> PhaseCleared;

        /// <summary>(시작한 페이즈 번호) — 새 페이즈의 전투가 시작됐다. 겉모습 교체 훅.</summary>
        public event Action<int> PhaseStarted;

        /// <summary>버튼 활성/모션 표시용.</summary>
        public event Action<PlayerActionState> PlayerStateChanged;

        /// <summary>(승리 여부) — 세션 페이즈 전환은 Game 레이어가 이 이벤트로 수행한다.</summary>
        public event Action<bool> Ended;

        public CombatSystem(BossConfig config, PlayerStats stats, PlayerHealth health, Random rng = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _stats = stats ?? throw new ArgumentNullException(nameof(stats));
            _health = health ?? throw new ArgumentNullException(nameof(health));
            _rng = rng ?? new Random();

            if (config.MaxHp <= 0f || config.MaxPosture <= 0f)
                throw new ArgumentException("보스 HP/체간 한계치는 0보다 커야 합니다.", nameof(config));

            _battlePhases = config.ResolveBattlePhases();
            if (_battlePhases == null || _battlePhases.Count == 0)
                throw new ArgumentException("보스 페이즈가 비어 있습니다.", nameof(config));

            _battle = _battlePhases[0];
            BossHp = _battle.MaxHp;
            _gapTimer = Math.Max(0f, config.FirstAttackDelaySeconds);
        }

        /// <summary>
        /// 전환 컷씬이 끝났다 — 다음 페이즈를 만땅으로 시작한다.
        /// HP·체간을 이어받지 않는 것이 이 보스의 규칙이다(사실상 2연전). 페이즈마다 수치를 따로 두므로
        /// 1페이즈를 짧게 끊고 2페이즈를 무겁게 부는 완급이 데이터로 잡힌다.
        /// </summary>
        public void BeginNextPhase()
        {
            if (Finished || !AwaitingPhaseTransition)
                return;

            AwaitingPhaseTransition = false;
            _battle = _battlePhases[++_battleIndex];

            // 앞 페이즈의 잔재를 남기지 않는다 — 남은 노트 하나가 새 페이즈 첫 박에 튀어나온다
            _pattern = null;
            _notes.Clear();
            _activeNotes.Clear();
            _gapTimer = Math.Max(0f, _config.FirstAttackDelaySeconds);

            BossHp = _battle.MaxHp;
            Posture = 0f;
            BossState = BossActionState.Recovering;
            SetPlayerState(PlayerActionState.Ready);
            _playerTimer = 0f;

            BossHpChanged?.Invoke(BossHp, _battle.MaxHp);
            PostureChanged?.Invoke(Posture, _battle.MaxPosture);
            PhaseStarted?.Invoke(_battleIndex);
        }

        /// <summary>
        /// 시간 진행. 상태 경계(타격 시점, 윈도우 만료 등)마다 끊어 진행하므로
        /// deltaSeconds가 커도 판정 순서가 프레임레이트와 무관하게 결정적이다.
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || Finished || AwaitingPhaseTransition)
                return;

            float remaining = deltaSeconds;
            while (remaining > 0f && !Finished)
            {
                ProcessBossEvents();
                ProcessPlayerExpiry();
                if (Finished)
                    break;

                float step = Math.Min(remaining, NextBossEventIn());
                if (PlayerState != PlayerActionState.Ready)
                    step = Math.Min(step, _playerTimer);
                step = Math.Max(step, TimeEpsilon); // 0초 경계에서 제자리걸음 방지

                Advance(step);
                remaining -= step;
            }

            ProcessBossEvents();
            ProcessPlayerExpiry();
            RebuildActiveNotes();
        }

        /// <summary>
        /// 좌측 버튼 — <b>누른 순간 성공/실패가 결판난다.</b>
        ///
        /// 판정 구간 <c>[타격 − 윈도우, 타격 + 유예]</c> 안에 있는 노트를 그 자리에서 막는다.
        /// 예전에는 눌러 두고 타격이 올 때까지 기다렸다가 결판이 났는데, 그러면
        /// <b>이미 막았는데도 원이 끝까지 줄어드는 것을 보고 있어야 해서</b> 손맛이 죽는다.
        /// 이 장르(세키로 등)는 누른 즉시 반응이 나오고 그 공격이 사라진다.
        ///
        /// 판정 밖에서 눌렀으면 헛침 — 잠깐 잠겨서 연타로 도배할 수 없다.
        /// </summary>
        public void PressParry()
        {
            if (Finished || AwaitingPhaseTransition)
                return;

            // 늦은 패링 — 원이 닿는 걸 보고 누르면 대개 살짝 늦는다. 잠금 중이어도 이건 받아 준다.
            RuntimeNote late = FindInGrace();
            if (late != null)
            {
                ParryNote(late);
                return;
            }

            if (PlayerState != PlayerActionState.Ready)
                return;

            RuntimeNote incoming = FindInWindow();
            if (incoming != null)
            {
                ParryNote(incoming);
                return;
            }

            Whiff();
        }

        /// <summary>
        /// 헛침 — 잠깐 잠기고(연타 방지), <b>보스는 그만큼 체간을 되찾는다</b>
        /// (<see cref="BossConfig.WhiffPostureRecovery"/>).
        ///
        /// <para>잠금만 있으면 헛치는 비용이 0.35초뿐이라 <b>막 눌러도 손해가 없다</b> —
        /// 판정을 읽는 게임이 아니라 연타 게임이 된다. 체간을 되돌려 놓아야 헛침이
        /// "여태 쌓은 것을 무르는 일"이 된다.</para>
        /// </summary>
        private void Whiff()
        {
            SetPlayerState(PlayerActionState.ParryRecovering);
            _playerTimer = _stats.ParryWhiffLockSeconds;
            RecoverPosture(_config.WhiffPostureRecovery);
        }

        /// <summary>
        /// 보스가 체간을 되찾는다(게이지가 내려간다). <b>이미 무너진 보스는 안 되돌린다</b> —
        /// 인살을 기다리는 동안 헛쳤다고 보스가 일어서면 인살 자체를 놓칠 수 있다.
        /// </summary>
        private void RecoverPosture(float amount)
        {
            if (amount <= 0f || Posture <= 0f || BossState == BossActionState.Broken)
                return;

            Posture = Math.Max(0f, Posture - amount);
            PostureChanged?.Invoke(Posture, _battle.MaxPosture);
        }

        private RuntimeNote FindInGrace()
        {
            foreach (RuntimeNote note in _notes)
                if (note.InGrace && !note.Resolved)
                    return note;
            return null;
        }

        /// <summary>
        /// 지금 눌러서 막을 수 있는 노트 — 예비동작이 시작됐고(화면에 원이 보이고)
        /// 타격까지 윈도우 안에 든 것 중 <b>가장 임박한</b> 하나.
        /// 아직 안 보이는 노트를 미리 막을 수는 없다.
        /// </summary>
        private RuntimeNote FindInWindow()
        {
            RuntimeNote best = null;
            float bestRemaining = float.MaxValue;
            float window = _stats.ParryWindowSeconds;

            foreach (RuntimeNote note in _notes)
            {
                if (note.Resolved || !note.Telegraphed)
                    continue;

                float remaining = note.HitSeconds - _patternTime;
                if (remaining < 0f || remaining > window + TimeEpsilon)
                    continue;

                if (remaining < bestRemaining)
                {
                    bestRemaining = remaining;
                    best = note;
                }
            }

            return best;
        }

        /// <summary>
        /// 우측 버튼 — 인살 마크가 떠 있으면 인살, 아니면 공격 커밋.
        /// <b>공격은 빈 박에만 들어간다</b> — 노트가 날아오는 중에 누르면 헛짓으로 잠긴다.
        /// </summary>
        public void PressAttack()
        {
            if (Finished || AwaitingPhaseTransition)
                return;

            if (ExecutionReady)
            {
                _deathblowsDone++;
                ExecutionPerformed?.Invoke();

                // 아직 페이즈가 남았으면 승리가 아니라 '전환' — 시간을 멈추고 컷씬에 넘긴다
                if (_battleIndex + 1 < _battlePhases.Count)
                {
                    AwaitingPhaseTransition = true;
                    _activeNotes.Clear();
                    PhaseCleared?.Invoke(_battleIndex);
                    return;
                }

                Finish(victory: true);
                return;
            }

            if (PlayerState != PlayerActionState.Ready)
                return;

            if (BossState == BossActionState.Telegraphing)
            {
                // 날아오는 노트가 있는데 공격을 시도했다 — 빈 박까지 기다려야 한다
                Whiff();
                return;
            }

            SetPlayerState(PlayerActionState.Attacking);
            _playerTimer = _stats.AttackCommitSeconds;
        }

        /// <summary>다음 보스 사건(노트 예비동작 시작 / 타격 / 패턴 종료)까지 남은 시간.</summary>
        private float NextBossEventIn()
        {
            if (BossState == BossActionState.Broken)
                return float.MaxValue;

            if (_pattern == null)
                return Math.Max(0f, _gapTimer);

            float next = float.MaxValue;
            foreach (RuntimeNote note in _notes)
            {
                if (note.Resolved)
                    continue;

                float target = note.InGrace
                    ? note.GraceEndsAt
                    : note.Telegraphed ? note.HitSeconds : note.TelegraphStartSeconds;
                next = Math.Min(next, target - _patternTime);
            }

            return Math.Max(0f, Math.Min(next, _pattern.DurationSeconds - _patternTime));
        }

        /// <summary>경과한 시간만큼 타이머/체간 회복을 진행한다 (상태 전환 없음).</summary>
        private void Advance(float step)
        {
            if (BossState != BossActionState.Broken)
            {
                if (_pattern == null)
                    _gapTimer -= step;
                else
                    _patternTime += step;

                DecayPosture(step);
            }

            if (PlayerState != PlayerActionState.Ready)
                _playerTimer -= step;
        }

        private void DecayPosture(float step)
        {
            if (Posture <= 0f || _config.PostureDecayPerSecond <= 0f)
                return;

            float decay = _config.PostureDecayPerSecond;
            if (_config.ScaleDecayWithHp)
                decay *= BossHp / _battle.MaxHp; // HP가 낮을수록 체간 회복 둔화

            Posture = Math.Max(0f, Posture - decay * step);
            PostureChanged?.Invoke(Posture, _battle.MaxPosture);
        }

        /// <summary>만료된 보스 사건을 순서대로 처리한다. 0초 간격이 연쇄될 수 있어 변화가 없을 때까지 반복한다.</summary>
        private void ProcessBossEvents()
        {
            bool transitioned = true;
            while (transitioned && !Finished && BossState != BossActionState.Broken)
            {
                transitioned = false;

                if (_pattern == null)
                {
                    if (_gapTimer <= TimeEpsilon)
                    {
                        StartNextPattern();
                        transitioned = true;
                    }

                    continue;
                }

                // 유예가 끝난 노트부터 확정한다
                RuntimeNote expired = FindGraceExpired();
                if (expired != null)
                {
                    ApplyNoteDamage(expired);
                    transitioned = true;
                    continue;
                }

                // 타격을 예비동작 시작보다 먼저 본다 — 같은 순간이면 먼저 온 노트가 먼저 해결돼야 한다
                RuntimeNote striking = FindDue(hit: true);
                if (striking != null)
                {
                    ResolveStrike(striking);
                    transitioned = true;
                    continue;
                }

                RuntimeNote starting = FindDue(hit: false);
                if (starting != null)
                {
                    starting.Telegraphed = true;
                    BossState = BossActionState.Telegraphing;
                    NoteTelegraphed?.Invoke(starting.Note);
                    transitioned = true;
                    continue;
                }

                RefreshBossState();

                if (_patternTime >= _pattern.DurationSeconds - TimeEpsilon && AllResolved())
                {
                    _pattern = null;
                    _gapTimer = Math.Max(0f, _config.PatternGapSeconds);
                    BossState = BossActionState.Recovering;
                    transitioned = true;
                }
            }
        }

        private RuntimeNote FindDue(bool hit)
        {
            foreach (RuntimeNote note in _notes)
            {
                if (note.Resolved)
                    continue;

                if (hit)
                {
                    if (note.Telegraphed && !note.InGrace && _patternTime >= note.HitSeconds - TimeEpsilon)
                        return note;
                }
                else if (!note.Telegraphed && _patternTime >= note.TelegraphStartSeconds - TimeEpsilon)
                {
                    return note;
                }
            }

            return null;
        }

        private RuntimeNote FindGraceExpired()
        {
            foreach (RuntimeNote note in _notes)
                if (note.InGrace && !note.Resolved && _patternTime >= note.GraceEndsAt - TimeEpsilon)
                    return note;
            return null;
        }

        private bool AllResolved()
        {
            foreach (RuntimeNote note in _notes)
                if (!note.Resolved)
                    return false;
            return true;
        }

        /// <summary>예비동작 중인 노트가 하나라도 있으면 Telegraphing, 아니면 빈 박(Recovering).</summary>
        private void RefreshBossState()
        {
            if (BossState == BossActionState.Broken)
                return;

            foreach (RuntimeNote note in _notes)
            {
                if (!note.Resolved && note.Telegraphed)
                {
                    BossState = BossActionState.Telegraphing;
                    return;
                }
            }

            BossState = BossActionState.Recovering;
        }

        private void StartNextPattern()
        {
            _pattern = PickPattern();
            _patternTime = -_pattern.LeadInSeconds; // 첫 노트의 예비동작이 잘리지 않게 앞당겨 시작
            _notes.Clear();

            foreach (BossNoteConfig note in _pattern.Notes)
            {
                _notes.Add(new RuntimeNote
                {
                    Note = note,
                    TelegraphStartSeconds = note.TelegraphStartBeat * _pattern.SecondsPerBeat,
                    HitSeconds = note.Beat * _pattern.SecondsPerBeat,
                });
            }

            BossState = BossActionState.Recovering;
            PatternStarted?.Invoke(_pattern);
        }

        /// <summary>현재 HP 페이즈의 풀에서 가중 무작위로 뽑는다.</summary>
        private BossPatternConfig PickPattern()
        {
            BossPhaseConfig phase = _battle.ResolveHpPhase(BossHp / _battle.MaxHp);
            float total = phase.TotalWeight;

            if (total <= 0f)
                return phase.Patterns[0].Pattern; // 가중치를 전부 0으로 둔 설정 — 첫 패턴으로 떨어진다

            float roll = (float)_rng.NextDouble() * total;
            foreach (WeightedPattern entry in phase.Patterns)
            {
                roll -= entry.Weight;
                if (roll <= 0f)
                    return entry.Pattern;
            }

            return phase.Patterns[phase.Patterns.Count - 1].Pattern;
        }

        /// <summary>
        /// 타격 시점 도달. 여기까지 왔다는 것은 <b>아직 안 눌렀다</b>는 뜻이다
        /// (눌렀으면 그 순간 <see cref="PressParry"/>에서 이미 결판났다).
        /// 유예를 열어 조금 늦은 입력까지 받아 준다.
        /// </summary>
        private void ResolveStrike(RuntimeNote runtime)
        {
            float grace = _stats.ParryLateGraceSeconds;
            if (grace > 0f)
            {
                runtime.InGrace = true;
                runtime.GraceEndsAt = _patternTime + grace;
                RefreshBossState();
                return;
            }

            ApplyNoteDamage(runtime);
        }

        private void ParryNote(RuntimeNote runtime)
        {
            runtime.Resolved = true;
            runtime.InGrace = false;

            // 패링 성공: 피해 0, 즉시 복귀(보상), 체간 대폭 상승.
            // 즉시 Ready로 돌리므로 연속기는 노트 수만큼 눌러야 한다.
            SetPlayerState(PlayerActionState.Ready);

            // 막은 공격은 그 자리에서 목록에서 빠진다 — 뷰의 흰 원이 다음 Tick을 기다리지 않고 사라진다.
            RebuildActiveNotes();
            AttackParried?.Invoke(runtime.Note);
            AddPosture(_config.ParryPostureGain);
            RefreshBossState();
        }

        /// <summary>유예까지 지나 확정된 피격.</summary>
        private void ApplyNoteDamage(RuntimeNote runtime)
        {
            runtime.Resolved = true;
            runtime.InGrace = false;

            // DEF로 완전 무효화는 불가 — 최소 1 피해로 압박을 유지한다
            int damage = Math.Max(1, (int)Math.Round(runtime.Note.Damage - _stats.DamageReduction));
            PlayerHit?.Invoke(runtime.Note, damage);

            if (_health.ApplyDamage(damage))
            {
                Finish(victory: false);
                return;
            }

            RefreshBossState();
        }

        private void ProcessPlayerExpiry()
        {
            while (!Finished && PlayerState != PlayerActionState.Ready && _playerTimer <= TimeEpsilon)
            {
                switch (PlayerState)
                {
                    case PlayerActionState.ParryRecovering:
                        SetPlayerState(PlayerActionState.Ready);
                        break;

                    case PlayerActionState.Attacking:
                        SetPlayerState(PlayerActionState.Ready);
                        LandPlayerAttack();
                        break;
                }
            }
        }

        private void RebuildActiveNotes()
        {
            _activeNotes.Clear();
            if (_pattern == null || BossState == BossActionState.Broken)
                return;

            foreach (RuntimeNote note in _notes)
            {
                if (note.Resolved || !note.Telegraphed)
                    continue;

                float span = note.HitSeconds - note.TelegraphStartSeconds;
                float progress = span <= 0f ? 1f : (_patternTime - note.TelegraphStartSeconds) / span;
                _activeNotes.Add(new ActiveNote(
                    note.Note, Math.Clamp(progress, 0f, 1f), note.HitSeconds - _patternTime));
            }

            // 타격이 임박한 것부터 — 뷰가 안쪽 원부터 그린다
            _activeNotes.Sort((a, b) => a.SecondsUntilHit.CompareTo(b.SecondsUntilHit));
        }

        private void LandPlayerAttack()
        {
            float damage = _stats.AttackDamage;
            BossHp = Math.Max(0f, BossHp - damage);
            PlayerAttackLanded?.Invoke(damage);
            BossHpChanged?.Invoke(BossHp, _battle.MaxHp);

            if (BossHp <= 0f)
                Break(); // HP 소진 = 체간 즉시 파괴 → 인살로만 마무리
            else
                AddPosture(damage * _config.AttackPostureFactor);
        }

        private void AddPosture(float amount)
        {
            if (BossState == BossActionState.Broken || amount <= 0f)
                return;

            Posture = Math.Min(_battle.MaxPosture, Posture + amount);
            PostureChanged?.Invoke(Posture, _battle.MaxPosture);

            if (Posture >= _battle.MaxPosture)
                Break();
        }

        private void Break()
        {
            Posture = _battle.MaxPosture;
            BossState = BossActionState.Broken;
            _pattern = null;
            _notes.Clear();
            _activeNotes.Clear();
            PostureChanged?.Invoke(Posture, _battle.MaxPosture);
            BossBroken?.Invoke();
        }

        private void SetPlayerState(PlayerActionState next)
        {
            if (PlayerState == next)
                return;

            PlayerState = next;
            PlayerStateChanged?.Invoke(next);
        }

        private void Finish(bool victory)
        {
            Finished = true;
            _activeNotes.Clear();
            Ended?.Invoke(victory);
        }
    }
}
