# 확장 레시피

코어(`Localization` + `CSVReader`)는 그대로 두고 바인더/도구만 추가하는 방식. 모두 "OnEnable 구독 → Apply" 패턴을 따른다.

---

## 1. 언어별 폰트 교체

CJK와 라틴 폰트를 나눠 쓸 때.

```csharp
using System.Collections.Generic;
using UnityEngine;
using TMPro;

[CreateAssetMenu(menuName = "Localization/Font Table")]
public class LocalizedFontTable : ScriptableObject
{
    [System.Serializable]
    public struct Entry { public SystemLanguage language; public TMP_FontAsset font; }

    public List<Entry> fonts = new List<Entry>();
    public TMP_FontAsset fallback;

    public TMP_FontAsset Get(SystemLanguage language)
    {
        foreach (var e in fonts)
            if (e.language == language) return e.font;
        return fallback;
    }
}

[RequireComponent(typeof(TMP_Text))]
public class LocalizedFont : MonoBehaviour
{
    [SerializeField] LocalizedFontTable _table;
    TMP_Text _text;

    void Awake() => _text = GetComponent<TMP_Text>();
    void OnEnable()  { Localization.OnLanguageChanged += Apply; Apply(); }
    void OnDisable() { Localization.OnLanguageChanged -= Apply; }
    void Apply()
    {
        var font = _table != null ? _table.Get(Localization.Language) : null;
        if (font != null) _text.font = font;
    }
}
```

---

## 2. 언어별 오디오 / 프리팹

`LocalizedImage`와 구조가 같다. 제네릭으로 한 번에 처리:

```csharp
using System.Collections.Generic;
using UnityEngine;

public abstract class LocalizedAsset<T> : MonoBehaviour where T : Object
{
    [System.Serializable]
    public struct Entry { public SystemLanguage language; public T asset; }

    [SerializeField] protected List<Entry> _entries = new List<Entry>();
    [SerializeField] protected T _fallback;

    void OnEnable()  { Localization.OnLanguageChanged += Apply; Apply(); }
    void OnDisable() { Localization.OnLanguageChanged -= Apply; }

    protected T Resolve()
    {
        foreach (var e in _entries)
            if (e.language == Localization.Language) return e.asset;
        return _fallback;
    }

    protected abstract void Apply();
}

public class LocalizedAudio : LocalizedAsset<AudioClip>
{
    [SerializeField] AudioSource _source;
    protected override void Apply()
    {
        if (_source == null) _source = GetComponent<AudioSource>();
        _source.clip = Resolve();
    }
}
```

> 제네릭 MonoBehaviour는 인스펙터에서 직렬화가 안 되므로 **반드시 구체 타입 서브클래스**(`LocalizedAudio`)를 만들어 붙인다.

---

## 3. 서식 인자 / 동적 텍스트

```csharp
// CSV:  Greet,{0}님 환영합니다,Welcome {0}
//       Score,점수: {0:N0},Score: {0:N0}

string msg = Localization.GetText("Greet", playerName);
string score = Localization.GetText("Score", 12345);
```

값이 바뀌는 텍스트는 바인더에 인자를 물려야 언어 전환 시에도 유지된다:

```csharp
public class LocalizedTextFormatted : MonoBehaviour
{
    [SerializeField] string _key;
    [SerializeField] Text _text;
    object[] _args = new object[0];

    public void SetArgs(params object[] args) { _args = args; Apply(); }

    void OnEnable()  { Localization.OnLanguageChanged += Apply; Apply(); }
    void OnDisable() { Localization.OnLanguageChanged -= Apply; }
    void Apply() => _text.text = Localization.GetText(_key, _args);
}
```

---

## 4. 에디터 툴 — CSV 리로드 & 누락 키 검사

`Assets/Editor/LocalizationMenu.cs`

```csharp
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class LocalizationMenu
{
    [MenuItem("Tools/Localization/CSV 다시 읽기 %#l")]   // Ctrl+Shift+L
    static void Reload()
    {
        Localization.Reload();
        Debug.Log($"[Localization] 리로드 완료. 지원 언어: {string.Join(", ", Localization.SupportedLanguages)}");
    }

    [MenuItem("Tools/Localization/씬의 누락 키 찾기")]
    static void FindMissingKeys()
    {
        Localization.EnsureInit();
        var missing = new List<string>();

        foreach (var t in Object.FindObjectsOfType<LocalizedText>(true))
        {
            var key = t.Key;
            if (string.IsNullOrEmpty(key))
                Debug.LogWarning($"[Localization] 키가 비어있음: {GetPath(t.transform)}", t);
            else if (!Localization.HasKey(key))
            {
                missing.Add(key);
                Debug.LogWarning($"[Localization] CSV에 없는 키 '{key}': {GetPath(t.transform)}", t);
            }
        }

        Debug.Log(missing.Count == 0
            ? "[Localization] 누락 키 없음 ✔"
            : $"[Localization] 누락 {missing.Count}건:\n" + string.Join("\n", missing));
    }

    static string GetPath(Transform t)
    {
        var sb = new StringBuilder(t.name);
        while (t.parent != null) { t = t.parent; sb.Insert(0, t.name + "/"); }
        return sb.ToString();
    }
}
```

