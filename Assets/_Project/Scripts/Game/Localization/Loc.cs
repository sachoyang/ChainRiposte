using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChainRiposte.Game.Localization
{
    public enum Language
    {
        Korean = 0,
        English = 1,
    }

    /// <summary>
    /// 문구 조회 창구. 테이블(<see cref="LocalizationTableSO"/>)은 Resources에서 자동 로드하고,
    /// 선택한 언어는 PlayerPrefs에 저장한다.
    ///
    /// 키가 없으면 키 자체를 돌려준다 — 문구를 아직 안 채웠어도 화면이 비지 않고 무엇이 빠졌는지 보인다.
    /// </summary>
    public static class Loc
    {
        public const string ResourcePath = "LocalizationTable";
        private const string PrefsKey = "ChainRiposte.Language";

        private static Dictionary<string, (string ko, string en)> _lookup;
        private static Language _language;
        private static bool _loaded;

        /// <summary>언어가 바뀌면 발행 — <see cref="LocalizedText"/>가 구독해 스스로 갱신한다.</summary>
        public static event Action Changed;

        public static Language Current
        {
            get
            {
                EnsureLoaded();
                return _language;
            }
            set
            {
                EnsureLoaded();
                if (_language == value)
                    return;

                _language = value;
                PlayerPrefs.SetInt(PrefsKey, (int)value);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        /// <summary>도메인 리로드를 꺼둔 환경 대비 — 부팅 시 구독자와 캐시를 비운다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStatics()
        {
            Changed = null;
            _lookup = null;
            _loaded = false;
        }

        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            EnsureLoaded();
            if (!_lookup.TryGetValue(key, out (string ko, string en) entry))
                return key; // 미등록 키는 그대로 보여 준다 (빠진 문구를 눈에 띄게)

            string text = _language == Language.Korean ? entry.ko : entry.en;
            return string.IsNullOrEmpty(text) ? key : text;
        }

        /// <summary>서식이 있는 문구 — 테이블 값에 {0}, {1}을 쓴다.</summary>
        public static string Format(string key, params object[] args) => string.Format(Get(key), args);

        private static void EnsureLoaded()
        {
            if (_loaded)
                return;

            _loaded = true;
            _language = (Language)PlayerPrefs.GetInt(PrefsKey, (int)Language.Korean);

            var table = Resources.Load<LocalizationTableSO>(ResourcePath);
            _lookup = table != null
                ? table.ToLookup()
                : new Dictionary<string, (string, string)>(StringComparer.Ordinal);

            if (table == null)
            {
                Debug.LogWarning(
                    $"[Loc] Resources/{ResourcePath} 을 찾지 못했습니다. 문구 대신 키가 표시됩니다. " +
                    "Tools ▸ ChainRiposte ▸ Localization ▸ Create/Update Table 을 실행하세요.");
            }
        }
    }
}
