# 전체 구현 (프로덕션 버전)

복사해서 바로 쓰는 소스. 각 블록 상단에 배치 경로가 적혀 있다. 순서대로 4개 파일 + Editor 1개면 완성이다.

의존성: 없음 (TextMeshPro는 선택 — 없으면 해당 블록만 지우면 된다).

---

## 1. `Assets/Scripts/Localization/CSVReader.cs`

CSV 텍스트를 `List<Dictionary<헤더, 값>>`으로 바꾸는 파서. 현지화 전용이 아니라 스탯 테이블 등 모든 CSV에 재사용된다.

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public static class CSVReader
{
    // 따옴표 밖의 콤마만 구분자로 인식한다. → "값1,값1-1" 처럼 콤마 포함 텍스트 지원
    const string SPLIT_RE = @",(?=(?:[^""]*""[^""]*"")*(?![^""]*""))";
    const string LINE_SPLIT_RE = @"\r\n|\n\r|\n|\r";   // CRLF / LF / CR 모두 대응
    static readonly char[] TRIM_CHARS = { '\"' };

    // ── 문자열 전용 (현지화는 이걸 쓴다) ─────────────────────────────
    /// <summary>Assets/Resources/{file}.csv 를 읽는다. 확장자는 붙이지 않는다.</summary>
    public static List<Dictionary<string, string>> ReadFile(string file)
    {
        TextAsset asset = Resources.Load<TextAsset>(file);
        if (asset == null)
        {
            Debug.LogError($"[CSVReader] Resources/{file}.csv 를 찾을 수 없습니다.");
            return new List<Dictionary<string, string>>();
        }
        return ReadString(asset.text);
    }

    /// <summary>공통 파서. 파일/웹/에디터 입력 모두 여기로 모인다.</summary>
    public static List<Dictionary<string, string>> ReadString(string text)
    {
        var list = new List<Dictionary<string, string>>();
        if (string.IsNullOrEmpty(text)) return list;

        var lines = Regex.Split(text, LINE_SPLIT_RE);
        if (lines.Length <= 1) return list;

        var header = Regex.Split(lines[0], SPLIT_RE);
        for (int i = 0; i < header.Length; i++)
            header[i] = Clean(header[i]).Trim();

        for (int i = 1; i < lines.Length; i++)
        {
            var values = Regex.Split(lines[i], SPLIT_RE);
            if (values.Length == 0 || string.IsNullOrWhiteSpace(values[0])) continue;   // 빈 줄 스킵

            var entry = new Dictionary<string, string>();
            for (int j = 0; j < header.Length && j < values.Length; j++)
                entry[header[j]] = Clean(values[j]);

            list.Add(entry);
        }
        return list;
    }

    // ── 타입 추론 버전 (스탯 테이블 등 숫자 섞인 CSV용) ───────────────
    /// <summary>int/float은 자동 변환해서 object로 담는다. 사용처에서 캐스팅 필요.</summary>
    public static List<Dictionary<string, object>> Read(string file)
    {
        var list = new List<Dictionary<string, object>>();
        foreach (var row in ReadFile(file))
        {
            var entry = new Dictionary<string, object>();
            foreach (var pair in row)
            {
                string v = pair.Value.Trim();
                // InvariantCulture 고정: 소수점을 콤마로 쓰는 로케일(독일/프랑스)에서 1.5가 15로 파싱되는 것을 막는다
                if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
                    entry[pair.Key] = n;
                else if (float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                    entry[pair.Key] = f;
                else
                    entry[pair.Key] = pair.Value;
            }
            list.Add(entry);
        }
        return list;
    }

    // ── 웹/구글 시트 버전 ────────────────────────────────────────────
    // 구글 시트: 공유를 "링크가 있는 모든 사용자 - 뷰어"로 설정한 뒤
    // https://docs.google.com/spreadsheets/d/{시트ID}/export?format=csv
    // (특정 탭은 &gid={탭ID} 추가)

    /// <summary>코루틴 버전. StartCoroutine(CSVReader.ReadURL(url, data => {...}))</summary>
    public static IEnumerator ReadURL(string url, Action<List<Dictionary<string, string>>> callback)
    {
        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            yield return www.SendWebRequest();

            if (IsError(www))
            {
                Debug.LogError("[CSVReader] 다운로드 실패: " + www.error);
                callback?.Invoke(null);
            }
            else
            {
                callback?.Invoke(ReadString(www.downloadHandler.text));
            }
        }
    }

    /// <summary>async 버전. var data = await CSVReader.ReadURL(url);</summary>
    public static async Task<List<Dictionary<string, string>>> ReadURL(string url)
    {
        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            var op = www.SendWebRequest();
            while (!op.isDone)
                await Task.Yield();   // 프레임을 넘기며 대기 → 게임은 계속 돈다

            if (IsError(www))
            {
                Debug.LogError("[CSVReader] 다운로드 실패: " + www.error);
                return null;
            }
            return ReadString(www.downloadHandler.text);
        }
    }

    static bool IsError(UnityWebRequest www)
    {
#if UNITY_2020_1_OR_NEWER
        return www.result != UnityWebRequest.Result.Success;
#else
        return www.isNetworkError || www.isHttpError;   // 2020부터 deprecated
#endif
    }

    static string Clean(string value) =>
        value.TrimStart(TRIM_CHARS).TrimEnd(TRIM_CHARS).Replace("\\", "").Replace("\"\"", "\"");
}
```

---

## 2. `Assets/Scripts/Localization/Localization.cs`

시스템의 심장. static이라 씬 어디서든 `Localization.GetText("Key")`로 접근한다.

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

public static class Localization
{
    // ── 설정 ────────────────────────────────────────────────────────
    const string CsvFileName = "Localization";              // Assets/Resources/Localization.csv
    const string KeyColumn = "Key";
    const string PrefsKey = "Localization.Language";
    public const SystemLanguage FallbackLanguage = SystemLanguage.English;

    // ── 상태 ────────────────────────────────────────────────────────
    static Dictionary<string, Dictionary<string, string>> _table;   // Key → (언어컬럼 → 값). O(1) 조회
    static SystemLanguage _language;
    static bool _initialized;

    /// <summary>언어가 바뀔 때마다 발행. 바인더는 OnEnable에서 구독하고 OnDisable에서 해제할 것.</summary>
    public static event Action OnLanguageChanged;

    /// <summary>CSV 헤더에서 추출한 지원 언어 목록. 언어 선택 UI를 만들 때 쓴다.</summary>
    public static IReadOnlyList<SystemLanguage> SupportedLanguages => _supported;
    static List<SystemLanguage> _supported = new List<SystemLanguage>();

    // ── 언어 ────────────────────────────────────────────────────────
    public static SystemLanguage Language
    {
        get { EnsureInit(); return _language; }
        set
        {
            EnsureInit();
            if (_language == value) return;      // 같은 값이면 이벤트를 쏘지 않는다 (불필요한 전체 갱신 방지)

            _language = value;
            PlayerPrefs.SetString(PrefsKey, value.ToString());
            OnLanguageChanged?.Invoke();         // 값 변경과 통지를 한 곳에 묶는다 = 빼먹을 수 없다
        }
    }

    // ── 조회 ────────────────────────────────────────────────────────
    /// <summary>키에 해당하는 현재 언어 문자열. 없으면 폴백 언어 → 그래도 없으면 키 자체를 반환.</summary>
    public static string GetText(string key)
    {
        EnsureInit();
        if (string.IsNullOrEmpty(key)) return string.Empty;

        if (!_table.TryGetValue(key, out var row))
        {
#if UNITY_EDITOR
            Debug.LogWarning($"[Localization] 존재하지 않는 키: '{key}'");
#endif
            return key;   // 화면에 키가 그대로 보이면 = 번역 누락 신호
        }

        if (row.TryGetValue(_language.ToString(), out var value) && !string.IsNullOrEmpty(value))
            return value;

        if (row.TryGetValue(FallbackLanguage.ToString(), out value) && !string.IsNullOrEmpty(value))
            return value;

        return key;
    }

    /// <summary>서식 인자 지원. CSV 값에 {0} {1} 을 넣어두고 GetText("Greet", playerName) 처럼 쓴다.</summary>
    public static string GetText(string key, params object[] args)
    {
        string format = GetText(key);
        try { return string.Format(format, args); }
        catch (FormatException)
        {
            Debug.LogError($"[Localization] 서식 불일치: '{key}' → \"{format}\"");
            return format;
        }
    }

    public static bool HasKey(string key)
    {
        EnsureInit();
        return !string.IsNullOrEmpty(key) && _table.ContainsKey(key);
    }

    // ── 초기화 / 리로드 ─────────────────────────────────────────────
    /// <summary>모든 진입점(GetText, Language)에서 호출되므로 별도로 부를 필요는 없다.
    /// 다만 로딩 화면에서 CSV 파싱 비용을 미리 치르고 싶다면 명시적으로 호출한다.</summary>
    public static void EnsureInit()
    {
        if (_initialized) return;
        _initialized = true;
        _language = LoadSavedLanguage();
        Reload();
    }

    /// <summary>CSV를 다시 읽는다. 에디터에서 번역을 수정했거나 시트를 갱신했을 때 호출.</summary>
    public static void Reload()
    {
        Load(CSVReader.ReadFile(CsvFileName));
    }

    /// <summary>구글 시트 등 외부에서 받아온 행 데이터를 주입한다.</summary>
    public static void Load(List<Dictionary<string, string>> rows)
    {
        _table = new Dictionary<string, Dictionary<string, string>>();
        _supported = new List<SystemLanguage>();
        if (rows == null || rows.Count == 0) return;

        foreach (var row in rows)
        {
            if (!row.TryGetValue(KeyColumn, out var key) || string.IsNullOrWhiteSpace(key)) continue;
            _table[key] = row;   // 중복 키는 마지막 행이 이긴다
        }

        // 헤더(= 첫 행의 컬럼)에서 Key를 뺀 나머지를 언어로 해석
        foreach (var column in rows[0].Keys)
        {
            if (column == KeyColumn) continue;
            if (Enum.TryParse(column, out SystemLanguage lang))
                _supported.Add(lang);
            else
                Debug.LogWarning($"[Localization] SystemLanguage로 해석할 수 없는 컬럼: '{column}'");
        }

        _initialized = true;
        OnLanguageChanged?.Invoke();   // 데이터가 통째로 바뀌었으니 전체 갱신
    }

    static SystemLanguage LoadSavedLanguage()
    {
        string saved = PlayerPrefs.GetString(PrefsKey, "");
        if (!string.IsNullOrEmpty(saved) && Enum.TryParse(saved, out SystemLanguage parsed))
            return parsed;

        return Application.systemLanguage;   // 첫 실행은 기기 언어를 따른다 (CSV에 없으면 폴백됨)
    }

    // Enter Play Mode Options(도메인 리로드 끄기) 사용 시 static이 남아있는 문제 방지
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _table = null;
        _supported = new List<SystemLanguage>();
        _initialized = false;
        OnLanguageChanged = null;
    }
}
```

