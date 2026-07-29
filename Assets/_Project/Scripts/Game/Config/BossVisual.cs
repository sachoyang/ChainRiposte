using ChainRiposte.Game.Characters;
using UnityEngine;

namespace ChainRiposte.Game.Config
{
    /// <summary>
    /// 이 판 보스의 <b>겉모습</b>을 정하는 단 하나의 규칙. 전투 화면과 준비 화면의 그림자가
    /// 각자 계산하면 <b>반드시 어긋난다</b> — 다가온 실루엣과 실제로 나오는 보스가 다른 사고가 그것이다.
    /// 그래서 부르는 곳이 둘이어도 규칙은 여기 하나뿐이다.
    ///
    /// <para>순서: <b>고른 캐릭터의 줄</b> → <b>0번 줄</b>(기본값). 그림의 자리가 목록 하나뿐이라
    /// "어디에 적었더라"가 생기지 않는다. 이름만 목록에 없을 때 보스의 공용 이름으로 떨어진다.</para>
    ///
    /// <para><b>겉모습만</b> 바뀐다. HP·체간·채보·패턴은 어느 캐릭터든 같은 <see cref="BossDataSO"/> 라서
    /// 캐릭터 선택이 난이도 선택이 되지 않는다 — 이 선을 넘으면 설계가 깨진 것이다.</para>
    /// </summary>
    public static class BossVisual
    {
        /// <summary>
        /// 전투에 설 그림. <paramref name="phaseIndex"/>는 <b>인살 페이즈</b> 번호다 —
        /// 맵의 실루엣과 준비 화면의 그림자는 항상 0(첫 모습)을 쓴다. 나중 모습을 미리 보여 주면
        /// 페이즈 전환이 놀랍지 않다.
        /// </summary>
        public static Sprite ResolveSprite(BossDataSO boss, int phaseIndex = 0) =>
            boss != null ? boss.GetBattleSprite(CharacterService.Current, phaseIndex) : null;

        /// <summary>
        /// 이 페이즈로 <b>넘어올 때</b>의 전환 그림(아세프리트의 <c>trans</c> 레이어).
        /// 없으면 null — 컷씬은 그림 없이 문구만으로도 돈다.
        /// </summary>
        public static Sprite ResolveTransitionSprite(BossDataSO boss, int phaseIndex) =>
            boss != null ? boss.GetTransitionSprite(CharacterService.Current, phaseIndex) : null;

        /// <summary>전환 컷씬 한 줄의 현지화 키. 없으면 null — 문구 없이 그림만 뜬다.</summary>
        public static string ResolveTransitionTextKey(BossDataSO boss, int phaseIndex) =>
            boss != null ? boss.GetTransitionTextKey(phaseIndex) : null;

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
