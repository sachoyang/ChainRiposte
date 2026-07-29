using System.Collections;
using System.Collections.Generic;
using ChainRiposte.Game.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChainRiposte.Game.Combat
{
    /// <summary>
    /// 보스 처형(인살) 컷씬. <b>화면 위아래에서 검은 띠가 닫혀</b> 가운데 가로 창만 남기고,
    /// 그 창 안에서 캐릭터가 보스를 다단으로 벤다 (포켓몬 비전머신식 화면 분할).
    ///
    /// <para><b>영상이 아니라 실시간 연출인 이유</b>: 보스 3종 × 캐릭터 2종이라 조합마다 영상을 찍어야 하고,
    /// 처형은 타이밍 보상이라 즉각 터져야 하는데 영상은 길이가 고정이라 리듬이 끊긴다.
    /// 여기서는 두 그림을 <see cref="Play"/>에 넘겨받으므로 <b>조합이 자동</b>이고,
    /// 아트가 생기면 스프라이트만 갈아 끼우면 된다.</para>
    ///
    /// <para><b>시간은 전부 unscaled</b>다 — 인살 순간 <see cref="Juice.JuiceDirector"/>가 히트스톱으로
    /// <c>Time.timeScale</c>을 0.05까지 떨어뜨리므로, 스케일 시간을 쓰면 이 연출이 20배로 늘어진다.
    /// 세상이 멎은 동안 컷씬만 제 속도로 도는 것이 의도한 그림이다.</para>
    ///
    /// <para>넘기기 버튼은 일부러 없다. 1.5초 남짓이고 <b>플레이어가 방금 얻어낸 보상</b>이라
    /// 건너뛸 것을 전제하면 연출을 그만큼 짧게 만드는 편이 낫다.</para>
    ///
    /// <para>씬 배치는 <c>Tools ▸ ChainRiposte ▸ Add Execution Cutscene To Main</c>.</para>
    /// </summary>
    public sealed class ExecutionCutscene : MonoBehaviour
    {
        [Header("씬 참조 (Add Execution Cutscene To Main 이 배선)")]
        [Tooltip("컷씬 전체 루트. 평소에는 꺼져 있다.")]
        [SerializeField] private RectTransform root;
        [Tooltip("통째로 페이드하기 위한 것. 비우면 루트에서 찾는다.")]
        [SerializeField] private CanvasGroup group;
        [Tooltip("위에서 내려오는 검은 띠. 높이가 0에서 목표까지 자라며 화면을 덮는다.")]
        [SerializeField] private RectTransform bandTop;
        [SerializeField] private RectTransform bandBottom;
        [Tooltip("띠 사이에 남는 가로 창. 여기 담긴 것만 보인다.")]
        [SerializeField] private RectTransform window;
        [SerializeField] private Image characterImage;
        [SerializeField] private Image bossImage;
        [Tooltip("검기·상처가 놓이는 자리. 창과 같은 크기·중심이라 좌표를 그대로 쓴다.")]
        [SerializeField] private RectTransform slashLayer;
        [Tooltip("검기 한 획의 복제 원본. 개수가 연출로 정해지므로 이것만 두고 필요한 만큼 복제한다.")]
        [SerializeField] private Image slashTemplate;
        [Tooltip("타격마다 번쩍이는 흰 판")]
        [SerializeField] private Image flash;
        [Tooltip("마지막 일격에 뜨는 문구")]
        [SerializeField] private TMP_Text line;

        [Header("창 / 띠")]
        [Tooltip("띠 사이에 남는 창의 높이(픽셀). 이 값이 곧 연출의 화면이다.")]
        [SerializeField, Min(80f)] private float windowHeight = 360f;
        [Tooltip("띠가 닫히는 시간 — 짧아야 '쾅' 하고 갇힌 느낌이 난다")]
        [SerializeField, Min(0.02f)] private float bandCloseSeconds = 0.18f;
        [SerializeField, Min(0.02f)] private float bandOpenSeconds = 0.3f;
        [Tooltip("보스를 좌우로 뒤집어 캐릭터를 보게 한다. 이 프로젝트 그림은 전부 오른쪽을 보고 그려져 있다.")]
        [SerializeField] private bool flipBoss = true;

        [Header("등장")]
        [Tooltip("캐릭터가 창 왼쪽 밖에서 미끄러져 들어오는 시간")]
        [SerializeField, Min(0.02f)] private float entrySeconds = 0.16f;
        [SerializeField, Min(0f)] private float entryOffsetX = 520f;

        [Header("다단 타격")]
        [Tooltip("마지막 일격 앞에 들어가는 잔타 수")]
        [SerializeField, Range(0, 12)] private int hitCount = 4;
        [Tooltip("잔타 간격 — 짧을수록 난도질로 읽힌다")]
        [SerializeField, Min(0.01f)] private float hitInterval = 0.085f;
        [Tooltip("마지막 일격 직전의 뜸. 잔타와 같은 박이면 마지막이 안 도드라진다.")]
        [SerializeField, Min(0f)] private float finisherDelaySeconds = 0.18f;
        [Tooltip("일격 뒤 여운")]
        [SerializeField, Min(0f)] private float holdSeconds = 0.55f;

        [Header("검기")]
        [SerializeField, Min(10f)] private float slashLength = 460f;
        [SerializeField, Min(2f)] private float slashThickness = 44f;
        [Tooltip("획의 기울기(도). 0이면 가로. 좌우로 번갈아 그어 엇갈리게 한다.")]
        [SerializeField] private float slashAngle = 32f;
        [SerializeField, Range(0f, 45f)] private float slashAngleJitter = 14f;
        [Tooltip("획이 보스 중심에서 흩어지는 반경")]
        [SerializeField, Min(0f)] private float hitScatter = 60f;
        [SerializeField, Min(0.02f)] private float slashSeconds = 0.22f;
        [SerializeField] private Color slashColor = new(1f, 0.97f, 0.9f, 0.95f);
        [Tooltip("마지막 일격의 배율 (길이·굵기·시간)")]
        [SerializeField, Min(1f)] private float finisherScale = 1.7f;

        [Header("상처 — 지나간 자리에 남는다")]
        [Tooltip("0이면 상처를 안 남긴다. 남는 자국이 있어야 '여러 번 벴다'가 결과로 보인다.")]
        [SerializeField, Min(0f)] private float scarThickness = 11f;
        [SerializeField] private Color scarColor = new(0.85f, 0.1f, 0.14f, 0.9f);

        [Header("타격 반응")]
        [SerializeField, Range(0f, 1f)] private float hitFlashAlpha = 0.35f;
        [SerializeField, Range(0f, 1f)] private float finisherFlashAlpha = 0.95f;
        [SerializeField, Min(0f)] private float bossShakeDistance = 26f;

        private readonly List<GameObject> _spawned = new();
        private Coroutine _flashRoutine;
        private Coroutine _shakeRoutine;
        private Vector2 _bossHome;
        private Vector2 _characterHome;

        private void Awake()
        {
            if (root == null)
                return;

            if (group == null)
                group = root.GetComponent<CanvasGroup>();
            if (slashTemplate != null)
                slashTemplate.gameObject.SetActive(false); // 복제 원본은 항상 꺼 둔다

            // 제자리는 씬에 잡아 둔 배치가 원본이다. 연출이 시작될 때 재면 앞 연출이 밀어 둔 자리를
            // 새 제자리로 알아듣고 처형을 거듭할수록 두 사람이 화면 밖으로 걸어 나간다.
            if (bossImage != null)
                _bossHome = bossImage.rectTransform.anchoredPosition;
            if (characterImage != null)
                _characterHome = characterImage.rectTransform.anchoredPosition;

            root.gameObject.SetActive(false);
        }

        /// <summary>
        /// 처형 한 번. <b>이 코루틴이 끝나야</b> 호출한 쪽이 다음(페이즈 전환 / 승리 처리)으로 넘어간다.
        /// </summary>
        /// <param name="characterSprite">고른 캐릭터의 전투 그림</param>
        /// <param name="bossSprite">지금 페이즈의 보스 그림</param>
        public IEnumerator Play(Sprite characterSprite, Sprite bossSprite)
        {
            if (root == null)
                yield break;

            Prepare(characterSprite, bossSprite);
            root.gameObject.SetActive(true);

            // 창 높이를 뺀 나머지를 위아래가 나눠 덮는다 — 화면 크기를 매번 재므로 방향이 바뀌어도 맞는다
            float bandTarget = Mathf.Max(0f, (root.rect.height - windowHeight) * 0.5f);

            yield return Bands(0f, bandTarget, bandCloseSeconds, fadeFrom: 0f, fadeTo: 1f);
            yield return EnterCharacter();

            for (int i = 0; i < hitCount; i++)
            {
                Strike(i, finisher: false);
                yield return Wait(hitInterval);
            }

            yield return Wait(finisherDelaySeconds);
            Strike(hitCount, finisher: true);
            if (line != null)
                line.text = Loc.GetText("combat.execute");

            yield return FadeBossOut();
            yield return Wait(holdSeconds);

            yield return Bands(bandTarget, 0f, bandOpenSeconds, fadeFrom: 1f, fadeTo: 0f);

            Cleanup();
            root.gameObject.SetActive(false);
        }

        private void Prepare(Sprite characterSprite, Sprite bossSprite)
        {
            Cleanup();

            SetBandHeight(bandTop, 0f);
            SetBandHeight(bandBottom, 0f);

            // 창 높이의 주인은 이 값 하나다 — 씬의 Window 높이와 여기 값을 따로 두면
            // 한쪽만 고친 순간 띠 사이 틈과 실제 창이 어긋난다.
            if (window != null)
                window.sizeDelta = new Vector2(window.sizeDelta.x, windowHeight);
            if (group != null)
                group.alpha = 0f;
            if (flash != null)
                flash.color = Color.clear;
            if (line != null)
                line.text = string.Empty;

            if (bossImage != null)
            {
                bossImage.enabled = bossSprite != null;
                if (bossSprite != null)
                {
                    bossImage.sprite = bossSprite;
                    bossImage.preserveAspect = true;
                }
                bossImage.color = Color.white;
                bossImage.rectTransform.anchoredPosition = _bossHome;
                bossImage.rectTransform.localScale = new Vector3(flipBoss ? -1f : 1f, 1f, 1f);
            }

            if (characterImage != null)
            {
                characterImage.enabled = characterSprite != null;
                if (characterSprite != null)
                {
                    characterImage.sprite = characterSprite;
                    characterImage.preserveAspect = true;
                }
                characterImage.color = Color.white;
                characterImage.rectTransform.anchoredPosition = _characterHome;
            }
        }

        /// <summary>띠 높이와 전체 알파를 한 번에 몬다 — 따로 돌리면 닫히는 속도와 어두워지는 속도가 어긋난다.</summary>
        private IEnumerator Bands(float from, float to, float seconds, float fadeFrom, float fadeTo)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                float height = Mathf.Lerp(from, to, k);
                SetBandHeight(bandTop, height);
                SetBandHeight(bandBottom, height);
                if (group != null)
                    group.alpha = Mathf.Lerp(fadeFrom, fadeTo, k);
                yield return null;
            }

            SetBandHeight(bandTop, to);
            SetBandHeight(bandBottom, to);
            if (group != null)
                group.alpha = fadeTo;
        }

        private static void SetBandHeight(RectTransform band, float height)
        {
            if (band == null)
                return;

            Vector2 size = band.sizeDelta;
            size.y = height;
            band.sizeDelta = size;
        }

        private IEnumerator EnterCharacter()
        {
            if (characterImage == null || entrySeconds <= 0f)
                yield break;

            RectTransform rect = characterImage.rectTransform;
            Vector2 start = _characterHome + Vector2.left * entryOffsetX;

            for (float t = 0f; t < entrySeconds; t += Time.unscaledDeltaTime)
            {
                // 끝에서 급히 멎어야 '자세를 잡았다'로 읽힌다
                float eased = 1f - Mathf.Pow(1f - t / entrySeconds, 3f);
                rect.anchoredPosition = Vector2.LerpUnclamped(start, _characterHome, eased);
                yield return null;
            }

            rect.anchoredPosition = _characterHome;
        }

        /// <summary>
        /// 한 획. 획은 좌우로 <b>번갈아</b> 긋는다 — 한 방향으로만 그으면 난도질이 아니라 빗금 무늬가 된다.
        /// 지나간 자리에는 상처가 남고, 사라지는 검기는 그 위를 스친 빛이다.
        /// </summary>
        private void Strike(int index, bool finisher)
        {
            if (slashTemplate == null || slashLayer == null)
                return;

            float scale = finisher ? finisherScale : 1f;
            float angle = (index % 2 == 0 ? 1f : -1f)
                * (slashAngle + Random.Range(-slashAngleJitter, slashAngleJitter));
            Vector2 center = BossCenter();
            if (!finisher)
                center += new Vector2(Random.Range(-hitScatter, hitScatter), Random.Range(-hitScatter, hitScatter));

            if (scarThickness > 0f)
                NewSlash(center, angle, slashLength * scale * 0.92f, scarThickness * scale, scarColor);

            Image slash = NewSlash(center, angle, slashLength * scale, slashThickness * scale, slashColor);
            StartCoroutine(SlashRoutine(slash, slashSeconds * (finisher ? 1.6f : 1f)));

            PlayFlash(finisher ? finisherFlashAlpha : hitFlashAlpha);
            PlayShake(finisher ? bossShakeDistance * 1.8f : bossShakeDistance);
        }

        private Vector2 BossCenter() =>
            bossImage != null ? bossImage.rectTransform.anchoredPosition : Vector2.zero;

        private Image NewSlash(Vector2 center, float angle, float length, float thickness, Color color)
        {
            Image slash = Instantiate(slashTemplate, slashLayer);
            RectTransform rect = slash.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            rect.localScale = Vector3.one;
            rect.sizeDelta = new Vector2(length, thickness);
            slash.color = color;
            slash.raycastTarget = false;
            slash.gameObject.SetActive(true);
            _spawned.Add(slash.gameObject);
            return slash;
        }

        /// <summary>굵기가 확 벌어졌다가 얇아지며 지워진다 — 칼이 지나간 잔상(SlashView와 같은 곡선).</summary>
        private IEnumerator SlashRoutine(Image slash, float seconds)
        {
            RectTransform rect = slash.rectTransform;
            float baseThickness = rect.sizeDelta.y;
            Color baseColor = slash.color;

            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = t / seconds;
                float width = k < 0.15f
                    ? Mathf.Lerp(0.25f, 1.4f, k / 0.15f)
                    : Mathf.Lerp(1.4f, 0.1f, (k - 0.15f) / 0.85f);
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, baseThickness * width);

                Color color = baseColor;
                color.a = baseColor.a * (1f - k * k); // 끝에서 급히 사라져야 잔상으로 읽힌다
                slash.color = color;
                yield return null;
            }

            _spawned.Remove(slash.gameObject);
            Destroy(slash.gameObject);
        }

        private void PlayFlash(float alpha)
        {
            if (flash == null || alpha <= 0f)
                return;

            if (_flashRoutine != null)
                StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(FlashRoutine(alpha));
        }

        private IEnumerator FlashRoutine(float alpha)
        {
            const float Seconds = 0.16f;
            for (float t = 0f; t < Seconds; t += Time.unscaledDeltaTime)
            {
                flash.color = new Color(1f, 1f, 1f, Mathf.Lerp(alpha, 0f, t / Seconds));
                yield return null;
            }

            flash.color = Color.clear;
            _flashRoutine = null;
        }

        private void PlayShake(float distance)
        {
            if (bossImage == null || distance <= 0f)
                return;

            if (_shakeRoutine != null)
                StopCoroutine(_shakeRoutine);
            _shakeRoutine = StartCoroutine(ShakeRoutine(distance));
        }

        private IEnumerator ShakeRoutine(float distance)
        {
            const float Seconds = 0.14f;
            RectTransform rect = bossImage.rectTransform;

            for (float t = 0f; t < Seconds; t += Time.unscaledDeltaTime)
            {
                float falloff = 1f - t / Seconds;
                rect.anchoredPosition = _bossHome + new Vector2(
                    Random.Range(-distance, distance) * falloff,
                    Random.Range(-distance, distance) * falloff);
                yield return null;
            }

            rect.anchoredPosition = _bossHome;
            _shakeRoutine = null;
        }

        /// <summary>일격을 맞은 보스가 밀려나며 지워진다. 상처만 남아 잠깐 떠 있다.</summary>
        private IEnumerator FadeBossOut()
        {
            if (bossImage == null)
                yield break;

            if (_shakeRoutine != null)
            {
                StopCoroutine(_shakeRoutine);
                _shakeRoutine = null;
            }

            const float Seconds = 0.35f;
            RectTransform rect = bossImage.rectTransform;
            Vector2 away = _bossHome + Vector2.right * 90f;

            for (float t = 0f; t < Seconds; t += Time.unscaledDeltaTime)
            {
                float k = t / Seconds;
                rect.anchoredPosition = Vector2.Lerp(_bossHome, away, k);
                bossImage.color = new Color(1f, 1f, 1f, 1f - k);
                yield return null;
            }

            bossImage.color = new Color(1f, 1f, 1f, 0f);
            rect.anchoredPosition = _bossHome;
        }

        /// <summary>
        /// 남은 검기·상처를 치운다. 다음 처형(2페이즈 보스)이 앞 판의 상처를 물려받으면 안 된다 —
        /// 시작할 때와 끝날 때 <b>양쪽에서</b> 부르는 이유다(연출이 중간에 끊겨도 다음이 깨끗하다).
        /// </summary>
        private void Cleanup()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null)
                    Destroy(_spawned[i]);
            _spawned.Clear();

            if (_flashRoutine != null)
            {
                StopCoroutine(_flashRoutine);
                _flashRoutine = null;
            }
            if (_shakeRoutine != null)
            {
                StopCoroutine(_shakeRoutine);
                _shakeRoutine = null;
            }

            if (flash != null)
                flash.color = Color.clear;
            if (line != null)
                line.text = string.Empty;
            if (bossImage != null)
            {
                bossImage.color = Color.white;
                bossImage.rectTransform.anchoredPosition = _bossHome;
            }
            if (characterImage != null)
                characterImage.rectTransform.anchoredPosition = _characterHome;
        }

        private IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
                yield return null;
        }
    }
}
