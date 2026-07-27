# CLAUDE.md — ChainRiposte 작업 지침 (매 세션 자동 로드)

> 이 파일은 세션 시작 시 자동으로 읽힌다. **인수인계는 이 파일의 `## 세션 인수인계` 섹션을 갱신**하는 것으로 대체한다.
> (기존 `마지막세션클로드답변.txt`는 이 파일로 이관됨.)

---

## 프로젝트 개요
- **ChainRiposte**: Unity 퍼즐 + 패링 전투 게임. 매치3 퍼즐 → 보스전(패링) 반복.
- **씬 흐름**: `StageSelect`(월드맵) → `Main`(퍼즐+전투). Main 단독 실행 가능(GameManager 인스펙터 기본 스테이지).
- **문서**: `Docs/GDD.md`(기획 기준), `Docs/ARCHITECTURE.md`(로드맵/구조), `Docs/PROJECTPLAN.md`.

## 구조 원칙 (자세한 내용은 ARCHITECTURE.md)
- **Core** = 순수 C# (`ChainRiposte.Core`, UnityEngine 참조 금지, 이벤트로만 노출).
- **Game** = 어댑터 (`ChainRiposte.Game`, Core + InputSystem/URP 참조). 의존 방향 `Game → Core` 단방향.
- **데이터 주도**: 밸런스는 전부 ScriptableObject. `~SO.ToConfig()`로 순수 C# config 주입. UnityEngine 타입(AnimationCurve 등)은 `Func<>`로 감싸 주입.
- **이벤트 기반 연출**: Core가 C# event 발행 → Game이 구독해 VFX/SFX/UI 갱신.
- **비주얼은 씬 오서링(중요)**: 손으로 배치·디자인하는 것(맵 노드, 배경, 캐릭터, UI 패널)은 **런타임 `new GameObject()`로 만들지 말고 씬/프리팹에 실물로 두고 컨트롤러가 참조만** 한다. 컨트롤러는 "행동"만. → 에셋/디자인 교체 = 씬에서 드래그(코드 0줄). 데이터로 개수가 정해지는 것(퍼즐 보드 타일 등)만 코드 생성 유지. UI 텍스트는 **TextMeshPro** 사용(legacy Text 아님).
- **초기 레이아웃은 에디터 빌더로**: 씬을 빈 채로 두지 말고 `Tools ▸ ChainRiposte ▸ ...` 빌더가 한 번 실물로 깔아주고, 이후 사용자가 씬에서 편집. (예: `StageSelectSceneBuilder`)
- **화면에 나오는 글씨는 전부 현지화(중요)**: 문자열을 코드/씬에 하드코딩하지 말 것. 원천은 **구글 시트 → CSV 한 장**(`Assets/_Project/Data/Resources/Localization.csv`)뿐이다.
  - 정적 문구 = TMP에 `LocalizedText` 컴포넌트 + 키 (빌더에서는 `EditorUiFactory.Localize(label, key)`).
  - 코드가 매번 채우는 문구(HP·턴 등) = `Loc.GetText(key, args)`. 이런 텍스트에는 `LocalizedText`를 붙이지 말 것(언어 전환 시 서로 덮어씀). 대신 그 컨트롤러가 `Loc.LanguageChanged`를 구독해 다시 그린다.
  - 새 키는 **시트에 추가**한다. `LocalizationMenu.StarterCsv()`는 최초 부팅용 목록일 뿐이다.
  - 검사: `Tools ▸ ChainRiposte ▸ Localization ▸ Find Missing Keys In Scene`. 설계 배경과 함정은 `.claude/skills/unity-localization/` 참조.
  - 한글 폰트는 `Assets/_Project/Font/neodgm SDF.asset`(네오둥근모, 동적 아틀라스). TMP Settings의 **기본 폰트 + 폴백** 양쪽에 등록돼 있다.

## 워크플로 지시 (메모리에도 저장됨)
- 세션 토큰 **90% 초과 시**: 하던 일 정리하고 아래 `## 세션 인수인계` 섹션 갱신 (①한 일 ②다음 할 일).
- **간단한 검증/플레이 확인은 MCP 돌리지 말고 사용자에게 요청** (토큰 절약). MCP는 컴파일/테스트/씬 배선 등 꼭 필요한 것만.
- 확인 요청은 **"①②③ 확인 부탁"** 형식.

## 핵심 자산 위치
- 데이터: `Assets/_Project/Data/` — Stage_01(기본/Main 인스펙터용), Stage_1_1~2_3, Boss_01/02, 타일 6종, PlayerStatsConfig.
- 테스트: EditMode (Core 대상). 수정 시 Test Runner 초록 유지.
- 스크린샷: `Assets/Screenshots/`(스모크 테스트, 삭제 무방).

---

## 세션 인수인계

> **다음 세션은 이 섹션을 먼저 읽고 시작한다.** 세션 종료 시 여기를 갱신할 것.

### 마지막 갱신: 2026-07-28 (세션 9)

- **커밋 규칙**: 메시지에 `Co-Authored-By: Claude` 트레일러를 넣지 않는다.
- **git**: 세션 9 = 사운드 배관 커밋 1개. push는 계속 안 함.
- **MCP 없었음**: 이번 세션은 MCP가 아예 안 붙어서 **컴파일 검증을 못 했다.** 눈으로만 검토함 — Unity 열면 콘솔 확인 부탁.
- **테스트**: 이번엔 Core를 안 건드려서 안 돌림(직전 99/99 유지 예상).

#### 다음 세션에서 가장 먼저 할 것

1. **사운드 배관 컴파일 확인** — `AudioService`/`JuiceDirector`/`SceneMusic` 3파일. 콘솔 에러 없는지.
2. **세션 8 미검증분 여전히 남음** — `Docs/VERIFICATION.md` §10-4~§10-9 (패링 띠 정합 / 판정 폭 / 보스 방향 / 길 클릭 / 맵 실루엣 / 2페이즈 보스). 세션 9에서 못 물어봄.
3. 그 다음: **사운드 클립 배선**(에셋 준비되면) 또는 **아트/배경 배선** 또는 **Boss_02 겉모습**(2-1~2-2 보스 투명) — 「게임 완성 남은 것」 참조.

#### ⚠ 게임 완성에 남은 것 (채보=사용자 기획 몫은 별개, 우선순위 순)

세션 9에서 "채보 빼고 제대로 된 게임이 되려면?" 을 점검한 결과. 게임 루프 자체는 처음~끝 다 돈다(골격 완성). 빠진 건 "살":

1. **🔴 사운드** — 프로젝트 전체 오디오 파일 **0개**. 배관은 세션 9에서 깔았으니(아래) **클립만 꽂으면 소리 남**. `Docs/AUDIO.md` 참조.
2. **🔴 Boss_02 겉모습 비어 있음** — 공용 그림이 없어 **2-1~2-2 전투에서 보스가 안 보임**(투명). 그림만 꽂으면 됨(버그급 구멍).
3. **🟠 아트가 아직 단색 사각형** — UI 버튼·패널, 퍼즐/전투 배경(세션 8에서 걷어냄). DEVNIK/Veilworks 팩 들어와 있음(배선 문제).
4. **🟠 2페이즈 보스 그림 미배선** — 껍데기(인살 페이즈)는 있고 phase1/trans/phase2 그림이 안 꽂혀 컷씬이 밋밋.
5. **🟡 튜토리얼/온보딩 없음** — 신규 플레이어가 매치3+하드코어 패링을 안내 없이 만남.
6. **🟡 콘텐츠 볼륨** — 스테이지 6개(월드2)뿐. 채보와 엮여 후순위.
7. **⚪ 안드로이드 실기 빌드 미검증**(GDD §9.3 출시 요구사항).

#### 세션 9에서 한 것 — 사운드 배관 정리 (코드만, 씬 배선 없음)

**핵심 버그 수정: 옵션 볼륨이 게임플레이 사운드에 안 먹었다.**
- 원인: 오디오 경로가 둘로 갈라져 있었다. `AudioService`(옵션 볼륨 연결)로 흐르는 건 월드맵 막힘음 하나뿐이고, **게임의 실제 음악·효과음 전부는 `JuiceDirector`가 자체 AudioSource로 재생**해 버스를 안 거쳤다. → 옵션 슬라이더가 게임 소리를 못 줄였다.
- 고침: **`AudioService`를 BGM/SFX 소스의 유일한 주인**으로. 버스 볼륨(`BgmVolume`/`SfxVolume`) 노출 + 생성 시 저장된 볼륨 적용 + `PlaySfx`에 pitch 인자 추가.
- `JuiceDirector`는 자체 음악·효과음 소스를 **버리고** `AudioService.PlayBgm`/`PlaySfx`로 흘림 — 매치·패링·피격·인살·BGM 전부 옵션을 탄다.
- 예외 **난입 크레센도(`tensionLoop`)만** 자체 루프 소스 유지(매 프레임 볼륨 조절 필요) + BGM 버스 볼륨 곱하기.
- 버스 배정: **음악·크레센도 → BGM 슬라이더**, **단발 효과음 → SFX 슬라이더**.

**신규 `Game/Audio/SceneMusic.cs`** — JuiceDirector 없는 씬(인트로/타이틀/월드맵) BGM용. GO에 붙이고 클립만 꽂으면 씬 진입 시 재생. **클립 없으면 no-op**(앞 씬 음악 안 끔). `AudioService.PlayBgm`은 같은 클립이면 안 끊으므로 씬 넘나들어도 이어짐.

**신규 `Docs/AUDIO.md`** — 클립 꽂을 슬롯 전수 목록(JuiceDirector 10 + SceneMusic 3씬 + `StageSelectController.blockedSfx`) · 버스 배정 · 배선 절차 · 검증 포인트 · CC0 조달 메모(freesound/kenney/incompetech + 검색어).

