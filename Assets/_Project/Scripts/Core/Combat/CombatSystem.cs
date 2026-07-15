using System;
using ChainRiposte.Core.Stats;

namespace ChainRiposte.Core.Combat
{
    /// <summary>
    /// 2버튼 패링 전투의 규칙 엔진 (GDD §5).
    /// 보스는 BossConfig의 패턴을 반복 수행하고, 플레이어는 PressParry/PressAttack 두 입력만 가진다.
    /// 퍼즐에서 파밍한 PlayerStats(ATK/DEF/판정치)와 PlayerHealth가 그대로 이월되어 주입된다.
    /// 시간은 Tick(deltaSeconds)으로만 흐른다 — UnityEngine 비의존, 테스트에서 임의 시간 진행 가능.
    /// </summary>
    public sealed class CombatSystem
    {
        /// <summary>경계 분할 진행에서 생기는 부동소수점 잔차 허용치 — 이보다 적게 남은 타이머는 만료로 본다.</summary>
        private const float TimeEpsilon = 1e-4f;

        private readonly BossConfig _config;
        private readonly PlayerStats _stats;
        private readonly PlayerHealth _health;

        private float _bossTimer;   // 현재 보스 상태(대기/텔레그래프)의 잔여 시간
        private float _playerTimer; // 현재 플레이어 상태(패링/후딜/커밋)의 잔여 시간
        private int _nextAttackIndex;

        public BossActionState BossState { get; private set; } = BossActionState.Recovering;
        public PlayerActionState PlayerState { get; private set; } = PlayerActionState.Ready;

        public float BossHp { get; private set; }
        public float BossMaxHp => _config.MaxHp;

        /// <summary>체간 게이지 — MaxPosture 도달 시 인살 가능.</summary>
        public float Posture { get; private set; }
        public float MaxPosture => _config.MaxPosture;

        /// <summary>텔레그래프 중인 공격 (그 외 상태에서는 null).</summary>
        public BossAttackConfig CurrentAttack { get; private set; }

        /// <summary>체간 파괴 — 화면에 붉은 인살 마크가 뜨는 상태. 공격 버튼이 인살로 바뀐다.</summary>
        public bool ExecutionReady => BossState == BossActionState.Broken;

        public bool Finished { get; private set; }

        /// <summary>(패턴 인덱스, 공격) — 텔레그래프 연출 시작 훅.</summary>
        public event Action<int, BossAttackConfig> AttackTelegraphed;

        /// <summary>패링 성공 — 쇳소리+기타 피드백 훅.</summary>
        public event Action<BossAttackConfig> AttackParried;

        /// <summary>(공격, 실제 피해량) — 패링 실패/미입력 피격. HP 반영은 PlayerHealth.Changed로 통지된다.</summary>
        public event Action<BossAttackConfig, int> PlayerHit;

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

        public CombatSystem(BossConfig config, PlayerStats stats, PlayerHealth health)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _stats = stats ?? throw new ArgumentNullException(nameof(stats));
            _health = health ?? throw new ArgumentNullException(nameof(health));
            if (config.Pattern == null || config.Pattern.Count == 0)
                throw new ArgumentException("보스 공격 패턴이 비어 있습니다.", nameof(config));
            if (config.MaxHp <= 0f || config.MaxPosture <= 0f)
                throw new ArgumentException("보스 HP/체간 한계치는 0보다 커야 합니다.", nameof(config));

            BossHp = config.MaxHp;
            _bossTimer = Math.Max(0f, config.FirstAttackDelaySeconds);
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
                ProcessExpiredStates();
                if (Finished)
                    return;

                float step = remaining;
                if (BossState != BossActionState.Broken)
                    step = Math.Min(step, _bossTimer);
                if (PlayerState != PlayerActionState.Ready)
                    step = Math.Min(step, _playerTimer);

