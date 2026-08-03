using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ChainRiposte.Game.UI
{
    /// <summary>
    /// NPC가 "지금 너를 강화했다"고 알려 주는 한 번짜리 반응.
    ///
    /// 지금은 정지 그림뿐이라 <b>코드로</b> 튀어오르고 번쩍인다.
    /// 나중에 스프라이트 시트가 생기면 <see cref="animator"/> 슬롯만 채우면 되고 —
    /// 애니메이터가 있으면 그쪽이 우선이라 이 스크립트를 지울 필요가 없다.
    /// </summary>
    public sealed class NpcReaction : MonoBehaviour
    {
        [Header("대상 (비우면 자기 자신에서 찾는다)")]
        [SerializeField] private RectTransform body;
        [Tooltip("번쩍일 그림")]
        [SerializeField] private Graphic tintTarget;

        [Header("애니메이터가 있으면 이쪽을 쓴다")]
        [SerializeField] private Animator animator;
        [SerializeField] private string animatorTrigger = "React";

        [Header("반응 자세 (비우면 그림을 안 바꾼다)")]
        [Tooltip("반응하는 동안 잠깐 갈아 끼울 그림. 예: 대장장이가 망치를 치켜든 blacksmith2")]
        [SerializeField] private Sprite reactSprite;
        [Tooltip("갈아 끼울 Image — 비우면 번쩍일 그림에서 찾는다")]
        [SerializeField] private Image spriteTarget;
        [Tooltip("반응 길이 중 어디서 갈아 끼우고 어디서 되돌릴지 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float swapInAt = 0.15f;
        [SerializeField, Range(0f, 1f)] private float swapOutAt = 0.75f;
        [Tooltip("두 그림의 도트 크기를 같게 맞춘다. 자세마다 잘린 여백이 달라도 안 튄다.")]
        [SerializeField] private bool matchPixelScale = true;

        [Header("코드 반응")]
        [SerializeField, Min(0.05f)] private float duration = 0.45f;
        [Tooltip("최대로 커지는 배율")]
        [SerializeField, Min(1f)] private float punchScale = 1.22f;
        [Tooltip("위로 튀어오르는 높이(px)")]
        [SerializeField, Min(0f)] private float hopHeight = 26f;
        [Tooltip("번쩍임 색 — 흰색에 가까울수록 성스럽고, 붉을수록 벼려지는 느낌")]
        [SerializeField] private Color flashColor = new(1f, 0.95f, 0.75f, 1f);

        private Coroutine _routine;
        private Vector2 _restPosition;
        private Vector3 _restScale;
        private Color _restColor;
        private Sprite _restSprite;
        private Vector2 _restSize;
        private bool _swapped;
        private bool _captured;

        private void Awake()
        {
            if (body == null)
                body = transform as RectTransform;
            if (tintTarget == null)
                tintTarget = GetComponent<Graphic>();
            if (spriteTarget == null)
                spriteTarget = tintTarget as Image;
            if (spriteTarget == null)
                spriteTarget = GetComponent<Image>();
        }

        private void OnEnable() => Capture();

        private void OnDisable()
        {
            // 반응 도중에 패널이 꺼지면 뒤틀린 상태로 남는다 — 껐다 켤 때 원래대로 돌려놓는다.
            _routine = null;
            Restore();
        }

        /// <summary>반응 한 번. 연달아 부르면 앞의 것을 끊고 처음부터 다시 한다.</summary>
        public void Play()
        {
            if (!isActiveAndEnabled)
                return;

            if (animator != null && !string.IsNullOrEmpty(animatorTrigger))
            {
                animator.SetTrigger(animatorTrigger);
                return;
            }

            Capture();
            if (_routine != null)
                StopCoroutine(_routine);
            _routine = StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float n = Mathf.Clamp01(t / duration);

                // 앞이 빠르고 뒤가 느린 한 번의 산 — 0 → 1 → 0
                float k = Mathf.Sin(n * Mathf.PI);
                float ease = k * k;

                if (body != null)
                {
                    body.localScale = Vector3.one * Mathf.Lerp(1f, punchScale, ease) * _restScale.x;
                    body.anchoredPosition = _restPosition + new Vector2(0f, hopHeight * ease);
                }

                if (tintTarget != null)
                    tintTarget.color = Color.Lerp(_restColor, flashColor, ease * 0.8f);

                // 갈아 끼우는 순간을 산의 양 끝에서 <b>안쪽으로</b> 들여놓는다 — 커지고 번쩍이는
                // 도중에 바뀌어야 자세가 바뀐 것으로 읽힌다. 멈춰 있을 때 바꾸면 그림이 툭 튄다.
                ApplySwap(n >= swapInAt && n < swapOutAt);

                yield return null;
            }

            _routine = null;
            Restore();
        }

        /// <summary>쉬는 상태(위치·크기·색)를 한 번만 기록한다. 반응 중에 덮어쓰면 자리가 밀린다.</summary>
        private void Capture()
        {
            if (_captured || _routine != null)
                return;

            _captured = true;
            if (body != null)
            {
                _restPosition = body.anchoredPosition;
                _restScale = body.localScale;
                if (_restScale == Vector3.zero)
                    _restScale = Vector3.one;
            }

            if (tintTarget != null)
                _restColor = tintTarget.color;

            if (spriteTarget != null)
            {
                _restSprite = spriteTarget.sprite;
                _restSize = spriteTarget.rectTransform.sizeDelta;
            }
        }

        private void Restore()
        {
            if (!_captured)
                return;

            if (body != null)
            {
                body.localScale = _restScale;
                body.anchoredPosition = _restPosition;
            }

            if (tintTarget != null)
                tintTarget.color = _restColor;

            ApplySwap(false);
        }

        /// <summary>
        /// 반응 자세로 갈아 끼우거나 되돌린다. <b>바뀔 때만</b> 손대므로 매 프레임 불러도 된다.
        /// <see cref="reactSprite"/>가 비어 있으면 한 번도 켜지지 않고, 따라서 되돌릴 것도 없다 —
        /// 자세 그림이 없는 NPC(성녀)의 그림을 이 코드가 건드리는 일은 생기지 않는다.
        /// </summary>
        private void ApplySwap(bool on)
        {
            on &= reactSprite != null && spriteTarget != null;
            if (on == _swapped)
                return;

            _swapped = on;
            spriteTarget.sprite = on ? reactSprite : _restSprite;

            // preserveAspect는 그림을 <b>제각각</b> 상자에 맞춘다. 자세마다 잘린 여백이 다르면
            // (망치를 든 쪽이 더 좁게 잘린다) 갈아 끼는 순간 도트가 통째로 커진다.
            // 상자를 그림 크기에 비례해 함께 줄이면 도트 크기가 그대로 남는다.
            if (matchPixelScale && spriteTarget.preserveAspect && _restSprite != null)
            {
                float restMax = Mathf.Max(_restSprite.rect.width, _restSprite.rect.height);
                Sprite shown = on ? reactSprite : _restSprite;
                float shownMax = Mathf.Max(shown.rect.width, shown.rect.height);

                if (restMax > 0f && shownMax > 0f)
                    spriteTarget.rectTransform.sizeDelta = _restSize * (shownMax / restMax);
            }
        }

        /// <summary>그림을 갈아 끼운 뒤 그 색을 '쉬는 색'으로 다시 잡는다.</summary>
        public void ResetRestColor(Color color)
        {
            _restColor = color;
            if (_routine == null && tintTarget != null)
                tintTarget.color = color;
        }

        /// <summary>
        /// 밖에서 그림을 갈아 끼웠을 때(성녀는 고른 캐릭터를 따라간다) '쉬는 그림'도 같이 옮긴다.
        /// 안 옮기면 반응이 끝날 때 <b>지난 캐릭터의 그림</b>으로 되돌아간다.
        /// 반응 자세로 바뀌어 있는 동안은 무시한다 — 그 그림을 쉬는 그림으로 굳히면 안 된다.
        /// </summary>
        public void ResetRestSprite(Sprite sprite)
        {
            if (!_swapped)
                _restSprite = sprite;
        }
    }
}
