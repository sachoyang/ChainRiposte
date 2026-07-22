# 함정 12가지 (실제로 터지는 것들)

증상 → 원인 → 해결 순. 위쪽일수록 자주, 크게 터진다.

---

## 1. 런타임 스크립트에 `using UnityEditor` → 빌드 실패

**증상**: 에디터에선 멀쩡한데 빌드하면 `The type or namespace name 'UnityEditor' could not be found`.

**원인**: `UnityEditor` 어셈블리는 빌드에 포함되지 않는다. `PropertyDrawer`, `CustomEditor`, `MenuItem`이 `Assets/Scripts/` 같은 런타임 폴더에 있으면 전부 터진다.

**해결**: 에디터 전용 코드는 `Assets/Editor/` 아래로 옮긴다(폴더명이 정확히 `Editor`여야 Unity가 에디터 전용으로 취급). 한 파일에 섞여야만 한다면:

```csharp
#if UNITY_EDITOR
using UnityEditor;
[CustomPropertyDrawer(typeof(LocalizedSprite))]
public class LocalizedSpriteDrawer : PropertyDrawer { ... }
#endif
```

> 이 저장소의 `Assets/Localizedimage.cs`가 이 상태다. 이식할 때 반드시 분리할 것.

---

## 2. static 이벤트 구독 해제 누락 → `MissingReferenceException`

**증상**: 씬을 다시 로드하거나 UI를 껐다 켠 뒤 언어를 바꾸면 `MissingReferenceException: The object of type 'Text' has been destroyed but you are still trying to access it`. 시간이 지날수록 느려진다.

**원인**: `Localization.OnLanguageChanged`는 static이라 씬을 넘어 살아있다. `Awake`에서 `+=`만 하고 해제하지 않으면 파괴된 컴포넌트의 델리게이트가 리스트에 계속 쌓이고, 이벤트가 발행될 때마다 죽은 객체를 건드린다. 메모리 릭이기도 하다.

**해결**: 구독/해제를 **쌍**으로 둔다.

```csharp
void OnEnable()  { Localization.OnLanguageChanged += Apply; Apply(); }
void OnDisable() { Localization.OnLanguageChanged -= Apply; }
```

`Awake`/`OnDestroy` 쌍도 되지만, `OnEnable`/`OnDisable`이 비활성 오브젝트까지 자동으로 처리해줘서 더 안전하다.

---

## 3. `list[(int)language]` — 인덱스로 언어 접근

**증상**: `ArgumentOutOfRangeException`, 또는 엉뚱한 언어 텍스트가 나옴.

**원인**: `SystemLanguage`는 알파벳 순서 enum이다.

```
Afrikaans=0 ... English=10 ... Italian=21, Japanese=22, Korean=23, ...
```
한국어를 쓰려면 리스트에 24칸이 필요하다.

**해결**: 언어는 **컬럼명 문자열**로 매칭한다.

```csharp
row[language.ToString()]   // "Korean" 컬럼
```

---

## 4. `GetText`마다 O(n) 선형 탐색

**증상**: 텍스트가 많은 화면 진입 시 프레임 드랍. 프로파일러에 `Localization.GetText`가 잡힌다.

**원인**:
```csharp
for (int i = 0; i < data.Count; i++)
    if (data[i]["Key"] == key) return data[i][language.ToString()];
```
키 1000개 × 화면 텍스트 100개 = 조회 10만 회. 매 언어 전환마다 반복된다.

**해결**: 로드 시 `Dictionary<string, Dictionary<string,string>>`로 인덱싱 → O(1). `implementation.md`의 `Localization.Load()` 참조.

---

## 5. 존재하지 않는 언어 컬럼 → `KeyNotFoundException`

**증상**: 일본어로 바꾸는 순간 예외. CSV에는 `Korean,English`만 있었다.

**원인**: `row[language.ToString()]`에서 없는 키를 인덱서로 접근.

**해결**: `TryGetValue` + 폴백 체인 (현재 언어 → 폴백 언어 → 키 자체). 화면에 키 이름이 그대로 보이면 "번역 누락"이라는 신호가 된다. 크래시보다 낫다.

---

## 6. 첫 실행 언어가 CSV에 없다

**증상**: 프랑스 유저에게 텍스트가 전부 키 이름으로 보임.

**원인**: `Application.systemLanguage`를 그대로 초기값으로 썼는데 CSV에 `French` 컬럼이 없음.

**해결**: 초기화 시 지원 목록을 확인한다.

```csharp
var sys = Application.systemLanguage;
_language = _supported.Contains(sys) ? sys : FallbackLanguage;
```
(`Localization.Load`가 `SupportedLanguages`를 채운 뒤에 판정해야 한다.)

---

## 7. CSV 인코딩 → 한글/일본어 깨짐

