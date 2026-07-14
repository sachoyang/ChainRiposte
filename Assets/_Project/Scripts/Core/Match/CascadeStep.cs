using System.Collections.Generic;

namespace ChainRiposte.Core.Match
{
    /// <summary>
    /// 스왑 한 번이 일으킨 연쇄(캐스케이드)의 한 단계.
    /// 뷰는 매치 파괴 → 낙하 웨이브(FallPhases) 순으로 재생해 애니메이션을 구성한다.
    /// </summary>
    public sealed class CascadeStep
    {
        /// <summary>1부터 시작하는 콤보 순번.</summary>
        public int ComboIndex { get; }

        public IReadOnlyList<MatchGroup> Matches { get; }
        public IReadOnlyList<WallHit> WallHits { get; }

        /// <summary>콤보 배수가 적용된 이 단계의 영혼석 획득량.</summary>
        public int SoulsEarned { get; }

        /// <summary>이 단계에서 매치된 물약 타일 수 (HP 회복 훅).</summary>
        public int PotionCount { get; }

        /// <summary>직선 낙하/대각선 슬라이드/리필이 안정될 때까지의 웨이브 기록.</summary>
        public IReadOnlyList<FallPhase> FallPhases { get; }

        public CascadeStep(
            int comboIndex,
            IReadOnlyList<MatchGroup> matches,
            IReadOnlyList<WallHit> wallHits,
            int soulsEarned,
            int potionCount,
            IReadOnlyList<FallPhase> fallPhases)
        {
            ComboIndex = comboIndex;
            Matches = matches;
            WallHits = wallHits;
            SoulsEarned = soulsEarned;
            PotionCount = potionCount;
            FallPhases = fallPhases;
        }
    }
}
