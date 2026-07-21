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
    }
}