---

## 3. `Assets/Scripts/Localization/LocalizedText.cs`

uGUI `Text`와 TextMeshPro를 모두 지원하는 바인더.

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;   // TextMeshPro를 안 쓰면 이 줄과 _tmp 관련 코드를 지운다

public class LocalizedText : MonoBehaviour
{
    [SerializeField] string _key;
    [SerializeField] Text _uguiText;
    [SerializeField] TMP_Text _tmpText;

    /// <summary>런타임에 키를 바꾸면 즉시 반영된다.</summary>
    public string Key
    {
        get => _key;
        set { _key = value; Apply(); }
    }

    void Reset()   // 컴포넌트를 처음 붙일 때 에디터가 자동으로 채워준다
    {
        _uguiText = GetComponent<Text>();
        _tmpText = GetComponent<TMP_Text>();
    }

    void Awake()
    {
        if (_uguiText == null) _uguiText = GetComponent<Text>();
        if (_tmpText == null) _tmpText = GetComponent<TMP_Text>();
    }

    // Awake/Start가 아니라 OnEnable/OnDisable 쌍으로 구독한다.
    // static 이벤트는 씬이 바뀌어도 살아있으므로, 해제하지 않으면 파괴된 오브젝트가 호출되어 터진다.
    void OnEnable()
    {
        Localization.OnLanguageChanged += Apply;
        Apply();   // 활성화 시점의 현재 언어를 즉시 반영
    }

