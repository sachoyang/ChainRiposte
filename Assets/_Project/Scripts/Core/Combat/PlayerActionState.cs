namespace ChainRiposte.Core.Combat
{
    /// <summary>플레이어의 현재 행동 상태. 2버튼 입력의 유효성이 이 상태로 결정된다.</summary>
    public enum PlayerActionState
    {
        /// <summary>입력 대기 — 패링/공격 모두 가능.</summary>
        Ready = 0,

        /// <summary>
        /// 패링 헛침 후딜레이 — 연타 방지. 이 동안 재입력 불가.
        /// <para>
        /// 예전에는 '누르고 있는 동안 판정이 열린 상태'(Parrying)가 따로 있었지만,
        /// 지금은 <b>누른 순간 성공/실패가 결판난다</b>. 그래서 남은 것은 실패했을 때의 잠금뿐이다.
        /// </para>
        /// </summary>
        ParryRecovering = 2,

        /// <summary>공격 커밋 중 — 끝나는 순간 타격. 이 동안 패링 불가 (리스크/리턴).</summary>
        Attacking,
    }
}
