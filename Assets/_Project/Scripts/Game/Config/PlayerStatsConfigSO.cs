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
        [SerializeField, Min(0f)] private float baseParryWindowSeconds = 0.25f;
        [SerializeField, Min(0f)] private float parryWindowPerLevelSeconds = 0.03f;
        [Tooltip("타격이 지난 뒤에도 이만큼은 패링을 받아 준다 — 사람은 원이 닿는 걸 보고 누르므로 대개 살짝 늦는다")]
        [SerializeField, Min(0f)] private float parryLateGraceSeconds = 0.12f;
        [Tooltip("하드 캡: 이 레벨에 도달하면 더 이상 분배할 수 없다")]
        [SerializeField, Min(0)] private int parryLevelHardCap = 5;

        [Header("전투 템포 (7단계)")]
        [Tooltip("공격 커밋 시간 — 이 동안 패링 불가, 끝나는 순간 타격")]
        [SerializeField, Min(0f)] private float attackCommitSeconds = 0.4f;
        [Tooltip("패링 헛침 후딜레이 — 연타 방지")]
        [SerializeField, Min(0f)] private float parryWhiffLockSeconds = 0.25f;

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
            AttackCommitSeconds = attackCommitSeconds,
            ParryWhiffLockSeconds = parryWhiffLockSeconds,
            ParryLateGraceSeconds = parryLateGraceSeconds,
        };
    }
}
