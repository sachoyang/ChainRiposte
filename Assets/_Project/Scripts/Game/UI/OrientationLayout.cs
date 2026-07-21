using System;
using UnityEngine;

namespace ChainRiposte.Game.UI
{
    /// <summary>
    /// UI 요소 하나의 <b>방향별 배치</b> (GDD §9.3). 세로/가로 프리셋을 각각 들고 있다가
    /// 화면 방향이 바뀌면 해당 프리셋을 RectTransform에 적용한다.
    ///
    /// 작업 방식은 씬 오서링 그대로 — <b>씬 뷰에서 원하는 대로 드래그해 배치한 뒤
    /// 인스펙터의 "현재 배치를 ○○ 프리셋으로 저장" 버튼</b>을 누른다. 코드 수정은 필요 없다.
    /// (예: 전투 2버튼 = 세로는 하단 좌우, 가로는 양쪽 사이드)
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public sealed class OrientationLayout : MonoBehaviour
    {
        /// <summary>RectTransform 배치 한 벌.</summary>
        [Serializable]
        public struct Preset
        {
            public Vector2 anchorMin;
            public Vector2 anchorMax;
            public Vector2 pivot;
            public Vector2 anchoredPosition;
            public Vector2 sizeDelta;
            public Vector3 scale;
        }

        [Tooltip("세로(Portrait) 화면일 때의 배치")]
        [SerializeField] private Preset portrait;

        [Tooltip("가로(Landscape) 화면일 때의 배치")]
        [SerializeField] private Preset landscape;

        [Tooltip("끄면 위치/크기만 적용하고 스케일은 씬 값 그대로 둔다")]
        [SerializeField] private bool applyScale = true;

        [Tooltip("프리셋을 한 번이라도 저장했는가. false면 아무것도 적용하지 않는다(빈 프리셋으로 배치가 날아가는 사고 방지)")]
        [SerializeField] private bool configured;

        private RectTransform _rect;

        private RectTransform Rect
        {
            get
            {
                if (_rect == null)
                    _rect = (RectTransform)transform;
                return _rect;
            }
        }

        private void OnEnable()
        {
            OrientationService.Changed += Apply;
            Apply(OrientationService.Current);
        }

        private void OnDisable() => OrientationService.Changed -= Apply;

        public void Apply(ScreenLayout layout)
        {
            if (!configured)
                return;

            ApplyPreset(layout == ScreenLayout.Landscape ? landscape : portrait);
        }

        private void ApplyPreset(Preset preset)
        {
            Rect.anchorMin = preset.anchorMin;
            Rect.anchorMax = preset.anchorMax;
            Rect.pivot = preset.pivot;
            Rect.anchoredPosition = preset.anchoredPosition;
            Rect.sizeDelta = preset.sizeDelta;
            if (applyScale && preset.scale != Vector3.zero)
                Rect.localScale = preset.scale;
        }

        private Preset CaptureCurrent() => new()
        {
            anchorMin = Rect.anchorMin,
            anchorMax = Rect.anchorMax,
            pivot = Rect.pivot,
            anchoredPosition = Rect.anchoredPosition,
            sizeDelta = Rect.sizeDelta,
            scale = Rect.localScale,
        };

#if UNITY_EDITOR
        /// <summary>컴포넌트를 붙인 순간 — 지금 배치를 양쪽 프리셋의 출발점으로 삼는다.</summary>
        private void Reset() => CaptureBothEditorOnly();

        /// <summary>에디터 전용 — 현재 배치를 한쪽 프리셋으로 저장한다.</summary>
        public void CaptureEditorOnly(ScreenLayout layout)
        {
            if (layout == ScreenLayout.Landscape)
                landscape = CaptureCurrent();
            else
                portrait = CaptureCurrent();
            configured = true;
        }

        /// <summary>에디터 전용 — 현재 배치를 양쪽 프리셋에 함께 저장한다 (빌더/초기값).</summary>
        public void CaptureBothEditorOnly()
        {
            portrait = landscape = CaptureCurrent();
            configured = true;
        }

        /// <summary>에디터 전용 — 가로 프리셋만 직접 지정한다 (빌더가 기본 가로 배치를 깔 때).</summary>
        public void SetLandscapeEditorOnly(
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            landscape = new Preset
            {
                anchorMin = anchorMin,
                anchorMax = anchorMax,
                pivot = pivot,
                anchoredPosition = anchoredPosition,
                sizeDelta = sizeDelta,
                scale = Vector3.one,
            };
            configured = true;
        }

        /// <summary>에디터 전용 — 프리셋을 씬에 즉시 적용해 눈으로 확인한다.</summary>
        public void PreviewEditorOnly(ScreenLayout layout)
        {
            if (!configured)
                return;
            ApplyPreset(layout == ScreenLayout.Landscape ? landscape : portrait);
        }
#endif
    }
}
