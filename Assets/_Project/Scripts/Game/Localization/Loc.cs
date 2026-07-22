using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChainRiposte.Game.Localization
{
    /// <summary>
    /// 현지화 코어. CSV 한 장(<c>Resources/Localization.csv</c>)을 키 기준으로 인덱싱해 O(1)로 조회한다.
    ///
    /// 규칙 셋:
    ///  1) 언어는 <b>컬럼 이름</b>(= <see cref="SystemLanguage"/> enum 이름)으로 매칭한다.
    ///     enum 값으로 인덱싱하면 안 된다 — 알파벳순이라 Korean은 23이다.
    ///  2) 언어 변경 진입점은 <see cref="Language"/> 프로퍼티 하나뿐이다. 값 대입과 통지가 한 몸이라 빼먹을 수 없다.
    ///  3) 없는 키·없는 컬럼은 예외 대신 폴백(현재 언어 → 영어 → 키 자체)한다.
    ///     화면에 키가 그대로 보이면 "번역 누락" 신호다.
    ///
    /// CSV는 구글 시트에서 굽는다 — <c>Tools ▸ ChainRiposte ▸ Localization</c> 참조.
    /// 런타임에는 시트를 치지 않는다(오프라인·WebGL CORS·시트 오조작으로 전체 텍스트가 날아가는 사고 방지).
    /// </summary>
    public static class Loc
    {
        public const string CsvResourceName = "Localization";
        public const string KeyColumn = "Key";
        public const SystemLanguage FallbackLanguage = SystemLanguage.English;

        private const string PrefsKey = "ChainRiposte.Language";

        /// <summary>
        /// 첫 실행 시 기기 언어를 따를지. 한글 폰트(neodgm SDF)가 들어와 켜 두었다 —
        /// 지원 목록에 없는 기기 언어면 <see cref="DefaultLanguage"/>로 떨어진다.
        /// </summary>
        public static bool UseDeviceLanguage = true;

        public static SystemLanguage DefaultLanguage = SystemLanguage.English;

        private static Dictionary<string, Dictionary<string, string>> _table;
        private static List<SystemLanguage> _supported = new();
        private static HashSet<string> _warnedKeys = new();
        private static SystemLanguage _language;
        private static bool _initialized;

        /// <summary>언어가 바뀌거나 테이블이 통째로 교체될 때 발행. 바인더는 OnEnable 구독 / OnDisable 해제 쌍으로 받는다.</summary>
        public static event Action LanguageChanged;

        /// <summary>CSV 헤더에서 뽑은 지원 언어. 옵션의 언어 선택 UI가 이걸 그대로 쓴다.</summary>
        public static IReadOnlyList<SystemLanguage> SupportedLanguages
        {
            get
            {
                EnsureInit();
                return _supported;
            }
        }

        public static SystemLanguage Language
        {
            get
            {
                EnsureInit();
                return _language;
            }
            set
            {
                EnsureInit();
                if (_language == value)
                    return; // 같은 값이면 전체 갱신을 쏘지 않는다

                _language = value;
                PlayerPrefs.SetString(PrefsKey, value.ToString());
                PlayerPrefs.Save();
                LanguageChanged?.Invoke();
            }
        }

        /// <summary>현재 언어 문자열. 없으면 폴백 언어 → 그래도 없으면 키 자체.</summary>
        public static string GetText(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            EnsureInit();

            if (!_table.TryGetValue(key, out Dictionary<string, string> row))
            {
                WarnOnce(key, $"[Loc] CSV에 없는 키: '{key}'");
                return key;
            }

            if (TryValue(row, _language, out string value) || TryValue(row, FallbackLanguage, out value))
                return value;

            WarnOnce(key, $"[Loc] '{key}' 에 '{_language}' / '{FallbackLanguage}' 값이 모두 비어 있습니다.");
            return key;
        }

        /// <summary>서식 인자 — CSV 값에 {0} {1} 을 넣어 둔다.</summary>
        public static string GetText(string key, params object[] args)
        {
            string format = GetText(key);
            try
            {
                return string.Format(format, args);
            }
            catch (FormatException)
            {
                Debug.LogError($"[Loc] 서식 불일치: '{key}' → \"{format}\"");
                return format;
            }
        }

        public static bool HasKey(string key)
        {
            EnsureInit();
            return !string.IsNullOrEmpty(key) && _table.ContainsKey(key);
        }

        /// <summary>지연 초기화. 로딩 화면에서 파싱 비용을 미리 치르고 싶을 때만 직접 부른다.</summary>
        public static void EnsureInit()
        {
            if (_initialized)
                return;

            _initialized = true; // Load 안에서 재진입하지 않도록 먼저 세운다
            var asset = Resources.Load<TextAsset>(CsvResourceName);
            if (asset == null)
            {
                _table = new Dictionary<string, Dictionary<string, string>>();
                _language = FallbackLanguage;
                Debug.LogWarning(
                    $"[Loc] Resources/{CsvResourceName}.csv 를 찾지 못했습니다. 문구 대신 키가 표시됩니다. " +
                    "Tools ▸ ChainRiposte ▸ Localization ▸ Sync From Google Sheet 를 실행하세요.");
                return;
            }

            LoadRows(CsvReader.ReadString(asset.text), notify: false);
            _language = ResolveStartupLanguage();
        }

        /// <summary>CSV를 다시 읽는다 (에디터에서 시트를 다시 구웠을 때).</summary>
        public static void Reload()
        {
            _initialized = false;
            _warnedKeys.Clear();
            SystemLanguage previous = _language;
            EnsureInit();

            // 되읽은 뒤에도 쓰던 언어를 유지한다 (지원 목록에 남아 있다면)
            if (_supported.Contains(previous))
                _language = previous;

            LanguageChanged?.Invoke();
        }

        private static void LoadRows(List<Dictionary<string, string>> rows, bool notify)
        {
            _table = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            _supported = new List<SystemLanguage>();

            if (rows == null || rows.Count == 0)
                return;

            foreach (Dictionary<string, string> row in rows)
            {
                if (!row.TryGetValue(KeyColumn, out string key) || string.IsNullOrWhiteSpace(key))
                    continue;

                if (_table.ContainsKey(key))
                    Debug.LogWarning($"[Loc] 중복 키 '{key}' — 마지막 행이 이깁니다. 시트를 확인하세요.");

                _table[key] = row;
            }

            foreach (string column in rows[0].Keys)
            {
                if (column == KeyColumn || string.IsNullOrWhiteSpace(column))
                    continue;

                if (Enum.TryParse(column, out SystemLanguage parsed))
                    _supported.Add(parsed);
                else
                    Debug.LogWarning($"[Loc] SystemLanguage로 해석할 수 없는 컬럼: '{column}' (무시됨)");
            }

            if (notify)
                LanguageChanged?.Invoke();
        }

        /// <summary>저장된 언어 → (기기 언어) → 기본 언어 → 폴백. 지원 목록에 없는 언어는 고르지 않는다.</summary>
        private static SystemLanguage ResolveStartupLanguage()
        {
            string saved = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (!string.IsNullOrEmpty(saved) && Enum.TryParse(saved, out SystemLanguage parsed) && _supported.Contains(parsed))
                return parsed;

            if (UseDeviceLanguage && _supported.Contains(Application.systemLanguage))
                return Application.systemLanguage;

            return _supported.Contains(DefaultLanguage) ? DefaultLanguage : FallbackLanguage;
        }

        private static bool TryValue(Dictionary<string, string> row, SystemLanguage language, out string value) =>
            row.TryGetValue(language.ToString(), out value) && !string.IsNullOrEmpty(value);

        /// <summary>같은 키를 매 프레임 조회해도 경고가 한 번만 나가게 한다.</summary>
        private static void WarnOnce(string key, string message)
        {
#if UNITY_EDITOR
            if (_warnedKeys.Add(key))
                Debug.LogWarning(message);
#endif
        }

        /// <summary>도메인 리로드를 꺼둔 환경에서 static이 남는 문제 방지 (pitfalls §8).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _table = null;
            _supported = new List<SystemLanguage>();
            _warnedKeys = new HashSet<string>();
            _initialized = false;
            LanguageChanged = null;
        }
    }
}