`FindObjectsOfType<T>(true)`의 `true`는 비활성 오브젝트 포함. Unity 2023+ 에서는 `FindObjectsByType<LocalizedText>(FindObjectsInactive.Include, FindObjectsSortMode.None)` 사용.

---

## 5. 키 자동완성 (문자열 오타 방지)

키를 문자열로 직접 타이핑하면 오타를 런타임까지 못 잡는다. CSV에서 상수 클래스를 생성해두면 컴파일 타임에 걸린다.

`Assets/Editor/LocalizationKeyGenerator.cs`

```csharp
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class LocalizationKeyGenerator
{
    const string OutputPath = "Assets/Scripts/Localization/LocKeys.cs";

    [MenuItem("Tools/Localization/키 상수 생성")]
    static void Generate()
    {
        var rows = CSVReader.ReadFile("Localization");
        var sb = new StringBuilder();
        sb.AppendLine("// 자동 생성 파일. 직접 수정하지 말 것. (Tools/Localization/키 상수 생성)");
        sb.AppendLine("public static class LocKeys");
        sb.AppendLine("{");

        foreach (var row in rows)
        {
            if (!row.TryGetValue("Key", out var key) || string.IsNullOrWhiteSpace(key)) continue;
            string ident = Regex.Replace(key, @"[^\w]", "_");        // UI.Title → UI_Title
            if (char.IsDigit(ident[0])) ident = "_" + ident;
            sb.AppendLine($"    public const string {ident} = \"{key}\";");
        }

        sb.AppendLine("}");
        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
        File.WriteAllText(OutputPath, sb.ToString(), new UTF8Encoding(false));
        AssetDatabase.Refresh();
        Debug.Log($"[Localization] {OutputPath} 생성 완료 ({rows.Count}개)");
    }
}
```

사용:
```csharp
_text.text = Localization.GetText(LocKeys.UI_Title);   // 오타 나면 컴파일 에러
```

---

## 6. 인스펙터 키 드롭다운

`LocalizedText`의 `_key`를 자유 입력 대신 CSV 키 목록에서 고르게 한다.

`Assets/Editor/LocalizedTextEditor.cs`

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LocalizedText))]
public class LocalizedTextEditor : Editor
{
    static string[] _keys;

    public override void OnInspectorGUI()
    {
        if (_keys == null)
            _keys = CSVReader.ReadFile("Localization")
                             .Where(r => r.ContainsKey("Key"))
                             .Select(r => r["Key"]).ToArray();

        var keyProp = serializedObject.FindProperty("_key");
        int index = System.Array.IndexOf(_keys, keyProp.stringValue);

        EditorGUI.BeginChangeCheck();
        int picked = EditorGUILayout.Popup("Key", index, _keys);
        if (EditorGUI.EndChangeCheck() && picked >= 0)
            keyProp.stringValue = _keys[picked];

        if (index < 0 && !string.IsNullOrEmpty(keyProp.stringValue))
            EditorGUILayout.HelpBox($"CSV에 없는 키: {keyProp.stringValue}", MessageType.Warning);

        DrawPropertiesExcluding(serializedObject, "_key", "m_Script");
        serializedObject.ApplyModifiedProperties();

        if (GUILayout.Button("키 목록 새로고침")) _keys = null;
    }
}
```

---

## 7. 로딩 순서 보장

여러 씬/부트스트랩이 얽힐 때 초기화 시점을 명시적으로 잡는다.

```csharp
public class LocalizationBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        Localization.EnsureInit();   // 첫 씬의 Awake보다 먼저 CSV 파싱을 끝내둔다
    }
}
```

`EnsureInit`은 지연 초기화라 없어도 동작하지만, 첫 `GetText` 호출에서 파싱 비용(수 ms~수십 ms)을 치른다. 게임 시작 직후 프레임 히칭이 싫으면 위처럼 앞당긴다.

---

## 8. 의사 언어(Pseudo-localization)로 QA

CSV에 컬럼 하나를 더한다.

```csv
Key,Korean,English,Pseudo
Btn.Start,시작,Start,[!! Start !!!!!!]
```

`Pseudo`는 `SystemLanguage`에 없으므로 코어가 경고를 낸다. QA 전용이라면 미사용 enum 값(예: `SystemLanguage.Unknown`)에 매핑하거나, 컬럼 파싱 시 화이트리스트를 두고 별도 처리한다. 목적은 두 가지 — **번역 누락 발견**(안 바뀌는 텍스트 = 하드코딩) + **레이아웃 오버플로 발견**.

---

## 9. 하드코딩 텍스트 색출

현지화를 뒤늦게 붙일 때 필요하다. 프로젝트 루트에서:

```bash
# 인스펙터에 박힌 텍스트 (씬/프리팹의 m_Text 필드)
grep -rn "m_Text:" Assets --include=*.unity --include=*.prefab

# 코드에 박힌 대입
grep -rnE '\.text\s*=\s*"' Assets --include=*.cs
```

각 결과를 CSV 키로 옮기고 `LocalizedText`로 교체한다.