**나중에 에디터에서 할 배선**(이번엔 코드만): 클립을 `Assets/_Project/Audio/`에 정리 → `Docs/AUDIO.md` 표대로 슬롯 드래그 → 인트로/타이틀/월드맵에 빈 GO + `Scene Music` 컴포넌트. 옵션 슬라이더로 각 소리 따로 줄어드는지 확인.

---

### 마지막 갱신: 2026-07-27 (세션 8)

- **커밋 규칙**: 메시지에 `Co-Authored-By: Claude` 트레일러를 넣지 않는다.
- **git**: 세션 8 작업 전부 커밋(5개). push는 계속 안 함.
- **테스트**: EditMode **99/99** (인살 페이즈 테스트 5개 추가). MCP로 확인함.
- **MCP**: 세션 중 한 번 끊겼다가 복구됨. 끊기면 컴파일 검증을 못 하니 사용자에게 콘솔 확인을 부탁할 것.

#### ⚠ 빌더 재실행 금지 (세션 7에 UI 날린 사고 — 계속 유효)

- **`Build Main Scene UI` / `Build App Scenes` 재실행 금지.** 화면을 자식째 지우고 재생성해서 손으로 꽂은 UI 스프라이트가 날아간다.
- 새 UI는 **비파괴 전용 메뉴**로 얹는다. 지금까지 만든 것: `Add Pause Menu To Main`, `Add Phase Cutscene To Main`.
- **UI 배선하면 즉시 커밋.**

#### 다음 세션에서 가장 먼저 할 것

1. **`Docs/VERIFICATION.md` §10-4 ~ §10-9 결과 물어보기** — 세션 8 몫 전체가 미검증이다.
   (패링 띠 정합 / 판정 폭 / 보스 방향 / 길 클릭 유도 / 맵 실루엣 / 2페이즈 보스)
2. **미커밋 데이터 확인** — 세션 끝 시점에 `Boss_01/02/03`·`Stage_2_3`·폰트가 미커밋 상태였다(사용자가 인스펙터에서 작업 중). 어디까지 됐는지 맞춰 볼 것.
3. **`Add Phase Cutscene To Main` 재실행했는지** — ◆ 텍스트 버전으로 깔았으면 빨간 원이 안 나온다.

#### ⚠ 지금 가장 큰 구멍 — 보스 채보가 없다

- **`Boss_02`·`Boss_03` 은 `patterns` 가 0개다.** `BossDataSO.BuildPatterns()` 가 에러 로그를 찍고 **임시 패턴(정박 4타)** 으로 대체해 돌고 있다. `Boss_01` 만 3개(Steady / Double Sweep / Flurry).
- 그래서 **2페이즈 보스가 "더 어려운 패턴"이 될 수 없다** — 1·2페이즈 둘 다 같은 임시 4타를 쓴다.
- **추천 순서**: 검증 → **채보 미리듣기 에디터** → 그걸로 Boss_02/03 채보 짜기.
  미리듣기를 먼저 만드는 이유: 지금은 인스펙터에서 찍고 → 플레이 → 퍼즐 깨고 → 보스전까지 가야 확인된다.
  짜야 할 채보가 사실상 3벌(Boss_02, Boss_03 1페이즈·2페이즈)이라 여기서 시간이 제일 많이 샌다.

#### 세션 8에서 한 것

**패링 띠와 실제 판정이 어긋나 있었다 (버그)** — `CombatScreen`
- 흰 원은 **두께가 있는 그림**인데 판정은 그 원의 **바깥 테두리** 위치로 계산하고 있었다. "띠에 걸친 것처럼 보이는데 아직 판정 밖"인 구간이 **원 두께만큼 통째로** 있었다.
- 이제 **원의 안쪽 테두리가 노트의 위치**다: 안쪽 테두리가 띠에 들어오면 판정 시작, 띠의 안쪽 끝을 지나면 유예 종료. 보이는 겹침이 곧 판정이다.
- 실제 아트로 갈 때를 위해 `noteRingInnerRatio`(기본 0.88 = `PlaceholderSprite.Ring`)를 인스펙터로 뺐다. **아트를 바꾸면 이 값도 그 그림의 비율로 고쳐야** 계속 맞는다.
- 사용자가 말한 "공격 후 패링이 밀리는 느낌"의 주범도 이것으로 본다(일찍 눌러 헛침 → 0.35초 잠금 → 다음이 밀려 보임).

**판정 폭 하향 (사용자 결정 A안)** — `PlayerStatsConfig`
- `baseParryWindowSeconds` 0.25 → **0.13**, `parryWindowPerLevelSeconds` 0.015 → **0.024**. 유예 0.12·캡 5는 그대로.
- 결과: **캡에서의 폭이 예전 기본값과 같아진다**(총 0.37초). Lv0 총 0.25초. 성장 체감 +20% → **+48%**.
- 기준: 무투자는 실제로 어려워야 하고, +PARRY를 찍으면 띠가 **눈에 띄게** 굵어져야 한다.
- 조일 순서: `parryWindowPerLevelSeconds`(캡이 관대) → `baseParryWindowSeconds`(초반이 관대) → `parryLateGraceSeconds`.

**공격 커밋 중 패링은 그대로 씹힌다 (사용자 결정)** — 버퍼링·취소 안 넣음. 쿨다운(0.4초) 후 누르면 된다.

**보스가 왼쪽을 본다** — `CombatScreen ▸ flipBossHorizontally`(기본 켬)
- 이 프로젝트 그림은 전부 오른쪽을 보고 그려져 있어서 **오른쪽에 서는 보스만** 뒤집어야 마주 본다.
- 평상시 스케일을 `_bossBaseScale` 한 곳에 두고 연출은 곱하기만 한다 — `Punch`가 `Vector3.one`으로 되돌려 반전을 풀던 것을 막았다.
- 준비 화면의 `BossShadow`는 안 건드렸다(위에서 내려오는 실루엣이라 마주 볼 상대가 없다).

**월드맵 — 길을 눌러도 한 칸 움직인다** — `StageSelectController.ResolvePathClick`
- 세로 스크롤에서 **다음 노드가 화면 밖이라 누를 방법이 없던** 문제. 노드를 못 맞히면 길을 맞혔는지 보고 **누른 쪽으로 한 칸** 간다.
- 방향은 고정이 아니라 **누른 지점이 서 있는 노드보다 앞/뒤냐**로 그때그때 정한다.
- 길 위의 점 번호 ÷ `pathSmoothing` = 노드 좌표(노드 i는 정확히 i×smoothing 번째 점)라는 성질을 쓴다.
- `RefreshPathLine`이 **LineRenderer가 없어도 경로를 계산**하게 바꿨다 — 클릭 판정이 같은 점들을 본다.
- 반경 = `pathClickRadius`(기본 1.2, 0이면 끔).

**맵 실루엣이 캐릭터별 겉모습을 못 타던 버그** — `ApplyPortrait`가 `BossData.Portrait`를 직접 읽고 있었다. 이제 `BossVisual.ResolveSprite`를 쓴다(전투·준비 그림자와 같은 규칙, 항상 페이즈 0).

**2페이즈 보스 (인살 두 번)** — 이번 세션의 본체
- ⚠ **이름 주의**: `BossPhaseConfig`는 **HP 구간별 채보 풀**이고 새로 만든 `BossBattlePhase`가 **인살 페이즈**다. 2층이다 — 인살 페이즈(겉모습·게이지가 통째로 바뀜) ▸ HP 구간 페이즈(같은 모습으로 채보만 험해짐).
- `Core/Combat/BossBattlePhase` = 인살 한 번 분량(HP·체간·채보 풀).
- 인살하면 페이즈가 남았는지 보고, 남았으면 승리가 아니라 **`AwaitingPhaseTransition`으로 시간을 멈춘다.** 컷씬이 끝나면 Game이 `BeginNextPhase()`로 재개시킨다 — **Core가 컷씬 길이를 알면 연출을 바꿀 때마다 규칙을 고쳐야 한다.**
- HP·체간은 **이어받지 않고 만땅으로 새로**(사용자 결정 = 사실상 2연전). 페이즈마다 수치를 따로 둔다.
- 인살 페이즈를 안 짠 보스는 `BossConfig.ResolveBattlePhases()`가 1페이즈로 감싼다 — `CombatSystem`에 "페이즈가 있는 보스/없는 보스" 두 갈래가 안 생긴다.
- **남은 인살 횟수는 페이즈 번호가 아니라 별도 카운터(`_deathblowsDone`)로 센다.** 인살 직후~다음 페이즈 시작 사이(컷씬)에는 번호가 아직 안 올라가서 방금 인살한 몫이 남아 보인다.
- `BossDataSO ▸ 인살 페이즈[]` — **비운 칸은 공용값으로 떨어진다**(페이즈를 나눴다는 이유로 같은 숫자를 두 번 적지 않게).
- 겉모습: `캐릭터별 겉모습 ▸ 인살 페이즈별 그림`(아세프리트 `phase1/trans/phase2` 레이어). **캐릭터 지정이 페이즈 지정을 이긴다** — 페이즈를 안 나눈 캐릭터가 남의 페이즈 그림을 쓰면 안 된다.
- 컷씬: 번쩍 → 암전 → trans 그림 + 문구 → phase2로 교체 → 오른쪽에서 재등장. **보스 그림은 화면이 덮인 동안 갈아 끼운다**(밝을 때 바꾸면 툭 바뀌는 게 보인다). 아무 데나 눌러 넘기기.
- 컷씬 자리를 안 배선했으면 그림만 바꾸고 짧게 넘어간다 — **배선이 덜 됐다고 전투가 멈추면 안 된다.**
- 현지화 키 `boss.phase.transition` 추가.