                Advance(step);
                remaining -= step;
                ProcessExpiredStates();
            }
        }

        /// <summary>좌측 버튼 — 탭 순간부터 판정치(초) 동안 패링 판정 활성.</summary>
        public void PressParry()
        {
            if (Finished || PlayerState != PlayerActionState.Ready)
                return;

            SetPlayerState(PlayerActionState.Parrying);
            _playerTimer = _stats.ParryWindowSeconds;
        }

        /// <summary>우측 버튼 — 인살 마크가 떠 있으면 인살, 아니면 공격 커밋 시작.</summary>
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

            SetPlayerState(PlayerActionState.Attacking);
            _playerTimer = _stats.AttackCommitSeconds;
        }

        /// <summary>경과한 시간만큼 타이머/체간 회복을 진행한다 (상태 전환 없음).</summary>
        private void Advance(float step)
        {
            if (step <= 0f)
                return;

            if (BossState != BossActionState.Broken)
            {
                _bossTimer -= step;

                if (Posture > 0f && _config.PostureDecayPerSecond > 0f)
                {
                    float decay = _config.PostureDecayPerSecond;
                    if (_config.ScaleDecayWithHp)
                        decay *= BossHp / _config.MaxHp; // HP가 낮을수록 체간 회복 둔화
                    Posture = Math.Max(0f, Posture - decay * step);
                    PostureChanged?.Invoke(Posture, _config.MaxPosture);
                }
            }

            if (PlayerState != PlayerActionState.Ready)
                _playerTimer -= step;
        }

        /// <summary>
        /// 만료된 상태를 전환한다. 보스 타격을 플레이어 윈도우 만료보다 먼저 처리해
        /// '윈도우가 끝나는 바로 그 순간'의 타격도 패링으로 인정한다 (관대한 판정).
        /// 0초짜리 후딜레이가 연쇄될 수 있어 남은 만료가 없을 때까지 반복한다.
        /// </summary>
        private void ProcessExpiredStates()
        {
            bool transitioned = true;
            while (transitioned && !Finished)
            {
                transitioned = false;

                if (BossState != BossActionState.Broken && _bossTimer <= TimeEpsilon)
                {
                    if (BossState == BossActionState.Telegraphing)
                        ResolveBossStrike();
                    else
                        StartTelegraph();
                    transitioned = true;
                }

                if (!Finished && PlayerState != PlayerActionState.Ready && _playerTimer <= TimeEpsilon)
                {
                    ResolvePlayerStateEnd();
                    transitioned = true;
                }
            }
        }

        private void StartTelegraph()
        {
            int index = _nextAttackIndex;
            _nextAttackIndex = (_nextAttackIndex + 1) % _config.Pattern.Count;

            CurrentAttack = _config.Pattern[index];
            BossState = BossActionState.Telegraphing;
            _bossTimer = CurrentAttack.TelegraphSeconds;
            AttackTelegraphed?.Invoke(index, CurrentAttack);
        }

        private void ResolveBossStrike()
        {
            BossAttackConfig attack = CurrentAttack;
            CurrentAttack = null;

            if (attack.Parryable && PlayerState == PlayerActionState.Parrying)
            {
                // 패링 성공: 피해 0, 후딜레이 없이 즉시 복귀(보상), 체간 대폭 상승
                SetPlayerState(PlayerActionState.Ready);
                AttackParried?.Invoke(attack);
                AddPosture(_config.ParryPostureGain);
            }
            else
            {
                // DEF로 완전 무효화는 불가 — 최소 1 피해로 압박을 유지한다
                int damage = Math.Max(1, (int)Math.Round(attack.Damage - _stats.DamageReduction));
                PlayerHit?.Invoke(attack, damage);
                if (_health.ApplyDamage(damage))
                {
                    Finish(victory: false);
                    return;
                }
            }

            if (BossState != BossActionState.Broken)
            {
                BossState = BossActionState.Recovering;
                _bossTimer = attack.RecoverySeconds;
            }
        }

        private void ResolvePlayerStateEnd()
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
            CurrentAttack = null;
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
            Ended?.Invoke(victory);
        }
    }
}
