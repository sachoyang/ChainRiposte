using ChainRiposte.Game.Characters;
using UnityEngine;

namespace ChainRiposte.Game.Config
{
    /// <summary>
    /// 이 판 보스의 <b>겉모습</b>을 정하는 단 하나의 규칙. 전투 화면과 준비 화면의 그림자가
    /// 각자 계산하면 <b>반드시 어긋난다</b> — 다가온 실루엣과 실제로 나오는 보스가 다른 사고가 그것이다.
    /// 그래서 부르는 곳이 둘이어도 규칙은 여기 하나뿐이다.
    ///
    /// <para>순서: <b>보스 에셋의 캐릭터별 겉모습</b>(기획이 채우는 자리) → <b>보스 에셋의 공용 그림·이름</b>.
    /// 자리가 하나뿐이라 "어디에 적었더라"가 생기지 않는다.</para>
    ///
    /// <para><b>겉모습만</b> 바뀐다. HP·체간·채보·패턴은 어느 캐릭터든 같은 <see cref="BossDataSO"/> 라서
    /// 캐릭터 선택이 난이도 선택이 되지 않는다 — 이 선을 넘으면 설계가 깨진 것이다.</para>
    /// </summary>
    public static class BossVisual
    {
        public static Sprite ResolveSprite(BossDataSO boss)
        {
            if (boss == null)
                return null;

            Sprite perCharacter = boss.GetBattleSprite(CharacterService.Current);
            return perCharacter != null ? perCharacter : boss.BattleSprite;
        }

        /// <summary>
        /// 이름은 <b>현지화 키</b>다. 키가 CSV에 없으면 받은 문자열이 그대로 화면에 나오므로
        /// 구 데이터의 생 이름도 경고 없이 계속 보인다는 점에 주의.
        /// </summary>
        public static string ResolveNameKey(BossDataSO boss)
        {
            if (boss == null)
                return null;

            string perCharacter = boss.GetNameKey(CharacterService.Current);
            return !string.IsNullOrWhiteSpace(perCharacter) ? perCharacter : boss.NameKey;
        }
    }
}
