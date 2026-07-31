using System.Collections;
using ChainRiposte.Core.Combat;
using ChainRiposte.Core.Flow;
using ChainRiposte.Game.Config;
using UnityEngine;

namespace ChainRiposte.Game.Combat
{
    /// <summary>
    /// 전투 페이즈의 Unity 측 오케스트레이터.
    /// Combat 페이즈 진입 시 퍼즐에서 이월된 Stats/Health로 CombatSystem을 조립하고,
    /// 입력 → 엔진 → 화면(CombatScreen) → 세션 종료를 연결한다.
    /// </summary>
    public sealed class CombatController : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private CombatInput input;
        [SerializeField] private CombatScreen screen;
        [Tooltip("전투 동안 숨길 퍼즐 보드 루트")]
        [SerializeField] private GameObject puzzleBoardRoot;
        [SerializeField] private Juice.JuiceDirector juice;

        private CombatSystem _combat;

        /// <summary>
        /// 이 판의 전투. 보스전에 들어가기 전에는 null이다 — 구독하려면
        /// <see cref="CombatBegun"/>을 기다릴 것.
        /// </summary>
        public CombatSystem Combat => _combat;

        /// <summary>
        /// 전투가 만들어진 직후 (아직 첫 노트 전). 튜토리얼처럼 <b>전투를 옆에서 지켜보는</b> 쪽이
        /// 이벤트를 붙일 자리다 — 전투 코드가 그 존재를 알 필요가 없다.
        /// </summary>
        public event System.Action<CombatSystem> CombatBegun;

        private void Awake()
        {
            gameManager.Session.PhaseChanged += OnPhaseChanged;
            input.ParryPressed += OnParryPressed;
            input.AttackPressed += OnAttackPressed;
        }

        private void OnDestroy()
        {
            if (gameManager != null && gameManager.Session != null)
                gameManager.Session.PhaseChanged -= OnPhaseChanged;
            input.ParryPressed -= OnParryPressed;
            input.AttackPressed -= OnAttackPressed;
        }

        private void Update()
        {
            if (_combat != null && !_combat.Finished)
                _combat.Tick(Time.deltaTime);
        }

        private void OnParryPressed() => _combat?.PressParry();

        private void OnAttackPressed() => _combat?.PressAttack();

        private void OnPhaseChanged(GamePhase previous, GamePhase next)
        {
            if (next == GamePhase.Combat)
                BeginCombat();
            else if (previous == GamePhase.Combat)
                input.SetActive(false);
        }

        private void BeginCombat()
        {
            BossConfig boss = gameManager.StageConfig.Boss;
            if (boss == null)
            {
                Debug.LogError($"{nameof(CombatController)}: 스테이지에 보스 데이터가 없습니다. StageDataSO의 bossData를 확인하세요.", this);
                return;
            }

            // 삼킨 기억은 합산된 수치 한 벌로만 들어간다 — 전투는 기억이 몇 개인지 모른다 (§2.2).
            _combat = new CombatSystem(
                boss, gameManager.Session.Stats, gameManager.Session.Health,
                memories: gameManager.MemoryEffects);
            _combat.Ended += OnCombatEnded;
            _combat.PhaseCleared += OnPhaseCleared;
            _combat.PhaseStarted += OnPhaseStarted;
            CombatBegun?.Invoke(_combat);

            if (puzzleBoardRoot != null)
                puzzleBoardRoot.SetActive(false);

            // 보스 생김새는 SO에서 직접 읽는다 — 스프라이트는 순수 C# BossConfig에 담을 수 없다.
            // 규칙은 BossVisual 한 곳뿐이다 — 준비 화면의 그림자도 같은 것을 부르므로 둘이 어긋날 수 없다.
            BossDataSO bossData = BossData;
            screen.SetBossVisual(BossVisual.ResolveSprite(bossData), BossVisual.ResolveNameKey(bossData));

            screen.Bind(_combat, gameManager.Session);
            if (juice != null)
                juice.BindCombat(_combat);
            input.SetActive(true);
        }

        /// <summary>
        /// 승리는 <b>언제나 인살</b>로 끝난다(Core의 유일한 승리 경로). 그래서 결과 화면으로 넘기기 전에
        /// 처형 컷씬을 돌린다 — 여기서 기다리는 것이 전부이고 Core는 이미 <c>Finished</c>라 아무것도 안 센다.
        /// 패배는 즉시 넘긴다(보여 줄 처형이 없다).
        /// </summary>
        private void OnCombatEnded(bool victory)
        {
            input.SetActive(false);
            if (!victory)
            {
                gameManager.Session.EndStage(false);
                return;
            }

            StartCoroutine(VictoryRoutine());
        }

        private IEnumerator VictoryRoutine()
        {
            yield return screen.PlayExecution();
            gameManager.Session.EndStage(true);
        }

        private BossDataSO BossData => gameManager.StageData != null ? gameManager.StageData.BossData : null;

        /// <summary>
        /// 인살했지만 페이즈가 남았다 — 컷씬을 돌리고, <b>끝난 뒤에</b> 전투를 재개시킨다.
        /// Core는 이 동안 시간을 세지 않으므로 연출 길이를 여기서 마음대로 정할 수 있다.
        /// </summary>
        private void OnPhaseCleared(int clearedPhase)
        {
            input.SetActive(false);
            StartCoroutine(PhaseTransitionRoutine(clearedPhase + 1));
        }

        private IEnumerator PhaseTransitionRoutine(int nextPhase)
        {
            BossDataSO bossData = BossData;

            // 처형이 먼저다 — 방금 벤 것이 '이 페이즈의 보스'이므로, 모습을 갈아 끼우는 전환 컷씬보다
            // 앞에 와야 한다. 순서가 뒤바뀌면 다음 페이즈 모습을 처형하는 그림이 된다.
            yield return screen.PlayExecution();

            yield return screen.PlayPhaseTransition(
                BossVisual.ResolveTransitionSprite(bossData, nextPhase),
                BossVisual.ResolveSprite(bossData, nextPhase),
                BossVisual.ResolveTransitionTextKey(bossData, nextPhase));

            _combat.BeginNextPhase();
            input.SetActive(true);
        }

        private void OnPhaseStarted(int phase) => screen.OnPhaseStarted();
    }
}
