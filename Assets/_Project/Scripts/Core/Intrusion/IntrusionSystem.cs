using System;
using ChainRiposte.Core.Board;
using ChainRiposte.Core.Match;
using ChainRiposte.Core.Stage;

namespace ChainRiposte.Core.Intrusion
{
    /// <summary>
    /// 보스 난입 시스템 (GDD §4).
    /// 점수/시간 곡선에 따라 보스 타일을 스폰시키고(스포너 데코레이터),
    /// <b>판 전체 시계</b>와 <b>보스 타일의 바닥 도달</b> 두 가지로 전투 돌입을 판정한다.
    ///
    /// <para>돌입 경로는 둘뿐이고 <b>어느 쪽도 페널티가 없다</b>:</para>
    /// <list type="number">
    ///   <item>판 시계(<see cref="StageConfig.BossEngageSeconds"/>) 만료 — 보스 타일과 무관하게 즉시.</item>
    ///   <item>보스 타일이 열 최하단에 도달 — 그만큼 일찍 만난다.</item>
    /// </list>
    ///
    /// <para>예전에는 보스 타일마다 듀얼 카운트다운이 돌고, 만료되면 <b>HP 반토막</b>을 물리는
    /// 기습 돌입이 있었다. 페널티를 규칙으로 물리는 대신 퍼즐 자체가 아프도록 바꿨으므로
    /// (성난 몬스터) 그 규칙은 없앴다 — 깎인 HP가 그대로 보스전으로 이어지는 것이 곧 처벌이다.</para>
    /// </summary>
    public sealed class IntrusionSystem
    {
        private readonly StageConfig _config;
        private BoardGrid _board;

        public TileDefinition BossDefinition { get; } = new("Boss", TileCategory.Boss);

        /// <summary>PuzzleEngine에 주입할 스포너 (일반 가중치 스포너 + 보스 확률 데코레이터).</summary>
        public BossTileSpawner Spawner { get; }

        public float ElapsedSeconds { get; private set; }

        /// <summary>전투 돌입 이후 true — 모든 판정이 정지한다.</summary>
        public bool Engaged { get; private set; }

        /// <summary>판 시계를 쓰는가 — 0 이하면 보스 타일이 내려올 때까지 기다린다.</summary>
        public bool HasEngageTimer => _config.BossEngageSeconds > 0f;

        /// <summary>보스전까지 남은 초. 시계를 안 쓰면 <see cref="float.PositiveInfinity"/>.</summary>
        public float SecondsUntilEngage => HasEngageTimer
            ? Math.Max(0f, _config.BossEngageSeconds - ElapsedSeconds)
            : float.PositiveInfinity;

        /// <summary>(남은 초) — HUD 시계 갱신 훅. 시계를 안 쓰면 발행되지 않는다.</summary>
        public event Action<float> EngageTimerChanged;

        /// <summary>(돌입 계기가 된 보스 타일. 판 시계 만료면 null) — 전투로 넘어간다.</summary>
        public event Action<Tile> Engage;

        /// <param name="baseSpawner">
        /// 보스 데코레이터가 감쌀 <b>속</b> 스포너. 비우면 평소대로 가중치 추첨이다.
        /// 튜토리얼이 <see cref="ScriptedTileSpawner"/>를 여기 끼워 넣는다 — 보스 난입은 그대로 두고
        /// 어떤 잡몹이 나올지만 대본으로 정하기 위해서다(<c>Docs/TUTORIAL.md</c> §4.3).
        /// </param>
        public IntrusionSystem(
            StageConfig config, Func<float> scoreGetter, Random rng = null, ITileSpawner baseSpawner = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (scoreGetter == null)
                throw new ArgumentNullException(nameof(scoreGetter));

            Spawner = new BossTileSpawner(
                baseSpawner ?? new WeightedTileSpawner(config.SpawnWeights, rng),
                BossDefinition,
                config.BossChanceByScore,
                config.BossChanceBySeconds,
                scoreGetter,
                () => ElapsedSeconds,
                CountLiveBossTiles,
                config.MaxLiveBossTiles,
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

            if (!HasEngageTimer)
                return;

            EngageTimerChanged?.Invoke(SecondsUntilEngage);

            if (SecondsUntilEngage <= 0f)
                EngageNow(null);
        }

        /// <summary>
        /// 스왑 해석 직후 호출 — 열 최하단 활성 셀에 도달한 보스 타일이 있으면 돌입한다.
        /// 보드가 정착했으므로 스포너의 웨이브 누적도 여기서 비운다.
        /// </summary>
        public void OnBoardSettled()
        {
            if (Engaged || _board == null)
                return;

            Spawner.ResetPendingGrants();

            foreach (GridPos pos in _board.ActivePositions())
            {
                Tile tile = _board.GetTile(pos);
                if (tile == null || tile.Category != TileCategory.Boss)
                    continue;

                GridPos? bottom = _board.BottomActiveCell(pos.X);
                if (bottom.HasValue && bottom.Value == pos)
                {
                    EngageNow(tile);
                    return;
                }
            }
        }

        /// <summary>보드 위 보스 타일 수 — 스포너의 동시 개수 상한이 이 값을 본다.</summary>
        private int CountLiveBossTiles()
        {
            if (_board == null)
                return 0;

            int count = 0;
            foreach (GridPos pos in _board.ActivePositions())
            {
                Tile tile = _board.GetTile(pos);
                if (tile != null && tile.Category == TileCategory.Boss)
                    count++;
            }

            return count;
        }

        private void EngageNow(Tile tile)
        {
            Engaged = true;
            Engage?.Invoke(tile);
        }
    }
}