**인살 UI (세키로식 빨간 원)**
- **인살 게이지**(남은 인살 횟수) = 보스 **왼쪽 위**. 보스 몸의 **형제**다 — 보스가 좌우 반전돼 있어서 자식으로 넣으면 오른쪽으로 뒤집혀 나온다.
- **인살 대기 마크** = 보스 **한가운데**, 체간이 무너졌을 때만. 보스 몸의 **자식**이라 보스가 튈 때 같이 튄다(원이라 반전은 안 보인다).
- 끝낸 인살은 **지우지 않고 어둡게** 남긴다 — 개수가 줄면 원래 몇 번짜리 보스였는지 알 수 없다.
- 개수가 데이터로 정해지므로 씬의 원본 하나를 복제한다(노트 원·캐릭터 카드와 같은 규칙). 아트가 생기면 `DeathblowMarks/MarkTemplate`·`BossBody/ExecuteMark`의 **Image 스프라이트만** 갈아 끼운다.

**에디터 (둘 다 비파괴)**
- `Tools ▸ ChainRiposte ▸ Add Phase Cutscene To Main` — 컷씬 캔버스(정렬 **17**: 준비 15 위, **일시정지 18 아래** — 컷씬 중 일시정지 메뉴가 가리면 안 된다) + 인살 게이지 + 대기 마크. 같은 이름의 것만 갈아 끼운다.
- `Tools ▸ ChainRiposte ▸ Create Two-Phase Boss (2-3)` — `Boss_02` 복사 → `Boss_03` + 인살 페이즈 2줄, **2-3에만** 배정.

**"이미 있으면 안 건드린다"가 사고를 조용하게 만든 건 (교훈)**
- 사용자가 `Boss_03`을 손으로 복제해 만들었고(인살 페이즈 1줄·텅 빔), 메뉴는 "이미 있으면 통째로 지나감"이라 그대로 뒀다. 결과: **2-3에서 첫 인살에 그냥 죽었다.**
- 고침: 메뉴가 **모자란 줄과 빈 칸만** 채우고(이미 적힌 값은 안 건드림), 스테이지 배정은 항상 다시 확인한다.
- ⚠ **SerializedProperty 배열을 늘리면 Unity가 직전 원소를 복사한다.** 새로 늘린 줄은 반드시 비워야 2페이즈가 1페이즈 그림을 물려받지 않는다.
- `BossDataSO`가 **인살 페이즈 한 줄이면 경고**한다. 조용히 넘어가면 "인살했는데 그냥 죽는다"로만 보여 원인을 못 찾는다.

#### 세션 8에서 남긴 것 (다음 후보)

- **Boss_02·Boss_03 채보** (위 「가장 큰 구멍」 참조) + **채보 미리듣기 에디터**
- `Boss_03` 이름 키가 `Boss_02`와 중복 — 2-1·2-2와 같은 이름으로 뜬다. 다르게 할 거면 CSV 키 추가 후 `캐릭터별 겉모습 ▸ nameKey` 교체
- 2페이즈 그림 배선(아세프리트 **Layer Import Mode = Individual Layers** 로 바꾼 뒤)
- 사운드 — `Main.unity` 에 빈 클립 슬롯 7개(JuiceDirector·AudioService)
- 퍼즐·전투 배경(테마 `puzzle`/`combat` 자리는 세션 7에서 걷어냄), NPC 스프라이트 시트 → Animator,
  UI 에셋 배선 잔여, 캐릭터 3번째, 안드로이드 실기 빌드

---

### 세션 7 갱신: 2026-07-24

- **커밋 규칙**: 메시지에 `Co-Authored-By: Claude` 트레일러를 넣지 않는다.
- **git**: 세션 7 작업 전부 커밋(주제별 다수) + 마지막에 **작업 중 씬·에셋 스냅샷** 1개. push는 계속 안 함.
- **테스트**: EditMode **94/94** (MCP로 확인함. 이번 세션 Core 안 건드림).
- **MCP 연결됨** — 컴파일/테스트는 `refresh_unity`(force+compile) → `read_console`(error) → `run_tests`(EditMode)로 확인 가능.

#### ⚠ 빌더 재실행 금지 (중요 — 지난 세션에 UI 날림)

- **`Build Main Scene UI` / `Build App Scenes` 재실행 금지.** 화면을 자식째 지우고 재생성해서 **손으로 꽂은 UI 스프라이트가 날아간다.** 실제로 이번 세션에 한 번 잃었다(커밋 전이라 복구 불가).
- 새 UI를 얹을 땐 **비파괴 전용 메뉴**를 만든다(예: `Add Pause Menu To Main`).
- **사용자에게 반복 강조**: UI 배선하면 **즉시 커밋**.

#### (기록) 세션 7 시점의 「먼저 할 것」 — 세션 8에서 대부분 처리됨

1. **일시정지 메뉴 배선 확인/마무리** — `Tools ▸ ChainRiposte ▸ Add Pause Menu To Main` 실행 후,
   `PauseCanvas/TopRight` 버튼 이미지 + `PauseMenu` 인스펙터의 `pauseSprite`/`playSprite`/설정 아이콘을 꽂았는지.
2. **버튼 pressed 스프라이트** — 사용자가 DEVNIK 버튼(눌림 스프라이트 포함)으로 전 버튼을 **직접** Sprite Swap 배선 중.
   툴 안 만들기로 함(사용자 요청). 방법은 Button ▸ Transition = Sprite Swap + Pressed Sprite.
3. **월드맵 배경/길 배치** — 사용자가 `ThemedBackground`(길)·`SkyBackground`·`BottomBackground`(월드 스프라이트로 바꿔야 함, UI로 넣었었음) 배치 + 「길 그리기」로 노드 찍는 중. `Docs/VERIFICATION.md` §9 참조.
4. `Docs/VERIFICATION.md` **§9 검증 결과** 물어보기 (세션 7 몫 전체 — 배경 왕복/테마/보스 이름/세로 스크롤/길 곡선/그림자/일시정지).

> 참고: 마지막 커밋은 **작업 중 스냅샷**이라 씬·에셋이 미완성 상태일 수 있다(노드 위치, 배경 배치, UI 배선 진행 중). 사용자와 어디까지 됐는지 맞춰 보고 이어갈 것.

#### 세션 7에서 한 것

**배경 좌우 왕복 — `Game/UI/BackgroundPanner`**
- 배경을 **원본 비율 그대로** 화면을 덮게(cover) 키우고, 잘려 나간 폭 안에서 사인 곡선으로 좌우 왕복(기본 24초/바퀴).
- **UI(`Image`) / 월드(`SpriteRenderer`) 둘 다** 지원. 덮을 범위가 UI면 부모 rect, 월드면 **카메라가 보는 크기**다. 월드맵 배경이 SpriteRenderer라 한쪽만으로는 모자랐다.
- **남는 폭이 없으면 안 움직인다** — 가로 화면에서 비율이 딱 맞으면 가운데 고정. 일부러 여유를 만들려면 `coverScale`을 1보다 올린다.
- `AspectRatioFitter(EnvelopeParent)`를 안 쓴 이유: 그쪽은 레이아웃마다 `anchoredPosition`을 0으로 되돌려 좌우 이동과 싸운다.
- 방향 전환: `OrientationService.Changed` 구독 + **매 프레임 덮을 범위·스프라이트 비교**로 이중 대비. 스프라이트가 바뀌어도(테마 전환) 알아서 다시 맞추므로 **실행 순서에 기대지 않는다.**

**캐릭터별 컨셉(테마)** — 기사=이루실 / 낭인=아시나
- **보이는 것만 바뀌고 난이도는 공유한다.** HP·체간·채보·패턴은 `BossDataSO` 하나 그대로 — 이 선을 넘으면 캐릭터 선택이 난이도 선택이 된다.
- `Game/Theme/ThemeSO` — 컨셉 한 벌 = 에셋 하나. `backgrounds[]`(키→스프라이트) + `bosses[]`(bossId→그림·이름키). 배경 키는 자유 문자열(`map`/`puzzle`/`combat`)이라 **나중에 `stage.1-1` 처럼 잘게 쪼개도 코드가 안 바뀐다.**
- `Game/Theme/ThemeService` — `CharacterService.Current.Theme`를 묻는 창구. **테마를 따로 저장하지 않는다**(저장 상태가 둘로 갈라지면 반드시 어긋난다). 이벤트도 중계하지 않는다 — 정적 초기화 순서에 기대게 되므로 구독자는 `CharacterService.Changed`를 직접 본다.
- `Game/Theme/ThemedSprite` — **`LocalizedText`와 같은 물건**. 씬의 그림에 붙이고 키만 준다. `Image`/`SpriteRenderer` 둘 다. **테마에 그 키가 없으면 씬의 그림을 그대로 둔다**(배선을 덜 했다고 화면이 비면 안 된다). 어느 화면을 테마로 바꿀지는 코드가 아니라 **컴포넌트를 어디 붙였는지**가 정한다.
- `PlayerCharacterSO.theme` 한 줄이 연결의 전부.
- **인트로·타이틀은 테마를 안 탄다**(사용자 결정) — 캐릭터를 고르기 전에도 보이는 공용 화면이라. 타이틀 배경은 `ashina` 고정 + 좌우 왕복.

**보스 겉모습·이름**
- `BossDataSO`에 `bossId`(비우면 에셋 이름) + `nameKey` 추가. `CombatController.ResolveBossSprite/NameKey`가 테마 → 없으면 SO로 떨어진다.
- `CombatScreen.SetBossVisual`이 이제 **이름 키**를 받는다. 키가 CSV에 없으면 받은 문자열을 그대로 쓰므로 구 데이터의 생 이름("The Warden")이 **경고 없이** 계속 나온다.
- 보스 **타일**은 통일 유지 — 종류와 무관하게 "이게 보스 타일이다"를 한눈에 알아야 한다.
- CSV/StarterCsv에 `boss.irithyll.01/02`, `boss.ashina.01/02` 추가.

