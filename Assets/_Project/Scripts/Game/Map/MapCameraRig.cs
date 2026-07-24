using ChainRiposte.Game.UI;
using UnityEngine;

namespace ChainRiposte.Game.Map
{
    /// <summary>
    /// 월드맵 카메라의 화면 잡기. <b>세로와 가로가 서로 다른 화면</b>이라 한쪽 규칙으로는 안 된다.
    ///
    /// <list type="bullet">
    /// <item><b>세로</b> — 길을 통째로 보여주지 않고 위(배경 띠)·아래(정보 띠) 사이의 <b>가운데 창</b>에만 담고,
    /// 서 있는 노드를 따라 위아래로 스크롤한다. 카메라 중심은 화면 한가운데라 창의 중심과 어긋나므로,
    /// 그 차이만큼 밀어 주는 게 이 컴포넌트의 핵심 계산이다.</item>
    /// <item><b>가로</b> — 지금까지처럼 <see cref="CameraFit2D"/>가 길 전체를 한 화면에 담는다. 스크롤은 없다.</item>
    /// </list>
    ///
    /// <para>띠의 비율은 <b>씬의 UI에서 직접 잰다</b>(<c>topBand</c>/<c>bottomBand</c>). 숫자를 양쪽에
    /// 적어 두면 씬에서 띠 높이를 고친 순간 창의 중심이 어긋나기 때문이다. 참조를 비우면 아래 기본값을 쓴다.</para>
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class MapCameraRig : MonoBehaviour
    {
        [Tooltip("가로 화면을 맡는다. 세로에서는 꺼서 서로 카메라를 다투지 않게 한다.")]
        [SerializeField] private CameraFit2D cameraFit;

        [Header("띠 (세로에서 길을 가리는 UI — 비우면 아래 비율을 쓴다)")]
        [SerializeField] private RectTransform topBand;
        [SerializeField] private RectTransform bottomBand;

        [Header("세로 — 길의 일부만 보이고 따라 올라간다")]
        [Tooltip("세로에서 화면에 담을 가로 폭(월드 유닛). 좁힐수록 확대되고 스크롤이 길어진다.")]
        [SerializeField, Min(1f)] private float portraitViewWidth = 7f;
        [Tooltip("topBand 를 비웠을 때 쓸 위쪽 띠 비율")]
        [SerializeField, Range(0f, 0.8f)] private float topBandRatio = 0.32f;
        [Tooltip("bottomBand 를 비웠을 때 쓸 아래쪽 띠 비율")]
        [SerializeField, Range(0f, 0.8f)] private float bottomBandRatio = 0.19f;
        [Tooltip("따라가는 부드러움(초). 0이면 즉시 따라붙는다.")]
        [SerializeField, Min(0f)] private float followSmoothTime = 0.35f;
        [Tooltip("길 끝에서 남기는 여백(월드 유닛)")]
        [SerializeField, Min(0f)] private float verticalPadding = 1f;

        private Camera _camera;
        private Bounds _bounds;
        private Vector3 _focus;
        private bool _hasBounds;
        private bool _snap;
        private float _velocity;
        private ScreenLayout _appliedLayout = (ScreenLayout)(-1);

        private void Awake() => _camera = GetComponent<Camera>();

        /// <summary>길 전체 범위를 알려 주고 지금 볼 지점을 정한다. 컨트롤러가 시작할 때 한 번.</summary>
        public void Frame(Bounds bounds, Vector3 focus, bool instant)
        {
            _bounds = bounds;
            _focus = focus;
            _hasBounds = true;
            _snap |= instant;
            _appliedLayout = (ScreenLayout)(-1); // 다음 틱에서 방향 규칙을 새로 적용한다
        }

        /// <summary>볼 지점만 바꾼다 — 캐릭터가 노드를 하나 지날 때마다.</summary>
        public void Focus(Vector3 point, bool instant = false)
        {
            _focus = point;
            _snap |= instant;
        }

        /// <summary>
        /// 계산을 전부 LateUpdate에 모은 이유: 띠 높이를 UI에서 재는데, 방향 전환 때
        /// <see cref="OrientationLayout"/>과 실행 순서를 다투면 한 프레임 어긋난 값을 읽게 된다.
        /// </summary>
        private void LateUpdate()
        {
            if (!_hasBounds)
                return;

            ScreenLayout layout = OrientationService.Current;
            bool entered = layout != _appliedLayout;
            _appliedLayout = layout;

            if (layout == ScreenLayout.Landscape)
                TickLandscape(entered);
            else
                TickPortrait(entered || _snap);

            _snap = false;
        }

        /// <summary>가로는 예전 그대로 — 길 전체를 한 화면에. 이후 창 크기 변화는 CameraFit2D가 알아서 한다.</summary>
        private void TickLandscape(bool entered)
        {
            if (cameraFit == null || !entered)
                return;

            cameraFit.enabled = true;
            cameraFit.FitTo(_bounds);
        }

        private void TickPortrait(bool snap)
        {
            if (_camera == null)
                _camera = GetComponent<Camera>();
            if (cameraFit != null && cameraFit.enabled)
                cameraFit.enabled = false; // 카메라를 둘이 동시에 만지면 서로 되돌린다

            _camera.orthographic = true;
            float viewHeight = portraitViewWidth / Mathf.Max(_camera.aspect, 0.01f);
            _camera.orthographicSize = viewHeight * 0.5f;

            float top = BandRatio(topBand, topBandRatio);
            float bottom = BandRatio(bottomBand, bottomBandRatio);
            float windowRatio = Mathf.Max(0.05f, 1f - top - bottom);
            float windowHeight = viewHeight * windowRatio;
            float windowCenter01 = bottom + windowRatio * 0.5f;

            // 보이는 창의 중심이 놓일 월드 Y. 길 밖이 드러나지 않게 가둔다 —
            // 길이 창보다 짧으면 가둘 게 없으므로 그냥 가운데에 놓는다.
            float min = _bounds.min.y - verticalPadding + windowHeight * 0.5f;
            float max = _bounds.max.y + verticalPadding - windowHeight * 0.5f;
            float windowY = min > max ? _bounds.center.y : Mathf.Clamp(_focus.y, min, max);

            // 카메라 중심은 늘 화면 한가운데(0.5)라, 창 중심과의 차이만큼 밀어 준다.
            float targetY = windowY + (0.5f - windowCenter01) * viewHeight;

            float y = snap || followSmoothTime <= 0f
                ? targetY
                : Mathf.SmoothDamp(transform.position.y, targetY, ref _velocity, followSmoothTime);
            if (snap)
                _velocity = 0f;

            transform.position = new Vector3(_bounds.center.x, y, -10f);
        }

        /// <summary>띠가 화면에서 차지하는 세로 비율. 참조가 없으면 인스펙터 값으로 떨어진다.</summary>
        private static float BandRatio(RectTransform band, float fallback)
        {
            if (band == null || !band.gameObject.activeInHierarchy)
                return fallback;

            var canvas = band.GetComponentInParent<Canvas>();
            if (canvas == null)
                return fallback;

            float canvasHeight = ((RectTransform)canvas.transform).rect.height;
            if (canvasHeight <= 0f)
                return fallback;

            return Mathf.Clamp01(band.rect.height / canvasHeight);
        }
    }
}
