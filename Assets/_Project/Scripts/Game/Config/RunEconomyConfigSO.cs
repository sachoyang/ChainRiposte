using ChainRiposte.Core.Progress;
using UnityEngine;

namespace ChainRiposte.Game.Config
{
    /// <summary>
    /// 기획자가 인스펙터에서 조절하는 런 경제 밸런스 (<c>Docs/PROGRESSION.md</c> §2.4). 런타임에는 순수 C# config로 변환된다.
    /// <b>여기가 "돈을 얼마나 줄지"의 유일한 손잡이</b> — 성장이 너무 빠르면 <see cref="soulIncomeMultiplier"/>부터 내린다.
    /// </summary>
    [CreateAssetMenu(menuName = "ChainRiposte/Run Economy Config", fileName = "RunEconomyConfig")]
    public sealed class RunEconomyConfigSO : ScriptableObject
    {
        [Header("소울 인컴")]
        [Tooltip("매치로 버는 소울 배수. 성장 캐리로 소울이 판을 넘어 쌓이므로, 1보다 낮춰 스노볼을 억제한다. " +
            "쉽다고 느끼면 여기부터 내린다.")]
        [SerializeField, Min(0f)] private float soulIncomeMultiplier = 1f;

        [Header("사슬 배수 (무사망 연속 클리어 보상)")]
        [Tooltip("사슬 한 칸당 추가 인컴 비율. 0.1 = +10%/칸")]
        [SerializeField, Min(0f)] private float chainSoulBonusPerStep = 0.1f;
        [Tooltip("사슬 인컴 배수 상한. 2 = 최대 +100%")]
        [SerializeField, Min(1f)] private float maxChainMultiplier = 2f;

        [Header("소울 광맥 (스테이지별 매장량)")]
        [Tooltip("스테이지가 자기 매장량(Stage Data ▸ 소울 매장량)을 안 적었을 때 쓰는 기본값. " +
            "한 스테이지에서 이 런 동안 캘 수 있는 총량이다. 0이면 무제한 — 앞 스테이지 반복 파밍이 열린다.")]
        [SerializeField, Min(0)] private int defaultStageSoulBudget;

        public RunEconomyConfig ToConfig() => new()
        {
            SoulIncomeMultiplier = soulIncomeMultiplier,
            ChainSoulBonusPerStep = chainSoulBonusPerStep,
            MaxChainMultiplier = maxChainMultiplier,
            DefaultStageSoulBudget = defaultStageSoulBudget,
        };
    }
}
