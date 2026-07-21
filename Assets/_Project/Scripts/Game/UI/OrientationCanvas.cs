using UnityEngine;
using UnityEngine.UI;

namespace ChainRiposte.Game.UI
{
    /// <summary>
    /// 캔버스의 기준 해상도를 방향에 맞게 바꾼다 (GDD §9.3).
    /// 세로 1080×1920 / 가로 1920×1080이 기본 — 이게 없으면 가로에서 UI가 통째로 확대돼 보인다.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    [DisallowMultipleComponent]
    public sealed class OrientationCanvas : MonoBehaviour
    {
        [SerializeField] private Vector2 portraitReference = new(1080f, 1920f);
        [SerializeField] private Vector2 landscapeReference = new(1920f, 1080f);

        [Tooltip("CanvasScaler의 Match (0=폭 기준, 1=높이 기준)")]
        [SerializeField, Range(0f, 1f)] private float portraitMatch = 0.5f;
        [SerializeField, Range(0f, 1f)] private float landscapeMatch = 0.5f;

        private CanvasScaler _scaler;

        private void OnEnable()
        {
            _scaler = GetComponent<CanvasScaler>();
            OrientationService.Changed += Apply;
            Apply(OrientationService.Current);
        }

        private void OnDisable() => OrientationService.Changed -= Apply;

        private void Apply(ScreenLayout layout)
        {
            if (_scaler == null)
                return;

            bool landscape = layout == ScreenLayout.Landscape;
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = landscape ? landscapeReference : portraitReference;
            _scaler.matchWidthOrHeight = landscape ? landscapeMatch : portraitMatch;
        }
    }
}
