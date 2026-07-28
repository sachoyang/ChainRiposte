using System;
using ChainRiposte.Core.Board;
using ChainRiposte.Core.Match;

namespace ChainRiposte.Core.Intrusion
{
    /// <summary>
    /// 일반 스포너를 감싸 확률적으로 보스 타일을 내놓는 데코레이터.
    /// 확률 = max(점수 곡선, 시간 곡선) — 파밍 욕심(점수)과 시간 지연 어느 쪽이든 압박이 커진다.
    ///
    /// <para><b>동시 개수 상한이 핵심이다.</b> 이 스포너는 리필 타일 <b>하나하나</b>에 확률을 굴린다 —
    /// 한 번의 연쇄로 10칸이 채워지면 주사위를 10번 굴리는 셈이라, 확률 0.3이면 한 웨이브에 3개가
    /// 튀어나와 보드가 보스 타일로 도배된다. 상한이 그것을 구조로 막는다.</para>
    /// </summary>
    public sealed class BossTileSpawner : ITileSpawner
    {
        private readonly ITileSpawner _inner;
        private readonly TileDefinition _bossDefinition;
        private readonly Func<float, float> _chanceByScore;
        private readonly Func<float, float> _chanceBySeconds;
        private readonly Func<float> _scoreGetter;
        private readonly Func<float> _secondsGetter;
        private readonly Func<int> _liveBossCount;
        private readonly int _maxLive;
        private readonly Random _rng;

        // 이번 리필 웨이브에서 이미 내준 보스 타일 수. 갓 내준 타일은 아직 보드에 놓이지 않아
        // _liveBossCount 에 안 잡히므로, 이것을 더해야 웨이브 안에서도 상한이 지켜진다.
        private int _pendingGrants;

        public BossTileSpawner(
            ITileSpawner inner,
            TileDefinition bossDefinition,
            Func<float, float> chanceByScore,
            Func<float, float> chanceBySeconds,
            Func<float> scoreGetter,
            Func<float> secondsGetter,
            Func<int> liveBossCount = null,
            int maxLive = 1,
            Random rng = null)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _bossDefinition = bossDefinition ?? throw new ArgumentNullException(nameof(bossDefinition));
            _chanceByScore = chanceByScore ?? throw new ArgumentNullException(nameof(chanceByScore));
            _chanceBySeconds = chanceBySeconds ?? throw new ArgumentNullException(nameof(chanceBySeconds));
            _scoreGetter = scoreGetter ?? throw new ArgumentNullException(nameof(scoreGetter));
            _secondsGetter = secondsGetter ?? throw new ArgumentNullException(nameof(secondsGetter));
            _liveBossCount = liveBossCount ?? (() => 0);
            _maxLive = maxLive;
            _rng = rng ?? new Random();
        }

        /// <summary>현재 보스 타일 스폰 확률 (0~1). HUD 경고/오디오 크레센도 훅.</summary>
        public float CurrentChance =>
            Math.Clamp(Math.Max(_chanceByScore(_scoreGetter()), _chanceBySeconds(_secondsGetter())), 0f, 1f);

        /// <summary>상한에 걸려 더는 보스 타일을 내놓지 않는 상태인가.</summary>
        public bool AtLiveCap => _maxLive > 0 && _liveBossCount() + _pendingGrants >= _maxLive;

        /// <summary>보드가 정착해 실제 개수를 다시 셀 수 있게 됐다 — 웨이브 누적을 비운다.</summary>
        public void ResetPendingGrants() => _pendingGrants = 0;

        public TileDefinition NextDefinition()
        {
            if (AtLiveCap || _rng.NextDouble() >= CurrentChance)
                return _inner.NextDefinition();

            _pendingGrants++;
            return _bossDefinition;
        }
    }
}
