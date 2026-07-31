namespace ChainRiposte.Core.Board
{
    /// <summary>타일의 시스템 동작 분류. 종류별 세부 데이터는 TileDefinition이 갖는다.</summary>
    public enum TileCategory
    {
        /// <summary>일반 몬스터 — 3매치로 처치 시 영혼석 드랍.</summary>
        Monster = 0,

        /// <summary>물약 — 매치 시 HP 즉시 회복.</summary>
        Potion = 1,

        /// <summary>벽 — 매치·스왑·낙하 불가. 인접 매치로 내구도를 깎아 파괴.</summary>
        Wall = 2,

        /// <summary>보스 타일 — 매치 불가, 듀얼 카운트다운 보유. 바닥 도달 시 전투 돌입.</summary>
        Boss = 3,

        /// <summary>부패 타일 (GDD §3.6 전염) — 매치·스왑 불가, 낙하는 한다. 인접 매치로 제거.</summary>
        Corruption = 4,

        /// <summary>
        /// 시한폭탄 타일 (GDD §3.6) — 부패와 같은 규칙(매치·스왑 불가, 낙하는 함)에
        /// <b>턴 카운트</b>가 붙은 것. 인접 매치로 해체하고, 0이 되면 터져 플레이어 HP를 깎는다.
        ///
        /// <para>예전에는 몬스터 타일에 붙는 <b>상태</b>였다. 그때는 「그 몬스터를 매치로 없애면 해체」라
        /// 폭탄이 붙은 색을 세 개 모아야 했고, 그 색이 보드에 없으면 손쓸 방법이 없었다.
        /// 타일로 떼어 내면 해법이 <b>어느 색이든 옆에서 매치</b> 하나로 정리된다.</para>
        /// </summary>
        Bomb = 5,
    }
}