**에디터**
- `Tools ▸ ChainRiposte ▸ Theme ▸ Create Default Themes` — `Theme_Irithyll`/`Theme_Ashina` 생성 + 캐릭터에 연결. **빈 슬롯만 채운다.** 보스 그림은 일부러 비워 둠(비면 SO 그림으로 떨어짐).
- `... ▸ Setup Background In Open Scene` — **열려 있는 씬**만 손댄다(씬을 몰래 열고 저장하지 않는다). Title이면 배경=ashina+왕복, StageSelect면 `ThemedBackground` 생성 + 색 사각형 `World1Bg`/`World2Bg` 비활성.
- `StageSelectSceneBuilder`도 색 사각형 2장 대신 `ThemedBackground`를 깔도록 교체.

**인트로 — 검정 + 부드러운 페이드**
- 배경을 그림 없이 완전한 검정으로(메뉴가 카메라 클리어 컬러까지 맞춘다). 로고만 보이는 화면이라 배경에 뭘 두면 로고가 죽는다.
- `IntroController`의 페이드가 선형 → `SmoothStep`. 양 끝이 눕지 않으면 알파가 툭 끊겨 보인다.

**월드맵 세로 재구성 (사용자 요청)** — "배경이 계속 움직여 정신사납다 / 길을 다 보여주지 말고 스크롤"
- **세로** = 위 배경 띠 / 가운데 길(스크롤) / 아래 정보 띠. **가로** = 예전처럼 길 전체 + 오른쪽 정보 컬럼(이미 그렇게 돼 있었다).
- 마스크를 따로 안 썼다 — **불투명한 UI 띠가 그대로 마스크다.** Overlay 캔버스는 월드 스프라이트 위에 그려지므로 띠가 길의 위/아래를 가려 창을 만든다. LineRenderer는 SpriteMask가 안 먹으므로 이 방법이 아니면 마스킹이 까다롭다.
- `Game/Map/MapCameraRig` — 세로에서 카메라를 스크롤. **카메라 중심(화면 0.5)과 창 중심이 어긋나므로 그 차이만큼 밀어 주는 게 핵심 계산.** 가로에서는 `CameraFit2D`에 넘기고 자기는 빠진다(둘이 동시에 카메라를 만지면 서로 되돌린다 → 세로에서 `cameraFit.enabled = false`).
- 띠 비율을 **씬의 RectTransform에서 매 프레임 잰다**(`topBand`/`bottomBand`). 숫자를 코드와 씬 양쪽에 적어 두면 띠 높이를 고친 순간 창 중심이 어긋난다. 계산을 전부 `LateUpdate`에 모은 것도 같은 이유 — 방향 전환 때 `OrientationLayout`과 실행 순서를 다투면 한 프레임 어긋난 값을 읽는다.
- `Game/UI/OrientationVisibility` — 방향에 따라 **그리는 컴포넌트만** 끈다. `SetActive(false)`로 자기를 끄면 스크립트도 같이 멈춰 다시 켤 방법이 없어진다.
- **배경이 3층이다** (사용자 요청으로 역할 재정의):
  - `SkyBackground` (월드, sortingOrder −200, 키 `map`) — 배경(하늘·원경). 화면을 덮고 **세로·가로 모두**.
  - `ThemedBackground` (월드, −100, 키 **`path`**) — **길이 놓인 땅**. 배경과 다른 그림이라 키가 다르고, **화면을 덮지 않는다**(덮으면 뒤의 배경이 무의미). 크기·위치는 씬에서 길에 맞춰 잡는다 → `BackgroundPanner`는 꺼 둔다.
  - `Canvas/TopBackground` (UI 띠, 키 `map`, **세로 전용**) — 길 윗부분을 가려 창을 만드는 게 유일한 일.
  - `BottomBackground` (월드, −150, 키 `map`, **선택 요소**) — 화면 아래 빈 공간을 채운다. 길 뒤·하늘 앞이라 겹치면 길이 덮고 빈 아래에서만 보인다. **월드 스프라이트라 위로 스크롤하면 저절로 밀려 사라진다**(가리는 코드 불필요). 상단 띠와 같은 `map` 그림. `Setup Background`는 씬에 이 오브젝트가 **있을 때만** 배선한다(위치는 사용자가 잡는다).
  - 왜 `map` 그림을 두 오브젝트가 나눠 맡나: **Overlay 캔버스는 항상 월드 스프라이트 위에 그려진다.** 세로에서 길을 가리려면 UI여야 하고, 가로에서 길 뒤에 깔리려면 월드여야 한다. 한 오브젝트로는 둘 다 못 한다.
- 상단 띠는 **띠(RectMask2D) + 자식 Image** 구조다. `BackgroundPanner`는 '부모를 덮는' 물건이라 이미지에 직접 붙이면 캔버스 전체로 커진다.
- 월드맵 배경은 `amplitude = 0`(고정). 눈이 길을 따라가야 하는 화면이라 배경이 움직이면 방해다 — 타이틀과 정반대.
- `Setup Background In Open Scene` 이 노드 세로 간격을 1.8배로 벌릴지 **물어본다**(노드 위치는 사용자 것이라 말없이 안 바꾼다). 안 벌리면 세로 화면에 길이 거의 다 들어와서 스크롤이 안 느껴진다.

**월드맵 3종 수정 (사용자 보고: "카메라가 좌우로 안 움직이고, 길이 찍어 둔 곳과 다른 데 있다")**
1. **좌우 추적 추가** — `MapCameraRig`가 세로에서 X를 `_bounds.center.x`로 고정하고 있었다. 이제 Y와 같은 방식으로 X도 따라가고 길 밖으로 안 나가게 가둔다(`ClampToBounds` 공용).
2. **월드맵 배경은 정적** — `SkyBackground`/`ThemedBackground`의 `BackgroundPanner`를 끈다. **노드를 그림 위에 찍어 배치하는데 실행할 때 그림이 카메라를 따라 움직이면 찍어 둔 자리와 그림이 어긋난다.** 크기·위치는 씬에서 잡는다. (타이틀·인트로는 반대로 켜 둔다 — 거기엔 찍을 게 없다.)
3. **노드 벌리기를 `Setup Background In Open Scene`에서 분리** → `Theme ▸ Spread Map Nodes Vertically` 별도 메뉴. 배경을 다시 깔 때마다 노드가 1.8배로 딸려 움직이던 것이 "다른 곳에 찍힘"의 유력한 원인.
- ⚠ **테마별 길 그림은 크기·길 모양이 같아야 한다.** 노드 위치는 테마와 무관하게 하나뿐이라 이루실/아시나의 길이 다른 자리면 한쪽이 어긋난다. `ThemedSprite`는 에디트 모드에서 안 돌므로 씬 뷰에는 씬에 꽂아둔 그림이 보인다.

**길 그리기 (사용자 요청)** — "원하는 곳을 찍으면 노드가 생기고 길이 자연스럽게 이어지고 카메라도 따라가게"
- `Game/Map/MapPath` — 노드를 지나가는 Catmull-Rom 곡선. 꺾은선으로 이으면 지도가 아니라 그래프처럼 보인다. **경로선과 캐릭터 이동이 같은 곡선을 쓰는 게 핵심** — 따로 계산하면 캐릭터가 그려진 길에서 벗어나 걷는다. `pathSmoothing` 1이면 예전 꺾은선(하위호환).
- 걷기는 **점마다 멈추지 않는다** — 한 프레임 이동량을 다 쓸 때까지 점을 넘어간다. 점에서 끊으면 걸음이 계단처럼 보인다.
- `Editor/StageSelectControllerEditor` — 씬 뷰에서 노드 핸들을 끌면 경로선 즉시 갱신, 「노드 찍기」로 클릭한 자리에 노드 생성(**마지막 노드를 복제**하므로 라벨·배지가 따라온다), 스테이지 에셋은 순서대로 자동 배정.
- 테마의 `path` 는 지금 배경과 **같은 그림을 재활용**한다(사용자 결정). 길 전용 아트가 생기면 그 슬롯만 교체.

**테마별 길 배치 (사용자: "배경별로 노드를 다시 찍으려면 씬을 둘로 나눠야 하나?")** — 아니다, 씬 하나로.
- 길 모양이 배경마다 다르다 → **노드 위치를 `ThemeSO.nodeLayout`(List<Vector2>)에 데이터로** 둔다. 배경·보스처럼 테마가 위치도 들고 있는 것. **개수는 공유, 위치만 테마별**(사용자 결정 — 스테이지 수까지 다르게 하면 진행도·잠금·노드 생성까지 갈라져 너무 복잡).
- `StageSelectController.ApplyThemeLayout()` — Awake에서 현재 테마 레이아웃을 노드에 적용. **개수가 다르면 건드리지 않는다**(씬에서 노드 늘리고 저장 안 한 경우 옛 배치로 덮어쓰지 않게).
- `StageSelectControllerEditor` — 「편집 중인 테마」 드롭다운 + 「이 테마에 저장」/「불러오기」. 불러오면 그 테마의 배경 그림도 씬 뷰에 꽂아 준다(`ThemeAssetsMenu.PreviewThemeInSceneEditorOnly` — `ThemedSprite`가 에디트 모드에 안 돌므로). 편집은 씬 트랜스폼에서 하고 저장 버튼으로 테마에 굳힌다.
- **주의**: 노드 개수를 바꾸면 각 테마에서 다시 저장해야 한다.

