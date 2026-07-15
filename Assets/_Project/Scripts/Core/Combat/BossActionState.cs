namespace ChainRiposte.Core.Combat
{
    /// <summary>보스의 현재 행동 상태 — 전투 뷰가 텔레그래프/피격 연출을 고르는 기준.</summary>
    public enum BossActionState
    {
        /// <summary>다음 공격 대기 중 (전투 시작 직후 또는 공격 후딜레이).</summary>
        Recovering = 0,

        /// <summary>텔레그래프 재생 중 — 끝나는 순간 타격이 들어온다.</summary>
        Telegraphing,

        /// <summary>체간 파괴 — 공격이 멈추고 인살 입력을 기다린다.</summary>
        Broken,
    }
}
