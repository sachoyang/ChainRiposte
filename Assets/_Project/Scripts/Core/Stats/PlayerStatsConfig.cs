namespace ChainRiposte.Core.Stats
{
    /// <summary>
    /// 플레이어 성장 밸런스 값. Game 레이어의 PlayerStatsConfigSO에서 변환되어 주입된다.
    /// (Core는 ScriptableObject를 모른다.)
    /// </summary>
    public sealed class PlayerStatsConfig
    {
        public int MaxHp = 100;

        public int BaseSoulsToLevel = 30;
        public int SoulsToLevelGrowth = 15;

        public float BaseAttackDamage = 10f;
        public float AttackDamagePerLevel = 3f;

        public float BaseDamageReduction = 0f;
        public float DamageReductionPerLevel = 2f;

        public float BaseParryWindowSeconds = 0.15f;
        public float ParryWindowPerLevelSeconds = 0.03f;

        /// <summary>판정치 하드 캡 — 이 레벨에 도달하면 더 이상 분배할 수 없다.</summary>
        public int ParryLevelHardCap = 5;
    }
}
