using System;
using ChainRiposte.Core.Board;

namespace ChainRiposte.Core.Match
{
    /// <summary>
    /// "둘 수 있는 수가 남아 있는가"를 판정한다 — 데드락 검출과 보드 리롤의 기준.
    /// 인접 쌍을 실제로 스왑해보고 되돌리는 전수 조사이며, 보드를 변형한 채로 두지 않는다.
    /// </summary>
    public static class MoveFinder
    {
        /// <summary>
        /// 스왑에 참여할 수 있는 타일인가. 벽·보스·부패는 매치 대상이 아니고,
        /// 사슬에 결박된 타일은 그 자리에 고정된다 (GDD §3.6).
        /// PuzzleEngine의 실제 스왑 판정과 이 한 곳을 공유해야 '둘 수 있다고 했는데 못 두는' 유령 수가 안 생긴다.
        /// </summary>
        public static bool IsSwappable(Tile tile) => MatchFinder.IsMatchable(tile) && !tile.Status.Chained;

        public static bool AreSwappable(BoardGrid board, GridPos a, GridPos b)
        {
            bool adjacent = Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) == 1;
            return adjacent && IsSwappable(board.GetTile(a)) && IsSwappable(board.GetTile(b));
        }

        /// <summary>
        /// 매치를 만드는 인접 스왑이 하나라도 있는가. 없으면 데드락 — 턴이 영영 안 넘어간다.
        /// 정착이 끝나 매치가 남아 있지 않은 보드를 전제로 한다(엔진이 연쇄를 다 턴 뒤 상태).
        /// </summary>
        public static bool HasAnyValidMove(BoardGrid board) => TryFindMove(board, out _, out _);

        /// <summary>매치를 만드는 인접 스왑을 하나 찾는다 (아래·왼쪽 우선의 결정적 탐색). 힌트 표시에도 쓸 수 있다.</summary>
        public static bool TryFindMove(BoardGrid board, out GridPos a, out GridPos b)
        {
            // 각 쌍을 한 번만 보면 되므로 오른쪽·위 두 방향만 검사한다
            foreach (GridPos pos in board.ActivePositions())
            {
                if (CreatesMatch(board, pos, pos.Right))
                {
                    (a, b) = (pos, pos.Right);
                    return true;
                }

                if (CreatesMatch(board, pos, pos.Up))
                {
                    (a, b) = (pos, pos.Up);
                    return true;
                }
            }

            a = default;
            b = default;
            return false;
        }

        private static bool CreatesMatch(BoardGrid board, GridPos a, GridPos b)
        {
            if (!AreSwappable(board, a, b))
                return false;

            Swap(board, a, b);
            // 스왑 전 보드에 매치가 없으므로, 새 매치는 반드시 바뀐 두 칸 중 하나를 지난다 → 지역 검사로 충분
            bool matched = HasRunThrough(board, a) || HasRunThrough(board, b);
            Swap(board, a, b);

            return matched;
        }

        private static void Swap(BoardGrid board, GridPos a, GridPos b)
        {
            Tile tileA = board.RemoveTile(a);
            Tile tileB = board.RemoveTile(b);
            board.PlaceTile(a, tileB);
            board.PlaceTile(b, tileA);
        }

        /// <summary>이 칸을 지나는 3개 이상의 가로/세로 런이 있는가.</summary>
        private static bool HasRunThrough(BoardGrid board, GridPos pos)
        {
            Tile tile = board.GetTile(pos);
            if (!MatchFinder.IsMatchable(tile))
                return false;

            TileDefinition def = tile.Definition;
            return 1 + CountSame(board, pos, -1, 0, def) + CountSame(board, pos, +1, 0, def) >= 3
                || 1 + CountSame(board, pos, 0, -1, def) + CountSame(board, pos, 0, +1, def) >= 3;
        }

        /// <summary>한 방향으로 같은 종류가 몇 칸 이어지는가. 구멍·벽·보드 밖에서 끊긴다(MatchFinder와 동일 규칙).</summary>
        private static int CountSame(BoardGrid board, GridPos from, int dx, int dy, TileDefinition def)
        {
            int count = 0;

            for (var pos = new GridPos(from.X + dx, from.Y + dy); ; pos = new GridPos(pos.X + dx, pos.Y + dy))
            {
                Tile tile = board.GetTile(pos);
                if (!MatchFinder.IsMatchable(tile) || tile.Definition != def)
                    return count;

                count++;
            }
        }
    }
}
