namespace ChainRiposte.Core.Stats
{
    /// <summary>
    /// <see cref="PlayerStats"/>의 <b>가변 상태</b>만 떼어낸 값 꾸러미. 스테이지를 넘어 성장을 이어가려면
    /// (성장 캐리 — <c>Docs/PROGRESSION.md</c>) 이 스냅샷을 저장했다가 다음 판의 PlayerStats에 씨앗으로 넣는다.
    ///
    /// <para>PlayerStats는 밸런스 <see cref="PlayerStatsConfig"/>(불변)와 이 스냅샷(가변)을 합쳐 만들어진다 —
    /// 밸런스를 인스펙터에서 고쳐도 진행 중인 런의 누적치는 스냅샷에 남아 그대로 이어진다.</para>
    ///
    /// <para><b>담지 않는 것: <see cref="PlayerStats.TotalSoulsEarned"/></b>. 그것은 "이 판에서 번 소울"로
    /// 보스가 조급하게 난입하는 <b>판 단위 압박 게이지</b>라서 이월하면 안 된다 — 이월하면 다음 판이
    /// 시작부터 난입 확률 최대가 되어 보드가 보스 타일로 도배된다(실제로 그 버그가 났었다).
    /// 이월되는 것은 성장(레벨·소울 은행·포인트·스탯 레벨)뿐이다.</para>
    /// </summary>
    public sealed class PlayerStatsSnapshot
    {
        /// <summary>누적 레벨 (1부터).</summary>
        public int Level = 1;

        /// <summary>다음 레벨까지 쌓인 영혼석(부분 진행).</summary>
        public int Souls;

        /// <summary>아직 분배하지 않은 스탯 포인트.</summary>
        public int PendingPoints;

        /// <summary>StatType 인덱스별 스탯 레벨 (ATK/DEF/Parry).</summary>
        public readonly int[] StatLevels = new int[3];
    }
}
