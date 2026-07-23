# UI 에셋 배치 계획 (미배선)

> 마지막 갱신: 2026-07-23
> **상태: 계획 + 추천 확정. 배선은 사용자가 직접 한다.** §1이 무엇을 어디에, §2가 어떻게/왜 그렇게.

---

## 0. 보유 에셋

| 팩 | 경로 | 용도 |
|---|---|---|
| DEVNIK 2D — UI PIXEL BUTTONS | `Assets/DEVNIK 2D/2D UI PIXEL BUTTONS/` | **UI** (버튼·패널·아이콘·슬라이더) |
| Veilworks — 2D Pixel Art Blocks | `Assets/Veilworks Studio/2D Pixel Art Blocks/` | **보드 타일·벽 소재** (UI 아님) |

DEVNIK 시트(`UI SIMPLE PIXEL UNSPLIT.png`)는 원본이 안 잘려 있어서
알파 연결성분으로 **29조각**을 잘라 이름 + 9슬라이스 테두리 + Point 필터를 넣어 두었다.

### 쓰면 안 되는 것

`baked_play` / `baked_levels` / `baked_settings` / `baked_exit`
— **글자가 이미지에 구워져 있어 현지화가 불가능하다.** 한국어로 못 바꾼다.
빈 버튼(`btn_wide` 등) + TMP 텍스트 조합으로만 간다. (CLAUDE.md 「구조 원칙」 현지화 항목)

### 잘라 둔 조각

- 아이콘(각 6종 × 기본/눌림): `icon_play`, `icon_pause`, `icon_settings`,
  `icon_back`, `icon_sound_on`, `icon_sound_off` (+ `_pressed`)
- 빈 버튼: `btn_wide`, `btn_wide_pressed`, `btn_wide_3d`, `btn_pill`
- 패널: `panel`, `panel_tabbed`, `box_small`, `box_small_b`
- 슬라이더/컨테이너: `bar_container_a`, `bar_container_b`, `slider_handle`
- 기타: `arrow_left`, `arrow_right`

---

## 1. 화면별 배치안

| 화면 | 요소 | 스프라이트 | 비고 |
|---|---|---|---|
| **타이틀** | 메뉴 4버튼 | `btn_wide` / 눌림 `btn_wide_pressed` | 라벨은 TMP + `LocalizedText` 유지 |
| | 확인 대화상자 | `panel` | |
| **옵션** | 패널 배경 | `panel_tabbed` | 탭 부분에 "옵션" 얹기 |
| | BGM/SFX 슬라이더 | 트랙 `bar_container_a` + 핸들 `slider_handle` | 채움은 `bar_container_b` |
| | 방향 3버튼 / 언어 버튼 | `btn_wide` | 선택된 것만 `btn_wide_pressed` |
| | 진행도 초기화 | `box_small_b` | 위험 버튼이라 형태로 구분 (`btn_pill`은 초록이라 뺌 — §2①) |
| | 닫기 | `icon_back` | 회색조 사본을 떠서 쓸 것 |
| **월드맵** | 정보 패널 | `panel` | |
| | START | `btn_wide_3d` | 주요 행동이라 가장 두드러지는 것 |
| | LOCK / CLEAR 배지 | `box_small` | |
| **퍼즐 HUD** | +ATK / +DEF / +PARRY | `btn_wide` (짧게) | 라벨은 코드가 채움(`Loc.GetText`) |
| | 설정 진입 | `icon_settings` | 아직 없음 — 넣을지는 미정 |
| **전투** | PARRY / ATTACK | `btn_wide` (크게, 어둡게) | 원(패링 표시)에서 시선을 뺏으면 안 된다 — §2⑤ |
| **결과** | 패널 | `panel` | |
| | RESTART / MAP | `btn_wide` | |

---

## 2. 배선 전에 정해야 할 것 (추천 — 2026-07-23)

### ① 큰 방향: **회색 조각만 쓴다**

이 팩은 밝은 카툰 톤이고 우리 게임은 어두운 소울라이크 도트다. 톤이 안 맞는다.
그래서 **형태(두꺼운 검정 아웃라인 + 오른쪽아래 그림자)만 빌리고 색은 전부 우리 걸로 덮는다.**
아웃라인/그림자 자체는 도트 게임에 오히려 잘 어울리니 살린다.

- **쓴다**: `panel`, `panel_tabbed`, `box_small(_b)`, `btn_wide(_pressed)`, `btn_wide_3d`,
  `bar_container_a/b`, `slider_handle`, `arrow_left/right`
  → 원본이 거의 흰 회색(≈0.9)이라 `Image.color` 곱하기 틴트가 **거의 그대로 나온다.** 통일의 유일한 수단.
