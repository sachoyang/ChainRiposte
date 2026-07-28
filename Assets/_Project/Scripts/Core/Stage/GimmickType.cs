namespace ChainRiposte.Core.Stage
{
    /// <summary>
    /// 스테이지 기믹 종류 (GDD §3.6). StageData의 목록에 담긴 것만 해당 스테이지에서 활성화된다.
    /// 실제 기믹 로직(IStageGimmick 모듈)은 확장 단계에서 구현 예정.
    /// </summary>
    public enum GimmickType
    {
        /// <summary>전염되는 타일 — 부패 타일이 인접 매칭 없이 턴이 지나면 주변을 감염시킨다.</summary>
        SpreadingCorruption = 0,

        /// <summary>시한폭탄 몬스터 — 턴 카운트 내 처치 실패 시 플레이어 HP에 직접 피해.</summary>
        TickingDeath = 1,

        /// <summary>사슬 결박 — 타일이 스왑/낙하 불가로 고정. 매치 또는 인접 매칭으로 해제.</summary>
        LockedTiles = 2,

        /// <summary>
        /// 성난 몬스터 — 보드의 잡몹이 카운트다운 후 플레이어를 때린다. 매치로 없애면 취소.
        /// <b>다른 셋과 달리 상시 규칙이다</b> — 스테이지 목록에 없어도 항상 켜지고,
        /// 세기는 <see cref="GimmickSettings.EnrageChance"/>로 조절한다(0이면 사실상 꺼짐).
        /// </summary>
        EnragedMonsters = 3,
    }
}
