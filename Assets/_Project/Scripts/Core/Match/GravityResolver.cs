using System;
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
    ///
    /// <para>4) <b>갇힌 칸 끌어오기</b> — 1~3이 전부 멈췄는데도 빈 활성 칸이 남을 수 있다.
    /// 대각선 슬라이드는 <b>한 칸짜리 이동</b>이라 출처가 정확히 대각선 위여야 하는데,
    /// 그 자리가 벽·보드 밖·빈 구멍이면 어떤 타일도 그 칸에 닿지 못한다(부서진 벽 자리도 마찬가지).
    /// 그러면 <see cref="PullTowardSealedHoles"/>가 <b>구멍을 리필 입구까지 걸어 올라가게</b> 한다 —
    /// 길 위의 타일을 한 칸씩 밀어, 없던 타일이 생겨나는 대신 <b>있던 타일이 미끄러져 들어간다.</b>
    /// 실측으로 갇힌 칸의 <b>99%가 이 경로로</b> 메워진다.</para>
    ///
    /// <para>5) <b>제자리 생성</b> — 끌어올 길조차 없는 칸(벽·보드 끝에 완전히 둘러싸임)만
    /// <see cref="BoardRefiller.FillSealedPockets"/>가 그 자리에서 채운다. 최후의 수단이다.</para>
    ///
    /// <para><b>불변식(무조건 성립)</b>: 정착이 끝나면 비활성(X) 셀 이외의 모든 칸이 채워져 있다.
    /// 벽도 타일이므로 여기 포함된다 — 즉 <b>보이는 빈칸은 의도한 구멍뿐</b>이다.</para>
    /// </summary>
    public static class GravityResolver
    {
        private const int MaxPhases = 200; // 안전 상한

        /// <summary>보드를 변형하며 웨이브별 이동/스폰 기록을 반환한다.</summary>
        public static IReadOnlyList<FallPhase> Settle(BoardGrid board, ITileSpawner spawner)
        {
            var phases = new List<FallPhase>();

            // 한 정착 동안 <이미 끌어당겨 본> 구멍. 같은 칸을 두 번 당기지 않는다 — 아래 주석 참조.
            var pulledHoles = new HashSet<GridPos>();

            while (phases.Count < MaxPhases)
            {
                var moves = new List<TileMove>();
                moves.AddRange(Collapse(board));
                moves.AddRange(SlideDiagonalOnce(board));
                IReadOnlyList<TileSpawn> spawns = BoardRefiller.Refill(board, spawner);

                if (moves.Count == 0 && spawns.Count == 0)
                {
                    // 낙하도 슬라이드도 리필도 못 하는데 빈 활성 칸이 남았다 = 갇힌 칸이다.
                    // 먼저 <옆 타일을 끌어와> 메운다 — 구멍이 리필 닿는 자리까지 걸어 올라간다.
                    List<TileMove> pulled = PullTowardSealedHoles(board, pulledHoles);
                    if (pulled.Count > 0)
                    {
                        phases.Add(new FallPhase(Coalesce(pulled), Array.Empty<TileSpawn>()));
                        continue;
                    }

                    // 끌어올 길조차 없는 칸(벽·보드 끝에 완전히 둘러싸임)만 그 자리에서 채운다.
                    spawns = BoardRefiller.FillSealedPockets(board, spawner);
                    if (spawns.Count == 0)
                        break;

                    phases.Add(new FallPhase(Array.Empty<TileMove>(), spawns));
                    continue;
                }

                phases.Add(new FallPhase(Coalesce(moves), spawns));
            }

            return phases;
        }

        /// <summary>
        /// 한 웨이브 안에서 같은 타일이 두 번 움직인 기록(낙하 → 벽에서 슬라이드)을 하나로 합친다.
        /// 뷰는 이동을 '칸 좌표'로 추적하므로 중간 좌표가 남으면 뷰가 어긋난다 (FallPhase 주석 참조).
        /// </summary>
        private static List<TileMove> Coalesce(List<TileMove> moves)
        {
            var indexByTile = new Dictionary<long, int>();
            var merged = new List<TileMove>(moves.Count);

            foreach (TileMove move in moves)
            {
                if (indexByTile.TryGetValue(move.Tile.InstanceId, out int index))
                    merged[index] = new TileMove(move.Tile, merged[index].From, move.To);
                else
                {
                    indexByTile[move.Tile.InstanceId] = merged.Count;
                    merged.Add(move);
                }
            }

            return merged;
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

                    if (tile.IsFixed)
                    {
                        // 벽·결박 타일은 고정. 그 위의 타일은 아래로 내려올 수 없다.
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
                // 대각선 위 칸이 구멍이면 그 위의 첫 활성 칸을 본다 — 타일은 구멍을 통과해 떨어지므로
                // 구멍은 대각선 경로를 끊지 않는다 (비정형 보드에서 벽 그늘이 영영 안 채워지는 것을 막는다)
                if (!TryFirstActiveAbove(board, empty.X + dx, empty.Y + 1, out GridPos source))
                    continue;

                Tile tile = board.GetTile(source);
                if (tile == null || tile.IsFixed)
                    continue;

                // 그늘 칸이 아니면 위에서 리필/낙하로 채워지므로,
                // 고정 타일에 얹혀 직선 낙하가 영영 불가능한 타일만 미끄러뜨린다 (보스 하강 보장)
                if (!intoShadow && !IsRestingOnFixed(board, source))
                    continue;

                board.RemoveTile(source);
                board.PlaceTile(empty, tile);
                moves.Add(new TileMove(tile, source, empty));
                return;
            }
        }

        /// <summary>
        /// <b>갇힌 칸을 옆 타일로 메운다</b> — 새 타일을 만드는 대신, <b>구멍을 리필이 닿는 자리까지
        /// 걸어 올라가게</b> 한다.
        ///
        /// <para><b>발상</b>: 갇힌 칸에 도달할 수 있는 타일은 없지만, 그 칸에서 <b>기둥 꼭대기까지 이어지는
        /// 길</b>은 대개 있다. 그 길 위의 타일을 구멍 쪽으로 <b>한 칸씩 밀면</b> 구멍이 반대로 길을 따라
        /// 올라가 꼭대기에 닿고, 거기는 리필이 평소처럼 채운다.
        /// 즉 <b>없던 타일이 생겨나는 것이 아니라 있던 타일이 미끄러져 들어간다.</b></para>
        ///
        /// <para><b>왜 이게 나은가</b>: 화면에서 "구멍이 메워지는 별도 연출"이 아니라
        /// <b>평소 낙하와 같은 미끄러짐</b>으로 보인다. 뷰는 이걸 그냥 <see cref="TileMove"/>로 받으므로
        /// 연출 코드를 하나도 안 고쳐도 된다.</para>
        ///
        /// <para>한 번에 길 전체를 한 칸씩 민다 — 웨이브마다 한 칸씩 기어가면 느리고,
        /// 구멍이 <b>정확히 꼭대기에 도착</b>해야 다음 웨이브의 리필이 받아 준다.</para>
        ///
        /// <para>⚠ <b>같은 구멍은 한 정착에 한 번만 당긴다</b>(<paramref name="alreadyPulled"/>).
        /// 끌어오기가 <b>대각선 슬라이드와 싸우는</b> 배치가 있기 때문이다 — 벽에 얹힌 타일은
        /// 슬라이드 규칙이 밖으로 빼내려 하고(§규칙 ②ⓑ) 끌어오기는 도로 당겨서, 타일 하나가
        /// 두 칸 사이를 영원히 오간다. 퍼즈가 실제로 잡아낸 무한 루프다.
        /// 한 번 당겨 보고 그래도 남으면 <see cref="BoardRefiller.FillSealedPockets"/>에 넘긴다.</para>
        /// </summary>
        private static List<TileMove> PullTowardSealedHoles(BoardGrid board, HashSet<GridPos> alreadyPulled)
        {
            var moves = new List<TileMove>();

            foreach (GridPos hole in board.ActivePositions())
            {
                if (board.IsOccupied(hole))
                    continue;

                // 리필이 이미 닿는 자리면 그쪽에 맡긴다 — 여기서 건드리면 리필 몫을 가로챈다.
                if (IsRefillEntry(board, hole))
                    continue;

                // 이 칸은 이미 한 번 당겨 봤다. 또 비어 있다면 슬라이드와 맞물려 되돌아온 것이다.
                if (!alreadyPulled.Add(hole))
                    continue;

                List<GridPos> path = FindPathToRefillEntry(board, hole);
                if (path == null)
                    continue; // 끌어올 길이 없다 — 부르는 쪽이 그 자리에서 채운다

                // path[0] = 구멍, path[^1] = 리필이 닿는 칸. 길 위의 타일을 구멍 쪽으로 한 칸씩 당긴다.
                for (int i = 0; i < path.Count - 1; i++)
                {
                    Tile tile = board.RemoveTile(path[i + 1]);
                    board.PlaceTile(path[i], tile);
                    moves.Add(new TileMove(tile, path[i + 1], path[i]));
                }
            }

            return moves;
        }

        /// <summary>
        /// 이 칸이 <b>리필이 들어오는 입구</b>인가 — 같은 열에서 위쪽이 전부 비활성(구멍)이라
        /// 보드 밖에서 새 타일이 곧장 내려올 수 있는 자리. <see cref="BoardRefiller.Refill"/>이
        /// 채우기 시작하는 지점과 같은 뜻이다.
        /// </summary>
        private static bool IsRefillEntry(BoardGrid board, GridPos pos)
        {
            for (int y = pos.Y + 1; y < board.Height; y++)
                if (board.IsActive(new GridPos(pos.X, y)))
                    return false;

            return true;
        }

        /// <summary>
        /// 구멍에서 <b>리필 입구까지의 최단 경로</b>를 찾는다 (너비 우선). 길 위의 칸은
        /// <b>움직일 수 있는 타일</b>이어야 한다 — 벽·사슬은 밀 수 없으므로 길이 그쪽으로 못 뚫린다.
        ///
        /// <para>대각선도 이웃으로 친다. 타일이 원래 대각선으로 미끄러지는 게임이라
        /// 그 방향으로 밀려 들어오는 것이 눈에 자연스럽고, 길도 짧아진다.</para>
        ///
        /// <para>돌려주는 목록은 <b>구멍 → 입구</b> 순서다. 길이 없으면 null.</para>
        /// </summary>
        private static List<GridPos> FindPathToRefillEntry(BoardGrid board, GridPos hole)
        {
            var cameFrom = new Dictionary<GridPos, GridPos>();
            var visited = new HashSet<GridPos> { hole };
            var queue = new Queue<GridPos>();
            queue.Enqueue(hole);

            while (queue.Count > 0)
            {
                GridPos current = queue.Dequeue();

                foreach (GridPos next in Neighbors(current))
                {
                    if (!board.IsActive(next) || !visited.Add(next))
                        continue;

                    // ⚠ 길 위의 칸은 <반드시 밀 수 있는 타일>이 들어 있어야 한다.
                    //   · 벽·사슬(IsFixed)은 밀 수 없다.
                    //   · 빈 칸도 안 된다 — 밀 타일이 없다. 그 칸은 <제 몫의 구멍>이라
                    //     자기 차례에 따로 길을 찾는다. 여기로 지나가게 두면 밀 것이 없는
                    //     자리를 옮기려다 터진다(퍼즈가 잡아낸 실제 사고다).
                    Tile tile = board.GetTile(next);
                    if (tile == null || tile.IsFixed)
                        continue;

                    cameFrom[next] = current;

                    if (IsRefillEntry(board, next))
                        return BuildPath(cameFrom, hole, next);

                    queue.Enqueue(next);
                }
            }

            return null;
        }

        /// <summary>8방향 이웃 (대각선 포함).</summary>
        private static IEnumerable<GridPos> Neighbors(GridPos pos)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (dx != 0 || dy != 0)
                        yield return new GridPos(pos.X + dx, pos.Y + dy);
        }

        /// <summary>도착점에서 거슬러 올라가 <b>구멍 → 입구</b> 순서의 경로를 만든다.</summary>
        private static List<GridPos> BuildPath(
            Dictionary<GridPos, GridPos> cameFrom, GridPos hole, GridPos entry)
        {
            var path = new List<GridPos> { entry };
            GridPos current = entry;

            while (!current.Equals(hole))
            {
                current = cameFrom[current];
                path.Add(current);
            }

            path.Reverse(); // 구멍이 앞
            return path;
        }

        /// <summary>해당 열에서 fromY 이상의 첫 활성 셀 (구멍은 건너뛴다). 없으면 false.</summary>
        private static bool TryFirstActiveAbove(BoardGrid board, int column, int fromY, out GridPos found)
        {
            for (int y = fromY; y < board.Height; y++)
            {
                var pos = new GridPos(column, y);
                if (!board.IsActive(pos))
                    continue;

                found = pos;
                return true;
            }

            found = default;
            return false;
        }

        /// <summary>이 타일이 고정 타일(벽·결박) 바로 위에 얹혀 있는가 (구멍은 통과하므로 첫 활성 셀 기준).</summary>
        private static bool IsRestingOnFixed(BoardGrid board, GridPos pos)
        {
            for (int y = pos.Y - 1; y >= 0; y--)
            {
                var below = new GridPos(pos.X, y);
                if (!board.IsActive(below))
                    continue;

                Tile tile = board.GetTile(below);
                return tile != null && tile.IsFixed;
            }

            return false;
        }

        /// <summary>이 칸의 열 위쪽에 고정 타일이 있어 리필/직선 낙하가 도달할 수 없는가.</summary>
        private static bool IsBlockedFromAbove(BoardGrid board, GridPos pos)
        {
            for (int y = pos.Y + 1; y < board.Height; y++)
            {
                var above = new GridPos(pos.X, y);
                if (!board.IsActive(above))
                    continue;

                Tile tile = board.GetTile(above);
                if (tile != null && tile.IsFixed)
                    return true;
            }

            return false;
        }
    }
}
