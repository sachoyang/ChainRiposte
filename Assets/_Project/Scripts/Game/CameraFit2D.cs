using UnityEngine;

namespace ChainRiposte.Game
{
    /// <summary>직교 카메라를 주어진 월드 영역(보드)이 화면에 들어오도록 맞춘다. 세로/가로 화면 모두 대응.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFit2D : MonoBehaviour
    {
        [Tooltip("보드 가장자리 여백 (월드 유닛)")]
        [SerializeField, Min(0f)] private float margin = 1f;

        public void FitTo(Bounds worldBounds)
        {
            var cam = GetComponent<Camera>();
            cam.orthographic = true;

            float halfWidth = worldBounds.extents.x + margin;
            float halfHeight = worldBounds.extents.y + margin;
            cam.orthographicSize = Mathf.Max(halfHeight, halfWidth / cam.aspect);

            transform.position = new Vector3(worldBounds.center.x, worldBounds.center.y, -10f);
        }
    }
}
