using System.Collections.Generic;
using System.Linq;
using ChainRiposte.Core.Board;

namespace ChainRiposte.Core.Match
{
    /// <summary>보드 전체에서 가로/세로 3개 이상 매치를 찾는다.</summary>
    public static class MatchFinder
    {
        /// <summary>매치에 참여할 수 있는 카테고리인가 (벽·보스·부패는 불가).</summary>
        public static bool IsMatchable(Tile tile) => tile != null && IsMatchable(tile.Definition);

        /// <summary>
        /// 타일을 만들기 <b>전에</b> 같은 것을 묻는다 — 스포너가 내놓은 종류를 걸러낼 때 쓴다.
        /// 규칙을 두 곳에 적지 않으려고 위 판정이 이것을 거쳐 간다.
        /// </summary>
        public static bool IsMatchable(TileDefinition definition) =>
            definition != null &&
            (definition.Category == TileCategory.Monster || definition.Category == TileCategory.Potion);

        public static IReadOnlyList<MatchGroup> FindAll(BoardGrid board)
        {
            var runs = new List<(TileDefinition def, List<GridPos> positions)>();

            // 가로 런
            for (int y = 0; y < board.Height; y++)
                CollectRuns(board, runs, start: new GridPos(0, y), dx: 1, dy: 0, length: board.Width);

            // 세로 런
            for (int x = 0; x < board.Width; x++)
                CollectRuns(board, runs, start: new GridPos(x, 0), dx: 0, dy: 1, length: board.Height);

            return MergeOverlapping(runs);
        }

        private static void CollectRuns(
            BoardGrid board,
            List<(TileDefinition, List<GridPos>)> runs,
            GridPos start, int dx, int dy, int length)
        {
            TileDefinition runDef = null;
            var run = new List<GridPos>();

            for (int i = 0; i <= length; i++) // length 지점은 런 마감용 경계
            {
                var pos = new GridPos(start.X + dx * i, start.Y + dy * i);
                Tile tile = i < length ? board.GetTile(pos) : null;
                TileDefinition def = IsMatchable(tile) ? tile.Definition : null;

                if (def != null && def == runDef)
                {
                    run.Add(pos);
                    continue;
                }

                if (runDef != null && run.Count >= 3)
                    runs.Add((runDef, run));

                runDef = def;
                run = def != null ? new List<GridPos> { pos } : new List<GridPos>();
            }
        }

        /// <summary>같은 종류의 런들이 좌표를 공유하면(L/T자) 하나의 그룹으로 합친다.</summary>
        private static IReadOnlyList<MatchGroup> MergeOverlapping(
            List<(TileDefinition def, List<GridPos> positions)> runs)
        {
            var groups = new List<(TileDefinition def, HashSet<GridPos> positions)>();

            foreach ((TileDefinition def, List<GridPos> positions) run in runs)
            {
                var overlapping = groups
                    .Where(g => g.def == run.def && g.positions.Overlaps(run.positions))
                    .ToList();

                if (overlapping.Count == 0)
                {
                    groups.Add((run.def, new HashSet<GridPos>(run.positions)));
                    continue;
                }

                // 첫 그룹에 런과 나머지 겹친 그룹들을 전부 흡수
                (TileDefinition def, HashSet<GridPos> positions) target = overlapping[0];
                target.positions.UnionWith(run.positions);
                for (int i = 1; i < overlapping.Count; i++)
                {
                    target.positions.UnionWith(overlapping[i].positions);
                    groups.Remove(overlapping[i]);
                }
            }

            return groups
                .Select(g => new MatchGroup(g.def, g.positions.ToList()))
                .ToList();
        }
    }
}
