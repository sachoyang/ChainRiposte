namespace ChainRiposte.Core.Combat
{
    /// <summary>플레이어의 현재 행동 상태. 2버튼 입력의 유효성이 이 상태로 결정된다.</summary>
    public enum PlayerActionState
    {
        /// <summary>입력 대기 — 패링/공격 모두 가능.</summary>
        Ready = 0,

        /// <summary>패링 판정 활성 — 탭 순간부터 판정치(초) 동안 유지.</summary>
        Parrying,

        /// <summary>패링 헛침 후딜레이 — 연타 방지. 이 동안 재입력 불가.</summary>
        ParryRecovering,

        /// <summary>공격 커밋 중 — 끝나는 순간 타격. 이 동안 패링 불가 (리스크/리턴).</summary>
        Attacking,
    }
}
