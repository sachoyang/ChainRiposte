using System;

namespace ChainRiposte.Core.Progress
{
    /// <summary>
    /// 런 경제 밸런스 (<c>Docs/PROGRESSION.md</c> §2.4). Game 레이어의 RunEconomyConfigSO에서 변환되어 주입된다.
    /// (Core는 ScriptableObject를 모른다.)
    ///
    /// <para>성장 캐리로 소울이 판을 넘어 쌓이므로 <b>인컴을 조여 스노볼을 막고</b>, 대신 사슬(무사망 연속
    /// 클리어)에 보상을 준다. 난이도는 실행(패링)에서 오고 소울은 모서리만 깎는다는 원칙(§0)을 지키는 손잡이.</para>
    /// </summary>
    public sealed class RunEconomyConfig
    {
        /// <summary>매치로 버는 소울 배수. 1보다 낮추면 성장이 느려져 스노볼을 억제한다.</summary>
        public float SoulIncomeMultiplier = 1f;

        /// <summary>사슬 한 칸(무사망 연속 클리어)당 추가 인컴 비율. 0.1 = +10%/칸.</summary>
        public float ChainSoulBonusPerStep = 0.1f;

        /// <summary>사슬 인컴 배수 상한 — 끝없이 커지지 않게. 2 = 최대 +100%.</summary>
        public float MaxChainMultiplier = 2f;

        /// <summary>
        /// 스테이지가 자기 매장량을 안 적었을 때 쓰는 기본 <b>소울 광맥</b> 크기 —
        /// 한 스테이지에서 이 런 동안 캘 수 있는 총량. 0 이하면 무제한(광맥 개념 끔).
        ///
        /// <para>이게 없으면 쉬운 앞 스테이지를 반복해서 무한 파밍이 된다. 반대로 "클리어한 판은 인컴 0"으로
        /// 막으면 <b>한 번에 다 캐지 못한 사람이 영구히 손해</b>를 본다 — 퍼즐을 잘 푸는 것에 벌을 주는 셈이다.
        /// 매장량은 천장을 같게 하고, 잘하는 사람은 대신 <b>시간</b>을 아낀다(실력 보상은 사슬 배수가 맡는다).</para>
        /// </summary>
        public int DefaultStageSoulBudget = 0;

        /// <summary>
        /// 그 스테이지에 아직 남아 있는 소울. <paramref name="budget"/>이 0 이하면 무제한이라
        /// <see cref="int.MaxValue"/>를 돌려준다 — 부르는 쪽이 "무제한"을 따로 분기하지 않게 하기 위한 것이다.
        /// </summary>
        public int RemainingSouls(int budget, int harvested)
        {
            int total = budget > 0 ? budget : DefaultStageSoulBudget;
            if (total <= 0)
                return int.MaxValue;

            return Math.Max(0, total - Math.Max(0, harvested));
        }

        /// <summary>그 스테이지의 총 매장량 (스테이지 값 우선, 없으면 기본값). 0이면 무제한.</summary>
        public int ResolveBudget(int budget) => budget > 0 ? budget : Math.Max(0, DefaultStageSoulBudget);

        /// <summary>
        /// 매치 소울(<paramref name="rawSouls"/>)에 인컴 배수와 사슬 배수를 적용한 최종 획득량.
        /// 사슬 배수 = 1 + 사슬칸수 × 칸당보너스, 상한으로 자른다.
        /// </summary>
        public int ScaleSoulIncome(int rawSouls, int chainStep)
        {
            if (rawSouls <= 0)
                return 0;

            float income = Math.Max(0f, SoulIncomeMultiplier);
            float chainMult = 1f + Math.Max(0, chainStep) * Math.Max(0f, ChainSoulBonusPerStep);
            chainMult = Math.Min(chainMult, Math.Max(1f, MaxChainMultiplier));

            int scaled = (int)Math.Round(rawSouls * income * chainMult, MidpointRounding.AwayFromZero);
            return Math.Max(0, scaled);
        }
    }
}
