using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ChainRiposte.Game.Juice
{
    /// <summary>
    /// 다크 판타지 톤의 포스트 프로세싱 프로토타입 — 필름 그레인 + 비네팅 (GDD §7).
    /// 프로필 에셋 없이 런타임에 전역 Volume을 조립한다 (에셋 단계에서 프로필 에셋으로 교체 예정).
    /// </summary>
    public sealed class PostFxBootstrap : MonoBehaviour
    {
        [Header("비네팅 — 화면 가장자리를 어둡게")]
        [SerializeField, Range(0f, 1f)] private float vignetteIntensity = 0.32f;
        [SerializeField, Range(0.01f, 1f)] private float vignetteSmoothness = 0.45f;

        [Header("필름 그레인 — B급 거친 질감")]
        [SerializeField, Range(0f, 1f)] private float grainIntensity = 0.35f;
        [SerializeField] private FilmGrainLookup grainType = FilmGrainLookup.Medium1;

        private VolumeProfile _profile;

        private void Awake()
        {
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();

            var vignette = _profile.Add<Vignette>();
            vignette.intensity.Override(vignetteIntensity);
            vignette.smoothness.Override(vignetteSmoothness);

            var grain = _profile.Add<FilmGrain>();
            grain.type.Override(grainType);
            grain.intensity.Override(grainIntensity);

            var volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.profile = _profile;

            // 메인 카메라의 포스트 프로세싱 활성화
            Camera main = Camera.main;
            if (main != null && main.TryGetComponent(out UniversalAdditionalCameraData cameraData))
                cameraData.renderPostProcessing = true;
        }

        private void OnDestroy()
        {
            if (_profile != null)
                Destroy(_profile);
        }
    }
}
