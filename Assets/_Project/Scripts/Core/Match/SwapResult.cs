using System;
using System.Collections.Generic;
using ChainRiposte.Core.Board;

namespace ChainRiposte.Core.Match
{
    /// <summary>스왑 시도의 전체 결과. 실패(매치 없음)면 보드는 원상 복구되고 턴을 소모하지 않는다.</summary>
    public sealed class SwapResult
    {
        public bool Success { get; }
        public GridPos A { get; }
        public GridPos B { get; }

        /// <summary>연쇄 단계 목록. 실패 시 비어 있다.</summary>
        public IReadOnlyList<CascadeStep> Steps { get; }

        public int TotalSouls { get; }
        public int TotalPotions { get; }

        /// <summary>최종 콤보 수 (= 연쇄 단계 수).</summary>
        public int ComboCount => Steps.Count;

        private SwapResult(bool success, GridPos a, GridPos b, IReadOnlyList<CascadeStep> steps)
        {
            Success = success;
            A = a;
            B = b;
            Steps = steps;

            foreach (CascadeStep step in steps)
            {
                TotalSouls += step.SoulsEarned;
                TotalPotions += step.PotionCount;
            }
        }

        public static SwapResult Failed(GridPos a, GridPos b) =>
            new(false, a, b, Array.Empty<CascadeStep>());

        public static SwapResult Resolved(GridPos a, GridPos b, IReadOnlyList<CascadeStep> steps) =>
            new(true, a, b, steps);
    }
}
