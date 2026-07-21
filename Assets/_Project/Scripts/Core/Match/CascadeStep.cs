using System.Collections.Generic;
using ChainRiposte.Core.Board;
using ChainRiposte.Core.Stage.Gimmicks;

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

        /// <summary>
        /// 실제로 보드에서 사라진 칸. 기믹이 개입하면 매치 좌표와 달라진다 —
        /// 사슬 타일은 살아남아 빠지고, 인접한 부패 타일은 추가로 들어온다.
        /// <b>뷰는 이 목록으로 파괴 연출을 재생한다.</b>
        /// </summary>
        public IReadOnlyList<GridPos> ClearedPositions { get; }

        public IReadOnlyList<WallHit> WallHits { get; }

        /// <summary>이 단계에서 기믹이 일으킨 사건 (사슬 해제, 부패 소각 등).</summary>
        public IReadOnlyList<GimmickEvent> GimmickEvents { get; }

        /// <summary>콤보 배수가 적용된 이 단계의 영혼석 획득량.</summary>
        public int SoulsEarned { get; }

        /// <summary>이 단계에서 매치된 물약 타일 수 (HP 회복 훅).</summary>
        public int PotionCount { get; }

        /// <summary>직선 낙하/대각선 슬라이드/리필이 안정될 때까지의 웨이브 기록.</summary>
        public IReadOnlyList<FallPhase> FallPhases { get; }

        public CascadeStep(
            int comboIndex,
            IReadOnlyList<MatchGroup> matches,
            IReadOnlyList<GridPos> clearedPositions,
            IReadOnlyList<WallHit> wallHits,
            IReadOnlyList<GimmickEvent> gimmickEvents,
            int soulsEarned,
            int potionCount,
            IReadOnlyList<FallPhase> fallPhases)
        {
            ComboIndex = comboIndex;
            Matches = matches;
            ClearedPositions = clearedPositions;
            WallHits = wallHits;
            GimmickEvents = gimmickEvents;
            SoulsEarned = soulsEarned;
            PotionCount = potionCount;
            FallPhases = fallPhases;
        }
    }
}
