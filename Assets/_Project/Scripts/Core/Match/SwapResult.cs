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

        /// <summary>턴 종료 후 기믹이 일으킨 일 (GDD §3.6). 기믹이 없으면 <see cref="GimmickPhase.Empty"/>.</summary>
        public GimmickPhase Gimmicks { get; }

        /// <summary>
        /// 턴이 끝난 뒤 둘 수 있는 수가 없어 보드를 섞었다면 그 이동 기록. 아니면 비어 있다.
        /// 리롤은 턴을 소모하지 않는다 — 플레이어 잘못이 아니기 때문.
        /// </summary>
        public IReadOnlyList<TileMove> ShuffleMoves { get; }

        public bool Shuffled => ShuffleMoves.Count > 0;

        public int TotalSouls { get; }
        public int TotalPotions { get; }

        /// <summary>최종 콤보 수 (= 연쇄 단계 수).</summary>
        public int ComboCount => Steps.Count;

        private SwapResult(
            bool success, GridPos a, GridPos b, IReadOnlyList<CascadeStep> steps,
            GimmickPhase gimmicks, IReadOnlyList<TileMove> shuffleMoves)
        {
            Success = success;
            A = a;
            B = b;
            Steps = steps;
            Gimmicks = gimmicks ?? GimmickPhase.Empty;
            ShuffleMoves = shuffleMoves ?? Array.Empty<TileMove>();

            foreach (CascadeStep step in steps)
            {
                TotalSouls += step.SoulsEarned;
                TotalPotions += step.PotionCount;
            }

            // 기믹이 만든 연쇄(폭발로 무너진 자리에서 터진 매치)도 같은 보상으로 친다
            foreach (CascadeStep step in Gimmicks.Cascades)
            {
                TotalSouls += step.SoulsEarned;
                TotalPotions += step.PotionCount;
            }
        }

        public static SwapResult Failed(GridPos a, GridPos b) =>
            new(false, a, b, Array.Empty<CascadeStep>(), GimmickPhase.Empty, Array.Empty<TileMove>());

        public static SwapResult Resolved(
            GridPos a, GridPos b, IReadOnlyList<CascadeStep> steps,
            GimmickPhase gimmicks = null, IReadOnlyList<TileMove> shuffleMoves = null) =>
            new(true, a, b, steps, gimmicks, shuffleMoves);
    }
}
