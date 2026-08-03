using System;
using System.Collections.Generic;
using ChainRiposte.Core.Board;

namespace ChainRiposte.Core.Match
{
    /// <summary>
    /// 중력 압축 후 각 열의 상단 빈 칸을 새 타일로 채운다.
    ///
    /// <para>여기서는 벽 아래로 내려가지 않는다 — 기둥 중간에 스폰하면 위에 있던 타일(보스 포함)이
    /// 내려올 자리를 가로챈다. 벽 그늘은 <b>대각선 슬라이드</b>가 채우는 것이 정상 경로이고,
    /// 그마저 닿을 수 없는 칸만 <see cref="FillSealedPockets"/>가 마지막에 맡는다.</para>
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

        /// <summary>
        /// <b>갇힌 칸</b>을 그 자리에서 채운다 — 낙하·슬라이드·리필이 전부 끝났는데도 남은 빈 활성 칸.
        ///
        /// <para><b>왜 필요한가.</b> 대각선 슬라이드는 <b>한 칸짜리 이동</b>이라 출처가 정확히
        /// 대각선 위 칸이어야 한다. 그 자리가 벽이거나, 보드 밖이거나, 위에 아무것도 없는 구멍이면
        /// <b>어떤 타일도 그 칸에 도달할 수 없다.</b> 부서진 벽이 남긴 자리도 같은 처지가 된다.
        /// 중력만으로는 판을 꽉 채울 수 없고, 빈 칸이 남으면 화면에서 그냥 구멍으로 보인다.</para>
        ///
        /// <para><b>반드시 정착이 끝난 뒤에만 부른다.</b> 도중에 부르면 아직 내려오는 중인 타일의
        /// 자리를 가로채, 위의 타일(보스 포함)이 영영 안 내려온다 —
        /// <see cref="Refill"/>이 기둥 중간을 건드리지 않는 것과 같은 이유다.
        /// 그래서 <see cref="GravityResolver.Settle"/>은 "이번 웨이브에 아무 일도 없었다"를
        /// 확인한 뒤에만 이것을 부른다.</para>
        /// </summary>
        public static IReadOnlyList<TileSpawn> FillSealedPockets(BoardGrid board, ITileSpawner spawner)
        {
            List<TileSpawn> spawns = null;

            foreach (GridPos pos in board.ActivePositions())
            {
                if (board.IsOccupied(pos))
                    continue;

                var tile = new Tile(ResolvePlainDefinition(board, spawner));
                board.PlaceTile(pos, tile);
                (spawns ??= new List<TileSpawn>()).Add(new TileSpawn(tile, pos, sealedPocket: true));
            }

            return (IReadOnlyList<TileSpawn>)spawns ?? Array.Empty<TileSpawn>();
        }

        /// <summary>
        /// 갇힌 칸에는 <b>평범한(매치 가능한) 타일만</b> 넣는다. 아니면 다시 뽑는다.
        ///
        /// <para><b>보스 타일이 여기 생기면 안 된다.</b> 보스 타일은 <b>바닥에 닿아야</b> 제 일(난입)을 하는데,
        /// 갇힌 칸은 정의상 중력이 닿지 않는 곳이다. 거기 생기면 영영 안 내려오면서
        /// 동시 상한(<c>MaxLiveBossTiles</c>, 기본 1)만 차지해 <b>난입이 통째로 막히고</b>,
        /// 하필 그 칸이 바닥이면 반대로 <b>예고 없이 즉시 난입</b>한다. 둘 다 규칙이 아니라 사고다.
        /// 부패·벽도 같은 이유로 거른다 — 스스로 사라질 수 없는 종류를 손 닿기 어려운 칸에 두지 않는다.</para>
        ///
        /// <para>다시 뽑아 버린 보스 몫은 <c>BossTileSpawner</c>의 웨이브 누적 카운터에 남지만,
        /// 그것은 <b>보스 타일이 덜 나오는</b> 쪽으로만 어긋나고 정착이 끝나면 초기화된다.
        /// 안전한 방향이라 그대로 둔다.</para>
        ///
        /// <para>⚠ <b>재추첨만으로는 못 막는다.</b> 상한까지 실패했을 때 "마지막 것을 그냥 쓰면"
        /// 확률이 낮아졌을 뿐 보스가 갇힐 길이 그대로 남는다 — 드물게 나는 사고가 제일 못 찾는 사고다.
        /// 그래서 마지막에는 <b>판에 이미 있는 평범한 종류를 빌려온다.</b> 실패해도 특수 타일은 안 놓는다.</para>
        /// </summary>
        private static TileDefinition ResolvePlainDefinition(BoardGrid board, ITileSpawner spawner)
        {
            const int MaxRerolls = 24;

            TileDefinition definition = spawner.NextDefinition();
            for (int attempt = 0; attempt < MaxRerolls && !MatchFinder.IsMatchable(definition); attempt++)
                definition = spawner.NextDefinition();

            if (MatchFinder.IsMatchable(definition))
                return definition;

            // 스포너가 특수 타일만 계속 내놓는다 — 판 위의 평범한 타일에서 종류를 빌린다.
            // (그 결과로 매치가 생기면 엔진이 다음 연쇄로 정상 처리한다.)
            foreach (GridPos pos in board.ActivePositions())
            {
                Tile existing = board.GetTile(pos);
                if (MatchFinder.IsMatchable(existing))
                    return existing.Definition;
            }

            // 판에도 평범한 타일이 하나도 없다 — 스테이지 설계 쪽 문제다. 빈 칸보다는 낫다.
            return definition;
        }
    }
}
