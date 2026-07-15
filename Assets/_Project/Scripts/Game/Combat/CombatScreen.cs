using System.Collections;
using ChainRiposte.Core.Combat;
using ChainRiposte.Core.Flow;
using UnityEngine;
using UnityEngine.UI;

namespace ChainRiposte.Game.Combat
{
    /// <summary>
    /// 전투 프레젠테이션 — 코드로 uGUI를 조립한다 (에셋 단계에서 교체 예정).
    /// CombatSystem의 이벤트만 구독해 연출하며 모델을 절대 수정하지 않는다.
    /// 핵심 타이밍 큐: 텔레그래프 링이 보스 크기까지 줄어드는 순간 = 타격 시점.
    /// </summary>
    public sealed class CombatScreen : MonoBehaviour
    {
        [SerializeField] private CombatInput input;

        [Header("연출 색")]
        [SerializeField] private Color parryableRingColor = new(0.95f, 0.85f, 0.45f);
        [SerializeField] private Color unparryableRingColor = new(0.75f, 0.15f, 0.55f);
        [SerializeField] private Color bossColor = new(0.62f, 0.08f, 0.12f);

        private RectTransform _root;          // 전투 동안만 활성화되는 화면 루트
        private Text _bossNameText;
        private Image _bossHpFill;
        private Image _postureFill;
        private Image _playerHpFill;
        private Text _playerHpText;
        private RectTransform _bossBody;
        private Image _bossBodyImage;
        private RectTransform _telegraphRing;
        private Image _telegraphRingImage;
        private Text _popupText;
        private Text _executeText;
        private Image _flashOverlay;
        private Text _introText;
        private Button _parryButton;
        private Button _attackButton;
        private Image _parryButtonImage;
        private Image _attackButtonImage;

        private CombatSystem _combat;
        private GameSession _session;
        private Coroutine _telegraphRoutine;
        private Coroutine _executePulseRoutine;

        private static readonly Color ParryButtonColor = new(0.18f, 0.28f, 0.42f, 0.95f);
        private static readonly Color AttackButtonColor = new(0.42f, 0.14f, 0.16f, 0.95f);
        private static readonly Color ButtonDisabledColor = new(0.15f, 0.14f, 0.17f, 0.95f);
        private static readonly Color ExecuteButtonColor = new(0.85f, 0.15f, 0.18f, 1f);

        private void Awake()
        {
            BuildUi();
            _root.gameObject.SetActive(false);
        }

        private void OnDestroy() => Unbind();

        /// <summary>전투 돌입 시 컨트롤러가 호출한다.</summary>
        public void Bind(CombatSystem combat, GameSession session)
        {
            Unbind();
            _combat = combat;
            _session = session;

            combat.AttackTelegraphed += OnAttackTelegraphed;
            combat.AttackParried += OnAttackParried;
            combat.PlayerHit += OnPlayerHit;
            combat.PlayerAttackLanded += OnPlayerAttackLanded;
            combat.BossHpChanged += OnBossHpChanged;
            combat.PostureChanged += OnPostureChanged;
            combat.BossBroken += OnBossBroken;
            combat.ExecutionPerformed += OnExecutionPerformed;
            combat.PlayerStateChanged += OnPlayerStateChanged;
            session.Health.Changed += OnPlayerHealthChanged;

            _root.gameObject.SetActive(true);
            ResetVisuals();
            StartCoroutine(FadeOutIntro());
        }

        private void Unbind()
        {
            if (_combat != null)
            {
                _combat.AttackTelegraphed -= OnAttackTelegraphed;
                _combat.AttackParried -= OnAttackParried;
                _combat.PlayerHit -= OnPlayerHit;
                _combat.PlayerAttackLanded -= OnPlayerAttackLanded;
                _combat.BossHpChanged -= OnBossHpChanged;
                _combat.PostureChanged -= OnPostureChanged;
                _combat.BossBroken -= OnBossBroken;
                _combat.ExecutionPerformed -= OnExecutionPerformed;
                _combat.PlayerStateChanged -= OnPlayerStateChanged;
                _combat = null;
            }

            if (_session != null)
            {
                _session.Health.Changed -= OnPlayerHealthChanged;
                _session = null;
            }
        }