- **안 쓴다(그대로는)**: 컬러 아이콘 6종(초록/빨강/파랑/주황), `btn_pill`(초록)
  → 색이 구워져 있어 곱하기 틴트가 안 먹는다. 초록(0.1,0.7,0.2)에 핏빛(0.55,0.16,0.18)을 곱하면 거의 검정이 된다.
  쓰고 싶으면 **텍스처를 회색조로 만든 사본**을 하나 떠서 그걸 틴트한다. 그게 정공법.

### ② 틴트 팔레트 (현재 씬 색에서 뽑음)

| 역할 | 값 | 어디에 |
|---|---|---|
| 패널 바닥 | `0.11, 0.10, 0.14` | `panel`, `panel_tabbed` |
| 일반 버튼 | `0.26, 0.24, 0.32` | `btn_wide` |
| 눌림·비활성 | `0.16, 0.15, 0.20` | `btn_wide_pressed`, disabled |
| **주요 행동** | `0.55, 0.16, 0.18` | START / FIGHT / RESTART |
| 위험 | `0.30, 0.09, 0.11` + 형태 구분 | 진행도 초기화 |
| 텍스트 | `0.86, 0.83, 0.76` (뼈색) | 순백은 픽셀 폰트에서 눈이 아프다 |

강조 금색 `0.95, 0.83, 0.35` / 체간 주황 `0.95, 0.62, 0.12`은 이미 쓰고 있으니 UI에서 재사용하지 말 것 —
**게이지 색과 버튼 색이 겹치면 전투 중 시선이 분산된다.**

### ③ 픽셀 뭉개짐 (여기서 제일 많이 망한다)

Canvas 기준 해상도가 1080×1920인데 조각은 32~64px이라 그냥 늘리면 픽셀이 뭉갠다.

- 텍스처: **Filter = Point, Compression = None**, Pixels Per Unit은 팩 원본 그대로.
- `Image.type = Sliced`(안 하면 9슬라이스가 아예 안 먹는다) + **`pixelsPerUnitMultiplier`로 정수배 유지.**
- 9슬라이스 border는 **그림자까지 포함해서** 잡는다. 그림자를 빼면 늘렸을 때 그림자만 끊긴다.
- `btn_wide_3d`는 오른쪽 두께가 비대칭 → **좌/우 border 값을 다르게** 줘야 한다.

### ④ 눌림 상태

`Button.transition`이 지금 `None`이다. `SpriteSwap`으로 바꾸고 `_pressed`를 물린다.
`_pressed` 조각이 없는 곳(패널/배지)은 `ColorTint`로 어둡게만 해도 충분하다.

### ⑤ 우선순위 — **전투 화면은 마지막에, 대비를 낮게**

1. **타이틀 / 결과 / 월드맵 정보패널** — 정지 상태로 오래 보는 화면. 체감이 가장 크다.
2. **옵션** — 슬라이더가 지금 제일 허술하다.
3. **퍼즐 HUD 스탯 3버튼** — 보드가 주인공이니 버튼은 조용해야 한다.
4. **전투 PARRY / ATTACK** — 여기는 **일부러 마지막**. 전투 중 시선은 다가오는 흰 원에 있어야 하는데
   버튼이 화려하면 그걸 뺏는다. 테두리만 주고 채움은 어둡게, 눌림 반응만 확실히.

### ⑥ 팩에 없어서 따로 구해야 하는 것

- **ATK / DEF / PARRY 아이콘** — 없다. 당분간 TMP 텍스트로 간다.
- **자물쇠 / 깃발 배지** — 없다. `box_small` + 글자, 또는 `DotImgs`에서 대체.
- **체크박스 / 토글** — 없다. `btn_wide` 선택-비선택 2벌로 대신한다(옵션 방향·언어가 이미 그 방식).

---

## 3. Veilworks 블록 팩 (보드 쪽)

25종의 32×32 블록. 각 `*Full.png`는 이미 프레임별로 잘려 있다.

- **`BreakableFull` (4프레임) → 벽 손상 단계로 이미 배선 완료.**
  `BoardView.wallDamageSprites`에 0~3이 들어가 있고, 벽 HP가 깎일수록 금이 간 그림으로 바뀐다.
- 나머지 블록은 월드별 벽·배경 타일 후보. 월드2 이후 분위기 차별화에 쓸 수 있다.
