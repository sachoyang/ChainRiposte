using System.Collections.Generic;
using ChainRiposte.Core.Board;

namespace ChainRiposte.Core.Match
{
    /// <summary>
    /// 매치 파괴 후 보드를 안정 상태까지 정착시킨다.
    /// 규칙:
    ///  1) 직선 낙하 — 타일은 열 안에서 비활성(구멍) 셀을 통과해 떨어지고, 벽은 고정이며 낙하를 막는다.
    ///  2) 대각선 슬라이드 — ①위에서 리필이 도달할 수 없는 칸(벽 아래)은 대각선 위 타일이 미끄러져 채우고,
    ///     ②벽에 얹혀 직선 낙하가 막힌 타일(보스 포함)은 대각선 아래 빈 칸으로 미끄러져 내려간다.
    ///  3) 리필 — 각 열 최상단의 연속된 빈 칸만 새 타일로 채운다 (기둥 중간 빈 칸은 다음 웨이브 낙하 몫).
    /// 1~3을 웨이브(FallPhase)로 반복해 빈 칸이 없어질 때까지 정착시킨다.
    /// 결과: 직접 배치한 벽과 비활성(X) 셀 이외의 모든 칸이 항상 채워진다.
    /// </summary>
    public static class GravityResolver
    {
        private const int MaxPhases = 200; // 안전 상한

        /// <summary>보드를 변형하며 웨이브별 이동/스폰 기록을 반환한다.</summary>
        public static IReadOnlyList<FallPhase> Settle(BoardGrid board, ITileSpawner spawner)
        {
            var phases = new List<FallPhase>();

            while (phases.Count < MaxPhases)
            {
                var moves = new List<TileMove>();
                moves.AddRange(Collapse(board));
                moves.AddRange(SlideDiagonalOnce(board));
                IReadOnlyList<TileSpawn> spawns = BoardRefiller.Refill(board, spawner);

                if (moves.Count == 0 && spawns.Count == 0)
                    break;

                phases.Add(new FallPhase(moves, spawns));
            }

            return phases;
        }

        /// <summary>직선 낙하 압축. 타일은 구멍을 통과하고 벽 위에 쌓인다.</summary>
        public static IReadOnlyList<TileMove> Collapse(BoardGrid board)
        {
            var moves = new List<TileMove>();
            var slots = new List<GridPos>();

            for (int x = 0; x < board.Width; x++)
            {
                // 이 열의 활성 셀을 아래에서 위로 수집 (비활성 구멍은 제외 = 통과 낙하)
                slots.Clear();
                for (int y = 0; y < board.Height; y++)
                {
                    var pos = new GridPos(x, y);
                    if (board.IsActive(pos))
                        slots.Add(pos);
                }

                int writeIndex = 0;
                for (int readIndex = 0; readIndex < slots.Count; readIndex++)
                {
                    Tile tile = board.GetTile(slots[readIndex]);
                    if (tile == null)
                        continue;

                    if (tile.Category == TileCategory.Wall)
                    {
                        // 벽은 고정. 벽 위의 타일은 벽 아래로 내려올 수 없다.
                        writeIndex = readIndex + 1;
                        continue;
                    }

                    if (readIndex != writeIndex)
                    {
                        board.RemoveTile(slots[readIndex]);
                        board.PlaceTile(slots[writeIndex], tile);
                        moves.Add(new TileMove(tile, slots[readIndex], slots[writeIndex]));
                    }

                    writeIndex++;
                }
            }

            return moves;
        }

        /// <summary>
        /// 대각선 슬라이드 한 웨이브. 반드시 Collapse 직후에 호출.
        /// ①벽 그늘 칸(위에서 채울 수 없음)은 대각선 위 아무 타일이나 끌어내리고,
        /// ②그 외 빈 칸은 벽에 얹혀 오도 가도 못하는 타일만 끌어내린다 (리필 몫을 빼앗지 않기 위함).
        /// </summary>
        private static List<TileMove> SlideDiagonalOnce(BoardGrid board)
        {
            var moves = new List<TileMove>();

            // 아래 행부터 훑어 깊은 칸이 먼저 채워지게 한다
            foreach (GridPos empty in board.ActivePositions())
            {
                if (board.IsOccupied(empty))
                    continue;

                TrySlideInto(board, empty, IsBlockedFromAbove(board, empty), moves);
            }

            return moves;
        }

        private static void TrySlideInto(BoardGrid board, GridPos empty, bool intoShadow, List<TileMove> moves)
        {
            // 좌상단 우선 — 결정적 동작 (밸런스상 좌우 무작위가 필요해지면 이곳만 수정)
            foreach (int dx in new[] { -1, +1 })
            {
                var source = new GridPos(empty.X + dx, empty.Y + 1);
                Tile tile = board.GetTile(source);
                if (tile == null || tile.Category == TileCategory.Wall)
                    continue;

                // 그늘 칸이 아니면 위에서 리필/낙하로 채워지므로,
                // 벽에 얹혀 직선 낙하가 영영 불가능한 타일만 미끄러뜨린다 (보스 하강 보장)
                if (!intoShadow && !IsRestingOnWall(board, source))
                    continue;

                board.RemoveTile(source);
                board.PlaceTile(empty, tile);
                moves.Add(new TileMove(tile, source, empty));
                return;
            }
        }

        /// <summary>이 타일이 벽 바로 위에 얹혀 있는가 (구멍은 통과하므로 건너뛰고 첫 활성 셀 기준).</summary>
        private static bool IsRestingOnWall(BoardGrid board, GridPos pos)
        {
            for (int y = pos.Y - 1; y >= 0; y--)
            {
                var below = new GridPos(pos.X, y);
                if (!board.IsActive(below))
                    continue;

                Tile tile = board.GetTile(below);
                return tile != null && tile.Category == TileCategory.Wall;
            }

            return false;
        }

        /// <summary>이 칸의 열 위쪽에 벽이 있어 리필/직선 낙하가 도달할 수 없는가.</summary>
        private static bool IsBlockedFromAbove(BoardGrid board, GridPos pos)
        {
            for (int y = pos.Y + 1; y < board.Height; y++)
            {
                var above = new GridPos(pos.X, y);
                if (!board.IsActive(above))
                    continue;

                Tile tile = board.GetTile(above);
                if (tile != null && tile.Category == TileCategory.Wall)
                    return true;
            }

            return false;
        }
    }
}
