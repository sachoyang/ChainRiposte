using ChainRiposte.Core.Flow;
using ChainRiposte.Core.Match;
using ChainRiposte.Core.Stats;
using UnityEngine;
using UnityEngine.UI;

namespace ChainRiposte.Game.UI
{
    /// <summary>
    /// 프로토타입 HUD — 코드로 uGUI를 조립한다 (에셋 단계에서 프리팹 UI로 교체 예정).
    /// Core 이벤트 구독으로만 갱신되고, 게임 상태 변경은 스탯 분배 버튼 하나뿐이다.
    /// 폰트 이슈를 피하기 위해 표기는 영문 약어를 쓴다.
    /// </summary>
    public sealed class PuzzleHud : MonoBehaviour
    {
        private Text _hpText;
        private Text _soulsText;
        private Text _turnsText;
        private Text _statsText;
        private Text _bannerText;
        private Button _attackButton;
        private Button _defenseButton;
        private Button _parryButton;

        private GameSession _session;
        private PuzzleEngine _engine;

        private void Awake() => BuildUi();

        private void OnDestroy() => Unbind();

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
            _bannerText.text = next switch
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
            _bannerText.text = string.Empty;
        }

        private void RefreshHealth() =>
            _hpText.text = $"HP {_session.Health.Current}/{_session.Health.Max}";

        private void RefreshSouls()
        {
            PlayerStats stats = _session.Stats;
            _soulsText.text = $"Lv {stats.Level}   Souls {stats.Souls}/{stats.SoulsToNextLevel}   Points {stats.PendingPoints}";
            RefreshStats();
        }

        private void RefreshTurns() =>
            _turnsText.text = $"Turns {_engine.TurnsRemaining}";

        private void RefreshStats()
        {
            PlayerStats stats = _session.Stats;
            _statsText.text =
                $"ATK {stats.AttackDamage:0}   DEF {stats.DamageReduction:0}   PARRY {stats.ParryWindowSeconds:0.00}s";

            SetButton(_attackButton, $"+ATK\nLv {stats.GetStatLevel(StatType.Attack)}", stats.CanAllocate(StatType.Attack));
            SetButton(_defenseButton, $"+DEF\nLv {stats.GetStatLevel(StatType.Defense)}", stats.CanAllocate(StatType.Defense));
            bool parryCapped = stats.PendingPoints > 0 && !stats.CanAllocate(StatType.Parry);
            SetButton(_parryButton,
                parryCapped ? "+PARRY\nMAX" : $"+PARRY\nLv {stats.GetStatLevel(StatType.Parry)}",
                stats.CanAllocate(StatType.Parry));
        }

        private static void SetButton(Button button, string label, bool interactable)
        {
            button.interactable = interactable;
            button.GetComponentInChildren<Text>().text = label;
        }

        // ── UI 조립 ──

        private void BuildUi()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            _hpText = CreateText("HpText", new Vector2(40f, -40f), 52, TextAnchor.UpperLeft);
            _soulsText = CreateText("SoulsText", new Vector2(40f, -110f), 44, TextAnchor.UpperLeft);
            _turnsText = CreateText("TurnsText", new Vector2(40f, -170f), 44, TextAnchor.UpperLeft);
            _statsText = CreateText("StatsText", new Vector2(40f, -230f), 44, TextAnchor.UpperLeft);
            _bannerText = CreateBanner();

            _attackButton = CreateButton("AllocAttack", -330f, () => RequestAllocate(StatType.Attack));
            _defenseButton = CreateButton("AllocDefense", 0f, () => RequestAllocate(StatType.Defense));
            _parryButton = CreateButton("AllocParry", 330f, () => RequestAllocate(StatType.Parry));
        }

        private static Font BuiltinFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private Text CreateText(string objName, Vector2 anchoredPos, int size, TextAnchor anchor)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(1000f, 60f);

            var text = go.AddComponent<Text>();
            text.font = BuiltinFont;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = new Color(0.92f, 0.90f, 0.85f);
            text.raycastTarget = false;
            return text;
        }

        private Text CreateBanner()
        {
            var go = new GameObject("Banner");
            go.transform.SetParent(transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1000f, 240f);

            var text = go.AddComponent<Text>();
            text.font = BuiltinFont;
            text.fontSize = 130;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.85f, 0.2f, 0.25f);
            text.raycastTarget = false;
            return text;
        }

        private Button CreateButton(string objName, float x, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(x, 60f);
            rect.sizeDelta = new Vector2(300f, 150f);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.22f, 0.20f, 0.26f, 0.95f);

            var button = go.AddComponent<Button>();
            button.onClick.AddListener(onClick);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

            var label = labelGo.AddComponent<Text>();
            label.font = BuiltinFont;
            label.fontSize = 40;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.92f, 0.90f, 0.85f);
            label.raycastTarget = false;

            return button;
        }
    }
}
