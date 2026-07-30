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
    ///   <item><b>한 박에 최대 하나만 새로 성난다</b> + 동시 상한 —
    ///     보드가 통째로 성나 손쓸 수 없게 되는 사고를 구조로 막는다.</item>
    /// </list>
    ///
    /// <para><b>턴이 아니라 시간으로 돈다.</b> 턴으로 세면 손을 놓고 있는 동안 위협도 멈춰서,
    /// "아무것도 안 하기"가 가장 안전한 수가 된다 — 그동안 보스 시계는 계속 흐르므로
    /// 가만히 기다리기만 해도 만피로 보스전에 갈 수 있었다. 이제 기다림에도 값이 붙는다.</para>
    /// </summary>
    public sealed class EnragedMonstersGimmick : StageGimmick
    {
        // 갓 성난 놈은 그 박에 카운트가 줄지 않는다 — 성나자마자 한 칸을 손해 보면 예고의 의미가 없다.
        private readonly HashSet<long> _enragedThisBeat = new();
        private float _beatTimer;
        private int _beatsElapsed;

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

        /// <summary>
        /// 흐른 시간을 <b>박</b>으로 바꿔 소화한다. 남은 시간을 버리지 않고 다음으로 넘기므로
        /// 프레임이 튀거나 연출 때문에 한동안 안 불려도 밀린 박이 그대로 따라온다 —
        /// 화면이 버벅인 사람이 덜 맞는 일이 생기면 안 된다.
        /// </summary>
        public override void OnTimeElapsed(GimmickContext context, float deltaSeconds)
        {
            if (deltaSeconds <= 0f)
                return;

            float beat = Math.Max(0.05f, context.Settings.EnrageBeatSeconds);
            _beatTimer += deltaSeconds;

            // 한 번에 몰아치지 않도록 상한을 둔다 — 오래 멈췄다 돌아온 프레임에서 한꺼번에 맞으면
            // 무슨 일이 벌어졌는지 읽을 수 없다.
            for (int guard = 0; _beatTimer >= beat && guard < MaxBeatsPerCall; guard++)
            {
                _beatTimer -= beat;
                _beatsElapsed++;

                TickExisting(context);
                TryEnrageOne(context);

                _enragedThisBeat.Clear();
            }

            if (_beatTimer > beat)
                _beatTimer = beat; // 상한에 걸려 남은 몫은 버린다(밀린 박이 무한히 쌓이지 않게)
        }

        private const int MaxBeatsPerCall = 3;

        private void TickExisting(GimmickContext context)
        {
            foreach (GridPos pos in context.Collect(t => t.Status.IsEnraged))
            {
                Tile tile = context.Board.GetTile(pos);
                if (_enragedThisBeat.Contains(tile.InstanceId))
                    continue;

                tile.Status.EnrageCountdown--;
                if (tile.Status.EnrageCountdown > 0)
                {
                    context.Report(new GimmickEvent(
                        GimmickEventType.EnrageTicked, pos, tile, tile.Status.EnrageCountdown));
                    continue;
                }

                int damage = ResolveDamage(tile, context.Settings);
                context.DealPlayerDamage(damage);

                // 재장전 — 없애지 않으면 계속 맞는다. 보드는 안 건드리므로 재정착도 필요 없다.
                tile.Status.EnrageCountdown = ResolveBeats(tile, context.Settings);
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

            float chance = settings.EnrageChance + settings.EnrageChanceRampPerBeat * _beatsElapsed;
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
            tile.Status.EnrageCountdown = ResolveBeats(tile, settings);
            _enragedThisBeat.Add(tile.InstanceId);
            context.Report(new GimmickEvent(
                GimmickEventType.EnrageStarted, pos, tile, tile.Status.EnrageCountdown));
        }

        /// <summary>타일 종류가 자기 공격력을 적었으면 그쪽이 이긴다 — 해골과 슬라임이 같이 아프면 안 된다.</summary>
        private static int ResolveDamage(Tile tile, GimmickSettings settings) =>
            tile.Definition.AttackDamage > 0 ? tile.Definition.AttackDamage : Math.Max(0, settings.EnrageDamage);

        /// <summary>
        /// 이 종류가 때리기까지 몇 박인가. 타일이 <b>초</b>를 적었으면 그것을 박으로 환산하고,
        /// 안 적었으면 스테이지의 공용 박 수를 쓴다 — 해골은 느리게 크게, 쥐는 빠르게 작게 같은 차등을 준다.
        ///
        /// <para>카운트다운을 초가 아니라 <b>박</b>으로 세는 이유: 보드에 뜨는 숫자가 정수여야 읽히고,
        /// 몬스터마다 제 속도로 흐르면 "다음 박에 저놈이 때린다"를 셀 수 없다. 맥박은 하나여야 한다.</para>
        ///
        /// <para><b>최소 1박</b> — 0이 되면 성난 그 순간에 때리므로 예고가 사라진다.</para>
        /// </summary>
        private static int ResolveBeats(Tile tile, GimmickSettings settings)
        {
            float seconds = tile.Definition.AttackSeconds;
            if (seconds <= 0f)
                return Math.Max(1, settings.EnrageBeats);

            float beat = Math.Max(0.05f, settings.EnrageBeatSeconds);
            return Math.Max(1, (int)Math.Round(seconds / beat, MidpointRounding.AwayFromZero));
        }
    }
}
