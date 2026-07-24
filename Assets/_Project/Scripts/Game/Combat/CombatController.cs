using ChainRiposte.Core.Combat;
using ChainRiposte.Core.Flow;
using ChainRiposte.Game.Config;
using ChainRiposte.Game.Theming;
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

            // 보스 생김새는 SO에서 직접 읽는다 — 스프라이트는 순수 C# BossConfig에 담을 수 없다
            BossDataSO bossData = gameManager.StageData != null ? gameManager.StageData.BossData : null;
            screen.SetBossVisual(ResolveBossSprite(bossData), ResolveBossNameKey(bossData));

            screen.Bind(_combat, gameManager.Session);
            if (juice != null)
                juice.BindCombat(_combat);
            input.SetActive(true);
        }

        /// <summary>
        /// 겉모습만 테마가 갈아 끼운다 — HP·체간·채보는 <see cref="BossDataSO"/> 그대로라 난이도는 캐릭터와 무관하다.
        /// 테마에 항목이 없거나 칸이 비어 있으면 SO 값으로 떨어진다.
        /// </summary>
        private static Sprite ResolveBossSprite(BossDataSO bossData)
        {
            if (bossData == null)
                return null;

            if (ThemeService.TryGetBoss(bossData.BossId, out ThemeSO.BossEntry themed) && themed.sprite != null)
                return themed.sprite;

            return bossData.BattleSprite;
        }

        private static string ResolveBossNameKey(BossDataSO bossData)
        {
            if (bossData == null)
                return null;

            if (ThemeService.TryGetBoss(bossData.BossId, out ThemeSO.BossEntry themed) &&
                !string.IsNullOrWhiteSpace(themed.nameKey))
                return themed.nameKey;

            return bossData.NameKey;
        }

        private void OnCombatEnded(bool victory)
        {
            input.SetActive(false);
            gameManager.Session.EndStage(victory);
        }
    }
}
