# 데이터 포맷 · CSV 규격 · 구글 시트 연동

## 1. CSV 규격

```csv
Key,Korean,English,Japanese
Title,이것은 제목,This is Title,これはタイトル
Btn.Start,시작,Start,スタート
```

| 항목 | 규칙 |
|---|---|
| 1행 | 헤더. 첫 컬럼은 반드시 `Key` |
| 언어 컬럼명 | **`SystemLanguage` enum 이름과 정확히 일치**해야 한다. `Korean`, `English`, `Japanese`, `ChineseSimplified`, `Spanish`, `French`, `German`, `Portuguese`, `Russian` … (`KO`, `한국어`, `kr` 같은 이름은 파싱 실패) |
| 키 이름 | 영문 + 점 표기 권장: `UI.Title`, `Btn.Start`, `Dialog.Npc01.Line3`. 화면/기능 단위로 접두사를 두면 검색과 누락 확인이 쉽다 |
| 저장 인코딩 | **UTF-8** (한글/일본어 깨짐 방지). 엑셀의 "CSV UTF-8(쉼표로 분리)" 형식 사용 |
| 위치 | `Assets/Resources/` 아래. `Resources.Load`는 확장자 없이 이름만 쓴다 |
| 빈 값 | 허용. 폴백 언어 → 키 자체 순으로 대체된다 |

### 특수 문자 처리

| 넣고 싶은 것 | CSV에 쓰는 법 |
|---|---|
| 콤마 포함 문장 | `Key,"안녕, 반가워",Hi, there` → 큰따옴표로 감싼다 |
| 큰따옴표 | `Key,"그는 ""좋아""라고 했다",...` → `""` 두 번 |
| 줄바꿈 | `\n`을 문자열로 넣고 런타임에서 `value.Replace("\\n", "\n")` 처리. **셀 안 실제 줄바꿈은 파서가 행 구분으로 오인하므로 금지** |
| 서식 인자 | `Greet,{0}님 환영합니다,Welcome {0}` → `Localization.GetText("Greet", name)` |

### 파서 정규식 해설

```csharp
const string SPLIT_RE = @",(?=(?:[^""]*""[^""]*"")*(?![^""]*""))";
```
"뒤에 남은 큰따옴표 개수가 짝수인 콤마"만 구분자로 본다 → 따옴표 안의 콤마는 건너뛴다.

```csharp
const string LINE_SPLIT_RE = @"\r\n|\n\r|\n|\r";
```
Windows(CRLF) / Unix(LF) / 구형 Mac(CR) 개행을 모두 처리. **긴 것부터 나열해야** CRLF가 두 줄로 쪼개지지 않는다.

---

## 2. 엑셀 → CSV 작업 흐름

```
Origin/번역.xlsx  (기획자 작업본, 병합셀·색상 등 자유)
        │  다른 이름으로 저장 → "CSV UTF-8 (쉼표로 분리) (*.csv)"
        ▼
Assets/Resources/Localization.csv   (게임이 읽는 유일한 파일)
```

- 원본 xlsx는 `Assets/` 밖이나 `Origin/` 폴더에 두고, Unity가 읽는 건 `Resources/`의 CSV 하나로 고정한다.
- **사본을 여러 곳에 두지 말 것.** 이 저장소는 `Assets/LocalizationTool/Data.csv`와 `Assets/Resources/Data.csv` 두 벌이 있는데 실제로 로드되는 건 `Resources/` 쪽뿐이라, 다른 쪽을 수정하고 "왜 안 바뀌지" 하기 딱 좋다.
- 엑셀이 앞뒤 공백을 남기는 경우가 많다(`100.5 `). 파서에서 `Trim()`하거나 엑셀에서 `TRIM()`으로 정리한다.

---

## 3. 구글 시트 연동

### 준비
1. 시트 공유 → **"링크가 있는 모든 사용자" · 뷰어**로 설정.
2. CSV 내보내기 URL 조립:
   ```
   https://docs.google.com/spreadsheets/d/{시트ID}/export?format=csv
   https://docs.google.com/spreadsheets/d/{시트ID}/export?format=csv&gid={탭ID}
   ```
   `시트ID`는 편집 URL의 `/d/`와 `/edit` 사이 문자열. `gid`는 탭을 선택했을 때 주소 끝에 붙는 숫자.