    void OnDisable()
    {
        Localization.OnLanguageChanged -= Apply;
    }

    void Apply()
    {
        string value = Localization.GetText(_key);
        if (_uguiText != null) _uguiText.text = value;
        if (_tmpText != null) _tmpText.text = value;
    }
}
```

---

## 4. `Assets/Scripts/Localization/LocalizedImage.cs`

국기 아이콘, 언어별 로고, 텍스트가 박힌 이미지 등을 교체한다.

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public struct LocalizedSprite
{
    public SystemLanguage language;
    public Sprite sprite;
}

[RequireComponent(typeof(Image))]
public class LocalizedImage : MonoBehaviour
{
    [SerializeField] List<LocalizedSprite> _sprites = new List<LocalizedSprite>();
    [SerializeField] Sprite _fallback;   // 목록에 없는 언어일 때 쓸 기본 이미지

    Image _image;

    void Awake() => _image = GetComponent<Image>();

    void OnEnable()
    {
        Localization.OnLanguageChanged += Apply;
        Apply();
    }

    void OnDisable()
    {
        Localization.OnLanguageChanged -= Apply;
    }

    void Apply()
    {
        var current = Localization.Language;
        foreach (var item in _sprites)
        {
            if (item.language == current)
            {
                _image.sprite = item.sprite;
                return;
            }
        }
        if (_fallback != null) _image.sprite = _fallback;
    }
}
```

