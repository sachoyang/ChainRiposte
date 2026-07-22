using System.Collections;
using System.Collections.Generic;
using ChainRiposte.Core.Combat;
using ChainRiposte.Core.Flow;
using ChainRiposte.Game.Localization;
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
        [SerializeField] private Color bossColor = new(0.62f, 0.08f, 0.12f);

        [Header("패링 타이밍 원")]
        [Tooltip("다가오는 노트를 나타내는 원의 색")]
        [SerializeField] private Color noteRingColor = Color.white;
        [Tooltip("패링 가능 구간을 나타내는 띠의 색 — 연한 회색")]
        [SerializeField] private Color parryBandColor = new(1f, 1f, 1f, 0.22f);
        [Tooltip("원이 다가오는 속도 (초당 스케일). 모든 노트가 같은 속도로 와야 회색 띠가 의미를 갖는다.")]
        [SerializeField, Min(0.05f)] private float approachSpeed = 0.9f;
        [Tooltip("이 스케일보다 멀리 있는 노트는 아직 그리지 않는다")]
        [SerializeField, Min(1.1f)] private float maxVisibleScale = 3.2f;
        [Tooltip("가장 멀리 있는 원의 투명도 — 임박할수록 진해진다")]
        [SerializeField, Range(0f, 1f)] private float farthestRingAlpha = 0.2f;

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
        [Tooltip("플레이어 본체 (왼쪽 아래). 패링 원이 여기로 모인다.")]
        [SerializeField] private RectTransform playerBody;
        [SerializeField] private Image playerBodyImage;
        [Tooltip("플레이어 그림. 비우면 색 사각형 플레이스홀더.")]
        [SerializeField] private Sprite playerSprite;
        [SerializeField] private Color playerColor = new(0.35f, 0.55f, 0.75f);

        [Header("등장 연출 (포켓몬식 대치)")]
        [Tooltip("각자 자기 쪽 화면 밖에서 미끄러져 들어오는 시간")]
        [SerializeField, Min(0.05f)] private float entranceSeconds = 0.7f;
        [Tooltip("화면 밖 시작 위치까지의 가로 거리")]
        [SerializeField, Min(100f)] private float entranceOffsetX = 900f;
        [Tooltip("공격 시 보스 쪽으로 찔러 들어가는 거리")]
        [SerializeField, Min(0f)] private float lungeDistance = 120f;
        [SerializeField, Min(0.05f)] private float lungeSeconds = 0.3f;
        [Tooltip("노트 원의 복제 원본. 개수가 채보로 정해지므로 이것만 씬에 두고 필요한 만큼 복제한다.")]
        [SerializeField] private RectTransform noteRingTemplate;
        [Tooltip("패링 가능 구간 띠. 두께가 PARRY 스탯에 따라 굵어진다.")]
        [SerializeField] private RectTransform parryBand;
        [SerializeField] private Image parryBandImage;
        [SerializeField] private TMP_Text popupText;
        [SerializeField] private TMP_Text executeText;
        [SerializeField] private Image flashOverlay;
        [SerializeField] private TMP_Text introText;
        [SerializeField] private Button parryButton;
        [SerializeField] private Button attackButton;
        [SerializeField] private Image parryButtonImage;
        [SerializeField] private Image attackButtonImage;

        private readonly List<RectTransform> _rings = new();
        private readonly List<Image> _ringImages = new();
        private Coroutine _entranceRoutine;

        private CombatSystem _combat;
        private GameSession _session;
        private Sprite _bossSprite;
        private string _bossName;
        private Coroutine _executePulseRoutine;
        private Vector2 _popupOrigin;
        private bool _popupPlaying;

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

        /// <summary>
        /// 전투에 설 보스 이미지를 지정한다. 보스 <b>타일</b>은 종류와 무관하게 하나로 통일하고
        /// (플레이어가 "이게 보스 타일이다"를 한눈에 알아야 한다), 실제 생김새는 이 화면에서만 다르다.
        /// </summary>
        public void SetBossVisual(Sprite sprite, string displayName)
        {
            _bossSprite = sprite;
            _bossName = displayName;
        }

        /// <summary>전투 돌입 시 컨트롤러가 호출한다.</summary>
        public void Bind(CombatSystem combat, GameSession session)
        {
            Unbind();
            _combat = combat;
            _session = session;

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
            bossNameText.text = string.IsNullOrWhiteSpace(_bossName) ? Loc.GetText("combat.boss") : _bossName;
            postureFill.fillAmount = _combat.Posture / _combat.MaxPosture;
            bossHpFill.fillAmount = _combat.BossHp / _combat.BossMaxHp;
            OnPlayerHealthChanged(_session.Health.Current, _session.Health.Max);
            HideTelegraph();
            noteRingTemplate.gameObject.SetActive(false); // 복제 원본은 항상 꺼 둔다
            popupText.text = string.Empty;
            executeText.gameObject.SetActive(false);
            flashOverlay.color = Color.clear;
            introText.color = new Color(0.85f, 0.2f, 0.25f, 1f);
            bossBody.localScale = Vector3.one;

            // 스프라이트가 지정되면 원색 그대로(흰 틴트), 없으면 기존 색 사각형 플레이스홀더
            bool hasSprite = _bossSprite != null;
            if (hasSprite)
            {
                bossBodyImage.sprite = _bossSprite;
                bossBodyImage.preserveAspect = true;
            }

            bossBodyImage.color = hasSprite ? Color.white : bossColor;

            if (playerBody != null)
            {
                playerBody.localScale = Vector3.one;
                if (playerSprite != null)
                {
                    playerBodyImage.sprite = playerSprite;
                    playerBodyImage.preserveAspect = true;
                }

                playerBodyImage.color = playerSprite != null ? Color.white : playerColor;
            }

            StartEntrance();
            OnPlayerStateChanged(PlayerActionState.Ready);
        }

        // ── CombatSystem 이벤트 연출 ──

        /// <summary>
        /// 다가오는 노트를 흰 원으로 그린다. 원이 보스 크기(×1)에 닿는 순간이 타격 시점이다.
        ///
        /// 원은 <b>모두 같은 속도로</b> 다가온다(진행률이 아니라 남은 시간 × 속도로 크기를 정함).
        /// 그래야 "보스에서 이만큼 떨어진 거리 = 이만큼의 시간"이 항상 같고,
        /// 패링 구간을 두께가 고정된 회색 띠 하나로 표현할 수 있다.
        /// 예비동작이 긴 노트는 그만큼 더 멀리서부터 보인다.
        /// </summary>
        private void Update()
        {
            if (_combat == null || _combat.Finished)
            {
                SetRingCount(0);
                return;
            }

            UpdateParryBand();

            IReadOnlyList<ActiveNote> notes = _combat.ActiveNotes;
            int drawn = 0;

            for (int i = 0; i < notes.Count; i++)
            {
                float scale = 1f + notes[i].SecondsUntilHit * approachSpeed;
                if (scale > maxVisibleScale)
                    continue; // 아직 멀다 — 화면 밖

                RectTransform ring = GetRing(drawn);
                ring.localScale = Vector3.one * scale;

                // 임박할수록 진하게 — 어느 것을 먼저 쳐야 하는지가 한눈에 읽힌다
                float nearness = Mathf.InverseLerp(maxVisibleScale, 1f, scale);
                Color color = noteRingColor;
                color.a *= Mathf.Lerp(farthestRingAlpha, 1f, nearness);
                _ringImages[drawn].color = color;

                drawn++;
            }

            SetRingCount(drawn);
        }

        /// <summary>패링 구간 띠 — 두께가 곧 PARRY 스탯이다. 스탯을 올리면 눈에 보이게 굵어진다.</summary>
        private void UpdateParryBand()
        {
            if (parryBand == null)
                return;

            bool visible = _combat.ActiveNotes.Count > 0;
            if (parryBand.gameObject.activeSelf != visible)
                parryBand.gameObject.SetActive(visible);
            if (!visible)
                return;

            // 띠의 바깥 지름 = 패링 윈도우 동안 원이 지나가는 거리
            float outerScale = 1f + _combat.ParryWindowSeconds * approachSpeed;
            parryBand.localScale = Vector3.one * outerScale;

            if (parryBandImage != null)
                parryBandImage.color = parryBandColor;
        }

        private RectTransform GetRing(int index)
        {
            while (_rings.Count <= index)
            {
                RectTransform ring = Instantiate(noteRingTemplate, noteRingTemplate.parent);
                ring.gameObject.name = $"NoteRing_{_rings.Count}";
                _rings.Add(ring);
                _ringImages.Add(ring.GetComponent<Image>());
            }

            RectTransform result = _rings[index];
            if (!result.gameObject.activeSelf)
                result.gameObject.SetActive(true);
            return result;
        }

        private void SetRingCount(int active)
        {
            for (int i = active; i < _rings.Count; i++)
                if (_rings[i].gameObject.activeSelf)
                    _rings[i].gameObject.SetActive(false);
        }

        private void OnAttackParried(BossNoteConfig note)
        {
            HideTelegraph();
            ShowPopup(Loc.GetText("combat.popup.parry"), new Color(0.95f, 0.9f, 0.5f));
            StartCoroutine(Flash(new Color(1f, 1f, 0.9f, 0.4f)));
            StartCoroutine(Punch(bossBody, 0.85f));
            // 쳐낸 쪽도 반응해야 '내가 막았다'가 읽힌다
            if (playerBody != null)
                StartCoroutine(Punch(playerBody, 1.12f));
        }

        private void OnPlayerHit(BossNoteConfig note, int damage)
        {
            HideTelegraph();
            ShowPopup($"-{damage}", new Color(0.9f, 0.3f, 0.3f));
            StartCoroutine(Flash(new Color(0.8f, 0.05f, 0.05f, 0.45f)));
            if (playerBody != null)
                StartCoroutine(Punch(playerBody, 1.3f));
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

        private void OnPlayerStateChanged(PlayerActionState state)
        {
            RefreshButtons(state);

            // 커밋이 시작되는 순간 찔러 들어간다 — 피해가 들어오는 커밋 종료 시점에 맞추면 늦어 보인다
            if (state == PlayerActionState.Attacking)
                StartCoroutine(Lunge());
        }

        private void OnPlayerHealthChanged(int current, int max)
        {
            playerHpFill.fillAmount = max > 0 ? (float)current / max : 0f;
            playerHpText.text = Loc.GetText("combat.hp", current, max);
        }

        /// <summary>타격이 해결된 직후 원을 즉시 치운다 — 다음 Update가 남은 노트로 다시 채운다.</summary>
        private void HideTelegraph()
        {
            SetRingCount(0);
            if (parryBand != null)
                parryBand.gameObject.SetActive(false);
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
                state == PlayerActionState.Parrying ? noteRingColor : (ready ? ParryButtonColor : ButtonDisabledColor),
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

        /// <summary>
        /// 팝업 시작 위치를 하드코딩하지 않는다 — 방향별 배치(OrientationLayout)를 그대로 기준으로 삼는다.
        /// 연출 중에 다시 호출되면 밀어둔 위치를 먼저 되돌려 기준점이 위로 밀리는 것을 막는다 (GDD §9.3).
        /// </summary>
        private void ShowPopup(string message, Color color)
        {
            var rect = (RectTransform)popupText.transform;
            if (_popupPlaying)
                rect.anchoredPosition = _popupOrigin;
            else
                _popupOrigin = rect.anchoredPosition;

            StopCoroutine(nameof(PopupRoutine));
            popupText.text = message;
            popupText.color = color;
            StartCoroutine(nameof(PopupRoutine));
        }

        private IEnumerator PopupRoutine()
        {
            Color baseColor = popupText.color;
            var rect = (RectTransform)popupText.transform;
            _popupPlaying = true;

            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                rect.anchoredPosition = _popupOrigin + Vector2.up * (60f * (t / 0.6f));
                popupText.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f - t / 0.6f);
                yield return null;
            }

            rect.anchoredPosition = _popupOrigin;
            popupText.text = string.Empty;
            _popupPlaying = false;
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

        // ── 등장 연출 ──

        /// <summary>
        /// 둘이 각자 자기 쪽 화면 밖에서 미끄러져 들어온다 (플레이어는 왼쪽, 보스는 오른쪽).
        /// 첫 공격까지의 대기(BossConfig.FirstAttackDelaySeconds) 안에 끝나야 등장 중에 얻어맞지 않는다.
        /// </summary>
        private void StartEntrance()
        {
            if (_entranceRoutine != null)
                StopCoroutine(_entranceRoutine);
            _entranceRoutine = StartCoroutine(EntranceRoutine());
        }

        private IEnumerator EntranceRoutine()
        {
            Vector2 playerHome = playerBody != null ? playerBody.anchoredPosition : Vector2.zero;
            Vector2 bossHome = bossBody.anchoredPosition;

            Vector2 playerStart = playerHome + Vector2.left * entranceOffsetX;
            Vector2 bossStart = bossHome + Vector2.right * entranceOffsetX;

            for (float t = 0f; t < entranceSeconds; t += Time.deltaTime)
            {
                // 끝에서 부드럽게 멎도록 감속 — 미끄러져 '자리 잡는' 느낌
                float eased = 1f - Mathf.Pow(1f - t / entranceSeconds, 3f);
                if (playerBody != null)
                    playerBody.anchoredPosition = Vector2.LerpUnclamped(playerStart, playerHome, eased);
                bossBody.anchoredPosition = Vector2.LerpUnclamped(bossStart, bossHome, eased);
                yield return null;
            }

            if (playerBody != null)
                playerBody.anchoredPosition = playerHome;
            bossBody.anchoredPosition = bossHome;
            _entranceRoutine = null;
        }

        /// <summary>공격 커밋 동안 보스 쪽으로 찔러 들어갔다 돌아온다.</summary>
        private IEnumerator Lunge()
        {
            if (playerBody == null)
                yield break;

            Vector2 home = playerBody.anchoredPosition;
            Vector2 toward = home + (bossBody.anchoredPosition - home).normalized * lungeDistance;

            float half = Mathf.Max(0.05f, lungeSeconds * 0.5f);
            for (float t = 0f; t < half; t += Time.deltaTime)
            {
                playerBody.anchoredPosition = Vector2.Lerp(home, toward, t / half);
                yield return null;
            }

            for (float t = 0f; t < half; t += Time.deltaTime)
            {
                playerBody.anchoredPosition = Vector2.Lerp(toward, home, t / half);
                yield return null;
            }

            playerBody.anchoredPosition = home;
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
