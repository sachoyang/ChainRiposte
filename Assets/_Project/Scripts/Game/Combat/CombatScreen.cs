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
        [Tooltip("패링 가능 구간을 나타내는 회색 원의 색 — 흰 원이 여기에 <b>조금이라도 겹치면</b> 패링이 된다. 연하게 깔아 둘 것.")]
        [SerializeField] private Color parryBandColor = new(1f, 1f, 1f, 0.15f);
        [Tooltip("띠 두께를 판정 폭에 맞춰 자동으로 다시 그린다. 실제 아트를 꽂았다면 끌 것.")]
        [SerializeField] private bool generateBandSprite = true;
        [Tooltip("원이 다가오는 속도 (초당 스케일). 모든 노트가 같은 속도로 와야 회색 띠가 의미를 갖는다.")]
        [SerializeField, Min(0.05f)] private float approachSpeed = 0.9f;
        [Tooltip("노트 원 그림도 회색 원과 같은 두께로 자동으로 굽는다 — " +
            "무투자(PARRY 0) 상태에서 두 원이 정확히 포개지는 것이 이 화면의 기준이다. " +
            "실제 아트를 꽂았다면 끄고 아래 비율을 그 그림에 맞춰 적을 것.")]
        [SerializeField] private bool matchNoteRingToBand = true;
        [Tooltip("노트 원 그림의 안쪽 구멍 비율(안쪽 반지름 ÷ 바깥 반지름). " +
            "이 원의 <b>두께</b>가 곧 판정의 여유분이므로, 실제 아트로 갈아 끼우면 그 그림의 비율을 여기에 적어야 " +
            "보이는 것과 판정이 계속 맞는다. 기본 원(PlaceholderSprite.Ring)은 0.88.")]
        [SerializeField, Range(0.05f, 0.99f)] private float noteRingInnerRatio = 0.88f;
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
        [Tooltip("게이지 이미지를 자동으로 Filled(가로·왼쪽 기준)로 맞춘다. " +
            "9슬라이스 스프라이트를 꽂으면 Unity가 Image Type 을 Sliced 로 되돌리는데, 그러면 fillAmount 가 " +
            "아무 효과도 없어 게이지가 <b>조용히 안 줄어든다</b>. 게이지를 다른 방식으로 그린다면 끌 것.")]
        [SerializeField] private bool forceFilledGauges = true;
        [SerializeField] private TMP_Text playerHpText;
        [SerializeField] private RectTransform bossBody;
        [SerializeField] private Image bossBodyImage;
        [Tooltip("보스를 좌우로 뒤집어 왼쪽(플레이어 쪽)을 보게 한다. " +
            "이 프로젝트의 그림은 전부 오른쪽을 보고 그려져 있어서, 오른쪽에 서는 보스만 뒤집어야 서로 마주 본다. " +
            "왼쪽을 보고 그린 보스 그림을 쓴다면 끌 것.")]
        [SerializeField] private bool flipBossHorizontally = true;
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

        [Header("인살 게이지 — 보스 왼쪽 위의 빨간 원 (세키로식)")]
        [Tooltip("빨간 원들이 놓이는 자리. 인살 페이즈가 하나뿐인 보스에서는 스스로 꺼진다.")]
        [SerializeField] private RectTransform deathblowMarkRoot;
        [Tooltip("원 하나의 복제 원본. 개수가 보스 데이터로 정해지므로 이것만 씬에 두고 필요한 만큼 복제한다.")]
        [SerializeField] private Image deathblowMarkTemplate;
        [Tooltip("아직 남은 인살 한 번")]
        [SerializeField] private Color deathblowRemainingColor = new(0.88f, 0.14f, 0.16f, 1f);
        [Tooltip("이미 끝낸 인살 한 번 — 지우지 않고 어둡게 남겨야 '몇 번짜리 보스인지'가 계속 읽힌다")]
        [SerializeField] private Color deathblowSpentColor = new(0.22f, 0.09f, 0.10f, 0.85f);
        [Tooltip("원 사이 간격 (픽셀)")]
        [SerializeField, Min(0f)] private float deathblowMarkSpacing = 52f;

        [Header("인살 대기 마크 — 보스 한가운데")]
        [Tooltip("체간이 무너져 인살할 수 있을 때 보스 위에 뜨는 빨간 원. 비워도 동작한다.")]
        [SerializeField] private Image executeMark;
        [SerializeField] private Color executeMarkColor = new(0.90f, 0.12f, 0.15f, 0.85f);

        [Header("페이즈 전환 컷씬 (Add Phase Cutscene To Main 이 배선)")]
        [Tooltip("컷씬 전체 루트. 비어 있으면 컷씬 없이 그림만 갈아 끼우고 바로 재개한다 — " +
            "배선을 덜 했다고 전투가 멈추면 안 된다.")]
        [SerializeField] private RectTransform cutsceneRoot;
        [Tooltip("컷씬 전체를 한꺼번에 페이드하기 위한 것. 비우면 루트에서 찾는다.")]
        [SerializeField] private CanvasGroup cutsceneGroup;
        [Tooltip("trans 그림이 뜨는 자리. 그림이 없으면 스스로 꺼진다.")]
        [SerializeField] private Image cutsceneImage;
        [SerializeField] private TMP_Text cutsceneText;
        [Tooltip("아무 데나 눌러 넘기기 위한 전체 화면 버튼")]
        [SerializeField] private Button cutsceneSkipButton;
        [Tooltip("문구를 읽을 시간. 누르면 즉시 넘어간다.")]
        [SerializeField, Min(0.1f)] private float cutsceneHoldSeconds = 2.5f;

        [Header("처형(인살) 컷씬 (Add Execution Cutscene To Main 이 배선)")]
        [Tooltip("인살할 때마다 도는 검은 띠 컷씬. 비어 있으면 그냥 건너뛴다 — " +
            "배선을 덜 했다고 전투가 멈추면 안 된다.")]
        [SerializeField] private ExecutionCutscene executionCutscene;

        private readonly List<RectTransform> _rings = new();
        private readonly List<Image> _ringImages = new();
        private readonly List<Image> _deathblowMarks = new();
        private Coroutine _entranceRoutine;

        private CombatSystem _combat;
        private GameSession _session;
        private Sprite _bossSprite;
        private string _bossNameKey;
        private Coroutine _executePulseRoutine;
        private Vector2 _popupOrigin;
        private bool _popupPlaying;
        private float _bandInnerRatio = -1f;
        private float _ringInnerRatio = -1f;

        /// <summary>
        /// 보스의 '평상시' 스케일. 좌우 반전을 여기 한 곳에만 담아 두고 연출(펀치 등)은 이 값에 곱하기만 한다 —
        /// 연출이 <c>Vector3.one</c>으로 되돌리면 뒤집힌 게 한 프레임마다 풀렸다 다시 걸린다.
        /// </summary>
        private Vector3 _bossBaseScale = Vector3.one;
        private bool _cutsceneSkipped;

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

            if (forceFilledGauges)
            {
                EnsureFilled(bossHpFill);
                EnsureFilled(postureFill);
                EnsureFilled(playerHpFill);
            }

            parryButton.onClick.AddListener(() => input.PressParry());
            attackButton.onClick.AddListener(() => input.PressAttack());

            if (cutsceneSkipButton != null)
                cutsceneSkipButton.onClick.AddListener(() => _cutsceneSkipped = true);
            if (cutsceneRoot != null)
            {
                if (cutsceneGroup == null)
                    cutsceneGroup = cutsceneRoot.GetComponent<CanvasGroup>();
                cutsceneRoot.gameObject.SetActive(false);
            }

            root.gameObject.SetActive(false);
        }

        /// <summary>
        /// 게이지 이미지가 <c>fillAmount</c>에 반응하도록 보장한다. 스프라이트 교체 한 번으로
        /// 세 게이지가 통째로 멈추는 사고를 씬 상태에 기대지 않고 막는다.
        /// </summary>
        private static void EnsureFilled(Image image)
        {
            if (image == null || image.type == Image.Type.Filled)
                return;

            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        private void OnDestroy() => Unbind();

        /// <summary>
        /// 전투에 설 보스 이미지를 지정한다. 보스 <b>타일</b>은 종류와 무관하게 하나로 통일하고
        /// (플레이어가 "이게 보스 타일이다"를 한눈에 알아야 한다), 실제 생김새는 이 화면에서만 다르다.
        /// </summary>
        public void SetBossVisual(Sprite sprite, string nameKey)
        {
            _bossSprite = sprite;
            _bossNameKey = nameKey;
        }

        /// <summary>
        /// 이름은 <b>현지화 키</b>로 받는다. 키가 CSV에 없으면 받은 문자열을 그대로 쓴다 —
        /// 현지화 이전에 만든 BossDataSO의 생 이름(예: "The Warden")이 경고 없이 계속 나오게 하기 위해서다.
        /// </summary>
        private string ResolveBossName()
        {
            if (string.IsNullOrWhiteSpace(_bossNameKey))
                return Loc.GetText("combat.boss");

            return Loc.HasKey(_bossNameKey) ? Loc.GetText(_bossNameKey) : _bossNameKey;
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
            bossNameText.text = ResolveBossName();
            postureFill.fillAmount = _combat.Posture / _combat.MaxPosture;
            bossHpFill.fillAmount = _combat.BossHp / _combat.BossMaxHp;
            OnPlayerHealthChanged(_session.Health.Current, _session.Health.Max);
            HideTelegraph();
            noteRingTemplate.gameObject.SetActive(false); // 복제 원본은 항상 꺼 둔다
            popupText.text = string.Empty;
            executeText.gameObject.SetActive(false);
            SetExecuteMarkVisible(false);
            flashOverlay.color = Color.clear;
            introText.color = new Color(0.85f, 0.2f, 0.25f, 1f);
            _bossBaseScale = new Vector3(flipBossHorizontally ? -1f : 1f, 1f, 1f);
            bossBody.localScale = _bossBaseScale;

            ApplyBossSprite(_bossSprite);
            RefreshDeathblowMarks();

            if (playerBody != null)
            {
                playerBody.localScale = Vector3.one;

                Sprite body = ResolvePlayerSprite();
                if (body != null)
                {
                    playerBodyImage.sprite = body;
                    playerBodyImage.preserveAspect = true;
                }

                playerBodyImage.color = body != null ? Color.white : playerColor;
            }

            StartEntrance();
            OnPlayerStateChanged(PlayerActionState.Ready);
        }

        /// <summary>
        /// 고른 캐릭터가 있으면 그 그림이 우선. 없으면 인스펙터에 꽂아 둔 것(Main 단독 실행용).
        /// 처형 컷씬도 같은 규칙을 써야 전투 화면과 컷씬의 사람이 달라지지 않는다.
        /// </summary>
        private Sprite ResolvePlayerSprite()
        {
            Characters.PlayerCharacterSO character = Characters.CharacterService.Current;
            return character != null && character.CombatSprite != null ? character.CombatSprite : playerSprite;
        }

        /// <summary>
        /// 스프라이트가 있으면 원색 그대로(흰 틴트), 없으면 색 사각형 플레이스홀더.
        /// <b>지금 서 있는 보스 그림을 여기 한 곳에서 기억한다</b> — 페이즈가 바뀌어 갈아 끼운 뒤에도
        /// 처형 컷씬이 '방금 화면에 있던 그 보스'를 그대로 데려갈 수 있어야 한다.
        /// </summary>
        private void ApplyBossSprite(Sprite sprite)
        {
            _bossSprite = sprite;
            bool hasSprite = sprite != null;
            if (hasSprite)
            {
                bossBodyImage.sprite = sprite;
                bossBodyImage.preserveAspect = true;
            }

            bossBodyImage.color = hasSprite ? Color.white : bossColor;
        }

        /// <summary>
        /// 남은 인살 횟수를 빨간 원으로 그린다. 개수가 보스 데이터로 정해지므로
        /// 씬의 원본 하나를 복제한다(노트 원·캐릭터 카드와 같은 규칙).
        ///
        /// <para><b>인살이 한 번뿐인 보스에서는 통째로 꺼진다</b> — 원 하나만 떠 있으면
        /// "아직 뭔가 남았나?"로 읽혀 오히려 방해다.</para>
        ///
        /// <para>끝낸 몫은 지우지 않고 어둡게 남긴다. 개수가 줄어들면 이 보스가 원래 몇 번짜리였는지 알 수 없다.</para>
        /// </summary>
        private void RefreshDeathblowMarks()
        {
            if (deathblowMarkTemplate == null)
                return;

            deathblowMarkTemplate.gameObject.SetActive(false); // 복제 원본은 항상 꺼 둔다

            int total = _combat != null ? _combat.BattlePhaseCount : 0;
            bool show = total > 1;
            if (deathblowMarkRoot != null && deathblowMarkRoot.gameObject.activeSelf != show)
                deathblowMarkRoot.gameObject.SetActive(show);

            if (!show)
            {
                SetDeathblowMarkCount(0);
                return;
            }

            int remaining = _combat.RemainingDeathblows;
            for (int i = 0; i < total; i++)
            {
                Image mark = GetDeathblowMark(i);
                ((RectTransform)mark.transform).anchoredPosition = new Vector2(i * deathblowMarkSpacing, 0f);
                mark.color = i < remaining ? deathblowRemainingColor : deathblowSpentColor;
            }

            SetDeathblowMarkCount(total);
        }

        private Image GetDeathblowMark(int index)
        {
            while (_deathblowMarks.Count <= index)
            {
                Image mark = Instantiate(deathblowMarkTemplate, deathblowMarkTemplate.transform.parent);
                mark.gameObject.name = $"DeathblowMark_{_deathblowMarks.Count}";
                _deathblowMarks.Add(mark);
            }

            Image result = _deathblowMarks[index];
            if (!result.gameObject.activeSelf)
                result.gameObject.SetActive(true);
            return result;
        }

        private void SetDeathblowMarkCount(int active)
        {
            for (int i = active; i < _deathblowMarks.Count; i++)
                if (_deathblowMarks[i].gameObject.activeSelf)
                    _deathblowMarks[i].gameObject.SetActive(false);
        }

        private void SetExecuteMarkVisible(bool visible)
        {
            if (executeMark == null)
                return;

            if (executeMark.gameObject.activeSelf != visible)
                executeMark.gameObject.SetActive(visible);
            if (visible)
                executeMark.color = executeMarkColor;
        }

        // ── CombatSystem 이벤트 연출 ──

        /// <summary>
        /// 다가오는 노트를 흰 원으로 그린다. 원이 보스 크기(×1)에 닿는 순간이 타격 시점이다.
        ///
        /// 원은 <b>모두 같은 속도로</b> 다가온다(진행률이 아니라 남은 시간 × 속도로 크기를 정함).
        /// 그래야 "보스에서 이만큼 떨어진 거리 = 이만큼의 시간"이 항상 같고,
        /// 패링 구간을 두께가 고정된 회색 띠 하나로 표현할 수 있다.
        /// 예비동작이 긴 노트는 그만큼 더 멀리서부터 보인다.
        ///
        /// <para><b>판정은 두 원이 겹치는 동안이다.</b> 흰 원과 회색 원은 같은 두께로 그려져 있고,
        /// 무투자 상태에서 정확히 포개지는 순간이 완벽한 타이밍이다. 조금만 걸쳐도 패링이 되므로
        /// 원의 두께 자체가 여유분이 된다 — "겹친 것처럼 보이는데 판정은 아직"이 생길 수 없다.
        /// 기준선은 여전히 <b>원의 안쪽 테두리</b>(= 노트의 위치)이고, 겹침 계산이 두께를 얹어 준다.</para>
        /// </summary>
        private void Update()
        {
            if (_combat == null || _combat.Finished)
            {
                SetRingCount(0);
                return;
            }

            float ringRatio = ResolveRingInnerRatio();
            UpdateParryBand(ringRatio);

            IReadOnlyList<ActiveNote> notes = _combat.ActiveNotes;
            int drawn = 0;

            for (int i = 0; i < notes.Count; i++)
            {
                // 노트가 있는 반지름 — 띠와 같은 자로 잰 값이다
                float radius = 1f + notes[i].SecondsUntilHit * approachSpeed;
                if (radius > maxVisibleScale)
                    continue; // 아직 멀다 — 화면 밖

                RectTransform ring = GetRing(drawn);
                // 안쪽 테두리가 그 반지름에 오도록 키운다
                ring.localScale = Vector3.one * (radius / ringRatio);

                // 임박할수록 진하게 — 어느 것을 먼저 쳐야 하는지가 한눈에 읽힌다
                float nearness = Mathf.InverseLerp(maxVisibleScale, 1f, radius);
                Color color = noteRingColor;
                color.a *= Mathf.Lerp(farthestRingAlpha, 1f, nearness);
                _ringImages[drawn].color = color;

                drawn++;
            }

            SetRingCount(drawn);
        }

        /// <summary>
        /// 노트 원 그림의 안쪽 구멍 비율. <see cref="matchNoteRingToBand"/>가 켜져 있으면
        /// <b>무투자 상태의 회색 원과 두께가 같아지는 값</b>을 판정 수치에서 역산해 그림까지 다시 굽는다.
        ///
        /// <para>유도: 회색 원 = [(1 − 유예×속도) ÷ k, 1 + 기본윈도우×속도], 흰 원의 두께 = 1/k − 1
        /// (반지름 1에서). 둘을 같게 놓으면 <c>k = (2 − 유예×속도) ÷ (2 + 기본윈도우×속도)</c>.
        /// 판정 값을 인스펙터에서 조여도 두 원이 계속 같은 두께로 남는다 —
        /// 숫자를 코드와 그림 양쪽에 적어 두면 한쪽만 고쳐 어긋난다.</para>
        /// </summary>
        private float ResolveRingInnerRatio()
        {
            if (!matchNoteRingToBand)
                return noteRingInnerRatio;

            float grace = _combat.ParryLateGraceSeconds * approachSpeed;
            float baseWindow = _combat.BaseParryWindowSeconds * approachSpeed;
            // 구워 주는 실제 비율(0.01 단위)로 맞춰야 아래 띠 계산과 그림이 어긋나지 않는다.
            float ratio = PlaceholderSprite.QuantizeRatio(
                Mathf.Clamp((2f - grace) / (2f + baseWindow), 0.05f, 0.99f));

            if (!Mathf.Approximately(ratio, _ringInnerRatio))
            {
                _ringInnerRatio = ratio;
                Sprite sprite = PlaceholderSprite.Annulus(ratio);
                if (noteRingTemplate != null)
                {
                    var templateImage = noteRingTemplate.GetComponent<Image>();
                    if (templateImage != null)
                        templateImage.sprite = sprite;
                }

                for (int i = 0; i < _ringImages.Count; i++)
                    if (_ringImages[i] != null)
                        _ringImages[i].sprite = sprite;
            }

            return ratio;
        }

        /// <summary>
        /// 패링 구간 회색 원 — <b>흰 원이 여기에 조금이라도 겹치면 패링이 된다.</b> 그게 전부다.
        ///
        /// <para>판정은 타격 시점 T를 기준으로 <c>[T − 윈도우, T + 유예]</c>에 열린다. 원은
        /// "남은 시간 × 속도"로 다가오므로 그 구간은 그대로 노트 반지름 구간
        /// <c>[1 − 유예×속도, 1 + 윈도우×속도]</c>이 된다. 흰 원은 안쪽 테두리 r 에서
        /// <c>[r, r/k]</c>를 차지하므로 <b>겹침</b> 조건은 <c>k×안쪽 &lt; r &lt; 바깥</c> —
        /// 즉 겹침이 곧 판정이 되려면 바깥 = 1 + 윈도우×속도, <b>안쪽 = (1 − 유예×속도) ÷ k</b>다.
        /// 안쪽을 원 두께만큼 밀어 올린 이 한 줄이 "회색 원을 흰 원과 같은 두께로 얇게 그리고,
        /// 대신 조금 겹쳐도 인정"의 전부다.</para>
        ///
        /// <para>PARRY를 올리면 바깥이 커져 <b>회색 원이 실제로 굵어지고</b> 판정이 더 일찍 열린다 —
        /// 보이는 것과 판정이 같다.</para>
        /// </summary>
        private void UpdateParryBand(float ringInnerRatio)
        {
            if (parryBand == null)
                return;

            bool visible = _combat.ActiveNotes.Count > 0;
            if (parryBand.gameObject.activeSelf != visible)
                parryBand.gameObject.SetActive(visible);
            if (!visible)
                return;

            float outerScale = 1f + _combat.ParryWindowSeconds * approachSpeed;
            float innerScale = Mathf.Clamp(
                (1f - _combat.ParryLateGraceSeconds * approachSpeed) / ringInnerRatio,
                0.05f, outerScale - 0.001f);
            parryBand.localScale = Vector3.one * outerScale;

            if (parryBandImage == null)
                return;

            parryBandImage.color = parryBandColor;

            // 스케일은 바깥 지름만 정한다 — 안쪽 구멍 크기는 스프라이트를 다시 구워야 나온다.
            if (generateBandSprite)
            {
                float innerRatio = innerScale / outerScale;
                if (!Mathf.Approximately(innerRatio, _bandInnerRatio) || parryBandImage.sprite == null)
                {
                    _bandInnerRatio = innerRatio;
                    parryBandImage.sprite = PlaceholderSprite.Annulus(innerRatio);
                }
            }
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
            SetExecuteMarkVisible(true);
            _executePulseRoutine = StartCoroutine(PulseExecuteMark());
            RefreshButtons(_combat.PlayerState);
        }

        private void OnExecutionPerformed()
        {
            StopExecutePulse();
            executeText.gameObject.SetActive(false);
            SetExecuteMarkVisible(false);
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
                ready ? ParryButtonColor : ButtonDisabledColor,
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

        // ── 처형(인살) 컷씬 ──

        /// <summary>
        /// 인살 한 번 분량의 연출. <b>인살할 때마다</b> 돈다 — 마지막 인살이면 뒤에 승리가 오고,
        /// 페이즈가 남았으면 뒤에 전환 컷씬이 온다. 순서를 정하는 것은 <see cref="CombatController"/>다.
        ///
        /// <para>Core는 이 동안 시간을 세지 않으므로(마지막 인살은 이미 <c>Finished</c>,
        /// 중간 인살은 <c>AwaitingPhaseTransition</c>) 길이를 여기서 마음대로 정할 수 있다.</para>
        /// </summary>
        public IEnumerator PlayExecution()
        {
            HideTelegraph();
            StopExecutePulse();
            executeText.gameObject.SetActive(false);
            SetExecuteMarkVisible(false);

            if (executionCutscene == null)
                yield break;

            yield return executionCutscene.Play(ResolvePlayerSprite(), _bossSprite);
        }

        // ── 페이즈 전환 컷씬 ──

        /// <summary>
        /// 1페이즈를 인살한 뒤 2페이즈가 시작되기 전까지의 연출.
        /// <b>이 코루틴이 끝나야</b> 컨트롤러가 <c>BeginNextPhase()</c>를 불러 전투를 재개한다 —
        /// 그동안 Core는 시간을 세지 않으므로 연출 길이를 자유롭게 바꿀 수 있다.
        ///
        /// <para>보스 그림은 <b>화면이 덮인 동안</b> 갈아 끼운다. 밝을 때 바꾸면 그림이 툭 바뀌는 게 보인다.</para>
        ///
        /// <para>컷씬 자리를 아직 안 배선했으면 그림만 바꾸고 짧게 넘어간다 —
        /// 배선이 덜 됐다고 전투가 멈춰 있으면 안 된다.</para>
        /// </summary>
        public IEnumerator PlayPhaseTransition(Sprite transitionSprite, Sprite nextSprite, string textKey)
        {
            HideTelegraph();
            StopExecutePulse();
            executeText.gameObject.SetActive(false);
            SetExecuteMarkVisible(false);

            if (cutsceneRoot == null)
            {
                ApplyBossSprite(nextSprite);
                yield return new WaitForSeconds(0.4f);
                yield break;
            }

            _cutsceneSkipped = false;

            if (cutsceneImage != null)
            {
                Sprite shown = transitionSprite != null ? transitionSprite : nextSprite;
                cutsceneImage.enabled = shown != null;
                if (shown != null)
                {
                    cutsceneImage.sprite = shown;
                    cutsceneImage.preserveAspect = true;
                }
            }

            if (cutsceneText != null)
                cutsceneText.text = string.IsNullOrWhiteSpace(textKey) ? string.Empty : Loc.GetText(textKey);

            cutsceneRoot.gameObject.SetActive(true);
            yield return FadeCutscene(0f, 1f, 0.5f);

            ApplyBossSprite(nextSprite);
            yield return HoldOrSkip(cutsceneHoldSeconds);

            yield return FadeCutscene(1f, 0f, 0.45f);
            cutsceneRoot.gameObject.SetActive(false);

            yield return BossEntrance();
        }

        private IEnumerator FadeCutscene(float from, float to, float seconds)
        {
            if (cutsceneGroup == null)
                yield break;

            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                // 양 끝이 눕지 않으면 알파가 툭 끊겨 보인다 (인트로 페이드와 같은 이유)
                cutsceneGroup.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds));
                yield return null;
            }

            cutsceneGroup.alpha = to;
        }

        /// <summary>문구를 읽을 시간. 누르면 즉시 넘어간다 — 두 번째부터는 이미 본 연출이다.</summary>
        private IEnumerator HoldOrSkip(float seconds)
        {
            for (float t = 0f; t < seconds && !_cutsceneSkipped; t += Time.deltaTime)
                yield return null;
        }

        /// <summary>바뀐 모습으로 다시 자기 자리에 미끄러져 들어온다.</summary>
        private IEnumerator BossEntrance()
        {
            Vector2 home = bossBody.anchoredPosition;
            Vector2 start = home + Vector2.right * entranceOffsetX;

            for (float t = 0f; t < entranceSeconds; t += Time.deltaTime)
            {
                float eased = 1f - Mathf.Pow(1f - t / entranceSeconds, 3f);
                bossBody.anchoredPosition = Vector2.LerpUnclamped(start, home, eased);
                yield return null;
            }

            bossBody.anchoredPosition = home;
        }

        /// <summary>새 페이즈가 시작됐다 — 인살 마크를 다시 그린다 (게이지는 Core 이벤트가 갱신한다).</summary>
        public void OnPhaseStarted() => RefreshDeathblowMarks();

        private void StopExecutePulse()
        {
            if (_executePulseRoutine == null)
                return;

            StopCoroutine(_executePulseRoutine);
            _executePulseRoutine = null;
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
            // 뒤집힌 보스도 뒤집힌 채로 튄다 — 평상시 스케일에 곱하기만 한다
            Vector3 baseScale = target == bossBody ? _bossBaseScale : Vector3.one;

            for (float t = 0f; t < 0.18f; t += Time.deltaTime)
            {
                target.localScale = baseScale * Mathf.Lerp(scale, 1f, t / 0.18f);
                yield return null;
            }
            target.localScale = baseScale;
        }

        /// <summary>
        /// 인살할 수 있다는 신호. 글씨와 보스 위의 빨간 원이 <b>같은 박자로</b> 뛴다 —
        /// 따로 놀면 어느 쪽이 신호인지 헷갈린다.
        /// </summary>
        private IEnumerator PulseExecuteMark()
        {
            while (true)
            {
                float beat = Mathf.PingPong(Time.time * 3f, 1f);
                executeText.color = new Color(0.9f, 0.12f, 0.15f, 0.6f + 0.4f * beat);

                if (executeMark != null)
                {
                    Color color = executeMarkColor;
                    color.a *= 0.55f + 0.45f * beat;
                    executeMark.color = color;
                    // 보스 몸에 붙어 있으므로 자기 스케일만 만진다 (보스의 뒤집힘·펀치와 안 싸운다)
                    executeMark.transform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.1f, beat);
                }

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
