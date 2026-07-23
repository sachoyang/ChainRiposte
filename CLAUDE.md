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

### 마지막 갱신: 2026-07-23 (세션 6)

- **커밋 규칙**: 메시지에 `Co-Authored-By: Claude` 트레일러를 넣지 않는다.
- **git**: 세션 6 작업 커밋 완료(주제별 5개). push는 계속 안 하고 있음 — 필요하면 물어볼 것.
- **테스트**: EditMode **94/94**.

#### 다음 세션에서 가장 먼저 할 것

1. **빌더 2종 재실행 확인** — `Build App Scenes` / `Build Main Scene UI`. 안 돌리면 캐릭터 선택 화면과 NPC 반응이 안 보인다.
2. **대장장이 그림 배선 확인** — `IntermissionScreen` 인스펙터의 `blacksmithImage`. 코드가 안 채우는 유일한 NPC 그림이다.
3. `Docs/VERIFICATION.md` **§8** 결과 물어보기.

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