        private void ResetVisuals()
        {
            _bossNameText.text = "BOSS";
            _bossHpFill.fillAmount = 1f;
            _postureFill.fillAmount = _combat.Posture / _combat.MaxPosture;
            _bossHpFill.fillAmount = _combat.BossHp / _combat.BossMaxHp;
            OnPlayerHealthChanged(_session.Health.Current, _session.Health.Max);
            _telegraphRing.gameObject.SetActive(false);
            _popupText.text = string.Empty;
            _executeText.gameObject.SetActive(false);
            _flashOverlay.color = Color.clear;
            _introText.color = new Color(0.85f, 0.2f, 0.25f, 1f);
            _bossBody.localScale = Vector3.one;
            _bossBodyImage.color = bossColor;
            OnPlayerStateChanged(PlayerActionState.Ready);
        }

        // ── CombatSystem 이벤트 연출 ──

        private void OnAttackTelegraphed(int index, BossAttackConfig attack)
        {
            if (_telegraphRoutine != null)
                StopCoroutine(_telegraphRoutine);
            _telegraphRoutine = StartCoroutine(TelegraphRoutine(attack));
        }

        /// <summary>링이 보스 크기(×1)까지 수축하는 순간이 타격 시점 — 유일한 타이밍 큐.</summary>
        private IEnumerator TelegraphRoutine(BossAttackConfig attack)
        {
            _telegraphRing.gameObject.SetActive(true);
            _telegraphRingImage.color = attack.Parryable ? parryableRingColor : unparryableRingColor;

            float elapsed = 0f;
            while (elapsed < attack.TelegraphSeconds)
            {
                float t = elapsed / attack.TelegraphSeconds;
                _telegraphRing.localScale = Vector3.one * Mathf.Lerp(2.4f, 1f, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            _telegraphRing.gameObject.SetActive(false);
            _telegraphRoutine = null;
        }

        private void OnAttackParried(BossAttackConfig attack)
        {
            HideTelegraph();
            ShowPopup("PARRY!", new Color(0.95f, 0.9f, 0.5f));
            StartCoroutine(Flash(new Color(1f, 1f, 0.9f, 0.4f)));
            StartCoroutine(Punch(_bossBody, 0.85f));
        }

        private void OnPlayerHit(BossAttackConfig attack, int damage)
        {
            HideTelegraph();
            ShowPopup($"-{damage}", new Color(0.9f, 0.3f, 0.3f));
            StartCoroutine(Flash(new Color(0.8f, 0.05f, 0.05f, 0.45f)));
            StartCoroutine(Punch(_bossBody, 1.25f));
        }

        private void OnPlayerAttackLanded(float damage)
        {
            ShowPopup($"HIT {damage:0}", new Color(0.85f, 0.85f, 0.9f));
            StartCoroutine(Punch(_bossBody, 0.92f));
        }

        private void OnBossHpChanged(float current, float max) =>
            _bossHpFill.fillAmount = max > 0f ? current / max : 0f;

        private void OnPostureChanged(float current, float max) =>
            _postureFill.fillAmount = max > 0f ? current / max : 0f;

        private void OnBossBroken()
        {
            HideTelegraph();
            _executeText.gameObject.SetActive(true);
            _executePulseRoutine = StartCoroutine(PulseExecuteMark());
            RefreshButtons(_combat.PlayerState);
        }

        private void OnExecutionPerformed()
        {
            if (_executePulseRoutine != null)
                StopCoroutine(_executePulseRoutine);
            _executeText.gameObject.SetActive(false);
            StartCoroutine(Flash(new Color(1f, 1f, 1f, 0.9f)));
        }

        private void OnPlayerStateChanged(PlayerActionState state) => RefreshButtons(state);

        private void OnPlayerHealthChanged(int current, int max)
        {
            _playerHpFill.fillAmount = max > 0 ? (float)current / max : 0f;
            _playerHpText.text = $"HP {current}/{max}";
        }

        private void HideTelegraph()
        {
            if (_telegraphRoutine != null)
            {
                StopCoroutine(_telegraphRoutine);
                _telegraphRoutine = null;
            }
            _telegraphRing.gameObject.SetActive(false);
        }

        private void RefreshButtons(PlayerActionState state)
        {
            if (_combat != null && _combat.ExecutionReady)
            {
                SetButtonVisual(_parryButton, _parryButtonImage, ButtonDisabledColor, "PARRY\n[<-]", false);
                SetButtonVisual(_attackButton, _attackButtonImage, ExecuteButtonColor, "EXECUTE!\n[->]", true);
                return;
            }

            bool ready = state == PlayerActionState.Ready;
            SetButtonVisual(_parryButton, _parryButtonImage,
                state == PlayerActionState.Parrying ? parryableRingColor : (ready ? ParryButtonColor : ButtonDisabledColor),
                "PARRY\n[<-]", ready);
            SetButtonVisual(_attackButton, _attackButtonImage,
                state == PlayerActionState.Attacking ? AttackButtonColor : (ready ? AttackButtonColor : ButtonDisabledColor),
                "ATTACK\n[->]", ready);
        }

        private static void SetButtonVisual(Button button, Image image, Color color, string label, bool interactable)
        {
            image.color = color;
            button.interactable = interactable;
            button.GetComponentInChildren<Text>().text = label;
        }

        // ── 코루틴 연출 ──

        private void ShowPopup(string message, Color color)
        {
            StopCoroutine(nameof(PopupRoutine));
            _popupText.text = message;
            _popupText.color = color;
            StartCoroutine(nameof(PopupRoutine));
        }

        private IEnumerator PopupRoutine()
        {
            Color baseColor = _popupText.color;
            var rect = (RectTransform)_popupText.transform;
            Vector2 origin = new(0f, 560f);
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                rect.anchoredPosition = origin + Vector2.up * (60f * (t / 0.6f));
                _popupText.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f - t / 0.6f);
                yield return null;
            }
            _popupText.text = string.Empty;
        }