**증상**: `���` 또는 `ì´ê²ì`.

**원인**: 엑셀 기본 CSV 저장은 시스템 ANSI(한국어 Windows는 CP949)다. Unity `TextAsset`은 UTF-8로 읽는다.

**해결**: 엑셀에서 **"CSV UTF-8(쉼표로 분리)"** 로 저장. 이미 깨졌다면 메모장으로 열어 "다른 이름으로 저장 → 인코딩: UTF-8". BOM이 있으면 첫 헤더가 `﻿Key`가 되어 `Key` 조회가 실패하니, 파서에서 헤더를 `TrimStart('﻿')` 하거나 BOM 없는 UTF-8로 저장한다.

---

## 8. Enter Play Mode Options → static 상태가 남는다

**증상**: 두 번째 플레이부터 언어가 이전 실행 값으로 시작하거나, 이벤트에 옛 구독자가 남아있다.

**원인**: Project Settings → Editor → "Enter Play Mode Options"에서 Reload Domain을 끄면 static 필드가 초기화되지 않는다.

**해결**:
```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
static void ResetStatics() { _table = null; _initialized = false; OnLanguageChanged = null; }
```

---

## 9. `Awake` 구독 + `Start` 적용 순서 문제

**증상**: 씬 시작 시 일부 텍스트만 기본값(인스펙터에 박아둔 "New Text")으로 남는다.

**원인**: 구독은 `Awake`에서 했는데 최초 1회 적용을 `Start`에서 하거나, 아예 빠뜨림. 이벤트는 "변경 시점"에만 오므로 시작 시에는 아무도 갱신해주지 않는다.

**해결**: `OnEnable`에서 구독 직후 `Apply()`를 즉시 호출한다. 구독과 초기 적용이 같은 자리에 있으면 빠뜨릴 수 없다.

---

## 10. 값 대입과 갱신 호출이 분리되어 있다

**증상**: 어떤 버튼은 언어가 바뀌는데 어떤 버튼은 안 바뀐다.

**원인**:
```csharp
Localization.language = SystemLanguage.English;   // 필드 직접 대입
Localization.UpdateLanguage();                     // 이걸 빼먹으면 끝
```

**해결**: 필드를 `private`로 막고 프로퍼티 `set`에서 이벤트까지 발행한다. 외부 API를 `Localization.Language = x;` 하나로 줄이면 실수할 여지가 사라진다.

---

## 11. TextMeshPro 폰트 아틀라스에 글리프가 없다

**증상**: 일본어/중국어로 바꿨더니 네모(`□□□`)만 보인다.

**원인**: TMP는 미리 구운 아틀라스에서 글자를 찾는다. CJK는 문자 수가 많아 Static 아틀라스에 다 못 담는다.

**해결**:
- Font Asset의 Generation Settings를 **Dynamic**으로, Atlas Population Mode를 Dynamic OS 또는 Dynamic으로 설정.
- 또는 언어별 폰트 에셋을 만들고 언어 전환 시 교체한다 (`recipes.md`의 `LocalizedFont`).
- 폴백 폰트 체인(TMP Settings → Fallback Font Assets)에 CJK 폰트를 등록해두면 대부분 해결된다.

---

## 12. 레이아웃이 언어 길이를 못 견딘다

**증상**: 한국어에선 예쁜데 독일어로 바꾸면 버튼 밖으로 글자가 튀어나간다.

**원인**: 같은 문장이 언어에 따라 길이가 1.5~2배 차이난다(한/중/일 짧음, 독/러 김).

**해결**:
- 버튼 텍스트는 고정 크기 대신 `Content Size Fitter` + `Horizontal Layout Group`.
- TMP의 **Auto Size**(최소/최대 폰트 크기 지정)를 켠다.
- QA용으로 CSV에 `Pseudo` 컬럼을 만들어 `[!!! 이것은 제목 !!!]`처럼 길게 늘린 의사 언어를 넣고 테스트한다.

---

## 빠른 체크리스트 (코드 리뷰용)

- [ ] 에디터 전용 코드가 `Assets/Editor/` 또는 `#if UNITY_EDITOR` 안에 있는가
- [ ] 모든 바인더에 `OnEnable` 구독 / `OnDisable` 해제 쌍이 있는가
- [ ] `OnEnable`에서 최초 1회 `Apply()`를 호출하는가
- [ ] 조회가 Dictionary 기반 O(1)인가
- [ ] 없는 키 / 없는 언어 컬럼에 폴백이 있는가
- [ ] CSV가 UTF-8이고 사본이 한 벌뿐인가
- [ ] 언어 변경 진입점이 프로퍼티 하나뿐인가
- [ ] static 리셋(`RuntimeInitializeOnLoadMethod`)이 있는가
- [ ] 가장 긴 언어로 레이아웃을 확인했는가
