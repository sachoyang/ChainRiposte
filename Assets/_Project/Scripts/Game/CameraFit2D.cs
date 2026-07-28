using UnityEngine;

namespace ChainRiposte.Game
{
    /// <summary>
    /// 직교 카메라를 주어진 월드 영역(보드/맵)이 화면에 들어오도록 맞춘다. 세로/가로 화면 모두 대응.
    /// 화면 크기가 바뀌면(회전·창 크기 변경) 마지막 영역으로 자동 재조정한다 (GDD §9.3).
    ///
    /// <para><b><see cref="viewportRect"/>를 꽂으면 화면 전체가 아니라 그 사각형 안에 맞춘다.</b>
    /// 보드는 데이터로 크기가 정해지므로(5×5든 10×10이든) 스케일은 알아서 줄지만, 어디에 놓일지는
    /// 화면 전체를 기준으로 잡혀 <b>HUD 띠 밑에 깔린다</b>. 놓일 자리를 씬의 사각형으로 못박아 두면
    /// 보드 크기가 바뀌어도 UI를 침범하지 않고, 그 자리를 드래그해서 눈으로 맞출 수 있다.</para>
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFit2D : MonoBehaviour
    {
        [Tooltip("가장자리 여백 (월드 유닛)")]
        [SerializeField, Min(0f)] private float margin = 1f;

        [Tooltip("보드가 들어갈 화면 영역. 비우면 화면 전체를 쓴다(예전 동작). " +
            "HUD 띠를 피하려면 그 띠들 사이를 덮는 빈 RectTransform 을 만들어 꽂는다 — " +
            "Tools ▸ ChainRiposte ▸ Add Board Area To Main 이 하나 만들어 준다.")]
        [SerializeField] private RectTransform viewportRect;

        private Bounds _bounds;
        private bool _hasBounds;
        private int _lastWidth;
        private int _lastHeight;
        private Rect _lastViewport;

        public void FitTo(Bounds worldBounds)
        {
            _bounds = worldBounds;
            _hasBounds = true;
            Apply();
        }

        private void LateUpdate()
        {
            if (!_hasBounds)
                return;

            // 화면 크기뿐 아니라 영역 자체도 매 프레임 본다 — 방향 전환 시 OrientationLayout 이
            // 사각형을 옮기므로, 실행 순서에 기대지 않고 바뀐 것을 보고 따라간다.
            Rect viewport = ResolveViewport();
            if (Screen.width == _lastWidth && Screen.height == _lastHeight && viewport == _lastViewport)
                return;

            Apply();
        }

        private void Apply()
        {
            var cam = GetComponent<Camera>();
            cam.orthographic = true;

            Rect viewport = ResolveViewport();
            float halfWidth = _bounds.extents.x + margin;
            float halfHeight = _bounds.extents.y + margin;

            // 영역이 화면의 일부면 그만큼 더 줌아웃해야 그 안에 다 들어간다.
            float size = Mathf.Max(halfHeight / viewport.height, halfWidth / (cam.aspect * viewport.width));
            cam.orthographicSize = size;

            // 카메라 중심은 화면 한가운데(0.5, 0.5)다. 영역의 중심이 그와 어긋난 만큼 밀어 줘야
            // 보드가 영역 한가운데에 온다 — 이 한 줄이 "UI를 침범하지 않는다"의 전부다.
            float offsetX = (viewport.center.x - 0.5f) * 2f * size * cam.aspect;
            float offsetY = (viewport.center.y - 0.5f) * 2f * size;
            transform.position = new Vector3(_bounds.center.x - offsetX, _bounds.center.y - offsetY, -10f);

            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            _lastViewport = viewport;
        }

        /// <summary>보드를 넣을 영역을 화면 비율(0~1)로. 사각형이 없거나 납작하면 화면 전체로 떨어진다.</summary>
        private Rect ResolveViewport()
        {
            var full = new Rect(0f, 0f, 1f, 1f);
            if (viewportRect == null || Screen.width <= 0 || Screen.height <= 0)
                return full;

            var corners = new Vector3[4];
            viewportRect.GetWorldCorners(corners);

            Canvas canvas = viewportRect.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            Vector2 min = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]);

            var rect = new Rect(
                Mathf.Min(min.x, max.x) / Screen.width,
                Mathf.Min(min.y, max.y) / Screen.height,
                Mathf.Abs(max.x - min.x) / Screen.width,
                Mathf.Abs(max.y - min.y) / Screen.height);

            // 배선을 덜 했거나 레이아웃이 아직 안 잡힌 프레임에 0으로 나누지 않는다.
            return rect.width < 0.01f || rect.height < 0.01f ? full : rect;
        }
    }
}
