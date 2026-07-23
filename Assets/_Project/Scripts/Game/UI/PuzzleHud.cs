using System.Collections;
using ChainRiposte.Core.Flow;
using ChainRiposte.Game.Localization;
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
        private string _bannerKey;
        private string _flashKey;
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

        // 코드가 직접 채우는 텍스트(HP·턴 등)는 LocalizedText가 못 잡으므로 여기서 다시 그린다.
        private void OnEnable() => Loc.LanguageChanged += OnLanguageChanged;
        private void OnDisable() => Loc.LanguageChanged -= OnLanguageChanged;

        private void OnLanguageChanged()
        {
            if (_session != null && _engine != null)
                RefreshAll();
        }

        /// <summary>배너에 잠깐 문구를 띄운다 (데드락 리롤 등). 페이즈 배너와 같은 자리를 쓴다.</summary>
        public void FlashBanner(string locKey, float seconds)
        {
            if (bannerText == null)
                return;

            StopCoroutine(nameof(FlashBannerRoutine));
            _flashKey = locKey;
            _flashSeconds = seconds;
            StartCoroutine(nameof(FlashBannerRoutine));
        }

        private IEnumerator FlashBannerRoutine()
        {
            bannerText.text = Loc.GetText(_flashKey);
            yield return new WaitForSeconds(_flashSeconds);
            _flashKey = null;
            bannerText.text = _bannerKey == null ? string.Empty : Loc.GetText(_bannerKey);
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
            _bannerKey = next switch
            {
                GamePhase.Victory => "puzzle.banner.victory",
                GamePhase.Defeat => "puzzle.banner.defeat",
                GamePhase.Intermission => "puzzle.banner.intermission",
                GamePhase.Combat => "puzzle.banner.combat",
                _ => null,
            };

            bannerText.text = _bannerKey == null ? string.Empty : Loc.GetText(_bannerKey);
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
            bannerText.text = _bannerKey == null ? string.Empty : Loc.GetText(_bannerKey);
        }

        private void RefreshHealth() =>
            hpText.text = Loc.GetText("puzzle.hp", _session.Health.Current, _session.Health.Max);

        private void RefreshSouls()
        {
            PlayerStats stats = _session.Stats;
            soulsText.text = Loc.GetText(
                "puzzle.souls", stats.Level, stats.Souls, stats.SoulsToNextLevel, stats.PendingPoints);
            RefreshStats();
        }

        private void RefreshTurns() =>
            turnsText.text = Loc.GetText("puzzle.turns", _engine.TurnsRemaining);

        private void RefreshStats()
        {
            PlayerStats stats = _session.Stats;
            statsText.text = Loc.GetText(
                "puzzle.stats", stats.AttackDamage, stats.DamageReduction, stats.ParryWindowSeconds);

            SetButton(attackButton, "puzzle.alloc.attack", stats.GetStatLevel(StatType.Attack), StatType.Attack, stats);
            SetButton(defenseButton, "puzzle.alloc.defense", stats.GetStatLevel(StatType.Defense), StatType.Defense, stats);
            SetButton(parryButton, "puzzle.alloc.parry", stats.GetStatLevel(StatType.Parry), StatType.Parry, stats);
        }

        private static void SetButton(Button button, string locKey, int level, StatType stat, PlayerStats stats)
        {
            // 상한에 걸린 것과 포인트가 모자란 것은 다르다 — 비용이 2 이상인 스탯이 생기면서 갈렸다.
            string label = stats.IsAtCap(stat)
                ? Loc.GetText(locKey + ".max")
                : Loc.GetText(locKey, level);

            // 값이 1보다 비싸면 얼마가 드는지 버튼에 적는다 — 안 적으면 왜 안 눌리는지 알 수 없다.
            int cost = stats.GetPointCost(stat);
            if (cost > 1 && !stats.IsAtCap(stat))
                label += Loc.GetText("puzzle.alloc.cost", cost);

            button.interactable = stats.CanAllocate(stat);
            button.GetComponentInChildren<TMP_Text>().text = label;
        }

    }
}
