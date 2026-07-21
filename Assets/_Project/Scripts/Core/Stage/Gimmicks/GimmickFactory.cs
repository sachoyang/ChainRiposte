using System;
using System.Collections.Generic;

namespace ChainRiposte.Core.Stage.Gimmicks
{
    /// <summary>StageConfig의 기믹 목록을 실제 모듈로 조립한다 (조합 가능, 중복은 무시).</summary>
    public static class GimmickFactory
    {
        public static IReadOnlyList<IStageGimmick> CreateAll(IReadOnlyList<GimmickType> types)
        {
            if (types == null || types.Count == 0)
                return Array.Empty<IStageGimmick>();

            var created = new List<IStageGimmick>(types.Count);
            var seen = new HashSet<GimmickType>();

            foreach (GimmickType type in types)
            {
                if (!seen.Add(type))
                    continue;
                created.Add(Create(type));
            }

            return created;
        }

        public static IStageGimmick Create(GimmickType type) => type switch
        {
            GimmickType.SpreadingCorruption => new SpreadingCorruptionGimmick(),
            GimmickType.TickingDeath => new TickingDeathGimmick(),
            GimmickType.LockedTiles => new ChainedTilesGimmick(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "알 수 없는 기믹 종류"),
        };
    }
}
