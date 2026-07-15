using ChainRiposte.Core.Combat;
using ChainRiposte.Core.Flow;
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

            _combat = new CombatSystem(boss, gameManager.Session.Stats, gameManager.Session.Health);
            _combat.Ended += OnCombatEnded;

            if (puzzleBoardRoot != null)
                puzzleBoardRoot.SetActive(false);

            screen.Bind(_combat, gameManager.Session);
            if (juice != null)
                juice.BindCombat(_combat);
            input.SetActive(true);
        }

        private void OnCombatEnded(bool victory)
        {
            input.SetActive(false);
            gameManager.Session.EndStage(victory);
        }
    }
}
