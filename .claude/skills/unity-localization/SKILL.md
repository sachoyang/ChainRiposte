---
name: unity-localization
description: Unity 프로젝트에 CSV 기반 다국어(현지화) 시스템을 붙일 때 사용. 언어 전환, 텍스트/이미지/폰트 현지화, 구글 시트 연동, SystemLanguage 기반 키-값 테이블, LocalizedText/LocalizedImage 바인더 컴포넌트 구현이 필요할 때 읽는다. "다국어", "현지화", "localization", "language change", "i18n", "번역 테이블", "CSV 읽기" 요청에 반응한다.
---

# Unity CSV 현지화 시스템

CSV 한 장 + static 이벤트 하나로 동작하는 경량 현지화 시스템. 외부 패키지(`com.unity.localization`) 없이 순수 C#으로 구현하며, Unity 2019~6000 전 버전에서 동작한다.

## 언제 이 스킬을 쓰나

| 상황 | 이 스킬 | Unity Localization 패키지 |
|---|---|---|
| 텍스트 수백~수천 줄, 언어 2~5개 | ✅ | 과함 |
| 기획자가 엑셀/구글시트로 번역 관리 | ✅ | 별도 임포터 필요 |
| 런타임 언어 전환(재시작 없이) | ✅ | ✅ |
| 씬/에셋 자체를 언어별 분기, 음성 더빙, Addressables 연동 | ❌ | ✅ |
| 복수형(plural), 성별 어미 등 문법 규칙 | ❌ (직접 구현) | ✅ |

가볍고 통제 가능한 걸 원하면 이 스킬, 대형 상용 프로젝트면 공식 패키지를 권한다.

## 아키텍처 (4개 레이어)

```
┌─ 1. 데이터 ─────────────────────────────────────┐
│  Assets/Resources/Localization.csv              │
│  Key,Korean,English,Japanese                    │
│  Title,이것은 제목,This is Title,これはタイトル  │
└──────────────────┬──────────────────────────────┘
                   │ Resources.Load / UnityWebRequest
┌─ 2. 파서 ────────▼──────────────────────────────┐
│  CSVReader.ReadFile(name) / ReadString(text)    │
│  → List<Dictionary<string,string>>              │
│    (행 리스트, 각 행은 헤더명 → 값)              │
└──────────────────┬──────────────────────────────┘
                   │ 최초 1회 인덱싱
┌─ 3. 코어 ────────▼──────────────────────────────┐
│  static Localization                            │
│   • Language 프로퍼티 (set 시 이벤트 발행)       │
│   • GetText(key) → 현재 언어 문자열              │
│   • event Action OnLanguageChanged              │
└──────────────────┬──────────────────────────────┘
                   │ 구독 (OnEnable) / 해제 (OnDisable)
┌─ 4. 바인더 ──────▼──────────────────────────────┐
│  LocalizedText  (Text / TMP_Text)               │
│  LocalizedImage (Sprite 교체)                   │
│  ...LocalizedAudio, LocalizedFont 등 확장        │
└─────────────────────────────────────────────────┘
```

**핵심 규칙 3가지**

1. **키 기반, 인덱스 기반 금지.** `SystemLanguage`는 알파벳순 enum이라 `English=10`, `Japanese=22`, `Korean=23`이다. `list[(int)language]`로 접근하면 24칸짜리 리스트가 필요해진다. 반드시 CSV 컬럼명 = `SystemLanguage.ToString()`으로 매칭한다.
2. **언어 전환은 프로퍼티 set 하나로.** 외부에서 `Localization.Language = SystemLanguage.English;` 만 호출하면 값 변경 + 이벤트 발행이 원자적으로 끝난다. 값 대입과 갱신 함수 호출을 따로 두면 반드시 한쪽을 빼먹는다.
3. **구독은 `OnEnable`, 해제는 `OnDisable`.** static 이벤트에 `Awake`에서만 구독하면 파괴된 오브젝트가 계속 이벤트를 받아 `MissingReferenceException`이 터진다. 이게 이 패턴의 1순위 버그다.

## 빠른 적용 (다른 프로젝트에 이식)

1. `references/implementation.md`의 코드를 아래 경로로 복사한다.
   - `Assets/Scripts/Localization/CSVReader.cs`
   - `Assets/Scripts/Localization/Localization.cs`
   - `Assets/Scripts/Localization/LocalizedText.cs`
   - `Assets/Scripts/Localization/LocalizedImage.cs`
   - `Assets/Editor/LocalizedSpriteDrawer.cs` ← **반드시 Editor 폴더** (`using UnityEditor`)
2. `Assets/Resources/Localization.csv`를 **UTF-8**로 만든다. 1행: `Key,Korean,English` (필요 언어 추가).
3. 텍스트 오브젝트에 `LocalizedText` 붙이고 인스펙터에 Key 입력.
4. 언어 전환 호출: `Localization.Language = SystemLanguage.English;`
5. 확인: 실행 → 언어 버튼 클릭 → 모든 텍스트가 즉시 바뀌면 성공.

## 참조 문서

| 파일 | 내용 | 언제 읽나 |
|---|---|---|
| `references/implementation.md` | 복사해서 바로 쓰는 전체 소스 (프로덕션 버전) | 새 프로젝트에 이식할 때 |
| `references/data-format.md` | CSV 규격, 인코딩, 구글 시트 연동, 파서 정규식 해설 | 데이터를 만들거나 시트를 붙일 때 |
| `references/pitfalls.md` | 실제로 터지는 함정 12가지와 해결법 | 버그가 났을 때 / 코드 리뷰할 때 |
| `references/recipes.md` | 폰트 교체, 서식 인자, 에디터 툴, 누락 키 검사 등 확장 | 기능을 더 붙일 때 |

## 이 저장소의 학습 예제 대응표

이 프로젝트(`C:\Study\Unity\Localization`)는 위 시스템을 단계적으로 만들어본 실습본이다.

| 실습 파일 | 단계 | 한계 / 다음 단계로 넘어간 이유 |
|---|---|---|
| `Localization1.cs` + `LocalizedText1.cs` + `GUITest1.cs` | 1단계: MonoBehaviour 싱글턴, `List<string>` 인덱스 접근, 이벤트가 문자열을 실어 나름 | `test[(int)language]` — enum 값이 10/22/23이라 사실상 사용 불가. 텍스트 1종류만 가능 |
| `Localization2.cs` + `LocalizedText2.cs` + `Localizedimage.cs` + `GUITest2.cs` | 2단계: static 클래스, CSV + Key 조회, 프로퍼티 set에서 이벤트 발행, 이미지 현지화 + `PropertyDrawer` | 매 `GetText` 마다 O(n) 선형 탐색, 구독 해제 없음, `using UnityEditor`가 런타임 스크립트에 있음 |
| `Localization3.cs` | `Localization2`의 이름만 바꾼 동일 사본 (미사용) | 삭제 대상 |
| `CSVReader.cs` | 파서. `Read`(object 타입 추론) / `ReadFile`(string) / `ReadURL`(코루틴 + async 2종) | 그대로 사용 가능. 숫자 파싱 culture만 주의 |
| `Test.cs` + `Resources/What.csv` | CSV 파서 단독 검증용 (드래곤 스탯 테이블) | 현지화와 무관, 파서 사용법 예제 |

프로덕션 버전은 2단계를 기반으로 위 한계 3가지를 모두 고친 것이다. 상세는 `references/pitfalls.md` 참조.
