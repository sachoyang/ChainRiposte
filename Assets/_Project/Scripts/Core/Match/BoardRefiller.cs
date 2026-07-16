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

                    // 첫 타일(벽 포함)에서 중단 — 기둥 최상단의 연속된 빈 칸만 리필한다.
                    // 기둥 중간의 빈 칸(슬라이드가 만든 구멍)은 다음 웨이브의 낙하가 채워야
                    // 위에 있던 타일(보스 포함)이 실제로 내려온다. 중간 스폰은 낙하를 가로챈다.
                    Tile existing = board.GetTile(pos);
                    if (existing != null)
                        break;

                    var tile = new Tile(spawner.NextDefinition());
                    board.PlaceTile(pos, tile);
                    spawns.Add(new TileSpawn(tile, pos));
                }
            }

            return spawns;
        }
    }
}
