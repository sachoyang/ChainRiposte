using UnityEngine;

namespace ChainRiposte.Game
{
    /// <summary>
    /// 직교 카메라를 주어진 월드 영역(보드/맵)이 화면에 들어오도록 맞춘다. 세로/가로 화면 모두 대응.
    /// 화면 크기가 바뀌면(회전·창 크기 변경) 마지막 영역으로 자동 재조정한다 (GDD §9.3).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFit2D : MonoBehaviour
    {
        [Tooltip("가장자리 여백 (월드 유닛)")]
        [SerializeField, Min(0f)] private float margin = 1f;

        private Bounds _bounds;
        private bool _hasBounds;
        private int _lastWidth;
        private int _lastHeight;

        public void FitTo(Bounds worldBounds)
        {
            _bounds = worldBounds;
            _hasBounds = true;
            Apply();
        }

        private void Update()
        {
            if (!_hasBounds || (Screen.width == _lastWidth && Screen.height == _lastHeight))
                return;

            Apply();
        }

        private void Apply()
        {
            var cam = GetComponent<Camera>();
            cam.orthographic = true;

            float halfWidth = _bounds.extents.x + margin;
            float halfHeight = _bounds.extents.y + margin;
            cam.orthographicSize = Mathf.Max(halfHeight, halfWidth / cam.aspect);

            transform.position = new Vector3(_bounds.center.x, _bounds.center.y, -10f);

            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
        }
    }
}