**빌더 재실행 사고 (사용자: "UI 에셋 연결했던 게 사라짐")**
- 원인: `Build Main Scene UI` 는 각 화면의 **자식을 전부 지우고 재생성**한다. 손으로 꽂은 UI 스프라이트가 그 화면에 있으면 재실행 때 날아간다. 커밋된 적 없어 git 복구 불가였다(교훈: **UI 배선 후 즉시 커밋**).
- **대응 원칙**: 앞으로 UI를 얹을 땐 파괴적 빌더 대신 **비파괴 전용 메뉴**를 만든다(아래 일시정지처럼).

**클릭 상태 스프라이트 (Part A)** — Unity 내장 **Sprite Swap** 이라 코드 불필요. `EditorUiFactory.Button` 기본 Transition 을 `None → ColorTint` 로만 바꿔(스프라이트 없이도 누름 피드백) 슬롯이 열리게 했다. **SpriteSwap 을 기본으로 하면 안 된다** — 빈 Pressed 슬롯 탓에 누를 때 버튼이 사라진다. 아트 버튼은 인스펙터에서 개별로 SpriteSwap 전환.

**일시정지 / 설정 (전투 씬 우상단, Part B)**
- `Game/UI/PauseMenu` — `Time.timeScale = 0` 하나로 멈춘다(퍼즐 카운트다운·전투 채보 모두 `Time.deltaTime` 스케일 시간). 일시정지 버튼은 **토글**이고 아이콘이 pause↔play 로 바뀐다(두 스프라이트 다 씀). 설정은 기존 `OptionsPanel` 재사용(열면 멈추고 닫으면 재개 — OptionsPanel 을 안 고치려고 `Update`에서 activeSelf 를 감시). 지도로 나가기는 확인 패널 경유. **`OnDisable`에서 timeScale 원복**(멈춘 채 씬 넘어가면 다음 씬이 얼어붙는다).
- `Editor/PauseMenuBuilder` — `Tools ▸ ChainRiposte ▸ Add Pause Menu To Main`. **비파괴** — `PauseCanvas`(sortingOrder 18: 퍼즐 0·전투 10·준비 15 위, 결과 20 아래) 하나만 만들고 다른 화면은 안 건드린다. 아이콘 스프라이트는 비워 둔다(사용자가 꽂음).

**결과 화면 — 클리어/패배 분리 (사용자 요청)**
- **승리**(보스 인살): 다시 시작 버튼 없음. `victoryToMapDelay`(1.6초) 텀 뒤 **지도로 자동 복귀**. 나중에 인살 컷씬이 이 텀 자리를 채운다. 텀은 `WaitForSecondsRealtime`(연출이 timeScale 건드려도 안 늘어지게).
- **패배**: 다시 시작 / 지도 두 버튼(기존 동작). Restart/GoToMap 이 `Time.timeScale = 1` 원복(일시정지 중 사망 대비).

#### 세션 7 다음 후보

- 컨셉별 **보스 그림** (지금은 이름만 갈린다), 퍼즐·전투 배경(`puzzle`/`combat` 키가 비어 있음).
- 인트로/타이틀 공용 배경(사용자 작업분) 반영.
- 세션 6 후보 그대로: UI 에셋 배선, NPC 스프라이트 시트 → Animator, Boss_02 채보, 채보 미리듣기, 사운드.

---

### 세션 6 갱신: 2026-07-23

#### 세션 6에서 한 것

**UI 에셋 — 추천만 (배선은 사용자가 직접)**
- `Docs/UI_ASSETS.md` §2를 전면 개정. 요지: DEVNIK 팩은 밝은 카툰 톤이라 **회색 조각만 쓰고 색은 전부 우리 팔레트로 틴트**한다. 컬러 아이콘·초록 `btn_pill`은 곱하기 틴트가 안 먹으니 회색조 사본을 떠야 한다. 픽셀 뭉개짐(`Sliced` + `pixelsPerUnitMultiplier`), 9슬라이스 border에 그림자 포함, 전투 버튼은 **일부러 마지막에 어둡게**(시선이 패링 원에 있어야 함).

