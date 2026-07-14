using System.Collections.Generic;
using ChainRiposte.Core.Board;

namespace ChainRiposte.Core.Match
{
    /// <summary>
    /// 중력 압축 후 각 열의 상단 빈 칸을 새 타일로 채운다.
    /// 벽 아래 구간은 위에서 타일이 진입할 수 없으므로 리필되지 않는다 (의도된 기믹).
    /// </summary>
    public static class BoardRefiller
    {
        /// <summary>보드를 변형하고 생성된 타일 목록을 반환한다. 반드시 Collapse 이후에 호출한다.</summary>
        public static IReadOnlyList<TileSpawn> Refill(BoardGrid board, ITileSpawner spawner)
        {
            var spawns = new List<TileSpawn>();

            for (int x = 0; x < board.Width; x++)
            {
                // 위에서 아래로 훑으며 벽을 만나면 그 아래는 진입 불가
                for (int y = board.Height - 1; y >= 0; y--)
                {
                    var pos = new GridPos(x, y);
                    if (!board.IsActive(pos))
                        continue;

                    Tile existing = board.GetTile(pos);
                    if (existing != null)
                    {
                        if (existing.Category == TileCategory.Wall)
                            break;
                        continue;
                    }

                    var tile = new Tile(spawner.NextDefinition());
                    board.PlaceTile(pos, tile);
                    spawns.Add(new TileSpawn(tile, pos));
                }
            }

            return spawns;
        }
    }
}