---

## 5. `Assets/Editor/LocalizedSpriteDrawer.cs`

`LocalizedSprite`를 인스펙터에서 한 줄(언어 | 스프라이트)로 보이게 하는 드로어.

> **반드시 `Assets/Editor/` 아래에 둔다.** `using UnityEditor`가 들어간 스크립트가 런타임 폴더에 있으면 빌드가 컴파일 에러로 실패한다. (이 저장소의 `Localizedimage.cs`가 그 상태다.)

```csharp
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(LocalizedSprite))]
public class LocalizedSpriteDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        position.width /= 2f;
        EditorGUI.PropertyField(position, property.FindPropertyRelative("language"), GUIContent.none);
        position.x += position.width;
        EditorGUI.PropertyField(position, property.FindPropertyRelative("sprite"), GUIContent.none);

        EditorGUI.EndProperty();
    }
}
```

---

## 6. 언어 전환 UI (예시)

프로토타입용 IMGUI. 실제 게임에서는 uGUI 드롭다운으로 대체한다.

```csharp
using UnityEngine;

public class LanguageSwitcher : MonoBehaviour
{
    void OnGUI()
    {
        var languages = Localization.SupportedLanguages;

        GUI.Box(new Rect(20, 20, 200, 50 + languages.Count * 60), "Language");
        for (int i = 0; i < languages.Count; i++)
        {
            if (GUI.Button(new Rect(40, 60 + i * 60, 160, 40), languages[i].ToString()))
                Localization.Language = languages[i];   // 이 한 줄이면 화면 전체가 갱신된다
        }
    }
}
```

uGUI 드롭다운 버전:

```csharp
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Dropdown))]
public class LanguageDropdown : MonoBehaviour
{
    Dropdown _dropdown;

    void Start()
    {
        _dropdown = GetComponent<Dropdown>();
        var languages = Localization.SupportedLanguages;

        _dropdown.ClearOptions();
        _dropdown.AddOptions(languages.Select(l => l.ToString()).ToList());
        _dropdown.SetValueWithoutNotify(languages.ToList().IndexOf(Localization.Language));
        _dropdown.onValueChanged.AddListener(i => Localization.Language = languages[i]);
    }
}
```

---

## 7. `Assets/Resources/Localization.csv`

```csv
Key,Korean,English,Japanese
Title,이것은 제목,This is Title,これはタイトル
Desc1,내용을 적어봅니다.,Something to Write,内容を書いてみます。
Btn.Start,시작,Start,スタート
Btn.Quit,종료,Quit,終了
Greet,{0}님 환영합니다,Welcome {0},{0}さんようこそ
```

저장 시 **UTF-8** 필수. 상세 규격은 `data-format.md` 참조.