**캐릭터 선택 (신규)** — 타이틀 ▸ 새 게임에서 1회 선택
- `Game/Characters/PlayerCharacterSO` — 캐릭터 1명 = 에셋 1개. id(세이브 키, 비우면 에셋 이름 폴백) + 이름/설명 로컬 키 + 초상화/전투그림/**성녀그림** + 특화 가산치 4종.
- `Game/Characters/CharacterService` — 목록은 `Resources/Characters` 스캔, 선택은 PlayerPrefs(`ChainRiposte.Character.v1`). **캐릭터를 늘리려면 에셋만 추가하면 된다** — 코드도 씬 빌더도 안 건드림.
- `Game/UI/CharacterSelectPanel` — 카드 개수가 데이터로 정해지므로 씬의 템플릿 카드를 복제(옵션의 언어 버튼과 같은 규칙). 카드 안에서 `Portrait`/`Name` 을 **이름으로** 찾으므로 씬에서 카드 모양을 바꿔도 계속 붙는다.
- `TitleController` — 새 게임 확인 → 캐릭터 선택 → 그때서야 `ProgressService.ResetAll()`. **캐릭터가 1명 이하면 선택 화면을 건너뛴다.**
- `Editor/CharacterAssetsMenu` — `Tools ▸ ChainRiposte ▸ Create Default Characters`. 기사(`player_knight`+`darksouls_saint`) / 낭인(`player_sekiro`+`sekiro_shaman`) 2종 생성. **재실행해도 비어 있는 슬롯만 채운다**(손으로 고친 값 보존).
- `CombatScreen` — 플레이어 그림은 고른 캐릭터 우선, 없으면 인스펙터 값(Main 단독 실행용).

**패링 띠 재정의 (사용자 지적 → 버그 수정)**
- 증상: "회색 원과 흰 원이 겹치는 순간이 패링 타이밍이 아니다". 원인은 회색이 **얇은 원 하나**였고 그 반지름이 `1 + 윈도우×속도`, 즉 **판정이 열리는 가장 이른 순간**이었던 것. 겹침 = 판정 시작이지 타격 시점이 아니었다.
- 이제 회색은 **두께 있는 띠**이고 그 범위가 곧 판정 범위다: 바깥 `1 + 윈도우×속도`(가장 이른 성공), 안쪽 `1 - 유예×속도`(가장 늦은 성공). **흰 원이 띠에 겹쳐 있는 동안 누르면 반드시 된다.**
- `PlaceholderSprite.Annulus(innerRatio)` 신규(비율별 캐시, 기존 `Ring`은 innerRatio 0.88로 모양 동일) + `CombatSystem.ParryLateGraceSeconds` 노출.
- 두께를 판정에서 계산하므로 **보이는 것과 판정이 어긋날 수 없다** — PARRY를 올리면 실제로 굵어진다. 알파 0.22 → 0.15. 실제 아트로 갈아 끼울 땐 `CombatScreen ▸ generateBandSprite` 를 끌 것.

**패링 즉시 결판 (사용자 지적 → 모델 변경)**
- 증상: "판정 안에서 눌러도 원이 최소로 줄어드는 것까지 본 뒤에야 판정이 난다". 이 장르는 누른 즉시 반응이 나오고 그 공격이 사라져야 한다.
- **`PlayerActionState.Parrying` 제거.** 예전 모델은 "누르면 판정치(초) 동안 자세를 유지하고, 타격이 그 안에 들어오면 성공"이었다(`ResolveStrike`가 Parrying을 보고 판정). 지금은 `PressParry()`가 **그 자리에서** `[타격−윈도우, 타격+유예]` 안의 가장 임박한 노트를 찾아 즉시 `ParryNote`, 없으면 즉시 헛침 잠금.
- `ParryNote`가 `RebuildActiveNotes()`를 호출해 **막은 노트가 그 프레임에 목록에서 빠진다** — 흰 원이 다음 Tick을 기다리지 않고 사라진다.
- 부작용: 헛침 총 잠금이 0.45초(윈도우 0.2 + 잠금 0.25) → **0.25초**로 짧아졌다. 연타 방지는 그대로 동작(`parryWhiffLockSeconds`).
- **판정량 하향 ①** — `parryWindowPerLevelSeconds` 0.03 → **0.015**. 캡(5레벨) 기준 0.40 → 0.325초.
- **헛침 잠금** 0.25 → **0.35초**. 즉시 결판 모델에서는 이 값이 헛침 벌의 전부다(예전엔 윈도우를 다 기다린 뒤 잠금이 시작돼 실질 0.45초였다).

**판정량 하향 ② — 스탯별 분배 비용** (사용자: "여전히 판정이 넓다")
- **+PARRY 1레벨 = 2포인트**, +ATK/+DEF는 1포인트. `PlayerStatsConfig`에 `AttackPointCost/DefensePointCost/ParryPointCost`.
- 폭 증가량을 더 깎지 않고 **비용**을 택한 이유: 폭을 깎으면 "PARRY를 찍었더니 띠가 굵어졌다"는 피드백이 사라진다. 판정 폭은 실수 자체를 없애 주므로 값이 같으면 언제나 최선의 선택이 되는 스탯이라, 값을 비싸게 매기는 쪽이 맞다.
- `PlayerStats.IsAtCap(stat)` 추가 — **상한**과 **포인트 부족**을 구분해야 HUD가 MAX를 잘못 띄우지 않는다. 버튼에 `2P` 표시(`puzzle.alloc.cost`).
- 더 조일 순서: `parryPointCost` 3 → 그래도 넓으면 `baseParryWindowSeconds`(0.25).

**스탯 분배를 퍼즐 → 준비 화면으로 이동**
- `Game/UI/StatAllocationPanel` 신규 — +ATK/+DEF/+PARRY 버튼 묶음 + 분배 로직. `IntermissionScreen`의 자식(FIGHT 아래)으로 살고 그 페이즈에만 켜진다. `PuzzleHud`에서 버튼 필드·분배 로직 전부 제거(HP·영혼석·턴·현재 수치 표시만 남음).
- 준비 화면 구조 변경: `Root`(화면 전체 딤, raycast로 퍼즐 입력 차단) → `Band`(아래쪽 띠, 높이 420→700). `IntermissionScreen ▸ dimColor` 기본 검정 알파 0.45 — **판이 비쳐 보일 정도로만** (다음 판을 눈으로 재야 한다).
- ⚠ **`Build Main Scene UI` 재실행 필수.** 안 하면 퍼즐 화면에 옛 버튼이 배선 끊긴 채 남는다.
- **준비 화면 = 상점 느낌** (사용자 요청). 딤에 가려 HUD가 안 읽히므로 띠 안에 **체력 / Lv·영혼석·포인트 / 현재 공격·방어·패링**을 다시 적는다(기존 `puzzle.hp`·`puzzle.souls`·`puzzle.stats` 키 재사용). 남은 포인트는 금색 볼드. 띠 알파 0.88로 뒤 판이 어렴풋이 비친다. 배치: 제목 → 경고 → 현황 3줄 → 포인트 → NPC 2명 → FIGHT → **분배 버튼 3개(FIGHT 아래)**.
- 사용자 메모: "나중에 UI 확장할 것" — 지금은 필요한 숫자만 올려 둔 상태.

**월드맵 버그 — 잠긴 노드를 누른 뒤 원래 스테이지로 못 돌아감 (사용자 보고)**
- `Update`가 `nearest == _currentIndex`면 그냥 return 했다. 그래서 1-1에서 1-2(잠김)를 눌러 `ShowLocked`(START 비활성)가 뜬 뒤에는 **1-1을 다시 눌러도 아무 일이 없어 START를 되살릴 방법이 없었다.**
- 서 있는 노드를 다시 누르면 `ShowInfo(_currentIndex)`로 정보 패널을 되살리도록 수정.

**타일 배경판 (배선만, 아트는 사용자가 나중에)**
- 아이콘만 있어 타일 경계가 안 읽히는 문제. `TileDefinitionSO`에 `backgroundSprite` + `backgroundColor`, `BoardView`에 공용 `tileBackgroundSprite` / `tileBackgroundScale`(1.05) / `backgroundOnWalls`.
- 우선순위: 타일 전용 그림 → 공용 그림 → **그림이 없고 색만 있으면 사각형**. 알파 0이면 안 그리므로 **아무것도 안 하면 지금과 동일**하게 보인다(색만 넣어도 즉시 받침이 생긴다).
- 받침은 `TileView`의 자식(sortingOrder −1)이라 **낙하·스왑을 따라 움직인다** — 고정된 배경 셀(−10)과 다르다.
- `TileView.Create` 인자가 늘어 `TileView.Visual` 구조체로 묶었다.

- 테스트 갱신·추가 → EditMode **94/94**.

**캐릭터 특화** — `statsOverride`(설정 통째로 교체) 폐기, **가산 방식**으로 변경. 밸런스 원천을 `PlayerStatsConfigSO` 하나로 유지해야 기본값을 고칠 때 캐릭터 수만큼 따라 고치지 않는다.
- `PlayerCharacterSO.ApplyBonuses(config)` — `bonusMaxHp` / `bonusDamageReduction` / `bonusAttackDamage` / `bonusParryWindowSeconds`. `GameManager.BuildStatsConfig()`가 공용 config를 만든 뒤 얹는다.
- 기사 = HP+12, DEF+1.0 / 낭인 = ATK+1.5, PARRY+0.015초. **대략 반 레벨씩** — 고르는 재미는 주되 정답이 생기면 안 된다는 기준. 수치는 `Character_*.asset` 인스펙터에서 바로 조절.

**NPC 반응**
- **담당을 뒤집었다** — 성녀 = **공격·방어**(축복), 대장장이 = **판정/PARRY**(무기를 벼려 패링 창을 넓힌다). 이전 코드 주석과 반대이므로 주의.
- `Game/UI/NpcReaction` — 정지 그림뿐이라 **코드로** 튀어오르고 번쩍인다(스케일 펀치 + 홉 + 틴트). `animator` 슬롯을 채우면 그쪽이 우선하므로 **스프라이트 시트가 생겨도 이 스크립트를 지울 필요 없다.**
- `IntermissionScreen` — `StatAllocated`에서 Parry면 대장장이, 아니면 성녀. 성녀 그림은 `CharacterService.Current.SaintSprite` → 없으면 `fallbackSaintSprite`. **대장장이 그림은 코드가 안 채운다**(캐릭터 무관, 인스펙터에서 직접).
- NPC 자리 구조가 바뀜: `SaintNpc/Body`(Image + NpcReaction) + `SaintNpc/Label`. 이름표가 그림과 같이 튀지 않도록 형제로 분리.

**현지화** — `character.select.title/confirm`, `character.knight(.desc)`, `character.sekiro(.desc)` 를 CSV와 `LocalizationMenu.StarterCsv()` 양쪽에 추가.

#### 세션 6 다음 후보

- **플레이어 선택을 더 키우기** (사용자가 "나중에 더 ㄱㄱ") — 캐릭터 3번째 추가, 전용 시작 스킬/패시브, 캐릭터별 전용 성녀 대사 등. 지금 구조에서는 에셋 추가 + 가산치 조절만으로 붙는다.
- UI 에셋 배선(사용자가 직접), NPC 스프라이트 시트 → Animator 전환, Boss_02 채보, 채보 미리듣기, 사운드 클립.

---

### 세션 5 갱신: 2026-07-22

- **커밋 규칙**: 메시지에 `Co-Authored-By: Claude` 트레일러를 넣지 않는다.
- **git**: 세션 5 작업 전부 커밋 완료. 원격 `origin`(GitHub) 있으나 **push는 계속 안 하고 있음** — 필요하면 물어볼 것.
- **테스트**: EditMode **92/92**. 수정 시 초록 유지.

#### 다음 세션에서 가장 먼저 할 것

1. **빌더 3종 재실행이 됐는지 확인.** 세션 5에서 씬 구조가 크게 바뀌어서, 안 돌리면 새 UI가 안 보인다.
   `Build Main Scene UI` / `Build StageSelect Layout` / `Build App Scenes`
2. **미검증분 물어보기** — `Docs/VERIFICATION.md` §7이 세션 5 몫이다. (세션 5 검증은 2026-07-23에 완료됨)
3. 그 다음 방향은 아래 「남은 일」에서 사용자와 고를 것.

#### 세션 5에서 한 것 (커밋 순)

**퍼즐 안정화**
- 낙하 뷰 어긋남 수정 — 한 웨이브에서 타일이 두 번(낙하→슬라이드) 움직이면 뷰가 중간에 멈춰 "벽 밑 빈 칸 + 겹친 타일"이 됐다. `GravityResolver`가 이동 기록을 타일당 하나로 합친다.
- **데드락 검출 + 보드 리롤** (`Core/Match/MoveFinder`). 매치 없는 스왑은 턴을 안 먹으므로 수가 없으면 게임이 멈춘다. 검출 즉시 섞되 **턴 미소모**, 벽·보스·부패·사슬은 제자리.
- 비정형 보드(해골/하트) 대응 — 구멍이 대각선 슬라이드를 끊지 않게. 퍼즈 테스트 300턴.
- 벽 손상 단계 스프라이트(`BreakableFull` 4프레임) 배선.

**앱 흐름 (C2)** — 인트로 → 타이틀 → 스테이지선택 → 게임
- `SceneRouter`(씬 이름 창구) + `ScreenFader`(자동 생성 페이드).
- `Intro.unity` / `Title.unity` 신규 + 빌드 설정 순서 재구성. 타이틀 = 이어하기/새게임/옵션/나가기.
- **옵션 화면** — BGM·SFX 볼륨(`Game/Audio/AudioService` 2버스), 화면 방향 고정, 언어 전환, 진행도 초기화. 전부 실제 동작.

**현지화 (전면 교체)** — `.claude/skills/unity-localization/` 자료를 적용
- CSV 기반(`Assets/_Project/Data/Resources/Localization.csv`)으로 갈아엎음. 이전에 만든 SO 테이블은 폐기.
- **게임의 모든 문구가 이미 현지화를 거친다.** 새 문구는 반드시 CSV에. 규칙은 「구조 원칙」 참조.
- 한글 폰트 `neodgm SDF` 생성 + TMP 기본/폴백 등록 + 전 씬 통일.

**월드맵 (C1/C3)**
- 미해금 스테이지 정보 비공개 — **진입 기록이 있어야** 보스·기믹 공개(세이브 v2, v1 자동 마이그레이션).
- 잠긴 노드로 걸어가다 **막혀서 튕기는 연출**.

**보스 전투 재작성 (C3)** — 여기가 세션 5의 핵심
- **채보 기반으로 전면 재작성.** `BossNoteConfig`(박 단위 타격 시점 + 예비동작 + 노트별 배율) / `BossPatternConfig`(BPM·길이·배율) / `BossPhaseConfig`(HP 구간별 가중 무작위 풀).
- **연속기는 별도 타입이 아니다** — 노트를 촘촘히 찍으면 그게 연속기다. `parryable`(패링 불가) 개념은 삭제.
- **패링 표시 = 원.** 흰 원 여러 개가 **같은 속도로** 다가와 플레이어 몸에서 닫힌다. 회색 띠 두께 = `ParryWindowSeconds`(PARRY 스탯을 올리면 눈에 보이게 굵어짐).
  - 크기를 진행률이 아니라 **남은 시간 × 속도**로 정하는 게 핵심. 그래야 "거리 = 시간"이 성립해 띠 하나로 표현된다.
- **채보 에디터**(`BossDataSOEditor`) — 타임라인 클릭/드래그/우클릭, 스냅 1·1/2·1/4박, 예비동작을 막대로 시각화, 페이즈 편집.
- **패링 완화** — 윈도우 0.15→0.25초 + **타격 후 유예 0.12초**(늦게 눌러도 인정). 허용 폭 0.15→0.37초.
- **`GamePhase.Intermission`** — 보스 돌입 전 **시간 제한 없는** 레벨업 구간. 스탯 분배는 기존 HUD 버튼을 그대로 쓰고, 준비 패널은 아래쪽 띠로만 만들어 HUD를 가리지 않는다. 성녀·대장장이 NPC 자리(스프라이트 비어 있음).
- **포켓몬식 대치** — 플레이어 왼쪽 아래 / 보스 오른쪽 위, FIGHT 시 양쪽에서 미끄러져 등장. 플레이어 피격·패링·공격(찌르기) 반응.

**에셋**
- 도트 17종 + 보스 타일 + 플레이어 2종 + NPC 3종 배선/추가. UI 픽셀 시트 29조각 슬라이스(`Docs/UI_ASSETS.md`에 배치 계획, **아직 미배선**).

#### 남은 일 (사용자와 고를 것)

- **UI 에셋 배선** — `Docs/UI_ASSETS.md`의 표대로. 지금 UI가 전부 단색 사각형이다.
- **NPC 스프라이트 배선** — `darksouls_saint` / `blacksmith` / `sekiro_shaman`이 들어와 있으나 `IntermissionScreen`의 슬롯은 비어 있다. NPC를 실제 기능(성녀=방어·판정 / 대장장이=공격)으로 만들지도 미정.
- **Boss_02 채보** — 비어 있어 임시 패턴(정박 4타)으로 돈다. `Boss_01`에는 예시 3개(Steady / Double Sweep / Flurry)가 있다.
- **채보 미리듣기** — 에디터에서 플레이 없이 들어보는 기능. 튜닝 속도가 크게 오른다.
- 사운드 클립(JuiceDirector·AudioService 슬롯 전부 비어 있음), 잡기·회피(회피 키가 없어 보류), 안드로이드 빌드.

### 세션 4-2: B3 스테이지 기믹 3종 (완료 — 테스트 66/66, 플레이 검증 대기)
GDD §3.6. **규칙은 Core의 조합형 모듈, 엔진은 훅만 제공**.
- `Core/Stage/Gimmicks/` 신규 — `IStageGimmick`(+ no-op 기본 클래스 `StageGimmick`), `GimmickContext`(보드/RNG/설정 + 이벤트·피해 기록), `GimmickEvent`, `GimmickFactory`.
  - 훅 4개: `OnBoardInitialized` / `OnTilesSpawned` / `OnMatchesResolving`(파괴 목록을 고칠 수 있음) / `OnTurnEnded`.
- **전염** `SpreadingCorruptionGimmick`: 시작 시 부패 씨앗 N개 → 매 턴(주기 설정) 인접 몬스터 1개 감염. 부패는 **새 카테고리 `TileCategory.Corruption`**(매치·스왑 불가, 낙하는 함). 인접 매치가 나면 함께 파괴. `MaxCorruptionRatio`(기본 0.35)로 완전 데드락 방지.
- **시한폭탄** `TickingDeathGimmick`: 스폰 시 확률로 장전(그 턴은 유예), 턴마다 감소, 0이면 타일 소멸 + **플레이어 HP 직접 피해**(`SwapResult.Gimmicks.PlayerDamage` → PuzzleController가 적용, 사망 시 패배).
- **사슬 결박** `ChainedTilesGimmick`: `Tile.Status.Chained` → **스왑 불가 + 낙하 불가**(`Tile.IsFixed`로 GravityResolver가 벽과 동일 취급). 매치에 걸리면 **파괴 대신 사슬만 풀림**(영혼석 없음), 인접 매치로도 해제.
- **엔진 변경**: `PuzzleEngine(config, spawner, rng=null)`. `CascadeStep`에 **`ClearedPositions`**(실제 사라진 칸 — 뷰는 이제 이걸로 파괴 연출)와 `GimmickEvents` 추가. `SwapResult.Gimmicks`(GimmickPhase: 이벤트/피해/낙하/그 여파 연쇄) 추가. 매치가 통째로 사슬에 막히면 사슬만 풀고 그 스왑을 마감(무한루프 방지).
- **뷰**: `TileView.SetChained/SetBombTurns`(사슬 띠 + 폭탄 카운트), `BoardView`에 corruptionColor/corruptionSprite/chainSprite 슬롯 + `PlayGimmickPhase` 재생.
- **데이터**: `StageDataSO`에 `gimmickTuning`(전염/폭탄/사슬 수치) 인스펙터 노출. **월드2 배선 완료** — 2-1=사슬, 2-2=사슬+폭탄, 2-3=사슬+폭탄+전염. 월드1은 기믹 없음(확인함).
- 테스트 `Tests/GimmickTests.cs` 13개 추가 → EditMode **66/66 통과**.
- **사용자가 할 것(검증)**: ①StageSelect에서 2-1 진입(잠겨 있으면 `Tools ▸ ChainRiposte ▸ Progress ▸ Unlock All Stages`) — 사슬 감긴 타일이 안 움직이고 매치하면 사슬만 풀리는지 ②2-2에서 폭탄 숫자가 줄고 0에서 터지며 HP가 깎이는지 ③2-3에서 보라색 부패가 번지고 인접 매치로 지워지는지.

### 세션 4: B2 진행도 잠금 + 세이브 (완료 — 테스트 53/53, 플레이 검증 대기)
GDD §9.2. **규칙은 Core / 저장은 Game** 분리.
- `Core/Progress/StageProgress.cs` 신규 — 클리어한 **stageId 집합**만 보관. 잠금은 맵 노드 순서(`orderedStageIds`)와 조합해 계산 → 씬에서 노드를 옮기거나 추가해도 세이브 안 깨짐. 규칙: **index 0은 항상 열림, 나머지는 직전 스테이지 클리어 시 해금**. `Serialize/Deserialize`(';' 구분 문자열)까지 순수 C#.
- `Game/Progress/ProgressService.cs` 신규 — PlayerPrefs 어댑터(키 `ChainRiposte.Progress.v1`). `Current`/`MarkCleared`(새로 깬 경우만 저장)/`ResetAll`/`UnlockAll`.
- `StageDataSO.StageId` 추가 — 세이브 키. **비우면 에셋 이름 폴백**(기존 Stage_1_1~2_3 그대로 동작, 마이그레이션 불필요). 한 번 정하면 바꾸지 말 것.
- `GameManager` — `GamePhase.Victory` 시 `ProgressService.MarkCleared(stageData.StageId)`.
- `MapNode` — `ApplyState(unlocked, cleared)` 추가. 잠금 시 스프라이트 틴트 + **lockedBadge/clearedBadge 씬 오브젝트 on/off**(자물쇠·깃발 아트로 교체 가능, 비워도 동작).
- `StageSelectController` — Awake에서 진행도 로드 → 노드 상태 적용 → **가장 앞선 열린 노드에서 시작**. 잠긴 노드 클릭 시 이동 안 하고 패널에 `LOCKED / CLEAR THE PREVIOUS STAGE FIRST` + START 비활성. 클리어한 노드는 타이틀에 `- CLEAR`. (기본 TMP 폰트에 한글 글리프 없어 패널 문구는 영문 유지)
- `Editor/ProgressMenu.cs` 신규 — `Tools ▸ ChainRiposte ▸ Progress ▸` Reset / Unlock All / Log.
- `Editor/StageSelectSceneBuilder` — 노드마다 LOCK/CLEAR 배지 생성 + 자동 배선. **기존 씬에 배지를 넣으려면 빌더 재실행 필요**(안 해도 틴트로는 동작).
- 테스트 `Tests/StageProgressTests.cs` 8개 추가 → EditMode **53/53 통과**.
- **사용자가 할 것(검증)**: ①StageSelect 플레이 — 1-1만 열리고 1-2 이후는 어둡게+클릭 시 LOCKED ②Main에서 보스 잡고(Victory) MAP 복귀 → 1-2 해금 & 캐릭터가 1-2에 서 있는지 ③`Tools ▸ ChainRiposte ▸ Progress ▸ Reset Progress` 후 다시 잠기는지.

### 세션 3-2: Main 씬 UI 씬 편집형 전환 (진행 중)
- **한 것**:
  - `PuzzleHud`·`CombatScreen`·`ResultScreen` 3종을 재작성 — 런타임 `BuildUi()` 제거, **씬 참조(TMP 텍스트/Image/Button 등) + 행동만**. legacy Text → **TextMeshPro**. 참조 null이면 안내 로그 후 비활성화하는 가드 추가.
  - `Editor/EditorUiFactory.cs` 신규 — 빌더 공용 uGUI(TMP) 생성 헬퍼.
  - `Editor/MainSceneBuilder.cs` 신규 — 메뉴 `Tools ▸ ChainRiposte ▸ Build Main Scene UI`. 세 화면을 씬에 실물 생성 + 컨트롤러 참조 자동 배선. 재실행 시 각 화면 자식 지우고 재생성.
  - 보드 타일/셀(`BoardView`)은 데이터로 개수가 정해지므로 **런타임 생성 유지**(의도된 것).
- **검증 완료**: 컴파일 클린 + 빌더 정상 동작 확인(사용자 보고, 2026-07-16). 플레이 세부 검증은 진행하며 확인.
- 트러블슈팅 기록: UnityEngine.Object에 `??` 금지(가짜 null) — 메모리 `unity-null-operators-gotcha` 참조.

### 세션 3-3: B1 StageDataSO 보드 그리드 에디터 (완료 — 사용자 확인됨)
- `Editor/StageDataSOEditor.cs` — StageDataSO 커스텀 인스펙터. boardRows 문자열 대신 **클릭/드래그로 칠하는 그리드**(브러시 O=활성/X=구멍/W=벽, 크기 조절 1~20, 전체 채움, 활성/벽 개수 요약). X 브러시로 하트 등 임의 모양 가능.

### 세션 3-4: 낙하 버그 수정 + A2 에셋 스왑 (완료 — 테스트 45/45, 플레이 검증 대기)
- **버그(사용자 보고)**: 보스 타일 등이 잘 안 내려옴. 원인 2가지:
  1. `BoardRefiller`가 기둥 **중간 빈칸에도 새 타일을 스폰** → 위 타일(보스 포함)이 낙하로 내려올 자리를 가로챔. → **첫 타일에서 break**(기둥 최상단 연속 빈칸만 리필)로 수정.
  2. **벽에 얹힌 타일이 미끄러질 규칙이 없음** → `GravityResolver.TrySlideInto`에 추가: 그늘 칸이 아니어도 **벽에 얹힌(IsRestingOnWall) 타일은 대각선 아래 빈칸으로 슬라이드**. 결과: 벽(W)/구멍(X) 제외 전 칸이 항상 채워지고 보스가 벽을 우회해 하강.
  - 회귀 테스트 3개 추가(`GravityAndRefillTests`): 중간빈칸 스폰 금지 / 벽 위 타일 슬라이드 / 슬라이드 구멍은 위 타일 낙하로 채움. **EditMode 45/45 통과.**
  - 참고: 보스가 벽 위에 얹힌 동안은 대각선이 열릴 때까지 대기(그동안 카운트다운 압박 = 기획 의도로 둠). 정상 돌입 판정은 기존대로 "열 최하단 활성 셀 도달".
- **A2 에셋 스왑 파이프라인**: `TileDefinitionSO.sprite`(기존 필드)를 실제로 사용하도록 연결 — `TileView.Create`에 sprite 파라미터, `BoardView`에 스프라이트 테이블 + **wallSprite/bossSprite/cellSprite 인스펙터 슬롯**(비우면 기존 색 사각형). 스프라이트 지정 시 흰 틴트(원색). 전투 보스/배경/맵 비주얼은 씬 오서링이라 씬에서 직접 드래그 교체. JuiceDirector 클립 슬롯은 인스펙터에서 채우면 됨.
- **사용자가 할 것(검증)**: ①Main 플레이 — 벽 있는 스테이지(1-1)에서 보스 타일이 벽에 막혀도 옆으로 미끄러져 내려오는지, 빈칸이 안 생기는지 ②타일 SO에 스프라이트 아무거나 꽂아보고 보드에 표시되는지.

### 세션 3: StageSelect 씬 편집형 전환 (진행 중)
문제: StageSelect/Main이 런타임 `new GameObject()`로 전부 생성돼 씬에서 편집 불가 → 에셋/디자인 교체가 어려움. 결정: **StageSelect 먼저 / TMP 전환 / 에디터 빌더로 초기 생성**.
- **한 것**:
  - `Game/Map/MapNode.cs` 신규 — 노드에 붙는 컴포넌트(StageDataSO 참조 1개).
  - `StageSelectController.cs` 재작성 — 하드코딩 `NodePositions`/`BuildMap`/`BuildUi` 제거. 씬 참조(`MapNode[] nodes`, `character`, `pathLine`, `infoPanel`, TMP 텍스트, `startButton`)만 받아 **행동만** 담당. 카메라는 노드 bounds로 자동 fit. UI는 `TMP_Text`.
  - `Editor/ChainRiposte.Editor.asmdef` + `Editor/StageSelectSceneBuilder.cs` 신규 — 메뉴 `Tools ▸ ChainRiposte ▸ Build StageSelect Layout`. 배경/노드+라벨/경로 LineRenderer/캐릭터/Canvas(TMP 패널·버튼)를 실물 생성하고 컨트롤러 참조 자동 배선. Data의 Stage_1_1~2_3 자동 할당. 재실행 시 기존 자식 지우고 재생성(idempotent).
  - `ChainRiposte.Game.asmdef`에 `Unity.TextMeshPro` 참조 추가.
- **사용자가 할 것(검증)**: ①Unity에서 컴파일 클린 확인 ②`Tools ▸ ChainRiposte ▸ Build StageSelect Layout` 실행(TMP 처음이면 Window ▸ TextMeshPro ▸ Import TMP Essential Resources) ③StageSelect 플레이 — 노드 클릭→캐릭터 이동→패널/START 정상 & 씬에서 노드 드래그 시 경로 반영 확인.
- **다음**: 검증 OK면 Main 씬도 같은 방식(배경·HUD·전투 UI·결과 화면)으로 씬 편집형 전환.

### 세션 2에서 끝낸 것 (7·8·9·10단계)
- **7단계 패링 전투 코어**: `Core/Combat/` (CombatSystem — 2버튼, 경계분할 결정적 Tick, 패링윈도우/커밋/체간/인살, TimeEpsilon 1e-4), BossDataSO, Boss_01 "The Warden"(5수 패턴), PlayerStatsConfig에 전투템포(커밋0.4/헛침잠금0.25) 추가. 테스트 42/42 초록.
- **8단계 전투 프레젠테이션**: `Game/Combat/` (CombatInput ←→/마우스, CombatController, CombatScreen — 텔레그래프 링 수축=타격시점, 금색=패링가능/보라=불가), UI/ResultScreen(RESTART+MAP). GameManager가 StageDataSO 소유(StageConfig 공유), Main.unity 배선 완료.
- **9단계 Game Juice**: `Game/Juice/` (JuiceDirector — 클립 슬롯 널세이프 허브, 셰이크/히트스톱(패링0.06/인살0.35)/콤보피치/크레센도(CurrentChance), CameraShaker, PostFxBootstrap — 그레인+비네팅 런타임 Volume). BoardView.StepCleared 이벤트. asmdef에 URP 참조 추가. Main.unity에 Juice GO 배선.
- **10단계 월드맵**: GDD §9 기획(NSMB식, 1-1~2-3, 월드2=배경/보스 차별+추후 기믹, §9.3 모바일 세로뷰=출시 요구사항). Game/Map/StageSelectController(코드 조립: S자 6노드, 클릭→경로 따라 자동이동→하단 정보패널+START), StageSelection 정적 홀더(GameManager 우선 사용), Stage_1_1~2_3 + Boss_02 "The Butcher"(HP160/체간120, 월드2 연결), StageSelect.unity, 빌드설정 [0]=StageSelect [1]=Main.

### 세션 4-3: B4 모바일 세로/가로 뷰 대응 (완료 — 테스트 66/66 유지, 플레이 검증 대기)
GDD §9.3(출시 요구사항). **배치 값은 코드가 아니라 씬에서 잡는다.**
- `Game/UI/OrientationService.cs` 신규 — 해상도로 방향 판정(`Evaluate`) + `Changed` 이벤트. `[RuntimeInitializeOnLoadMethod]`로 감시자를 자동 생성하므로 **씬에 아무것도 놓을 필요 없음**. 도메인 리로드 끈 환경 대비해 부팅 시 `Changed = null`.
- `Game/UI/OrientationLayout.cs` 신규 — UI 요소마다 **세로/가로 프리셋 2벌**(앵커·피벗·위치·크기·스케일). `configured` 플래그가 false면 아무것도 적용하지 않음(빈 프리셋으로 배치 날아가는 사고 방지).
- `Editor/OrientationLayoutEditor.cs` 신규 — **씬에서 드래그 → "가로(Landscape)로 저장" 버튼** 워크플로 + 미리보기 버튼. 이게 이번 작업의 핵심 UX.
- `Game/UI/OrientationCanvas.cs` 신규 — 기준 해상도 1080×1920 ↔ 1920×1080 전환. `EditorUiFactory.SetupCanvas`가 자동으로 붙인다.
- `CameraFit2D` — 마지막 bounds를 기억했다가 **화면 크기 변경 시 자동 재조정**(보드/월드맵).
- `CombatScreen` — 팝업 시작 위치 하드코딩(`560`) 제거. 방향별 배치를 기준으로 삼고 연출 후 복원(중첩 호출 시 기준점이 위로 밀리던 것도 같이 해결).
- **빌더가 기본 가로 배치를 깔아준다**: 전투 2버튼=양쪽 사이드(GDD 예시 그대로), 스탯 분배 3버튼=우측 세로 스택, 월드맵 정보 패널=우측 컬럼+START 하단. → **양 씬 빌더 재실행 필요**.
- 주의: `EditorUiFactory.Bar()`는 '채움' 이미지를 돌려주므로 게이지는 **부모**를 Orient해야 한다.
- **사용자가 할 것(검증)**: `Docs/VERIFICATION.md` §4.

### 다음 로드맵 (우선순위는 사용자에게 확인)
- **다음 세션 시작 시**: 미검증분 결과부터 물어볼 것 (`Docs/VERIFICATION.md` 기준). 그리고 push 여부 확인.
- 백로그 A·B가 끝났으므로 **다음 방향은 사용자에게 물어볼 것**. 후보:
  - 실제 아트 에셋 제작/적용 (A2 파이프라인 + 부패/사슬/맵 배지 스프라이트 슬롯 전부 열려 있음)
  - UI 프리팹 추출 / 사운드(JuiceDirector 클립 슬롯 비어 있음)
  - 밸런스 패스(기믹 수치·보스 패턴·성장 곡선), 스테이지 추가(월드3)
  - 빌드 파이프라인(안드로이드 실기 테스트)

완료됨: A1(씬 편집형 전환: StageSelect+Main), A2(에셋 스왑 파이프라인), B1(보드 그리드 에디터), 낙하 버그 수정, B2(진행도 잠금+세이브), B3(스테이지 기믹 3종), B4(세로/가로 뷰 구조).
