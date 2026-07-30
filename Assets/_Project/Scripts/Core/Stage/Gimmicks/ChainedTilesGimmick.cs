using System;
using System.Collections.Generic;
using ChainRiposte.Core.Board;
using ChainRiposte.Core.Match;

namespace ChainRiposte.Core.Stage.Gimmicks
{
    /// <summary>
    /// 사슬 결박 (GDD §3.6). 몬스터 타일에 쇠사슬이 감겨 <b>스왑·낙하 불가</b>로 고정된다.
    /// 벽과 달리 매치는 가능하며, 결박 타일이 포함된 매치나 인접 매치는
    /// 타일을 파괴하는 대신 <b>사슬만 푼다</b> (해제되면 평범한 타일로 복귀).
    /// </summary>
    public sealed class ChainedTilesGimmick : StageGimmick
    {
        public override GimmickType Type => GimmickType.LockedTiles;

        public override void OnBoardInitialized(GimmickContext context)
        {
            // 이미 다른 기믹이 걸린 타일은 후보에서 뺀다 — 타일 하나에 기믹은 하나뿐이다.
            List<GridPos> monsters = context.Collect(t => t.Category == TileCategory.Monster && !t.Status.HasGimmick);
            int count = Math.Min(context.Settings.ChainInitialCount, monsters.Count);

            for (int i = 0; i < count; i++)
            {
                int index = context.Rng.Next(monsters.Count);
                context.Board.GetTile(monsters[index]).Status.Chained = true;
                monsters.RemoveAt(index);
            }
        }

        public override void OnTilesSpawned(GimmickContext context, IReadOnlyList<TileSpawn> spawns)
        {
            foreach (TileSpawn spawn in spawns)
            {
                if (spawn.Tile.Category != TileCategory.Monster || spawn.Tile.Status.HasGimmick)
                    continue;
                if (context.Rng.NextDouble() >= context.Settings.ChainChance)
                    continue;

                spawn.Tile.Status.Chained = true;
            }
        }

        public override void OnMatchesResolving(GimmickContext context, HashSet<GridPos> cleared)
        {
            // ① 매치에 걸린 결박 타일은 파괴 대신 사슬 해제 — 파괴 목록에서 뺀다
            var inMatch = new List<GridPos>();
            foreach (GridPos pos in cleared)
            {
                Tile tile = context.Board.GetTile(pos);
                if (tile != null && tile.Status.Chained)
                    inMatch.Add(pos);
            }

            foreach (GridPos pos in inMatch)
            {
                cleared.Remove(pos);
                Unchain(context, pos);
            }

            // ② 인접 매치도 사슬을 푼다 (①에서 살아남은 칸은 이미 해제됨)
            foreach (GridPos pos in context.Collect(t => t.Status.Chained))
            {
                foreach (GridPos neighbor in context.Board.ActiveNeighbors4(pos))
                {
                    if (!cleared.Contains(neighbor))
                        continue;
                    Unchain(context, pos);
                    break;
                }
            }
        }

        private static void Unchain(GimmickContext context, GridPos pos)
        {
            Tile tile = context.Board.GetTile(pos);
            if (tile == null || !tile.Status.Chained)
                return;

            tile.Status.Chained = false;
            // 사슬이 풀리면 그 자리에서 다시 떨어질 수 있게 된다
            context.MarkBoardChanged();
            context.Report(new GimmickEvent(GimmickEventType.ChainBroken, pos, tile));
        }
    }
}
