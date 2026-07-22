using System;
using UnityEngine;

namespace ChainRiposte.Game.Options
{
    /// <summary>화면 방향 정책. 자동이면 기기 회전을 그대로 따른다 (GDD §9.3).</summary>
    public enum OrientationMode
    {
        Auto = 0,
        Portrait = 1,
        Landscape = 2,
    }

    /// <summary>
    /// 사용자 설정 저장소. 값은 PlayerPrefs에 남고, 바뀌는 즉시 실제 효과까지 적용한다
    /// (값 대입과 적용을 분리하면 반드시 한쪽을 빼먹는다 — 현지화의 Language 프로퍼티와 같은 원칙).
    ///
    /// 언어는 여기서 다루지 않는다 — <see cref="Localization.Loc.Language"/>가 원천이다.
    /// </summary>
    public static class GameOptions
    {
        private const string BgmKey = "ChainRiposte.Options.Bgm";
        private const string SfxKey = "ChainRiposte.Options.Sfx";
        private const string OrientationKey = "ChainRiposte.Options.Orientation";

        private static float _bgmVolume = 1f;
        private static float _sfxVolume = 1f;
        private static OrientationMode _orientation = OrientationMode.Auto;
        private static bool _loaded;

        /// <summary>설정이 바뀐 순간 — 옵션 UI가 구독해 표시를 맞춘다.</summary>
        public static event Action Changed;

        public static float BgmVolume
        {
            get { EnsureLoaded(); return _bgmVolume; }
            set => SetVolume(ref _bgmVolume, BgmKey, value, Audio.AudioService.ApplyBgmVolume);
        }

        public static float SfxVolume
        {
            get { EnsureLoaded(); return _sfxVolume; }
            set => SetVolume(ref _sfxVolume, SfxKey, value, Audio.AudioService.ApplySfxVolume);
        }

        public static OrientationMode Orientation
        {
            get { EnsureLoaded(); return _orientation; }
            set
            {
                EnsureLoaded();
                if (_orientation == value)
                    return;

                _orientation = value;
                PlayerPrefs.SetInt(OrientationKey, (int)value);
                PlayerPrefs.Save();
                ApplyOrientation();
                Changed?.Invoke();
            }
        }

        /// <summary>부팅 시 저장된 설정을 실제로 적용한다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void ApplyAll()
        {
            Changed = null; // 도메인 리로드를 꺼둔 환경 대비
            _loaded = false;
            EnsureLoaded();

            Audio.AudioService.ApplyBgmVolume(_bgmVolume);
            Audio.AudioService.ApplySfxVolume(_sfxVolume);
            ApplyOrientation();
        }

        private static void SetVolume(ref float field, string prefsKey, float value, Action<float> apply)
        {
            EnsureLoaded();
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(field, value))
                return;

            field = value;
            PlayerPrefs.SetFloat(prefsKey, value);
            PlayerPrefs.Save();
            apply(value);
            Changed?.Invoke();
        }

        private static void ApplyOrientation()
        {
            switch (_orientation)
            {
                case OrientationMode.Portrait:
                    Screen.orientation = ScreenOrientation.Portrait;
                    break;
                case OrientationMode.Landscape:
                    Screen.orientation = ScreenOrientation.LandscapeLeft;
                    break;
                default:
                    // 자동 — 기기를 돌리는 대로 따라간다. 상하 반전 세로는 흔들림이 심해 빼 둔다.
                    Screen.autorotateToPortrait = true;
                    Screen.autorotateToPortraitUpsideDown = false;
                    Screen.autorotateToLandscapeLeft = true;
                    Screen.autorotateToLandscapeRight = true;
                    Screen.orientation = ScreenOrientation.AutoRotation;
                    break;
            }
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
                return;

            _loaded = true;
            _bgmVolume = PlayerPrefs.GetFloat(BgmKey, 1f);
            _sfxVolume = PlayerPrefs.GetFloat(SfxKey, 1f);
            _orientation = (OrientationMode)PlayerPrefs.GetInt(OrientationKey, (int)OrientationMode.Auto);
        }
    }
}
