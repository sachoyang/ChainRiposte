using System.Collections;
using ChainRiposte.Game.Audio;
using ChainRiposte.Game.Characters;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace ChainRiposte.Game.UI
{
    /// <summary>
    /// 마지막 고리를 끊었을 때 나오는 <b>캐릭터별 엔딩 영상</b>.
    /// 영상 파일은 기획이 넣는다 — 여기 있는 것은 <b>넣을 자리와 분기</b>뿐이다.
    ///
    /// <para><b>없으면 그냥 넘어간다.</b> 한 캐릭터의 영상이 아직 없다고 그 캐릭터로는 게임을 끝낼 수
    /// 없게 되면 안 된다. 클립이 비었으면 <see cref="Play"/>가 곧바로 끝나고 호출한 쪽이 다음으로 간다.</para>
    ///
    /// <para><b>언제나 스킵할 수 있다.</b> 두 번째부터는 이미 본 영상이고, 영상이 안 끝나서 게임이
    /// 멈춘 것처럼 보이는 것이 가장 나쁘다. 화면 아무 데나 누르면 넘어간다.</para>
    ///
    /// <para>소리는 <see cref="AudioService"/>의 BGM 버스 볼륨을 탄다 — 옵션에서 음악을 줄여 놨는데
    /// 엔딩만 크게 나오면 안 된다.</para>
    ///
    /// <para>씬 배치는 <c>Tools ▸ ChainRiposte ▸ Add Ending Video To Main</c>.</para>
    /// </summary>
    public sealed class EndingVideoPlayer : MonoBehaviour
    {
        [Header("씬 참조 (Add Ending Video To Main 이 배선)")]
        [Tooltip("영상이 도는 동안만 켜지는 루트")]
        [SerializeField] private RectTransform root;
        [Tooltip("영상이 그려지는 자리")]
        [SerializeField] private RawImage screen;
        [Tooltip("영상 비율을 지키는 장치. 비율은 클립마다 다르므로 재생할 때 여기에 실어 준다.")]
        [SerializeField] private AspectRatioFitter screenFitter;
        [SerializeField] private VideoPlayer video;
        [SerializeField] private AudioSource audioOut;
        [Tooltip("아무 데나 눌러 넘기기 위한 전체 화면 버튼")]
        [SerializeField] private Button skipButton;
        [Tooltip("영상 앞뒤의 암전. 비우면 페이드 없이 툭 시작한다.")]
        [SerializeField] private CanvasGroup group;

        [Header("연출")]
        [SerializeField, Min(0f)] private float fadeInSeconds = 0.6f;
        [SerializeField, Min(0f)] private float fadeOutSeconds = 0.8f;
        [Tooltip("영상을 못 여는 경우(파일 손상·플랫폼 미지원)에도 이 시간이 지나면 넘어간다. " +
                 "0이면 무한정 기다린다 — 권장하지 않는다.")]
        [SerializeField, Min(0f)] private float prepareTimeoutSeconds = 5f;

        private bool _skipped;
        private RenderTexture _target;

        private void Awake()
        {
            if (root == null)
                return;

            if (group == null)
                group = root.GetComponent<CanvasGroup>();
            if (skipButton != null)
                skipButton.onClick.AddListener(() => _skipped = true);

            root.gameObject.SetActive(false);
        }

        private void OnDestroy() => ReleaseTarget();

        /// <summary>고른 캐릭터의 엔딩 영상이 있는가 — 없으면 호출한 쪽이 곧장 다음으로 간다.</summary>
        public static VideoClip ResolveClip()
        {
            PlayerCharacterSO character = CharacterService.Current;
            return character != null ? character.EndingVideo : null;
        }

        /// <summary>
        /// 엔딩 영상 한 편. <b>이 코루틴이 끝나야</b> 호출한 쪽이 다음(타이틀 복귀)으로 넘어간다.
        /// </summary>
        public IEnumerator Play(VideoClip clip)
        {
            if (clip == null || root == null || video == null)
                yield break;

            _skipped = false;
            PrepareTarget(clip);

            video.clip = clip;
            video.isLooping = false;
            video.playOnAwake = false;
            if (audioOut != null)
            {
                video.audioOutputMode = VideoAudioOutputMode.AudioSource;
                video.EnableAudioTrack(0, true);
                video.SetTargetAudioSource(0, audioOut);
                // 옵션의 음악 볼륨을 그대로 탄다 — 엔딩만 따로 크게 나오면 안 된다
                audioOut.volume = AudioService.BgmVolume;
            }

            root.gameObject.SetActive(true);
            if (group != null)
                group.alpha = 0f;

            video.Prepare();
            yield return WaitForPrepare();

            video.Play();
            yield return null; // 재생 시작이 반영될 한 프레임 — 페이드가 0초여도 아래 루프가 헛돌지 않게
            yield return Fade(0f, 1f, fadeInSeconds);

            // 재생이 끝나거나, 누르거나, 어떤 이유로든 재생이 멈추면 나간다
            while (!_skipped && video.isPlaying)
                yield return null;

            yield return Fade(1f, 0f, fadeOutSeconds);

            video.Stop();
            root.gameObject.SetActive(false);
            ReleaseTarget();
        }

        /// <summary>
        /// 영상 크기에 맞춘 화면을 만든다. 클립마다 해상도가 다르므로 <b>씬에 굳혀 둘 수 없다</b> —
        /// 크기가 안 맞으면 늘어나거나 잘린 채로 나간다.
        /// </summary>
        private void PrepareTarget(VideoClip clip)
        {
            ReleaseTarget();

            int width = Mathf.Max(16, (int)clip.width);
            int height = Mathf.Max(16, (int)clip.height);
            _target = new RenderTexture(width, height, 0);

            video.renderMode = VideoRenderMode.RenderTexture;
            video.targetTexture = _target;
            if (screen != null)
                screen.texture = _target;
            if (screenFitter != null)
                screenFitter.aspectRatio = (float)width / height;
        }

        private void ReleaseTarget()
        {
            if (_target == null)
                return;

            if (video != null && video.targetTexture == _target)
                video.targetTexture = null;
            if (screen != null && screen.texture == _target)
                screen.texture = null;

            _target.Release();
            Destroy(_target);
            _target = null;
        }

        /// <summary>
        /// 여는 데 실패해도 <b>반드시 빠져나온다</b>. 엔딩에서 검은 화면에 갇히는 것은
        /// 영상이 안 나오는 것보다 훨씬 나쁘다.
        /// </summary>
        private IEnumerator WaitForPrepare()
        {
            float waited = 0f;
            while (!video.isPrepared && !_skipped)
            {
                if (prepareTimeoutSeconds > 0f && waited >= prepareTimeoutSeconds)
                {
                    Debug.LogWarning($"{nameof(EndingVideoPlayer)}: 엔딩 영상을 여는 데 실패했습니다. " +
                        "안드로이드에서는 H.264 mp4 만 확실히 재생됩니다.", this);
                    yield break;
                }

                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private IEnumerator Fade(float from, float to, float seconds)
        {
            if (group == null || seconds <= 0f)
            {
                if (group != null)
                    group.alpha = to;
                yield break;
            }

            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds));
                yield return null;
            }

            group.alpha = to;
        }
    }
}
