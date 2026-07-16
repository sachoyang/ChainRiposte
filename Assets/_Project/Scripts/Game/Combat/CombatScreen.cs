using System.Collections;
using ChainRiposte.Core.Combat;
using ChainRiposte.Core.Flow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChainRiposte.Game.Combat
{
    /// <summary>
    /// 전투 프레젠테이션. UI는 <b>씬에 실물로 배치</b>(TMP)하고 이 컴포넌트는 참조만 받아 연출만 한다.
    /// 초기 레이아웃은 <c>Tools ▸ ChainRiposte ▸ Build Main Scene UI</c>로 생성 후 씬에서 편집.
    /// CombatSystem 이벤트만 구독해 연출하며 모델을 절대 수정하지 않는다.
    /// 핵심 타이밍 큐: 텔레그래프 링이 보스 크기까지 줄어드는 순간 = 타격 시점.
    /// </summary>
    public sealed class CombatScreen : MonoBehaviour
    {
        [SerializeField] private CombatInput input;

        [Header("연출 색")]
        [SerializeField] private Color parryableRingColor = new(0.95f, 0.85f, 0.45f);
        [SerializeField] private Color unparryableRingColor = new(0.75f, 0.15f, 0.55f);
        [SerializeField] private Color bossColor = new(0.62f, 0.08f, 0.12f);

        [Header("씬 참조 (빌더가 자동 배선)")]
        [Tooltip("전투 동안만 활성화되는 화면 루트")]
        [SerializeField] private RectTransform root;
        [SerializeField] private TMP_Text bossNameText;
        [SerializeField] private Image bossHpFill;
        [SerializeField] private Image postureFill;
        [SerializeField] private Image playerHpFill;
        [SerializeField] private TMP_Text playerHpText;
        [SerializeField] private RectTransform bossBody;
        [SerializeField] private Image bossBodyImage;
        [SerializeField] private RectTransform telegraphRing;
        [SerializeField] private Image telegraphRingImage;
        [SerializeField] private TMP_Text popupText;
        [SerializeField] private TMP_Text executeText;
        [SerializeField] private Image flashOverlay;
        [SerializeField] private TMP_Text introText;
        [SerializeField] private Button parryButton;
        [SerializeField] private Button attackButton;
        [SerializeField] private Image parryButtonImage;
        [SerializeField] private Image attackButtonImage;

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
            if (root == null || parryButton == null || attackButton == null)
            {
                Debug.LogError($"{nameof(CombatScreen)}: UI 참조가 비어 있습니다. " +
                    "Tools ▸ ChainRiposte ▸ Build Main Scene UI 를 실행하세요.", this);
                enabled = false;
                return;
            }

            parryButton.onClick.AddListener(() => input.PressParry());
            attackButton.onClick.AddListener(() => input.PressAttack());
            root.gameObject.SetActive(false);
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

            root.gameObject.SetActive(true);
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
            bossNameText.text = "BOSS";
            postureFill.fillAmount = _combat.Posture / _combat.MaxPosture;
            bossHpFill.fillAmount = _combat.BossHp / _combat.BossMaxHp;
            OnPlayerHealthChanged(_session.Health.Current, _session.Health.Max);
            telegraphRing.gameObject.SetActive(false);
            popupText.text = string.Empty;
            executeText.gameObject.SetActive(false);
            flashOverlay.color = Color.clear;
            introText.color = new Color(0.85f, 0.2f, 0.25f, 1f);
            bossBody.localScale = Vector3.one;
            bossBodyImage.color = bossColor;
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
            telegraphRing.gameObject.SetActive(true);
            telegraphRingImage.color = attack.Parryable ? parryableRingColor : unparryableRingColor;

            float elapsed = 0f;
            while (elapsed < attack.TelegraphSeconds)
            {
                float t = elapsed / attack.TelegraphSeconds;
                telegraphRing.localScale = Vector3.one * Mathf.Lerp(2.4f, 1f, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            telegraphRing.gameObject.SetActive(false);
            _telegraphRoutine = null;
        }

        private void OnAttackParried(BossAttackConfig attack)
        {
            HideTelegraph();
            ShowPopup("PARRY!", new Color(0.95f, 0.9f, 0.5f));
            StartCoroutine(Flash(new Color(1f, 1f, 0.9f, 0.4f)));
            StartCoroutine(Punch(bossBody, 0.85f));
        }

        private void OnPlayerHit(BossAttackConfig attack, int damage)
        {
            HideTelegraph();
            ShowPopup($"-{damage}", new Color(0.9f, 0.3f, 0.3f));
            StartCoroutine(Flash(new Color(0.8f, 0.05f, 0.05f, 0.45f)));
            StartCoroutine(Punch(bossBody, 1.25f));
        }

        private void OnPlayerAttackLanded(float damage)
        {
            ShowPopup($"HIT {damage:0}", new Color(0.85f, 0.85f, 0.9f));
            StartCoroutine(Punch(bossBody, 0.92f));
        }

        private void OnBossHpChanged(float current, float max) =>
            bossHpFill.fillAmount = max > 0f ? current / max : 0f;

        private void OnPostureChanged(float current, float max) =>
            postureFill.fillAmount = max > 0f ? current / max : 0f;

        private void OnBossBroken()
        {
            HideTelegraph();
            executeText.gameObject.SetActive(true);
            _executePulseRoutine = StartCoroutine(PulseExecuteMark());
            RefreshButtons(_combat.PlayerState);
        }

        private void OnExecutionPerformed()
        {
            if (_executePulseRoutine != null)
                StopCoroutine(_executePulseRoutine);
            executeText.gameObject.SetActive(false);
            StartCoroutine(Flash(new Color(1f, 1f, 1f, 0.9f)));
        }

        private void OnPlayerStateChanged(PlayerActionState state) => RefreshButtons(state);

        private void OnPlayerHealthChanged(int current, int max)
        {
            playerHpFill.fillAmount = max > 0 ? (float)current / max : 0f;
            playerHpText.text = $"HP {current}/{max}";
        }

        private void HideTelegraph()
        {
            if (_telegraphRoutine != null)
            {
                StopCoroutine(_telegraphRoutine);
                _telegraphRoutine = null;
            }
            telegraphRing.gameObject.SetActive(false);
        }

        private void RefreshButtons(PlayerActionState state)
        {
            if (_combat != null && _combat.ExecutionReady)
            {
                SetButtonVisual(parryButton, parryButtonImage, ButtonDisabledColor, "PARRY\n[<-]", false);
                SetButtonVisual(attackButton, attackButtonImage, ExecuteButtonColor, "EXECUTE!\n[->]", true);
                return;
            }

            bool ready = state == PlayerActionState.Ready;
            SetButtonVisual(parryButton, parryButtonImage,
                state == PlayerActionState.Parrying ? parryableRingColor : (ready ? ParryButtonColor : ButtonDisabledColor),
                "PARRY\n[<-]", ready);
            SetButtonVisual(attackButton, attackButtonImage,
                state == PlayerActionState.Attacking ? AttackButtonColor : (ready ? AttackButtonColor : ButtonDisabledColor),
                "ATTACK\n[->]", ready);
        }

        private static void SetButtonVisual(Button button, Image image, Color color, string label, bool interactable)
        {
            image.color = color;
            button.interactable = interactable;
            button.GetComponentInChildren<TMP_Text>().text = label;
        }

        // ── 코루틴 연출 ──

        private void ShowPopup(string message, Color color)
        {
            StopCoroutine(nameof(PopupRoutine));
            popupText.text = message;
            popupText.color = color;
            StartCoroutine(nameof(PopupRoutine));
        }

        private IEnumerator PopupRoutine()
        {
            Color baseColor = popupText.color;
            var rect = (RectTransform)popupText.transform;
            Vector2 origin = new(0f, 560f);
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                rect.anchoredPosition = origin + Vector2.up * (60f * (t / 0.6f));
                popupText.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f - t / 0.6f);
                yield return null;
            }
            popupText.text = string.Empty;
        }

        private IEnumerator Flash(Color color)
        {
            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                flashOverlay.color = Color.Lerp(color, Color.clear, t / 0.25f);
                yield return null;
            }
            flashOverlay.color = Color.clear;
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
                executeText.color = new Color(0.9f, 0.12f, 0.15f, alpha);
                yield return null;
            }
        }

        private IEnumerator FadeOutIntro()
        {
            yield return new WaitForSeconds(0.9f);
            Color baseColor = introText.color;
            for (float t = 0f; t < 0.4f; t += Time.deltaTime)
            {
                introText.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f - t / 0.4f);
                yield return null;
            }
            introText.color = Color.clear;
        }
    }
}
