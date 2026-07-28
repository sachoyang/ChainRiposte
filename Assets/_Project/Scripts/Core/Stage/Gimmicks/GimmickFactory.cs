using System;
using System.Collections.Generic;

namespace ChainRiposte.Core.Stage.Gimmicks
{
    /// <summary>StageConfig의 기믹 목록을 실제 모듈로 조립한다 (조합 가능, 중복은 무시).</summary>
    public static class GimmickFactory
    {
        /// <summary>
        /// 스테이지 목록과 무관하게 <b>항상</b> 켜지는 기믹 — 퍼즐의 기본 난이도를 담당한다.
        /// 목록에 적어야 켜지는 방식이면 스테이지를 새로 만들 때마다 빠뜨려 퍼즐이 조용히 물렁해진다.
        /// 끄고 싶으면 목록이 아니라 <see cref="GimmickSettings.EnrageChance"/>를 0으로 둔다.
        /// </summary>
        private static readonly GimmickType[] AlwaysOn = { GimmickType.EnragedMonsters };

        public static IReadOnlyList<IStageGimmick> CreateAll(IReadOnlyList<GimmickType> types)
        {
            var created = new List<IStageGimmick>();
            var seen = new HashSet<GimmickType>();

            foreach (GimmickType type in AlwaysOn)
                if (seen.Add(type))
                    created.Add(Create(type));

            foreach (GimmickType type in types ?? Array.Empty<GimmickType>())
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
            GimmickType.EnragedMonsters => new EnragedMonstersGimmick(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "알 수 없는 기믹 종류"),
        };
    }
}
