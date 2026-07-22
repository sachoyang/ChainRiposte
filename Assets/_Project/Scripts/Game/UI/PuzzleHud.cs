using System.Collections;
using ChainRiposte.Core.Flow;
using ChainRiposte.Core.Match;
using ChainRiposte.Core.Stats;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChainRiposte.Game.UI
{
    /// <summary>
    /// 퍼즐 HUD. UI는 <b>씬에 실물로 배치</b>(TMP)하고 이 컴포넌트는 참조만 받아 갱신·버튼 처리만 한다.
    /// 초기 레이아웃은 <c>Tools ▸ ChainRiposte ▸ Build Main Scene UI</c>로 생성 후 씬에서 편집.
    /// Core 이벤트 구독으로만 갱신되고, 상태 변경은 스탯 분배 버튼뿐이다.
    /// </summary>
    public sealed class PuzzleHud : MonoBehaviour
    {
        [Header("씬 참조 (빌더가 자동 배선)")]
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private TMP_Text soulsText;
        [SerializeField] private TMP_Text turnsText;
        [SerializeField] private TMP_Text statsText;
        [SerializeField] private TMP_Text bannerText;
        [SerializeField] private Button attackButton;
        [SerializeField] private Button defenseButton;
        [SerializeField] private Button parryButton;

        private GameSession _session;
        private PuzzleEngine _engine;
        private string _flashMessage;
        private float _flashSeconds;

        private void Awake()
        {
            if (attackButton == null || defenseButton == null || parryButton == null)
            {
                Debug.LogError($"{nameof(PuzzleHud)}: UI 참조가 비어 있습니다. " +
                    "Tools ▸ ChainRiposte ▸ Build Main Scene UI 를 실행하세요.", this);
                enabled = false;
                return;
            }

            attackButton.onClick.AddListener(() => RequestAllocate(StatType.Attack));
            defenseButton.onClick.AddListener(() => RequestAllocate(StatType.Defense));
            parryButton.onClick.AddListener(() => RequestAllocate(StatType.Parry));
        }

        private void OnDestroy() => Unbind();

        /// <summary>배너에 잠깐 문구를 띄운다 (데드락 리롤 등). 페이즈 배너와 같은 자리를 쓴다.</summary>
        public void FlashBanner(string message, float seconds)
        {
            if (bannerText == null)
                return;

            StopCoroutine(nameof(FlashBannerRoutine));
            _flashMessage = message;
            _flashSeconds = seconds;
            StartCoroutine(nameof(FlashBannerRoutine));
        }

        private IEnumerator FlashBannerRoutine()
        {
            bannerText.text = _flashMessage;
            yield return new WaitForSeconds(_flashSeconds);
            bannerText.text = string.Empty;
        }

        /// <summary>퍼즐 시작 시 컨트롤러가 호출한다. 엔진은 스테이지마다 새로 생성되므로 매번 다시 바인딩.</summary>
        public void Bind(GameSession session, PuzzleEngine engine)
        {
            Unbind();
            _session = session;
            _engine = engine;

            session.Stats.SoulsChanged += OnSoulsChanged;
            session.Stats.LeveledUp += OnLeveledUp;
            session.Stats.StatAllocated += OnStatAllocated;
            session.Health.Changed += OnHealthChanged;
            session.PhaseChanged += OnPhaseChanged;
            engine.TurnsChanged += OnTurnsChanged;

            RefreshAll();
        }

        private void Unbind()
        {
            if (_session != null)
            {
                _session.Stats.SoulsChanged -= OnSoulsChanged;
                _session.Stats.LeveledUp -= OnLeveledUp;
                _session.Stats.StatAllocated -= OnStatAllocated;
                _session.Health.Changed -= OnHealthChanged;
                _session.PhaseChanged -= OnPhaseChanged;
                _session = null;
            }

            if (_engine != null)
            {
                _engine.TurnsChanged -= OnTurnsChanged;
                _engine = null;
            }
        }

        private void OnSoulsChanged(int souls, int required) => RefreshSouls();
        private void OnLeveledUp(int level) => RefreshSouls();
        private void OnStatAllocated(StatType stat, int newLevel) => RefreshStats();
        private void OnHealthChanged(int current, int max) => RefreshHealth();
        private void OnTurnsChanged(int remaining) => RefreshTurns();

        private void OnPhaseChanged(GamePhase previous, GamePhase next)
        {
            bannerText.text = next switch
            {
                GamePhase.Victory => "STAGE CLEAR",
                GamePhase.Defeat => "DEFEAT",
                GamePhase.Combat => "BOSS!",
                _ => string.Empty,
            };
        }

        private void RequestAllocate(StatType stat)
        {
            if (_session == null || !_session.Stats.CanAllocate(stat))
                return;

            _session.Stats.Allocate(stat);
            RefreshSouls();
        }

        private void RefreshAll()
        {
            RefreshHealth();
            RefreshSouls();
            RefreshTurns();
            RefreshStats();
            bannerText.text = string.Empty;
        }

        private void RefreshHealth() =>
            hpText.text = $"HP {_session.Health.Current}/{_session.Health.Max}";

        private void RefreshSouls()
        {
            PlayerStats stats = _session.Stats;
            soulsText.text = $"Lv {stats.Level}   Souls {stats.Souls}/{stats.SoulsToNextLevel}   Points {stats.PendingPoints}";
            RefreshStats();
        }

        private void RefreshTurns() =>
            turnsText.text = $"Turns {_engine.TurnsRemaining}";

        private void RefreshStats()
        {
            PlayerStats stats = _session.Stats;
            statsText.text =
                $"ATK {stats.AttackDamage:0}   DEF {stats.DamageReduction:0}   PARRY {stats.ParryWindowSeconds:0.00}s";

            SetButton(attackButton, $"+ATK\nLv {stats.GetStatLevel(StatType.Attack)}", stats.CanAllocate(StatType.Attack));
            SetButton(defenseButton, $"+DEF\nLv {stats.GetStatLevel(StatType.Defense)}", stats.CanAllocate(StatType.Defense));
            bool parryCapped = stats.PendingPoints > 0 && !stats.CanAllocate(StatType.Parry);
            SetButton(parryButton,
                parryCapped ? "+PARRY\nMAX" : $"+PARRY\nLv {stats.GetStatLevel(StatType.Parry)}",
                stats.CanAllocate(StatType.Parry));
        }

        private static void SetButton(Button button, string label, bool interactable)
        {
            button.interactable = interactable;
            button.GetComponentInChildren<TMP_Text>().text = label;
        }
    }
}
