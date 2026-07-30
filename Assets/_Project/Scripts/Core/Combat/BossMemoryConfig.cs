using System;
using System.Collections.Generic;

namespace ChainRiposte.Core.Combat
{
    /// <summary>
    /// 삼킨 <b>보스의 기억</b>들이 전투에 주는 효과의 합 (설계: <c>Docs/PROGRESSION.md</c> §2.2).
    ///
    /// <para>기억 하나도 이 타입이고, 여러 개를 합친 결과도 이 타입이다(<see cref="Combine"/>) —
    /// <see cref="CombatSystem"/>은 "기억이 몇 개인지"를 알 필요가 없고 <b>합산된 수치 한 벌</b>만 읽는다.
    /// 기억을 늘려도 전투 코드가 늘지 않는다.</para>
    ///
    /// <para><b>규칙: 평타 파워업은 넣지 않는다.</b> "공격력 +10" 같은 것은 스노볼을 키워서
    /// 뒤로 갈수록 판정을 안 봐도 이기게 만든다. 여기 있는 것은 전부 <b>잘 눌렀을 때 더 받는</b> 종류다.</para>
    /// </summary>
    public sealed class BossMemoryConfig
    {
        /// <summary>기억이 하나도 없는 상태 — 순수 기본 규칙. (null 검사를 전투 코드에서 없애기 위한 값)</summary>
        public static readonly BossMemoryConfig None = new();

        /// <summary>
        /// 헛침 잠금이 아무리 짧아져도 이 배수 아래로는 안 내려간다.
        /// 헛침 비용이 0에 가까워지면 판정을 읽는 게임이 아니라 연타 게임으로 돌아간다 —
        /// 기억을 다 모아도 그 선은 넘지 못한다.
        /// </summary>
        public const float MinWhiffLockMultiplier = 0.4f;

        /// <summary>패링 성공 시 체간에 <b>더</b> 얹는 양. 보스의 <c>ParryPostureGain</c>에 가산된다.</summary>
        public float BonusParryPostureGain;

        /// <summary>
        /// 헛침 잠금 시간 배수(1 = 그대로, 0.8 = −20%). <see cref="MinWhiffLockMultiplier"/>가 하한.
        /// <para><b>0 이하는 "효과 없음"으로 읽는다</b> — 인스펙터에서 안 채운 칸이 0으로 남는데,
        /// 그것을 곧이곧대로 곱하면 <b>안 채운 기억이 헛침 처벌을 통째로 없애 버린다.</b></para>
        /// </summary>
        public float WhiffLockMultiplier = 1f;

        /// <summary>
        /// 연속 패링 N회를 채우면 <b>다음 피격 1회를 무효</b>로 만든다. 0이면 이 효과 없음.
        /// <para>여러 기억이 각자 값을 주면 <b>가장 작은 값</b>(가장 관대한 쪽)이 이긴다 — 조건을 두 벌
        /// 따로 세면 어느 쪽이 찼는지 화면에서 읽을 수 없다.</para>
        /// </summary>
        public int PerfectStreakGuard;

        /// <summary>이 기억이 실제로 무언가를 하는가 — 수치를 하나도 안 채운 에셋을 걸러낸다.</summary>
        public bool HasEffect =>
            BonusParryPostureGain > 0f
            || (WhiffLockMultiplier > 0f && WhiffLockMultiplier < 1f)
            || PerfectStreakGuard > 0;

        /// <summary>
        /// 여러 기억을 한 벌로 합친다. 가산은 더하고, 배수는 곱하고, 조건은 관대한 쪽을 고른다.
        /// <para>비었거나 null이면 <see cref="None"/>과 같은 값이 나온다 — 부르는 쪽에서 개수를 세지 않게.</para>
        /// </summary>
        public static BossMemoryConfig Combine(IEnumerable<BossMemoryConfig> parts)
        {
            BossMemoryConfig total = new();
            if (parts == null)
                return total;

            foreach (BossMemoryConfig part in parts)
            {
                if (part == null)
                    continue;

                total.BonusParryPostureGain += Math.Max(0f, part.BonusParryPostureGain);

                // 0 이하(안 채운 칸)와 1 이상(효과 없음)은 그냥 넘긴다 — 위 필드 주석 참조.
                if (part.WhiffLockMultiplier > 0f && part.WhiffLockMultiplier < 1f)
                    total.WhiffLockMultiplier *= part.WhiffLockMultiplier;

                if (part.PerfectStreakGuard > 0
                    && (total.PerfectStreakGuard == 0 || part.PerfectStreakGuard < total.PerfectStreakGuard))
                    total.PerfectStreakGuard = part.PerfectStreakGuard;
            }

            total.WhiffLockMultiplier = Math.Max(MinWhiffLockMultiplier, total.WhiffLockMultiplier);
            return total;
        }
    }
}
