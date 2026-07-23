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
        private bool _captured;

        private void Awake()
        {
            if (body == null)
                body = transform as RectTransform;
            if (tintTarget == null)
                tintTarget = GetComponent<Graphic>();
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
                // 앞이 빠르고 뒤가 느린 한 번의 산 — 0 → 1 → 0
                float k = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
                float ease = k * k;

                if (body != null)
                {
                    body.localScale = Vector3.one * Mathf.Lerp(1f, punchScale, ease) * _restScale.x;
                    body.anchoredPosition = _restPosition + new Vector2(0f, hopHeight * ease);
                }

                if (tintTarget != null)
                    tintTarget.color = Color.Lerp(_restColor, flashColor, ease * 0.8f);

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
        }

        /// <summary>그림을 갈아 끼운 뒤 그 색을 '쉬는 색'으로 다시 잡는다.</summary>
        public void ResetRestColor(Color color)
        {
            _restColor = color;
            if (_routine == null && tintTarget != null)
                tintTarget.color = color;
        }
    }
}
