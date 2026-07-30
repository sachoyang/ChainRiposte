using System;
using System.Collections.Generic;
using ChainRiposte.Core.Board;

namespace ChainRiposte.Core.Stage.Gimmicks
{
    /// <summary>
    /// 전염되는 타일 (GDD §3.6). 부패 타일 몇 개로 시작해, 턴이 지날 때마다 인접 몬스터 타일을
    /// 하나씩 감염시킨다. 부패 타일은 매치도 스왑도 불가라 방치하면 매칭 공간이 말라붙는다.
    /// 해제법은 <b>부패 타일에 인접한 매치</b> — 그 부패 타일이 함께 파괴된다.
    /// </summary>
    public sealed class SpreadingCorruptionGimmick : StageGimmick
    {
        /// <summary>부패 타일의 종류 정의 (매치 불가 카테고리).</summary>
        public static readonly TileDefinition CorruptionDefinition = new("Corruption", TileCategory.Corruption);

        private int _turnsSinceSpread;

        public override GimmickType Type => GimmickType.SpreadingCorruption;

        public override void OnBoardInitialized(GimmickContext context)
        {
            // 다른 기믹이 걸린 타일은 씨앗에서 뺀다 — 타일 하나에 기믹은 하나뿐이다.
            List<GridPos> monsters = context.Collect(t => t.Category == TileCategory.Monster && !t.Status.HasGimmick);
            int seeds = Math.Min(context.Settings.CorruptionSeeds, monsters.Count);

            for (int i = 0; i < seeds; i++)
            {
                int index = context.Rng.Next(monsters.Count);
                Infect(context, monsters[index], report: false);
                monsters.RemoveAt(index);
            }
        }

        public override void OnMatchesResolving(GimmickContext context, HashSet<GridPos> cleared)
        {
            // 매치에 인접한 부패 타일을 파괴 대상에 추가한다 (부패는 영혼석을 주지 않는다)
            var burned = new List<GridPos>();
            foreach (GridPos pos in context.Collect(t => t.Category == TileCategory.Corruption))
            {
                foreach (GridPos neighbor in context.Board.ActiveNeighbors4(pos))
                {
                    if (!cleared.Contains(neighbor))
                        continue;
                    burned.Add(pos);
                    break;
                }
            }

            foreach (GridPos pos in burned)
            {
                if (!cleared.Add(pos))
                    continue;
                context.Report(new GimmickEvent(
                    GimmickEventType.CorruptionCleared, pos, context.Board.GetTile(pos)));
            }
        }

        public override void OnTurnEnded(GimmickContext context)
        {
            if (++_turnsSinceSpread < Math.Max(1, context.Settings.CorruptionSpreadEveryTurns))
                return;
            _turnsSinceSpread = 0;

            List<GridPos> sources = context.Collect(t => t.Category == TileCategory.Corruption);
            if (sources.Count == 0)
                return;

            int cap = CorruptionCap(context);
            int count = sources.Count;

            // 이번 턴에 새로 감염된 타일은 다음 턴부터 퍼진다 (스냅샷 순회)
            foreach (GridPos source in sources)
            {
                if (count >= cap)
                    break;

                List<GridPos> targets = InfectableNeighbors(context, source);
                if (targets.Count == 0)
                    continue;

                Infect(context, targets[context.Rng.Next(targets.Count)], report: true);
                count++;
            }
        }

        private static List<GridPos> InfectableNeighbors(GimmickContext context, GridPos source)
        {
            var targets = new List<GridPos>();
            foreach (GridPos neighbor in context.Board.ActiveNeighbors4(source))
            {
                Tile tile = context.Board.GetTile(neighbor);
                // 사슬·폭탄·성남이 걸린 놈은 감염시키지 않는다 — 부패로 바뀌면 그 기믹의 해법
                // (매치로 없애기)이 통째로 사라지는데, 화면에는 상태 표시가 남아 거짓말이 된다.
                if (tile != null && tile.Category == TileCategory.Monster && !tile.Status.HasGimmick)
                    targets.Add(neighbor);
            }
            return targets;
        }

        /// <summary>보드 전체가 부패해 아무 수도 둘 수 없게 되는 완전 데드락은 막는다.</summary>
        private static int CorruptionCap(GimmickContext context)
        {
            int activeCells = 0;
            foreach (GridPos _ in context.Board.ActivePositions())
                activeCells++;
            return Math.Max(1, (int)(activeCells * context.Settings.MaxCorruptionRatio));
        }

        private static void Infect(GimmickContext context, GridPos pos, bool report)
        {
            context.Board.RemoveTile(pos);
            var corruption = new Tile(CorruptionDefinition);
            context.Board.PlaceTile(pos, corruption);

            if (!report)
                return;

            context.MarkBoardChanged();
            context.Report(new GimmickEvent(GimmickEventType.CorruptionSpread, pos, corruption));
        }
    }
}
