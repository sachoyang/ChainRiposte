# ChainRiposte — 기술 설계 (Architecture)

## 1. 설계 원칙

1. **로직/프레젠테이션 분리**: 게임 규칙(매칭, 스탯, 전투 판정)은 UnityEngine에 의존하지 않는 순수 C# (`ChainRiposte.Core`). Unity 레이어(`ChainRiposte.Game`)는 입력을 Core에 전달하고 Core의 이벤트를 연출로 변환하는 어댑터.
   - 효과: 유닛 테스트 가능, 에셋/연출 교체가 로직에 영향 없음.
2. **데이터 주도**: 밸런스 수치는 전부 ScriptableObject. Core는 SO를 모르며, SO가 순수 C# config 객체로 변환되어 주입된다 (`~SO.ToConfig()` 패턴).
   - `AnimationCurve` 같은 UnityEngine 타입도 Core에 직접 넘기지 않는다. SO가 `Func<float, float>` (예: `curve.Evaluate`)로 감싸 주입 → Core는 테스트에서 임의 함수로 대체 가능.
3. **이벤트 기반 연출**: Core가 C# event를 발행 → Game 레이어가 구독해 VFX/SFX/UI 갱신. 연출 훅은 전부 이 경계에 위치.
4. **기믹 = 조합형 모듈**: 스테이지 기믹(전염/시한폭탄/결박)은 FSM이 아니라 공통 인터페이스(가칭 `IStageGimmick` — 턴 소모/매치 해결/타일 스폰 이벤트에 반응)를 구현한 독립 모듈. `StageData`의 기믹 목록에 담긴 것만 활성화되어 한 스테이지에 여러 기믹을 조합할 수 있다.

## 2. 어셈블리 구조

```
Assets/_Project/
├─ Scripts/
│  ├─ Core/                  # ChainRiposte.Core.asmdef (noEngineReferences)
│  │  ├─ Flow/               # GamePhase, GameSession (페이즈 FSM)
│  │  ├─ Stats/              # PlayerStats, StatType, PlayerStatsConfig
│  │  ├─ Board/              # (2단계) GridPos, BoardGrid(마스킹), Tile
│  │  ├─ Stage/              # (2단계) StageConfig, 스폰 가중치, 기믹 목록
│  │  ├─ Match/              # (3단계) 매치 탐지, 중력, 리필, 콤보
│  │  ├─ Combat/             # (7단계) 패링 판정, 체간, 보스 패턴 실행기
│  │  └─ Gimmicks/           # (확장) IStageGimmick 모듈 3종
│  ├─ Game/                  # ChainRiposte.Game.asmdef (Core + InputSystem 참조)
│  │  ├─ Config/             # ScriptableObject 정의 (~SO)
│  │  ├─ Puzzle/             # (4단계) 보드/타일 뷰, 퍼즐 입력
│  │  ├─ Combat/             # (8단계) 전투 뷰, 전투 입력
│  │  └─ UI/                 # (5단계~) HUD, 스탯 분배, 결과 화면
│  └─ Tests/                 # (3단계~) EditMode 테스트 (Core 대상)
├─ Data/                     # SO 에셋 인스턴스 (스테이지, 보스, 밸런스)
├─ Prefabs/
├─ Scenes/
└─ Art/, Audio/              # 에셋 단계에서 채움
```

의존 방향: `Game → Core` 단방향. Core는 어떤 어셈블리도 참조하지 않는다.

## 3. 핵심 런타임 구조

```
GameManager (MonoBehaviour, 씬 진입점)
 └─ GameSession (순수 C#, 스테이지 1회의 상태 루트)
     ├─ Phase FSM  : None → Puzzle → Combat → Victory/Defeat (전환 검증)
     ├─ PlayerStats: 영혼석 XP → 레벨업 → 포인트 적립 → 스탯 분배
     ├─ (2~3단계) PuzzleState : BoardGrid + MatchEngine
     └─ (7단계)   CombatState : 보스 패턴 실행기 + 패링 판정
```

- `GameManager`는 SO 참조를 들고 `GameSession`을 조립하는 컴포지션 루트. 싱글턴/서비스 로케이터를 쓰지 않고 인스펙터 참조로 연결한다.
- 각 페이즈 컨트롤러(퍼즐/전투)는 `GameSession.PhaseChanged` 이벤트로 활성/비활성된다.

## 4. 개발 로드맵

| 단계 | 범위 | 상태 |
|---|---|---|
| 1 | 골격 + 페이즈 FSM + PlayerStats + 문서 | ✅ 완료 |
| 2 | BoardGrid(마스킹) + StageData/TileDefinition SO | ✅ 완료 |
| 3 | 매치 엔진 (스왑/매치/중력/리필/콤보) + EditMode 테스트 | ✅ 완료 |
| 4 | 퍼즐 프레젠테이션 (씬, 타일 뷰, 입력) | ✅ 완료 |
| 5 | 소울/레벨업/스탯 분배 UI + PlayerHealth | ✅ 완료 |
| 6 | 보스 난입 — 동적 스포너(AnimationCurve 2종: 점수/시간), 보스 타일 듀얼 카운트다운(초/턴), 정상/기습 돌입 + 대각선 낙하 | ✅ 완료 |
| 7 | 전투 코어 (2버튼, 패턴 실행기, 체간, 인살) | ⬜ |
| 8 | 전투 프레젠테이션 + 결과 화면 | ⬜ |
| 9 | Game Juice 훅 정비 (에셋 결합 준비) | ⬜ |
| 확장 | 스테이지 기믹 3종 — 전염되는 타일 / 시한폭탄 몬스터 / 사슬 결박 (GDD §3.6) | ⬜ |

## 5. 컨벤션

- 네임스페이스 = 폴더 경로 (`ChainRiposte.Core.Flow` 등), 파일당 1타입.
- Core의 상태 변화는 반드시 event로 노출. Game 레이어는 Core 상태를 직접 수정하지 않고 메서드 호출만 한다.
- SO 필드는 `[SerializeField] private` + `[Header]`/`[Tooltip]` 한국어 설명 — 기획자 편집 편의.
- 임시 비주얼은 `Prefabs/Placeholder/` 아래에 격리해 에셋 교체 시 통째로 대체.
