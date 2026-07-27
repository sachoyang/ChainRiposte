using ChainRiposte.Game.Characters;
using UnityEngine;

namespace ChainRiposte.Game.Theming
{
    /// <summary>
    /// 지금 적용할 테마를 묻는 창구. 테마는 <b>고른 캐릭터가 가리키는 것</b>이라
    /// 따로 저장하지 않는다 — 저장 상태가 둘로 갈라지면 반드시 어긋난다.
    ///
    /// <para>바뀌는 순간을 알고 싶으면 <see cref="CharacterService.Changed"/>를 구독한다
    /// (여기서 이벤트를 한 번 더 중계하지 않는 이유: 정적 초기화 순서에 기대게 되기 때문).</para>
    /// </summary>
    public static class ThemeService
    {
        /// <summary>고른 캐릭터의 테마. 캐릭터가 없거나 테마를 안 걸었으면 null.</summary>
        public static ThemeSO Current
        {
            get
            {
                PlayerCharacterSO character = CharacterService.Current;
                return character != null ? character.Theme : null;
            }
        }

        /// <summary>테마의 배경. 없으면 null — 부르는 쪽은 씬에 꽂아둔 그림을 그대로 둔다.</summary>
        public static Sprite GetBackground(string key)
        {
            ThemeSO theme = Current;
            return theme != null ? theme.GetBackground(key) : null;
        }

        // 보스 겉모습은 이 창구를 지나지 않는다 — 보스 에셋이 캐릭터별로 직접 들고 있고,
        // 규칙은 BossVisual 한 곳뿐이다.
    }
}
