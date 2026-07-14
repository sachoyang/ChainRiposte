using System;
using System.Collections.Generic;
using ChainRiposte.Core.Board;
using ChainRiposte.Core.Match;
using ChainRiposte.Core.Stage;

namespace ChainRiposte.Core.Intrusion
{
    /// <summary>
    /// 보스 난입 시스템 (GDD §4).
    /// 점수/시간 곡선에 따라 보스 타일을 스폰시키고(스포너 데코레이터),
    /// 보드 위 각 보스 타일의 듀얼 카운트다운을 독립적으로 굴리며,
    /// 바닥 도달(정상 돌입)과 카운트다운 만료(기습 돌입)를 판정한다.
    /// </summary>
    public sealed class IntrusionSystem
    {
        private readonly StageConfig _config;
        private readonly Dictionary<long, BossCountdown> _countdowns = new();
        private BoardGrid _board;

        public TileDefinition BossDefinition { get; } = new("Boss", TileCategory.Boss);

        /// <summary>PuzzleEngine에 주입할 스포너 (일반 가중치 스포너 + 보스 확률 데코레이터).</summary>
        public BossTileSpawner Spawner { get; }

        public float ElapsedSeconds { get; private set; }

        /// <summary>전투 돌입 이후 true — 모든 판정이 정지한다.</summary>
        public bool Engaged { get; private set; }

        /// <summary>(보스 타일 id, 남은 초, 남은 턴) — 타일 위 카운트다운 표시용.</summary>
        public event Action<long, float, int> CountdownChanged;

        /// <summary>(돌입 계기가 된 보스 타일, 기습 여부). 기습이면 HP 페널티 적용 후 전투 전환.</summary>
        public event Action<Tile, bool> Engage;

        public IntrusionSystem(StageConfig config, Func<float> scoreGetter, Random rng = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (scoreGetter == null)
                throw new ArgumentNullException(nameof(scoreGetter));

            Spawner = new BossTileSpawner(
                new WeightedTileSpawner(config.SpawnWeights, rng),
                BossDefinition,
                config.BossChanceByScore,
                config.BossChanceBySeconds,
                scoreGetter,
                () => ElapsedSeconds,
                rng);
        }

        /// <summary>엔진 생성 직후 보드를 연결한다.</summary>
        public void AttachBoard(BoardGrid board) => _board = board;

        /// <summary>실시간 진행. 연출 재생 중에도 흐른다 — 시간 압박이 기획 의도.</summary>
        public void Tick(float deltaSeconds)
        {
            if (Engaged || deltaSeconds <= 0f)
                return;

            ElapsedSeconds += deltaSeconds;

            foreach (BossCountdown countdown in SnapshotCountdowns())
            {
                countdown.SecondsRemaining -= deltaSeconds;
                CountdownChanged?.Invoke(
                    countdown.Tile.InstanceId,
                    Math.Max(0f, countdown.SecondsRemaining),
                    countdown.TurnsRemaining);

                if (countdown.Expired)
                {
                    EngageNow(countdown.Tile, ambush: true);
                    return;
                }
            }
        }

        /// <summary>턴 소모 통지 (PuzzleEngine.TurnsChanged와 시그니처 호환).</summary>
        public void OnTurnConsumed(int turnsRemaining)
        {
            if (Engaged)
                return;

            foreach (BossCountdown countdown in SnapshotCountdowns())
            {
                countdown.TurnsRemaining--;
                CountdownChanged?.Invoke(
                    countdown.Tile.InstanceId,
                    Math.Max(0f, countdown.SecondsRemaining),
                    countdown.TurnsRemaining);

                if (countdown.Expired)
                {
                    EngageNow(countdown.Tile, ambush: true);
                    return;
                }
            }
        }

        /// <summary>
        /// 스왑 해석 직후 호출 — 새로 스폰된 보스 타일의 카운트다운을 시작하고,
        /// 열 최하단 활성 셀에 도달한 보스 타일이 있으면 정상 돌입한다.
        /// </summary>
        public void OnBoardSettled()
        {
            if (Engaged || _board == null)
                return;

            foreach (GridPos pos in _board.ActivePositions())
            {
                Tile tile = _board.GetTile(pos);
                if (tile == null || tile.Category != TileCategory.Boss)
                    continue;

                if (!_countdowns.ContainsKey(tile.InstanceId))
                {
                    var countdown = new BossCountdown(tile, _config.BossCountdownSeconds, _config.BossCountdownTurns);
                    _countdowns[tile.InstanceId] = countdown;
                    CountdownChanged?.Invoke(tile.InstanceId, countdown.SecondsRemaining, countdown.TurnsRemaining);
                }

                GridPos? bottom = _board.BottomActiveCell(pos.X);
                if (bottom.HasValue && bottom.Value == pos)
                {
                    EngageNow(tile, ambush: false);
                    return;
                }
            }
        }

        private void EngageNow(Tile tile, bool ambush)
        {
            Engaged = true;
            Engage?.Invoke(tile, ambush);
        }

        /// <summary>이벤트 핸들러가 상태를 건드려도 안전하도록 복사본을 순회한다.</summary>
        private List<BossCountdown> SnapshotCountdowns() => new(_countdowns.Values);
    }
}
