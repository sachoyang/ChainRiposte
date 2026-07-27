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

        /// <summary>
        /// 아무것도 안 찍은 상태의 판정 폭. <b>여기가 좁은 것이 정상이다</b> —
        /// PARRY에 투자하지 않았으면 실제로 어려워야 스탯이 의미를 갖는다.
        /// </summary>
        public float BaseParryWindowSeconds = 0.13f;

        /// <summary>
        /// PARRY 1레벨당 넓어지는 판정 폭.
        /// 하드 캡(5)까지 찍으면 기본값의 <b>두 배 가까이</b> 넓어진다 — 화면의 회색 띠가
        /// 눈에 띄게 굵어져야 "찍었더니 실제로 쉬워졌다"가 읽히기 때문이다.
        /// 대신 캡의 폭(유예 포함 0.37초)이 <b>상한</b>이다. 이보다 더 넓히면 아무 때나 눌러도 막혀서
        /// 리듬 게임이 아니게 되므로, 조일 때는 기본값이 아니라 캡을 먼저 본다.
        /// </summary>
        public float ParryWindowPerLevelSeconds = 0.024f;

        /// <summary>
        /// 타격이 지난 뒤에도 이만큼은 패링을 받아 준다.
        /// 윈도우가 전부 타격 '이전'에만 열려 있으면 조금만 늦어도 무조건 실패해서 손이 아프다 —
        /// 사람은 원이 닿는 걸 보고 누르므로 대개 살짝 늦는다.
        /// </summary>
        public float ParryLateGraceSeconds = 0.12f;

        /// <summary>판정치 하드 캡 — 이 레벨에 도달하면 더 이상 분배할 수 없다.</summary>
        public int ParryLevelHardCap = 5;

        public int AttackPointCost = 1;
        public int DefensePointCost = 1;

        /// <summary>
        /// 판정치 1레벨에 드는 포인트.
        /// 판정 폭은 다른 스탯과 달리 <b>실수 자체를 없애 주기 때문에</b> 같은 값이면 언제나 최선의 선택이 된다.
        /// 폭 증가량을 더 깎으면 눈에 보이는 성장이 사라지므로, 대신 값을 비싸게 매겨 속도를 늦춘다.
        /// </summary>
        public int ParryPointCost = 2;

        /// <summary>공격 커밋 시간 — 이 동안 패링 불가, 끝나는 순간 타격 (리스크/리턴).</summary>
        public float AttackCommitSeconds = 0.4f;

        /// <summary>
        /// 패링 헛침 후딜레이 — 연타로 판정을 도배하는 것을 막는다.
        /// 누른 순간 성공/실패가 결판나므로 이 값이 헛침 벌의 <b>전부</b>다
        /// (예전에는 윈도우를 다 기다린 뒤에 잠금이 시작돼 실질 벌이 더 길었다).
        /// </summary>
        public float ParryWhiffLockSeconds = 0.35f;
    }
}