### 런타임 로드 (async)

```csharp
using UnityEngine;

public class LocalizationBootstrap : MonoBehaviour
{
    const string SheetUrl = "https://docs.google.com/spreadsheets/d/{시트ID}/export?format=csv";

    async void Awake()
    {
        Localization.EnsureInit();          // 1) 먼저 로컬 CSV로 즉시 동작 시작 (오프라인 보험)

        var rows = await CSVReader.ReadURL(SheetUrl);
        if (rows != null && rows.Count > 0)
            Localization.Load(rows);        // 2) 받아지면 덮어쓰고 OnLanguageChanged 발행 → 화면 자동 갱신
    }
}
```

코루틴 버전:

```csharp
void Start()
{
    Localization.EnsureInit();
    StartCoroutine(CSVReader.ReadURL(SheetUrl, rows =>
    {
        if (rows != null && rows.Count > 0) Localization.Load(rows);
    }));
}
```

### 주의
- **동기 로드는 불가능하다.** 웹 요청은 프레임을 넘겨야 하므로 `ReadFile`처럼 `return`으로 받을 수 없다. 반드시 로컬 CSV 폴백을 함께 둔다.
- 구글 시트는 리다이렉트를 태운다. `UnityWebRequest`는 기본적으로 따라가지만, WebGL 빌드에서는 **CORS로 막힌다**. WebGL이면 시트를 직접 치지 말고 빌드 파이프라인에서 CSV를 받아 `Resources/`에 굽는다.
- 출시 빌드가 외부 시트에 의존하면 시트 하나 잘못 건드려도 전체 텍스트가 깨진다. 실서비스는 **빌드 시점에 CSV를 동결**하고, 시트 직결은 개발/QA 빌드 한정으로 쓴다.

---

## 4. Resources 대신 쓸 수 있는 위치

| 방식 | 로드 코드 | 장단점 |
|---|---|---|
| `Resources/` | `Resources.Load<TextAsset>("Localization")` | 가장 간단. 단 Resources 폴더 전체가 빌드에 포함되고 초기 로딩이 길어진다 |
| `StreamingAssets/` | `UnityWebRequest.Get(Application.streamingAssetsPath + "/Localization.csv")` | 빌드 후 파일 교체 가능(핫픽스). 안드로이드는 압축돼 있어 UnityWebRequest 필수 |
| `Addressables` | `Addressables.LoadAssetAsync<TextAsset>("Localization")` | 패치 배포에 유리. 패키지 의존 추가 |
| `persistentDataPath` | `File.ReadAllText(...)` | 다운로드 후 캐시. 사용자 조작 가능(치트) |

기본은 `Resources/`로 시작하고, 텍스트를 자주 패치해야 하면 StreamingAssets로 옮긴다. 어느 경로든 최종적으로 `CSVReader.ReadString(text)` → `Localization.Load(rows)`로 들어오므로 코어는 그대로다.

---

## 5. 현지화 외 용도 (스탯 테이블)

같은 파서를 데이터 테이블에도 쓴다.

```csv
Damage,Defense,Name
10,100.5,블랙드라곤
20,150.0,레드드라곤
```

```csharp
List<Dictionary<string, object>> data = CSVReader.Read("Dragons");
for (int i = 0; i < data.Count; i++)
{
    int damage    = (int)data[i]["Damage"];
    float defense = (float)data[i]["Defense"];
    string name   = (string)data[i]["Name"];
}
```

`data[행번호]["헤더명"]` → 해당 셀 값. `Read`는 int → float → string 순으로 타입을 추론하므로, **`(float)data[i]["Defense"]`는 값이 `150`(소수점 없음)이면 int로 파싱되어 캐스팅 예외가 난다.** 소수 컬럼은 CSV에 반드시 `150.0`처럼 소수점을 남기거나, `Convert.ToSingle(...)`로 받는다.
