using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ChainRiposte.Game.UI
{
    /// <summary>
    /// 준비 화면 위쪽에 보스가 <b>그림자로 다가오는</b> 예고 연출. 정지 그림 한 장을
    /// 어둡게 칠하고, 위에서 조금씩 내려오며 커지고 짙어진다 — "곧 저게 온다"는 압박.
    ///
    /// <para><see cref="NpcReaction"/>과 같은 결 — 스프라이트뿐이라 코드로 움직인다.
    /// 나중에 실제 애니메이션이 생기면 이 스크립트를 지우고 그쪽을 쓰면 된다.</para>
    ///
    /// 그림은 매 판 바뀌므로(테마·스테이지) <see cref="Show"/>로 넣어 준다. 없으면 숨는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossShadow : MonoBehaviour
    {
        [Tooltip("비우면 같은 오브젝트의 Image")]
        [SerializeField] private Image image;

        [Header("그림자 색 — 실루엣이라 원색을 죽이고 어둡게")]
        [SerializeField] private Color shadowColor = new(0f, 0f, 0f, 0.55f);

        [Header("다가오는 연출")]
        [Tooltip("들어오는 데 걸리는 시간(초)")]
        [SerializeField, Min(0.1f)] private float approachSeconds = 1.6f;
        [Tooltip("시작 크기 → 도착 크기 (멀리 있다 다가오므로 작게 시작)")]
        [SerializeField] private float startScale = 0.82f;
        [SerializeField] private float endScale = 1f;
        [Tooltip("시작 Y 오프셋 → 도착 Y 오프셋 (위에서 내려온다)")]
        [SerializeField] private float startOffsetY = 120f;
        [SerializeField] private float endOffsetY = 0f;
        [Tooltip("시작 투명도 → 도착 투명도 배수 (옅게 나타나 짙어진다)")]
        [SerializeField, Range(0f, 1f)] private float startAlphaScale = 0.15f;

        [Header("도착 후 숨쉬기 — 살아 있는 느낌")]
        [Tooltip("위아래로 떠다니는 폭(px). 0이면 정지")]
        [SerializeField, Min(0f)] private float idleBobAmplitude = 10f;
        [Tooltip("한 번 떠다니는 주기(초)")]
        [SerializeField, Min(0.1f)] private float idleBobPeriod = 2.4f;

        private RectTransform _rect;
        private Vector2 _homePosition;
        private Coroutine _routine;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _homePosition = _rect.anchoredPosition;
            if (image == null)
                image = GetComponent<Image>();
        }

        /// <summary>이 판의 보스 그림자를 띄운다. 스프라이트가 없으면 숨긴다.</summary>
        public void Show(Sprite bossSprite)
        {
            if (image == null)
                return;

            if (bossSprite == null)
            {
                Hide();
                return;
            }

            gameObject.SetActive(true);
            image.sprite = bossSprite;

            if (_routine != null)
                StopCoroutine(_routine);
            _routine = StartCoroutine(Approach());
        }

        public void Hide()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            gameObject.SetActive(false);
        }

        private IEnumerator Approach()
        {
            // 준비 화면은 시간 압박이 없는 구간이라, 일시정지 여부와 무관하게 흐르는 unscaled 시간을 쓴다.
            for (float t = 0f; t < approachSeconds; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / approachSeconds);
                ApplyState(k, bob: 0f);
                yield return null;
            }

            // 도착 후에는 제자리에서 천천히 숨만 쉰다.
            float elapsed = 0f;
            while (true)
            {
                elapsed += Time.unscaledDeltaTime;
                float bob = idleBobAmplitude * Mathf.Sin(elapsed * Mathf.PI * 2f / idleBobPeriod);
                ApplyState(1f, bob);
                yield return null;
            }
        }

        /// <summary>진행도 <paramref name="k"/> (0=멀리, 1=도착) 에 맞춰 크기·위치·투명도를 잡는다.</summary>
        private void ApplyState(float k, float bob)
        {
            float scale = Mathf.Lerp(startScale, endScale, k);
            _rect.localScale = new Vector3(scale, scale, 1f);

            float offsetY = Mathf.Lerp(startOffsetY, endOffsetY, k);
            _rect.anchoredPosition = _homePosition + new Vector2(0f, offsetY + bob);

            Color color = shadowColor;
            color.a = shadowColor.a * Mathf.Lerp(startAlphaScale, 1f, k);
            image.color = color;
        }
    }
}
