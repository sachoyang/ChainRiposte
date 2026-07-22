using System;
using System.Collections;
using ChainRiposte.Core.Combat;
using ChainRiposte.Core.Flow;
using ChainRiposte.Core.Intrusion;
using ChainRiposte.Core.Match;
using UnityEngine;

namespace ChainRiposte.Game.Juice
{
    /// <summary>
    /// Game Juice 허브 — Core/뷰의 모든 연출 이벤트가 여기로 수렴한다 (GDD §7).
    /// 오디오 클립 슬롯은 전부 비어 있어도 동작하며(널 세이프), 에셋 단계에서 인스펙터에 꽂기만 하면 된다.
    /// 클립 없이도 카메라 셰이크/히트스톱/콤보 피치는 즉시 동작하는 저비용 juice.
    /// </summary>
    public sealed class JuiceDirector : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private CameraShaker cameraShaker;

        [Header("음악 (루프)")]
        [Tooltip("퍼즐 페이즈 — 고요한 앰비언트")]
        [SerializeField] private AudioClip puzzleMusic;
        [Tooltip("전투 페이즈 — 빠른 록 인스트루멘탈")]
        [SerializeField] private AudioClip combatMusic;

        [Header("난입 크레센도 (루프)")]
        [Tooltip("보스 스폰 확률에 비례해 커지는 디스토션 노이즈")]
        [SerializeField] private AudioClip tensionLoop;
        [Tooltip("이 스폰 확률에서 크레센도 볼륨이 최대(1)가 된다")]
        [SerializeField, Range(0.05f, 1f)] private float tensionMaxChance = 0.3f;

        [Header("퍼즐 SFX")]
        [SerializeField] private AudioClip matchClearClip;
        [Tooltip("콤보당 피치 증가량 — 연쇄가 이어질수록 경쾌해진다")]
        [SerializeField, Range(0f, 0.3f)] private float comboPitchStep = 0.08f;
        [SerializeField] private AudioClip levelUpClip;

        [Header("전투 SFX")]
        [SerializeField] private AudioClip parryClip;
        [SerializeField] private AudioClip playerHitClip;
        [SerializeField] private AudioClip attackLandClip;
        [SerializeField] private AudioClip bossBrokenClip;
        [SerializeField] private AudioClip executionClip;

        [Header("히트스톱")]
        [SerializeField, Range(0f, 0.2f)] private float parryHitStop = 0.06f;
        [SerializeField, Range(0f, 0.6f)] private float executionHitStop = 0.35f;

        private AudioSource _musicSource;
        private AudioSource _tensionSource;
        private AudioSource _sfxSource;

        private Func<float> _bossChanceGetter; // 난입 크레센도 입력
        private CombatSystem _combat;
        private Coroutine _hitStopRoutine;

        private void Awake()
        {
            _musicSource = CreateSource(loop: true);
            _tensionSource = CreateSource(loop: true);
            _sfxSource = CreateSource(loop: false);
        }

        private void Start()
        {
            gameManager.Session.PhaseChanged += OnPhaseChanged;
            gameManager.Session.Stats.LeveledUp += OnLeveledUp;
        }

        private void OnDestroy()
        {
            if (gameManager != null && gameManager.Session != null)
            {
                gameManager.Session.PhaseChanged -= OnPhaseChanged;
                gameManager.Session.Stats.LeveledUp -= OnLeveledUp;
            }
            UnbindCombat();
            Time.timeScale = 1f; // 히트스톱 도중 파괴돼도 복구
        }

        private void Update()
        {
            // 디스토션 크레센도: 스폰 확률이 오를수록 노이즈가 커진다 (GDD §4.1 '심연')
            if (_tensionSource.clip != null && _bossChanceGetter != null)
                _tensionSource.volume = Mathf.Clamp01(_bossChanceGetter() / tensionMaxChance);
        }

        // ── 바인딩 (컨트롤러가 엔진 생성 직후 호출) ──

