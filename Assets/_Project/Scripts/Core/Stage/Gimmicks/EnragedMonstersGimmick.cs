using System;
using System.Collections.Generic;
using ChainRiposte.Core.Board;

namespace ChainRiposte.Core.Stage.Gimmicks
{
    /// <summary>
    /// 성난 몬스터 — <b>퍼즐 페이즈의 유일한 상시 위협</b>.
    /// 보드의 잡몹 하나가 주기적으로 성나서 카운트다운을 시작하고, 0이 되면 플레이어 HP를 직접 깎는다.
    ///
    /// <para>이 기믹이 있기 전까지 퍼즐은 턴만 채우면 만피로 통과할 수 있었다 — 보스전만 잘하면 됐다.
    /// 이제 <b>못 풀면 퍼즐에서 죽고</b>, 어설프게 풀면 깎인 HP를 그대로 들고 보스를 만난다.</para>
    ///
    /// <para>설계 세 가지:</para>
    /// <list type="number">
    ///   <item><b>매치로 없애면 취소된다</b> — 해법이 퍼즐 안에 있어야 실력이 반영된다.
    ///     "지금 저놈부터 없애라"가 곧 이 판의 판단이 된다.</item>
    ///   <item><b>때린 뒤에도 사라지지 않고 재장전한다</b>(폭탄과의 결정적 차이) —
    ///     사라지면 방치가 오히려 이득이 되어 압박이 사라진다.</item>
    ///   <item><b>한 턴에 최대 하나만 새로 성난다</b> + 동시 상한 —
    ///     보드가 통째로 성나 손쓸 수 없게 되는 사고를 구조로 막는다.</item>
    /// </list>
    /// </summary>
    public sealed class EnragedMonstersGimmick : StageGimmick
    {
        // 갓 성난 놈은 그 턴에 카운트가 줄지 않는다 — 성나자마자 1턴을 손해 보면 예고의 의미가 없다.
        private readonly HashSet<long> _enragedThisTurn = new();
        private int _turnsElapsed;

        public override GimmickType Type => GimmickType.EnragedMonsters;

        /// <summary>매치로 사라지는 타일 중 성난 것을 알린다 — 취소 자체는 타일이 사라지며 저절로 된다.</summary>
        public override void OnMatchesResolving(GimmickContext context, HashSet<GridPos> cleared)
        {
            foreach (GridPos pos in cleared)
            {
                Tile tile = context.Board.GetTile(pos);
                if (tile != null && tile.Status.IsEnraged)
                    context.Report(new GimmickEvent(GimmickEventType.EnrageCleared, pos, tile));
            }
        }

        public override void OnTurnEnded(GimmickContext context)
        {
            _turnsElapsed++;

            TickExisting(context);
            TryEnrageOne(context);

            _enragedThisTurn.Clear();
        }

        private void TickExisting(GimmickContext context)
        {
            foreach (GridPos pos in context.Collect(t => t.Status.IsEnraged))
            {
                Tile tile = context.Board.GetTile(pos);
                if (_enragedThisTurn.Contains(tile.InstanceId))
                    continue;

                tile.Status.EnrageTurnsRemaining--;
                if (tile.Status.EnrageTurnsRemaining > 0)
                {
                    context.Report(new GimmickEvent(
                        GimmickEventType.EnrageTicked, pos, tile, tile.Status.EnrageTurnsRemaining));
                    continue;
                }

                int damage = ResolveDamage(tile, context.Settings);
                context.DealPlayerDamage(damage);

                // 재장전 — 없애지 않으면 계속 맞는다. 보드는 안 건드리므로 재정착도 필요 없다.
                tile.Status.EnrageTurnsRemaining = Math.Max(1, context.Settings.EnrageTurns);
                context.Report(new GimmickEvent(GimmickEventType.EnrageAttacked, pos, tile, damage));
            }
        }

        private void TryEnrageOne(GimmickContext context)
        {
            GimmickSettings settings = context.Settings;
            int max = Math.Max(0, settings.MaxEnragedTiles);
            if (max == 0)
                return;

            if (context.Collect(t => t.Status.IsEnraged).Count >= max)
                return;

            float chance = settings.EnrageChance + settings.EnrageChanceRampPerTurn * _turnsElapsed;
            if (chance <= 0f || context.Rng.NextDouble() >= Math.Clamp(chance, 0f, 1f))
                return;

            // 사슬·폭탄이 걸린 놈은 건너뛴다 — 이미 손이 묶였거나 곧 사라질 타일에 위협을 겹치면
            // "없애서 취소한다"는 해법이 성립하지 않는다.
            List<GridPos> candidates = context.Collect(t =>
                t.Category == TileCategory.Monster && !t.Status.IsEnraged && !t.Status.Chained && !t.Status.IsBomb);
            if (candidates.Count == 0)
                return;

            GridPos pos = candidates[context.Rng.Next(candidates.Count)];
            Tile tile = context.Board.GetTile(pos);
            tile.Status.EnrageTurnsRemaining = Math.Max(1, settings.EnrageTurns);
            _enragedThisTurn.Add(tile.InstanceId);
            context.Report(new GimmickEvent(
                GimmickEventType.EnrageStarted, pos, tile, tile.Status.EnrageTurnsRemaining));
        }

        /// <summary>타일 종류가 자기 공격력을 적었으면 그쪽이 이긴다 — 해골과 슬라임이 같이 아프면 안 된다.</summary>
        private static int ResolveDamage(Tile tile, GimmickSettings settings) =>
            tile.Definition.AttackDamage > 0 ? tile.Definition.AttackDamage : Math.Max(0, settings.EnrageDamage);
    }
}
