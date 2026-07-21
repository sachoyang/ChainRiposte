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

### 마지막 갱신: 2026-07-21 (세션 4)
- **진행 상황**: 로드맵 1~10단계 + A1/A2/B1/낙하버그 + **B2 진행도 잠금·세이브** + **B3 스테이지 기믹 3종** 완료.
- **사용자 확인 대기**: 세션 3-4(보스 하강/스프라이트) + B2(잠금) + B3(기믹) — 사용자가 "나중에 한 번에 검증" 하기로 함.
  → **체크리스트는 `Docs/VERIFICATION.md`에 정리됨. 검증 얘기가 나오면 이 파일부터 볼 것.**
- **git**: B2까지 `c07a251`로 커밋됨.

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

### 다음 로드맵 (우선순위는 사용자에게 확인)
- **다음 세션 시작 시**: 미검증분(세션 3-4 보스 하강/스프라이트 + B2 잠금 + B3 기믹) 결과부터 물어볼 것.
- B4. 모바일 세로 뷰 대응 — 방향별 UI 재배치 구조 (GDD §9.3, 출시 요구사항). ← 유력한 다음 작업
- (선택) UI 프리팹 추출, 실제 아트 에셋 제작/적용 — A2 파이프라인은 준비됨(부패/사슬 스프라이트 슬롯도 열려 있음).

완료됨: A1(씬 편집형 전환: StageSelect+Main), A2(에셋 스왑 파이프라인), B1(보드 그리드 에디터), 낙하 버그 수정, B2(진행도 잠금+세이브), B3(스테이지 기믹 3종).
