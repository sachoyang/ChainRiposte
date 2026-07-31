using System;
using System.Collections.Generic;
using ChainRiposte.Core.Board;
using ChainRiposte.Core.Match;

namespace ChainRiposte.Core.Stage.Gimmicks
{
    /// <summary>
    /// 시한폭탄 타일 (GDD §3.6). 리필로 내려오는 타일 일부가 <b>폭탄 타일</b>이 되어 턴마다 카운트가 줄고,
    /// 0이 되면 터져 <b>플레이어 HP에 직접 피해</b>를 준다. 해제법은 <b>폭탄에 인접한 매치</b>다.
    ///
    /// <para><b>왜 몬스터의 상태가 아니라 타일인가</b>: 예전에는 몬스터 타일에 카운트를 붙였고,
    /// 없애려면 <b>그 몬스터를 매치</b>해야 했다. 그러면 폭탄이 붙은 색이 보드에 두 개밖에 없을 때
    /// 플레이어가 할 수 있는 일이 없다 — 시간은 가는데 해법이 존재하지 않는 판이 나온다.
    /// 타일로 떼어 내면 해법이 <b>어느 색이든 옆에서 매치 하나</b>로 바뀌어 항상 손쓸 길이 있다.
    /// 부패 타일과 같은 규칙(매치·스왑 불가, 낙하는 함)에 타이머만 얹은 셈이다.</para>
    ///
    /// <para>스왑을 막는 것은 <see cref="MatchFinder.IsMatchable"/>에서 저절로 따라온다
    /// (<see cref="MoveFinder.IsSwappable"/>이 그 위에 서 있다) — 규칙을 두 곳에 적지 않는다.</para>
    /// </summary>
    public sealed class TickingDeathGimmick : StageGimmick
    {
        /// <summary>폭탄 타일의 종류 정의 (매치 불가 카테고리). 부패와 같은 방식으로 코드가 들고 있다.</summary>
        public static readonly TileDefinition BombDefinition = new("Bomb", TileCategory.Bomb);

        private readonly HashSet<long> _armedThisTurn = new();

        public override GimmickType Type => GimmickType.TickingDeath;

        public override void OnTilesSpawned(GimmickContext context, IReadOnlyList<TileSpawn> spawns)
        {
            foreach (TileSpawn spawn in spawns)
            {
                // 갓 내려온 몬스터만 폭탄이 된다. 벽·보스·물약은 건드리지 않는다.
                if (spawn.Tile.Category != TileCategory.Monster || spawn.Tile.Status.HasGimmick)
                    continue;
                if (context.Rng.NextDouble() >= context.Settings.BombChance)
                    continue;

                // 새 타일로 갈아 끼우지 않고 <b>제자리에서 종류만</b> 바꾼다. 이 훅은 낙하·스폰 기록이
                // 이미 만들어진 뒤에 불리므로, 새 타일을 만들면 그 기록이 옛 몬스터를 가리켜
                // 보드와 화면이 어긋난다. InstanceId 를 지키면 뷰가 그대로 따라온다.
                Tile bomb = spawn.Tile;
                bomb.ChangeDefinition(BombDefinition);
                bomb.Status.BombTurnsRemaining = Math.Max(1, context.Settings.BombTurns);
                _armedThisTurn.Add(bomb.InstanceId);

                context.Report(new GimmickEvent(
                    GimmickEventType.BombArmed, spawn.Position, bomb, bomb.Status.BombTurnsRemaining));
            }
        }

        /// <summary>
        /// 폭탄에 <b>인접한</b> 매치가 나면 해체된다 — 부패와 같은 규칙이다.
        /// 해체는 피해가 없고, 영혼석도 주지 않는다(폭탄은 몬스터가 아니다).
        /// </summary>
        public override void OnMatchesResolving(GimmickContext context, HashSet<GridPos> cleared)
        {
            var defused = new List<GridPos>();
            foreach (GridPos pos in context.Collect(t => t.Category == TileCategory.Bomb))
            {
                foreach (GridPos neighbor in context.Board.ActiveNeighbors4(pos))
                {
                    if (!cleared.Contains(neighbor))
                        continue;
                    defused.Add(pos);
                    break;
                }
            }

            foreach (GridPos pos in defused)
            {
                if (!cleared.Add(pos))
                    continue;
                context.Report(new GimmickEvent(
                    GimmickEventType.BombDefused, pos, context.Board.GetTile(pos)));
            }
        }

        public override void OnTurnEnded(GimmickContext context)
        {
            foreach (GridPos pos in context.Collect(t => t.Category == TileCategory.Bomb))
            {
                Tile tile = context.Board.GetTile(pos);

                // 이번 턴에 갓 내려온 폭탄은 다음 턴부터 카운트가 돈다 — 떨어지자마자 터지면
                // 플레이어가 손쓸 틈이 없어 운이 된다.
                if (_armedThisTurn.Contains(tile.InstanceId))
                    continue;

                tile.Status.BombTurnsRemaining--;
                if (tile.Status.BombTurnsRemaining > 0)
                {
                    context.Report(new GimmickEvent(
                        GimmickEventType.BombTicked, pos, tile, tile.Status.BombTurnsRemaining));
                    continue;
                }

                context.Board.RemoveTile(pos);
                context.MarkBoardChanged();
                context.DealPlayerDamage(context.Settings.BombDamage);
                context.Report(new GimmickEvent(
                    GimmickEventType.BombExploded, pos, tile, context.Settings.BombDamage));
            }

            _armedThisTurn.Clear();
        }
    }
}