        private IEnumerator Flash(Color color)
        {
            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                _flashOverlay.color = Color.Lerp(color, Color.clear, t / 0.25f);
                yield return null;
            }
            _flashOverlay.color = Color.clear;
        }

        private IEnumerator Punch(RectTransform target, float scale)
        {
            for (float t = 0f; t < 0.18f; t += Time.deltaTime)
            {
                target.localScale = Vector3.one * Mathf.Lerp(scale, 1f, t / 0.18f);
                yield return null;
            }
            target.localScale = Vector3.one;
        }

        private IEnumerator PulseExecuteMark()
        {
            while (true)
            {
                float alpha = 0.6f + 0.4f * Mathf.PingPong(Time.time * 3f, 1f);
                _executeText.color = new Color(0.9f, 0.12f, 0.15f, alpha);
                yield return null;
            }
        }

        private IEnumerator FadeOutIntro()
        {
            yield return new WaitForSeconds(0.9f);
            Color baseColor = _introText.color;
            for (float t = 0f; t < 0.4f; t += Time.deltaTime)
            {
                _introText.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f - t / 0.4f);
                yield return null;
            }
            _introText.color = Color.clear;
        }

        // ── UI 조립 ──

        private static Font BuiltinFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private void BuildUi()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10; // 퍼즐 HUD 위, 결과 화면 아래
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            Image rootBackground = CreateStretched("Root", transform);
            rootBackground.color = new Color(0.07f, 0.06f, 0.09f, 1f); // 퍼즐 화면을 완전히 덮는 배경
            _root = rootBackground.rectTransform;

            _bossNameText = CreateText(_root, "BossName", new Vector2(0f, -90f), new Vector2(0.5f, 1f), 60, TextAnchor.MiddleCenter);
            _bossNameText.fontStyle = FontStyle.Bold;

            _bossHpFill = CreateBar(_root, "BossHpBar", new Vector2(0f, -170f), new Vector2(0.5f, 1f),
                new Vector2(900f, 26f), new Color(0.55f, 0.10f, 0.12f));
            _postureFill = CreateBar(_root, "PostureBar", new Vector2(0f, -225f), new Vector2(0.5f, 1f),
                new Vector2(900f, 46f), new Color(0.95f, 0.62f, 0.12f));
            Text postureLabel = CreateText(_root, "PostureLabel", new Vector2(0f, -280f), new Vector2(0.5f, 1f), 32, TextAnchor.MiddleCenter);
            postureLabel.text = "POSTURE";
            postureLabel.color = new Color(0.95f, 0.62f, 0.12f);

            // 텔레그래프 링 (보스 뒤)
            var ringGo = new GameObject("TelegraphRing");
            ringGo.transform.SetParent(_root, false);
            _telegraphRing = ringGo.AddComponent<RectTransform>();
            _telegraphRing.anchorMin = _telegraphRing.anchorMax = new Vector2(0.5f, 0.5f);
            _telegraphRing.anchoredPosition = new Vector2(0f, 200f);
            _telegraphRing.sizeDelta = new Vector2(400f, 400f);
            _telegraphRingImage = ringGo.AddComponent<Image>();
            _telegraphRingImage.sprite = PlaceholderSprite.Square;
            _telegraphRingImage.raycastTarget = false;

            // 보스 본체 (플레이스홀더 사각형)
            var bossGo = new GameObject("BossBody");
            bossGo.transform.SetParent(_root, false);
            _bossBody = bossGo.AddComponent<RectTransform>();
            _bossBody.anchorMin = _bossBody.anchorMax = new Vector2(0.5f, 0.5f);
            _bossBody.anchoredPosition = new Vector2(0f, 200f);
            _bossBody.sizeDelta = new Vector2(340f, 340f);
            _bossBodyImage = bossGo.AddComponent<Image>();
            _bossBodyImage.sprite = PlaceholderSprite.Square;
            _bossBodyImage.color = bossColor;
            _bossBodyImage.raycastTarget = false;

            _executeText = CreateText(_root, "ExecuteMark", new Vector2(0f, 470f), new Vector2(0.5f, 0.5f), 96, TextAnchor.MiddleCenter);
            _executeText.text = "EXECUTE!!";
            _executeText.fontStyle = FontStyle.Bold;

            _popupText = CreateText(_root, "Popup", new Vector2(0f, 560f), new Vector2(0.5f, 0.5f), 72, TextAnchor.MiddleCenter);
            _popupText.fontStyle = FontStyle.Bold;

            _playerHpFill = CreateBar(_root, "PlayerHpBar", new Vector2(0f, 430f), new Vector2(0.5f, 0f),
                new Vector2(900f, 40f), new Color(0.25f, 0.62f, 0.30f));
            _playerHpText = CreateText(_root, "PlayerHpText", new Vector2(0f, 490f), new Vector2(0.5f, 0f), 40, TextAnchor.MiddleCenter);

            (_parryButton, _parryButtonImage) = CreateActionButton("ParryButton", -270f, () => input.PressParry());
            (_attackButton, _attackButtonImage) = CreateActionButton("AttackButton", 270f, () => input.PressAttack());

            var flashGo = CreateStretched("FlashOverlay", _root);
            flashGo.color = Color.clear;
            flashGo.raycastTarget = false;
            _flashOverlay = flashGo;

            _introText = CreateText(_root, "Intro", new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), 130, TextAnchor.MiddleCenter);
            _introText.text = "BOSS BATTLE";
            _introText.fontStyle = FontStyle.Bold;
        }

        private static Image CreateStretched(string objName, Transform parent)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return go.AddComponent<Image>();
        }

        private static Text CreateText(Transform parent, string objName, Vector2 pos, Vector2 anchor, int size, TextAnchor align)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(1000f, 140f);

            var text = go.AddComponent<Text>();
            text.font = BuiltinFont;
            text.fontSize = size;
            text.alignment = align;
            text.color = new Color(0.92f, 0.90f, 0.85f);
            text.raycastTarget = false;
            return text;
        }

        /// <summary>배경 + Filled 타입 게이지. 반환값은 fillAmount로 갱신하는 채움 이미지.</summary>
        private static Image CreateBar(Transform parent, string objName, Vector2 pos, Vector2 anchor, Vector2 size, Color fillColor)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var bg = go.AddComponent<Image>();
            bg.sprite = PlaceholderSprite.Square;
            bg.color = new Color(0.05f, 0.05f, 0.07f, 0.9f);
            bg.raycastTarget = false;

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(go.transform, false);
            var fillRect = fillGo.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(4f, 4f);
            fillRect.offsetMax = new Vector2(-4f, -4f);
            var fill = fillGo.AddComponent<Image>();
            fill.sprite = PlaceholderSprite.Square;
            fill.color = fillColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.raycastTarget = false;
            return fill;
        }

        private (Button, Image) CreateActionButton(string objName, float x, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(_root, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(x, 60f);
            rect.sizeDelta = new Vector2(440f, 300f);

            var image = go.AddComponent<Image>();
            image.sprite = PlaceholderSprite.Square;

            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None; // 색은 상태 연출이 직접 관리
            button.onClick.AddListener(onClick);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

            var label = labelGo.AddComponent<Text>();
            label.font = BuiltinFont;
            label.fontSize = 52;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.92f, 0.90f, 0.85f);
            label.raycastTarget = false;

            return (button, image);
        }
    }
}
