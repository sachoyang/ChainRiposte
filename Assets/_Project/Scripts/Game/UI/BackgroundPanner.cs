using UnityEngine;
using UnityEngine.UI;

namespace ChainRiposte.Game.UI
{
    /// <summary>
    /// 배경 그림을 <b>원본 비율 그대로</b> 화면을 덮도록 키우고(cover), 잘려 나간 폭 안에서 좌우로 천천히 왕복시킨다.
    /// 정지 그림 한 장이 살아 있는 것처럼 보이게 하는 최소 장치다.
    ///
    /// <para>UI(<see cref="Image"/>)와 월드(<see cref="SpriteRenderer"/>) 둘 다 붙는다 —
    /// 타이틀 배경은 캔버스 자식이고 월드맵 배경은 스프라이트라서 한쪽만으로는 모자란다.
    /// 덮을 대상은 UI면 <b>부모 RectTransform</b>, 월드면 <b>카메라가 보는 범위</b>다.</para>
    ///
    /// <para>남는 폭이 없으면(가로 화면에서 비율이 딱 맞는 등) <b>아무것도 움직이지 않는다</b> —
    /// 흔들 자리가 0이라 억지로 늘리지 않는다. 일부러 여유를 만들고 싶으면 <c>coverScale</c>을 1보다 올린다.</para>
    ///
    /// <para>Unity 기본 <c>AspectRatioFitter(EnvelopeParent)</c>를 쓰지 않는 이유: 그쪽은 레이아웃이 돌 때마다
    /// <c>anchoredPosition</c>을 0으로 되돌려 좌우 이동과 싸운다. 그래서 크기를 직접 계산한다.</para>
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class BackgroundPanner : MonoBehaviour
    {
        [Header("그림 (둘 중 있는 쪽을 쓴다 — 비우면 같은 오브젝트에서 찾는다)")]
        [SerializeField] private Image image;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [Tooltip("월드 모드에서 덮을 기준 카메라. 비우면 Camera.main.")]
        [SerializeField] private Camera targetCamera;

        [Header("움직임")]
        [Tooltip("좌 → 우 → 좌 한 바퀴에 걸리는 시간(초). 클수록 느리다.")]
        [SerializeField, Min(0.1f)] private float cycleSeconds = 24f;
        [Tooltip("남는 폭 중 실제로 쓸 비율. 1이면 잘린 끝까지 간다. 0이면 고정.")]
        [SerializeField, Range(0f, 1f)] private float amplitude = 1f;
        [Tooltip("덮는 크기의 배수. 1 = 딱 덮기. 1.05쯤으로 올리면 비율이 맞아떨어지는 화면에서도 흔들 여유가 생긴다.")]
        [SerializeField, Min(1f)] private float coverScale = 1f;
        [Tooltip("덮는 폭 대비 이 비율보다 적게 남으면 흔들지 않는다(떨림 방지).")]
        [SerializeField, Range(0f, 0.2f)] private float minOverflowRatio = 0.005f;
        [Tooltip("일시정지·히트스톱의 영향을 받지 않게 한다.")]
        [SerializeField] private bool unscaledTime = true;

        private RectTransform _rect;
        private RectTransform _parentRect;
        private Camera _camera;
        private Sprite _fittedSprite;
        private Vector2 _fittedArea;
        private float _halfTravel;
        private float _restZ;
        private Vector2 _restCenter;
        private float _time;

        private bool WorldMode => image == null && spriteRenderer != null;

        private void OnEnable()
        {
            _rect = transform as RectTransform;
            _parentRect = _rect != null ? _rect.parent as RectTransform : null;

            if (image == null)
                image = GetComponent<Image>();
            if (image == null && spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            if (Application.isPlaying)
                OrientationService.Changed += OnOrientationChanged;

            _fittedArea = Vector2.zero; // 다음 Update가 다시 맞춘다
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
                OrientationService.Changed -= OnOrientationChanged;
        }

        private void OnOrientationChanged(ScreenLayout layout) => _fittedArea = Vector2.zero;

        private void Update()
        {
            Sprite sprite = CurrentSprite();
            Vector2 area = CurrentArea();

            // 스프라이트 교체(테마 전환 포함)와 화면 크기 변화를 같은 자리에서 흡수한다 —
            // 누가 먼저 실행되든 상관없게 만들어 실행 순서에 기대지 않는다.
            if (sprite != _fittedSprite || area != _fittedArea)
                Refit(sprite, area);

            if (!Application.isPlaying || _halfTravel <= 0f)
                return;

            _time += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            // 사인이라 양 끝에서 저절로 느려진다 — 왕복이 기계적으로 보이지 않는다.
            float offsetX = _halfTravel * Mathf.Sin(_time * Mathf.PI * 2f / cycleSeconds);

            if (WorldMode)
                transform.position = new Vector3(_restCenter.x + offsetX, _restCenter.y, _restZ);
            else if (_rect != null)
                _rect.anchoredPosition = new Vector2(offsetX, 0f);
        }

        private Sprite CurrentSprite()
        {
            if (image != null)
                return image.sprite;
            return spriteRenderer != null ? spriteRenderer.sprite : null;
        }

        /// <summary>덮어야 하는 범위. UI는 부모 rect, 월드는 카메라가 보는 크기.</summary>
        private Vector2 CurrentArea()
        {
            if (WorldMode)
            {
                Camera cam = ResolveCamera();
                if (cam == null || !cam.orthographic)
                    return Vector2.zero;

                float height = cam.orthographicSize * 2f;
                return new Vector2(height * cam.aspect, height);
            }

            return _parentRect != null ? _parentRect.rect.size : Vector2.zero;
        }

        /// <summary>인스펙터 값이 우선. 캐시는 <b>직렬화하지 않는다</b> — 에디터에서 씬이 멋대로 더러워지지 않게.</summary>
        private Camera ResolveCamera()
        {
            if (targetCamera != null)
                return targetCamera;

            if (_camera == null)
                _camera = Camera.main;
            return _camera;
        }

        private void Refit(Sprite sprite, Vector2 area)
        {
            _fittedSprite = sprite;
            _fittedArea = area;
            _halfTravel = 0f;

            // 아트가 아직 없거나 화면 크기를 모르면 씬에 잡아둔 배치를 건드리지 않는다.
            if (sprite == null || area.x <= 0f || area.y <= 0f)
                return;

            Rect spriteRect = sprite.rect;
            if (spriteRect.width <= 0f || spriteRect.height <= 0f)
                return;

            float coveredWidth;

            if (WorldMode)
            {
                Vector2 spriteSize = sprite.bounds.size;
                if (spriteSize.x <= 0f || spriteSize.y <= 0f)
                    return;

                float scale = Mathf.Max(area.x / spriteSize.x, area.y / spriteSize.y) * coverScale;
                transform.localScale = new Vector3(scale, scale, 1f);

                Camera cam = ResolveCamera();
                _restZ = transform.position.z;
                _restCenter = cam != null ? (Vector2)cam.transform.position : Vector2.zero;
                transform.position = new Vector3(_restCenter.x, _restCenter.y, _restZ);

                coveredWidth = spriteSize.x * scale;
            }
            else
            {
                if (_rect == null)
                    return;

                Vector2 size = CoverSize(spriteRect.width / spriteRect.height, area) * coverScale;
                _rect.anchorMin = _rect.anchorMax = _rect.pivot = new Vector2(0.5f, 0.5f);
                _rect.sizeDelta = size;
                _rect.anchoredPosition = Vector2.zero;
                coveredWidth = size.x;
            }

            float overflow = coveredWidth - area.x;
            if (overflow > area.x * minOverflowRatio)
                _halfTravel = overflow * 0.5f * amplitude;
        }

        /// <summary>비율을 지키면서 <paramref name="area"/>를 덮는 가장 작은 크기.</summary>
        private static Vector2 CoverSize(float aspect, Vector2 area)
        {
            float width = area.x;
            float height = width / aspect;
            if (height < area.y)
            {
                height = area.y;
                width = height * aspect;
            }

            return new Vector2(width, height);
        }
    }
}
