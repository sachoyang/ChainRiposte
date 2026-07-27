# AUDIO.md — 사운드 배선 안내 (배관 완료, 클립만 꽂으면 됨)

> 세션 9에서 **오디오 배관을 정리**했다. 코드상 모든 소리가 옵션의 BGM/SFX 볼륨을 타도록 통일돼 있고,
> 이제 **클립 파일을 슬롯에 꽂기만** 하면 소리가 난다. 이 문서는 그 "꽂을 목록"이다.

## 구조 (왜 이렇게 됐나)

- **`AudioService`** = BGM 소스 1개 + SFX 소스 1개의 **유일한 주인**. 옵션의 볼륨 슬라이더가 이 두 소스에 직접 먹는다.
  - 이전에는 `JuiceDirector` 가 자기 AudioSource 를 따로 만들어 써서 **옵션 볼륨이 게임플레이 소리에 안 먹었다**(버그). 지금은 음악·효과음 전부 이 버스로 흐른다.
- **`JuiceDirector`** (Main 씬, Juice GO) = 게임플레이 이벤트 → 소리. 음악은 `PlayBgm`, 효과음은 `PlaySfx` 로 버스에 넘긴다.
  - 예외: **난입 크레센도**(`tensionLoop`)만 자체 소스. 스폰 확률에 따라 매 프레임 볼륨이 바뀌어야 해서. 이것도 BGM 버스 볼륨을 곱한다.
- **`SceneMusic`** (신규 컴포넌트) = JuiceDirector 없는 씬(인트로/타이틀/월드맵)의 BGM. GO 하나에 붙이고 클립만 꽂으면 그 씬 진입 시 재생.

버스 배정: **음악·크레센도 → BGM 슬라이더**, **매치·패링·피격 등 단발음 → SFX 슬라이더**.

---

## 꽂을 곳 목록

### 1) `JuiceDirector` (Main.unity ▸ Juice GO 인스펙터)

| 슬롯 | 종류 | 언제 | 버스 |
|---|---|---|---|
| `puzzleMusic` | 루프 | 퍼즐 페이즈 BGM | BGM |
| `combatMusic` | 루프 | 전투 페이즈 BGM | BGM |
| `tensionLoop` | 루프 | 보스 스폰 확률↑ 크레센도(디스토션 노이즈) | BGM |
| `matchClearClip` | 단발 | 타일 매치(콤보마다 피치↑) | SFX |
| `levelUpClip` | 단발 | 레벨업 | SFX |
| `parryClip` | 단발 | 패링 성공 | SFX |
| `playerHitClip` | 단발 | 피격 | SFX |
| `attackLandClip` | 단발 | 공격 명중 | SFX |
| `bossBrokenClip` | 단발 | 체간 붕괴 | SFX |
| `executionClip` | 단발 | 인살 | SFX |

### 2) `SceneMusic` (씬마다 GO 하나 + 컴포넌트 붙이고 `clip` 꽂기)

| 씬 | 곡 |
|---|---|
| `Intro.unity` | 인트로 BGM (짧아도 됨) |
| `Title.unity` | 타이틀 BGM |
| `StageSelect.unity` | 월드맵 BGM |

> `clip` 을 비워 두면 아무것도 안 한다(앞 씬 음악을 그대로 둠). `loop` 기본 켬.

### 3) `StageSelectController` (StageSelect.unity 인스펙터)

| 슬롯 | 종류 | 언제 | 버스 |
|---|---|---|---|
| `blockedSfx` | 단발 | 잠긴 노드로 막혀 튕길 때 | SFX |

---

## 배선 절차 (에디터에서)

1. 클립 파일을 `Assets/_Project/Audio/` (없으면 새로) 아래에 정리해 넣는다.
2. 위 표대로 각 인스펙터 슬롯에 드래그.
3. 인트로/타이틀/월드맵은 빈 GO 만들고 `Add Component ▸ Scene Music` → 클립 꽂기.
4. 옵션 화면에서 BGM/SFX 슬라이더를 움직여 **각각 해당 소리만 줄어드는지** 확인.

## 검증 포인트

- [ ] 옵션 BGM 슬라이더 0 → 음악·크레센도만 사라지고 효과음은 남는지
- [ ] 옵션 SFX 슬라이더 0 → 매치/패링/피격만 사라지고 음악은 남는지
- [ ] 퍼즐→전투 진입 시 BGM 이 교체되는지, 전투 승리 후 월드맵으로 오면 맵 BGM 이 다시 나는지
- [ ] 콤보가 길어질수록 매치음 피치가 올라가는지
- [ ] 보스 스폰 확률이 오를 때 크레센도가 커지는지

## 클립 조달 메모

전 프로젝트에 오디오 파일이 **아직 0개**다. 무료로 채우려면 CC0 우선:
- **효과음**: [freesound.org](https://freesound.org) (CC0 필터), [sonniss GDC 팩], [kenney.nl](https://kenney.nl) UI/impact 팩
- **BGM**: [incompetech](https://incompetech.com), [OpenGameArt](https://opengameart.org), [Pixabay Music]
- 검색어 예: `parry clang`, `sword hit`, `match pop`, `level up chime`, `execution stab`, `dark ambient tension loop`, `puzzle ambient loop`, `boss battle loop`