        /// <summary>퍼즐 시작 시 — 타일 파괴 SFX와 난입 크레센도를 연결한다.</summary>
        public void BindPuzzle(Puzzle.BoardView boardView, BossTileSpawner spawner)
        {
            boardView.StepCleared -= OnStepCleared; // 중복 구독 방지
            boardView.StepCleared += OnStepCleared;
            _bossChanceGetter = () => spawner.CurrentChance;

            if (tensionLoop != null)
            {
                _tensionSource.clip = tensionLoop;
                _tensionSource.volume = 0f;
                _tensionSource.Play();
            }
        }

        /// <summary>전투 돌입 시 — 패링/피격/인살 연출을 연결한다.</summary>
        public void BindCombat(CombatSystem combat)
        {
            UnbindCombat();
            _combat = combat;
            _bossChanceGetter = null;
            _tensionSource.Stop();

            combat.AttackParried += OnParried;
            combat.PlayerHit += OnPlayerHit;
            combat.PlayerAttackLanded += OnAttackLanded;
            combat.BossBroken += OnBossBroken;
            combat.ExecutionPerformed += OnExecution;
        }

        private void UnbindCombat()
        {
            if (_combat == null)
                return;

            _combat.AttackParried -= OnParried;
            _combat.PlayerHit -= OnPlayerHit;
            _combat.PlayerAttackLanded -= OnAttackLanded;
            _combat.BossBroken -= OnBossBroken;
            _combat.ExecutionPerformed -= OnExecution;
            _combat = null;
        }

        // ── 세션/퍼즐 훅 ──

        private void OnPhaseChanged(GamePhase previous, GamePhase next)
        {
            switch (next)
            {
                case GamePhase.Puzzle:
                    PlayMusic(puzzleMusic);
                    break;
                case GamePhase.Combat:
                    PlayMusic(combatMusic);
                    break;
                default: // Victory/Defeat — 결과 화면에 음악이 깔리지 않게 정지
                    _musicSource.Stop();
                    _tensionSource.Stop();
                    break;
            }
        }

        private void OnLeveledUp(int level) => PlaySfx(levelUpClip);

        private void OnStepCleared(CascadeStep step) =>
            PlaySfx(matchClearClip, 1f + comboPitchStep * (step.ComboIndex - 1));

        // ── 전투 훅 ──

        private void OnParried(BossNoteConfig note)
        {
            PlaySfx(parryClip);
            cameraShaker.Shake(0.12f, 0.15f);
            HitStop(parryHitStop);
        }

        private void OnPlayerHit(BossNoteConfig note, int damage)
        {
            PlaySfx(playerHitClip);
            cameraShaker.Shake(0.25f, 0.3f);
        }

        private void OnAttackLanded(float damage)
        {
            PlaySfx(attackLandClip);
            cameraShaker.Shake(0.06f, 0.1f);
        }

        private void OnBossBroken()
        {
            PlaySfx(bossBrokenClip);
            cameraShaker.Shake(0.3f, 0.4f);
        }

        private void OnExecution()
        {
            PlaySfx(executionClip);
            cameraShaker.Shake(0.4f, 0.5f);
            HitStop(executionHitStop);
        }

        // ── 재생 유틸 (전부 널 세이프) ──

        private AudioSource CreateSource(bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.loop = loop;
            source.playOnAwake = false;
            return source;
        }

        private void PlayMusic(AudioClip clip)
        {
            _musicSource.Stop();
            if (clip == null)
                return;
            _musicSource.clip = clip;
            _musicSource.Play();
        }

        private void PlaySfx(AudioClip clip, float pitch = 1f)
        {
            if (clip == null)
                return;
            _sfxSource.pitch = pitch;
            _sfxSource.PlayOneShot(clip);
        }

        /// <summary>순간 정지 손맛 — 전투 Tick이 Time.deltaTime을 쓰므로 판정도 함께 멈춘다.</summary>
        private void HitStop(float duration)
        {
            if (duration <= 0f)
                return;
            if (_hitStopRoutine != null)
                StopCoroutine(_hitStopRoutine);
            _hitStopRoutine = StartCoroutine(HitStopRoutine(duration));
        }

        private IEnumerator HitStopRoutine(float duration)
        {
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = 1f;
            _hitStopRoutine = null;
        }
    }
}
