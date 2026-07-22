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
        }

        private readonly BossConfig _config;
        private readonly PlayerStats _stats;
        private readonly PlayerHealth _health;
        private readonly Random _rng;

        private readonly List<RuntimeNote> _notes = new();
        private readonly List<ActiveNote> _activeNotes = new();

        private BossPatternConfig _pattern;
        private float _patternTime;   // 현재 패턴의 경과 시간(초). 리드인 동안은 음수.
        private float _gapTimer;      // 패턴 사이 대기. 0보다 크면 패턴이 아직 안 돌고 있다.
        private float _playerTimer;   // 현재 플레이어 상태(패링/후딜/커밋)의 잔여 시간

        public BossActionState BossState { get; private set; } = BossActionState.Recovering;
        public PlayerActionState PlayerState { get; private set; } = PlayerActionState.Ready;

        public float BossHp { get; private set; }
        public float BossMaxHp => _config.MaxHp;

        /// <summary>체간 게이지 — MaxPosture 도달 시 인살 가능.</summary>
        public float Posture { get; private set; }
        public float MaxPosture => _config.MaxPosture;

        /// <summary>재생 중인 패턴 (패턴 사이 대기 중에는 직전 패턴이 남아 있다).</summary>
        public BossPatternConfig CurrentPattern => _pattern;

        /// <summary>지금 그려야 할 노트들 — 예비동작이 시작됐고 아직 타격되지 않은 것. 타격이 임박한 순.</summary>
        public IReadOnlyList<ActiveNote> ActiveNotes => _activeNotes;

        /// <summary>패링 판정 폭(초). 뷰가 회색 띠의 두께로 그린다 — PARRY 스탯을 올리면 굵어진다.</summary>
        public float ParryWindowSeconds => _stats.ParryWindowSeconds;

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

        /// <summary>인살 입력 성공 — 피니시 연출 훅. 직후 Ended(true)가 발행된다.</summary>
        public event Action ExecutionPerformed;

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

            if (config.Phases == null || config.Phases.Count == 0)
                throw new ArgumentException("보스 페이즈가 비어 있습니다.", nameof(config));
            if (config.MaxHp <= 0f || config.MaxPosture <= 0f)
                throw new ArgumentException("보스 HP/체간 한계치는 0보다 커야 합니다.", nameof(config));

            BossHp = config.MaxHp;
            _gapTimer = Math.Max(0f, config.FirstAttackDelaySeconds);
        }

        /// <summary>
        /// 시간 진행. 상태 경계(타격 시점, 윈도우 만료 등)마다 끊어 진행하므로
        /// deltaSeconds가 커도 판정 순서가 프레임레이트와 무관하게 결정적이다.
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || Finished)
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

        /// <summary>좌측 버튼 — 탭 순간부터 판정치(초) 동안 패링 판정 활성.</summary>
        public void PressParry()
        {
            if (Finished || PlayerState != PlayerActionState.Ready)
                return;

            SetPlayerState(PlayerActionState.Parrying);
            _playerTimer = _stats.ParryWindowSeconds;
        }

        /// <summary>
        /// 우측 버튼 — 인살 마크가 떠 있으면 인살, 아니면 공격 커밋.
        /// <b>공격은 빈 박에만 들어간다</b> — 노트가 날아오는 중에 누르면 헛짓으로 잠긴다.
        /// </summary>
        public void PressAttack()
        {
            if (Finished)
                return;

            if (ExecutionReady)
            {
                ExecutionPerformed?.Invoke();
                Finish(victory: true);
                return;
            }

            if (PlayerState != PlayerActionState.Ready)
                return;

            if (BossState == BossActionState.Telegraphing)
            {
                // 날아오는 노트가 있는데 공격을 시도했다 — 빈 박까지 기다려야 한다
                SetPlayerState(PlayerActionState.ParryRecovering);
                _playerTimer = _stats.ParryWhiffLockSeconds;
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
                float target = note.Telegraphed ? note.HitSeconds : note.TelegraphStartSeconds;
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
                decay *= BossHp / _config.MaxHp; // HP가 낮을수록 체간 회복 둔화

            Posture = Math.Max(0f, Posture - decay * step);
            PostureChanged?.Invoke(Posture, _config.MaxPosture);
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
                    if (note.Telegraphed && _patternTime >= note.HitSeconds - TimeEpsilon)
                        return note;
                }
                else if (!note.Telegraphed && _patternTime >= note.TelegraphStartSeconds - TimeEpsilon)
                {
                    return note;
                }
            }

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
            BossPhaseConfig phase = _config.ResolvePhase(BossHp / _config.MaxHp);
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

        private void ResolveStrike(RuntimeNote runtime)
        {
            runtime.Resolved = true;
            BossNoteConfig note = runtime.Note;

            if (PlayerState == PlayerActionState.Parrying)
            {
                // 패링 성공: 피해 0, 즉시 복귀(보상), 체간 대폭 상승.
                // 즉시 Ready로 돌리므로 연속기는 노트 수만큼 눌러야 한다.
                SetPlayerState(PlayerActionState.Ready);
                AttackParried?.Invoke(note);
                AddPosture(_config.ParryPostureGain);
                RefreshBossState();
                return;
            }

            // DEF로 완전 무효화는 불가 — 최소 1 피해로 압박을 유지한다
            int damage = Math.Max(1, (int)Math.Round(note.Damage - _stats.DamageReduction));
            PlayerHit?.Invoke(note, damage);

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
                    case PlayerActionState.Parrying:
                        // 헛침 — 연타 방지 후딜레이
                        SetPlayerState(PlayerActionState.ParryRecovering);
                        _playerTimer = _stats.ParryWhiffLockSeconds;
                        break;

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
            BossHpChanged?.Invoke(BossHp, _config.MaxHp);

            if (BossHp <= 0f)
                Break(); // HP 소진 = 체간 즉시 파괴 → 인살로만 마무리
            else
                AddPosture(damage * _config.AttackPostureFactor);
        }

        private void AddPosture(float amount)
        {
            if (BossState == BossActionState.Broken || amount <= 0f)
                return;

            Posture = Math.Min(_config.MaxPosture, Posture + amount);
            PostureChanged?.Invoke(Posture, _config.MaxPosture);

            if (Posture >= _config.MaxPosture)
                Break();
        }

        private void Break()
        {
            Posture = _config.MaxPosture;
            BossState = BossActionState.Broken;
            _pattern = null;
            _notes.Clear();
            _activeNotes.Clear();
            PostureChanged?.Invoke(Posture, _config.MaxPosture);
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
