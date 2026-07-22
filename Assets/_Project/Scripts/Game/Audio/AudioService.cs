using UnityEngine;

namespace ChainRiposte.Game.Audio
{
    /// <summary>
    /// BGM / SFX 두 버스. 씬을 넘어 살아남으며 부팅 시 자동 생성된다(ScreenFader와 같은 방식).
    ///
    /// AudioMixer 에셋 없이 AudioSource 두 개로 간다 — 지금 오디오 클립이 하나도 없어도 볼륨 배선이
    /// 미리 끝나고, 나중에 믹서가 필요해지면 이 클래스 안만 바꾸면 된다(호출부는 그대로).
    /// 효과음은 <see cref="PlaySfx"/>로 흘려보내야 SFX 볼륨이 먹는다.
    /// </summary>
    public static class AudioService
    {
        private static AudioSource _bgm;
        private static AudioSource _sfx;

        public static AudioSource BgmSource => EnsureCreated() ? _bgm : null;
        public static AudioSource SfxSource => EnsureCreated() ? _sfx : null;

        /// <summary>배경음 교체. 같은 클립이면 다시 시작하지 않는다(씬 전환 때 음악이 끊기지 않게).</summary>
        public static void PlayBgm(AudioClip clip, bool loop = true)
        {
            if (!EnsureCreated() || _bgm.clip == clip)
                return;

            _bgm.clip = clip;
            _bgm.loop = loop;
            if (clip != null)
                _bgm.Play();
            else
                _bgm.Stop();
        }

        /// <summary>단발 효과음. 클립이 비어 있으면 조용히 무시한다(클립 슬롯이 아직 비어 있어도 안전).</summary>
        public static void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null || !EnsureCreated())
                return;

            _sfx.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
        }

        internal static void ApplyBgmVolume(float volume)
        {
            if (EnsureCreated())
                _bgm.volume = Mathf.Clamp01(volume);
        }

        internal static void ApplySfxVolume(float volume)
        {
            if (EnsureCreated())
                _sfx.volume = Mathf.Clamp01(volume);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _bgm = null;
            _sfx = null;
        }

        private static bool EnsureCreated()
        {
            if (_bgm != null && _sfx != null)
                return true;

            if (!Application.isPlaying)
                return false; // 에디터에서 값만 만질 때 씬을 더럽히지 않는다

            var root = new GameObject("~AudioService");
            Object.DontDestroyOnLoad(root);

            _bgm = root.AddComponent<AudioSource>();
            _bgm.playOnAwake = false;
            _bgm.loop = true;

            _sfx = root.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;

            return true;
        }
    }
}
