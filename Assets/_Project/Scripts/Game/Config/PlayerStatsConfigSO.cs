using ChainRiposte.Core.Stats;
using UnityEngine;

namespace ChainRiposte.Game.Config
{
    /// <summary>기획자가 인스펙터에서 조절하는 플레이어 성장 밸런스. 런타임에는 순수 C# config로 변환된다.</summary>
    [CreateAssetMenu(menuName = "ChainRiposte/Player Stats Config", fileName = "PlayerStatsConfig")]
    public sealed class PlayerStatsConfigSO : ScriptableObject
    {
        [Header("생존")]
        [SerializeField, Min(1)] private int maxHp = 100;

        [Header("레벨업 요구 영혼석")]
        [Tooltip("첫 레벨업에 필요한 영혼석")]
        [SerializeField, Min(1)] private int baseSoulsToLevel = 30;
        [Tooltip("레벨업할 때마다 증가하는 요구량")]
        [SerializeField, Min(0)] private int soulsToLevelGrowth = 15;

        [Header("공격력 (ATK)")]
        [SerializeField, Min(0f)] private float baseAttackDamage = 10f;
        [SerializeField, Min(0f)] private float attackDamagePerLevel = 3f;

        [Header("방어력 (DEF) — 피격 피해 감소량")]
        [SerializeField, Min(0f)] private float baseDamageReduction = 0f;
        [SerializeField, Min(0f)] private float damageReductionPerLevel = 2f;

        [Header("판정치 (Parry)")]
        [SerializeField, Min(0f)] private float baseParryWindowSeconds = 0.15f;
        [SerializeField, Min(0f)] private float parryWindowPerLevelSeconds = 0.03f;
        [Tooltip("하드 캡: 이 레벨에 도달하면 더 이상 분배할 수 없다")]
        [SerializeField, Min(0)] private int parryLevelHardCap = 5;

        public PlayerStatsConfig ToConfig() => new()
        {
            MaxHp = maxHp,
            BaseSoulsToLevel = baseSoulsToLevel,
            SoulsToLevelGrowth = soulsToLevelGrowth,
            BaseAttackDamage = baseAttackDamage,
            AttackDamagePerLevel = attackDamagePerLevel,
            BaseDamageReduction = baseDamageReduction,
            DamageReductionPerLevel = damageReductionPerLevel,
            BaseParryWindowSeconds = baseParryWindowSeconds,
            ParryWindowPerLevelSeconds = parryWindowPerLevelSeconds,
            ParryLevelHardCap = parryLevelHardCap,
        };
    }
}
