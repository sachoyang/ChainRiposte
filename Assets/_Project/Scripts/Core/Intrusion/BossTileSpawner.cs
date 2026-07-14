using System;
using ChainRiposte.Core.Board;
using ChainRiposte.Core.Match;

namespace ChainRiposte.Core.Intrusion
{
    /// <summary>
    /// 일반 스포너를 감싸 확률적으로 보스 타일을 내놓는 데코레이터.
    /// 확률 = max(점수 곡선, 시간 곡선) — 파밍 욕심(점수)과 시간 지연 어느 쪽이든 압박이 커진다.
    /// </summary>
    public sealed class BossTileSpawner : ITileSpawner
    {
        private readonly ITileSpawner _inner;
        private readonly TileDefinition _bossDefinition;
        private readonly Func<float, float> _chanceByScore;
        private readonly Func<float, float> _chanceBySeconds;
        private readonly Func<float> _scoreGetter;
        private readonly Func<float> _secondsGetter;
        private readonly Random _rng;

        public BossTileSpawner(
            ITileSpawner inner,
            TileDefinition bossDefinition,
            Func<float, float> chanceByScore,
            Func<float, float> chanceBySeconds,
            Func<float> scoreGetter,
            Func<float> secondsGetter,
            Random rng = null)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _bossDefinition = bossDefinition ?? throw new ArgumentNullException(nameof(bossDefinition));
            _chanceByScore = chanceByScore ?? throw new ArgumentNullException(nameof(chanceByScore));
            _chanceBySeconds = chanceBySeconds ?? throw new ArgumentNullException(nameof(chanceBySeconds));
            _scoreGetter = scoreGetter ?? throw new ArgumentNullException(nameof(scoreGetter));
            _secondsGetter = secondsGetter ?? throw new ArgumentNullException(nameof(secondsGetter));
            _rng = rng ?? new Random();
        }

        /// <summary>현재 보스 타일 스폰 확률 (0~1). HUD 경고/오디오 크레센도 훅.</summary>
        public float CurrentChance =>
            Math.Clamp(Math.Max(_chanceByScore(_scoreGetter()), _chanceBySeconds(_secondsGetter())), 0f, 1f);

        public TileDefinition NextDefinition() =>
            _rng.NextDouble() < CurrentChance ? _bossDefinition : _inner.NextDefinition();
    }
}
