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
    /// 퍼즐 HUD. UI는 <b>씬에 실물로 배치</b>(TMP)하고 이 컴포넌트는 참조만 받아 갱신만 한다.
    /// 초기 레이아웃은 <c>Tools ▸ ChainRiposte ▸ Build Main Scene UI</c>로 생성 후 씬에서 편집.
    ///
    /// <b>여기에는 버튼이 없다.</b> 스탯 분배는 준비 화면(<see cref="StatAllocationPanel"/>)으로 옮겼다 —
    /// 퍼즐 중에는 보드를 보는 것이 전부이고, 성장은 시간에 안 쫓기는 구간에서 정하는 편이 낫다.
    /// </summary>
    public sealed class PuzzleHud : MonoBehaviour
    {
        [Header("씬 참조 (빌더가 자동 배선)")]
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private TMP_Text soulsText;
        [SerializeField] private TMP_Text turnsText;
        [SerializeField] private TMP_Text statsText;
        [SerializeField] private TMP_Text bannerText;

        [Header("하단 바 (Add Puzzle Bottom Bar To Main 이 배선)")]
        [Tooltip("체력 게이지 — 전투 화면과 같은 모양. 숫자만 있으면 성난 몬스터에게 맞아도 눈에 안 들어온다.")]
        [SerializeField] private Image playerHpFill;
        [Tooltip("게이지 위에 겹치는 숫자. 비워도 게이지만으로 동작한다.")]
        [SerializeField] private TMP_Text playerHpText;
        [Tooltip("보스전까지 남은 시간 — 큰 글씨 전용 자리. 비우면 턴 표시 밑에 작게 붙는다(예전 동작).")]
        [SerializeField] private TMP_Text bossTimerText;
        [Tooltip("남은 시간이 이 값 아래로 내려가면 색이 바뀌며 경고한다.")]
        [SerializeField, Min(0f)] private float bossTimerWarnSeconds = 15f;
        [SerializeField] private Color bossTimerColor = new(0.85f, 0.82f, 0.75f);
        [SerializeField] private Color bossTimerWarnColor = new(0.95f, 0.25f, 0.22f);

        private GameSession _session;
        private PuzzleEngine _engine;
        // 소울 광맥 잔량을 아는 유일한 창구. 비어 있으면(Main 단독 실행 등) 광맥 표시를 아예 안 한다.
        private GameManager _game;
        // 보스전까지 남은 초. 음수 = 판 시계를 안 쓰는 스테이지라 표시하지 않는다.
        private float _bossTimerSeconds = -1f;
        private string _bannerKey;
        private string _flashKey;
        private float _flashSeconds;

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

        /// <summary>
        /// 퍼즐 시작 시 컨트롤러가 호출한다. 엔진은 스테이지마다 새로 생성되므로 매번 다시 바인딩.
        /// <paramref name="game"/>은 소울 광맥 잔량을 읽기 위한 것 — 비워도 광맥 표시만 빠지고 나머지는 그대로다.
        /// </summary>
        public void Bind(GameSession session, PuzzleEngine engine, GameManager game = null)
        {
            Unbind();
            _session = session;
            _engine = engine;
            _game = game;
            _bossTimerSeconds = -1f; // 시계가 도는 스테이지면 첫 Tick이 곧 채운다

            if (_game != null)
            {
                _game.RemainingStageSoulsChanged += OnRemainingSoulsChanged;
                // 들어오자마자 마른 땅이면 알려 준다 — 아무리 매치해도 소울이 0인 이유가 화면에 있어야 한다.
                if (_game.StageSoulsDepleted)
                    FlashBanner("puzzle.vein.depleted", 2f);
            }

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

            if (_game != null)
            {
                _game.RemainingStageSoulsChanged -= OnRemainingSoulsChanged;
                _game = null;
            }
        }

        private void OnRemainingSoulsChanged(int remaining) => RefreshSouls();

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

        private void RefreshAll()
        {
            RefreshHealth();
            RefreshSouls();
            RefreshTurns();
            RefreshStats();
            bannerText.text = _bannerKey == null ? string.Empty : Loc.GetText(_bannerKey);
        }

        private void RefreshHealth()
        {
            int current = _session.Health.Current;
            int max = _session.Health.Max;

            // 하단 바가 체력의 주인이 된 뒤로 상단 숫자는 중복이다 — 지우거나 비워 둬도 되게 null 허용.
            if (hpText != null)
                hpText.text = Loc.GetText("puzzle.hp", current, max);

            // 하단 바 — 성난 몬스터에게 맞았다는 걸 숫자가 아니라 길이로 읽게 한다.
            if (playerHpFill != null)
                playerHpFill.fillAmount = max > 0 ? (float)current / max : 0f;
            if (playerHpText != null)
                playerHpText.text = Loc.GetText("combat.hp", current, max);
        }

        private void RefreshSouls()
        {
            PlayerStats stats = _session.Stats;
            string text = Loc.GetText(
                "puzzle.souls", stats.Level, stats.Souls, stats.SoulsToNextLevel, stats.PendingPoints);

            // 광맥이 있는 스테이지만 한 줄 덧붙인다 — 매장량을 안 정한 판에서 "남은 넋 ∞"를 띄우면 소음이다.
            if (_game != null && _game.HasSoulBudget)
                text += "\n" + (_game.StageSoulsDepleted
                    ? Loc.GetText("puzzle.vein.depleted")
                    : Loc.GetText("puzzle.vein.remaining", _game.RemainingStageSouls));

            soulsText.text = text;
            RefreshStats();
        }

        private void RefreshTurns()
        {
            string text = Loc.GetText("puzzle.turns", _engine.TurnsRemaining);

            // 전용 자리가 없을 때만 턴 밑에 작게 붙인다 — 배선을 덜 했다고 시계가 사라지면 안 된다.
            if (bossTimerText == null && _bossTimerSeconds >= 0f)
                text += "\n" + Loc.GetText("puzzle.bossTimer", Mathf.CeilToInt(_bossTimerSeconds));

            turnsText.text = text;
            RefreshBossTimer();
        }

        private void RefreshBossTimer()
        {
            if (bossTimerText == null)
                return;

            if (_bossTimerSeconds < 0f)
            {
                bossTimerText.text = string.Empty;
                return;
            }

            bossTimerText.text = Loc.GetText("puzzle.bossTimer", Mathf.CeilToInt(_bossTimerSeconds));
            bossTimerText.color = _bossTimerSeconds <= bossTimerWarnSeconds ? bossTimerWarnColor : bossTimerColor;
        }

        /// <summary>
        /// 보스전까지 남은 초 — <see cref="Core.Intrusion.IntrusionSystem.EngageTimerChanged"/>가 매 프레임 부른다.
        /// 초 단위로 바뀔 때만 다시 그린다(매 프레임 문자열을 새로 만들면 GC가 튄다).
        /// </summary>
        public void SetBossTimer(float secondsRemaining)
        {
            int whole = Mathf.CeilToInt(Mathf.Max(0f, secondsRemaining));
            if (_bossTimerSeconds >= 0f && Mathf.CeilToInt(_bossTimerSeconds) == whole)
            {
                _bossTimerSeconds = secondsRemaining;
                return;
            }

            _bossTimerSeconds = secondsRemaining;
            if (_engine != null)
                RefreshTurns();
        }

        private void RefreshStats()
        {
            PlayerStats stats = _session.Stats;
            statsText.text = Loc.GetText(
                "puzzle.stats", stats.AttackDamage, stats.DamageReduction, stats.ParryWindowSeconds);
        }
    }
}
