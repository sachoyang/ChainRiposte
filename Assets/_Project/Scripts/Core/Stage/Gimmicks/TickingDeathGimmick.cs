using System;
using System.Collections.Generic;
using ChainRiposte.Core.Board;
using ChainRiposte.Core.Match;

namespace ChainRiposte.Core.Stage.Gimmicks
{
    /// <summary>
    /// 시한폭탄 몬스터 (GDD §3.6). 스폰되는 몬스터 타일 일부에 턴 카운트가 붙고,
    /// 카운트 안에 매치로 처치하지 못하면 폭발해 <b>플레이어 HP에 직접 피해</b>를 준다.
    /// (퍼즐 중에도 물약 타일을 노려야 하는 이유가 된다.)
    /// </summary>
    public sealed class TickingDeathGimmick : StageGimmick
    {
        private readonly HashSet<long> _armedThisTurn = new();

        public override GimmickType Type => GimmickType.TickingDeath;

        public override void OnTilesSpawned(GimmickContext context, IReadOnlyList<TileSpawn> spawns)
        {
            foreach (TileSpawn spawn in spawns)
            {
                Tile tile = spawn.Tile;
                // 사슬·성남이 이미 걸린 타일은 건너뛴다 — 타일 하나에 기믹은 하나뿐이다
                // (사슬은 매치해도 안 사라지는데 폭탄은 매치로 없애야 해서, 겹치면 해법이 서로 부딪힌다).
                if (tile.Category != TileCategory.Monster || tile.Status.HasGimmick)
                    continue;
                if (context.Rng.NextDouble() >= context.Settings.BombChance)
                    continue;

                tile.Status.BombTurnsRemaining = Math.Max(1, context.Settings.BombTurns);
                _armedThisTurn.Add(tile.InstanceId);
                context.Report(new GimmickEvent(
                    GimmickEventType.BombArmed, spawn.Position, tile, tile.Status.BombTurnsRemaining));
            }
        }

        public override void OnTurnEnded(GimmickContext context)
        {
            foreach (GridPos pos in context.Collect(t => t.Status.IsBomb))
            {
                Tile tile = context.Board.GetTile(pos);

                // 이번 턴에 갓 스폰된 폭탄은 다음 턴부터 카운트가 돈다
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
