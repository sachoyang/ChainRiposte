using System;
using System.Collections.Generic;
using ChainRiposte.Core.Stage.Gimmicks;

namespace ChainRiposte.Core.Match
{
    /// <summary>
    /// 턴이 소모된 뒤 기믹들이 일으킨 일 (GDD §3.6). 뷰는 이벤트 → 낙하 → 연쇄 순으로 재생하고,
    /// 상위 레이어는 PlayerDamage를 플레이어 HP에 반영한다.
    /// </summary>
    public sealed class GimmickPhase
    {
        public static readonly GimmickPhase Empty = new(
            Array.Empty<GimmickEvent>(), 0, Array.Empty<FallPhase>(), Array.Empty<CascadeStep>());

        /// <summary>확산/폭발/카운트다운 등 이 턴에 일어난 사건 (발생 순서).</summary>
        public IReadOnlyList<GimmickEvent> Events { get; }

        /// <summary>기믹이 준 플레이어 HP 피해 합계 (폭발 등).</summary>
        public int PlayerDamage { get; }

        /// <summary>기믹이 만든 빈 칸을 메우는 낙하 웨이브.</summary>
        public IReadOnlyList<FallPhase> FallPhases { get; }

        /// <summary>그 낙하로 새로 성립한 매치들의 연쇄 (폭발이 만든 공짜 콤보 등).</summary>
        public IReadOnlyList<CascadeStep> Cascades { get; }

        public GimmickPhase(
            IReadOnlyList<GimmickEvent> events,
            int playerDamage,
            IReadOnlyList<FallPhase> fallPhases,
            IReadOnlyList<CascadeStep> cascades)
        {
            Events = events;
            PlayerDamage = playerDamage;
            FallPhases = fallPhases;
            Cascades = cascades;
        }

        public bool IsEmpty => Events.Count == 0 && FallPhases.Count == 0 && Cascades.Count == 0;
    }
}
