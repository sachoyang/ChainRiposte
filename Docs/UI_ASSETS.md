# UI 에셋 배치 계획 (미배선)

> 마지막 갱신: 2026-07-22
> **상태: 계획만 확정. 실제 배선은 아직 안 함.** 이 문서를 보고 사용자가 직접 꽂거나,
> "UI 배선 해줘"라고 하면 이 표대로 빌더에 슬롯을 열고 채운다.

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
| | 진행도 초기화 | `btn_pill` | 위험 버튼이라 형태로 구분 |
| | 닫기 | `icon_back` | |
| **월드맵** | 정보 패널 | `panel` | |
| | START | `btn_wide_3d` | 주요 행동이라 가장 두드러지는 것 |
| | LOCK / CLEAR 배지 | `box_small` | |
| **퍼즐 HUD** | +ATK / +DEF / +PARRY | `btn_wide` (짧게) | 라벨은 코드가 채움(`Loc.GetText`) |
| | 설정 진입 | `icon_settings` | 아직 없음 — 넣을지는 미정 |
| **전투** | PARRY / ATTACK | `btn_pill` | 큰 터치 타겟에 둥근 형태가 맞음 |
| **결과** | 패널 | `panel` | |
| | RESTART / MAP | `btn_wide` | |

---

## 2. 배선 전에 정해야 할 것

**① 색.** 이 팩은 초록/회색 기조, 우리 게임은 어두운 보라(`0.10, 0.09, 0.13`) +
핏빛 붉은색(`0.55, 0.16, 0.18`)이다.
회색 조각(`btn_wide`, `panel`, `box_small`)은 `Image.color` 틴트로 톤을 맞출 수 있고,
초록 조각(`btn_pill`)은 틴트가 잘 안 먹으니 강조용으로만 쓰거나 뺀다.

**② 9슬라이스.** 테두리 값은 눈대중으로 넣어 뒀다(버튼 16, 패널 32~40).
실제로 늘려 보고 모서리가 뭉개지면 Sprite Editor에서 조정한다.
`Image.type`을 **Sliced**로 두어야 테두리가 먹는다.

**③ 눌림 상태.** `Button.transition`이 지금 `None`이다.
`SpriteSwap`으로 바꾸고 `_pressed` 조각을 물리면 누르는 느낌이 생긴다.

---

## 3. Veilworks 블록 팩 (보드 쪽)

25종의 32×32 블록. 각 `*Full.png`는 이미 프레임별로 잘려 있다.

- **`BreakableFull` (4프레임) → 벽 손상 단계로 이미 배선 완료.**
  `BoardView.wallDamageSprites`에 0~3이 들어가 있고, 벽 HP가 깎일수록 금이 간 그림으로 바뀐다.
- 나머지 블록은 월드별 벽·배경 타일 후보. 월드2 이후 분위기 차별화에 쓸 수 있다.
