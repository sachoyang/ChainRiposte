using System;
using System.Collections.Generic;
using ChainRiposte.Core.Board;
using ChainRiposte.Core.Stage;

namespace ChainRiposte.Core.Match
{
    /// <summary>StageData의 가중치 목록에 비례하는 확률로 타일 종류를 추첨한다.</summary>
    public sealed class WeightedTileSpawner : ITileSpawner
    {
        private readonly IReadOnlyList<TileSpawnWeight> _weights;
        private readonly Random _rng;
        private readonly float _totalWeight;

        /// <param name="rng">시드 고정 테스트를 위해 주입 가능. null이면 새로 생성.</param>
        public WeightedTileSpawner(IReadOnlyList<TileSpawnWeight> weights, Random rng = null)
        {
            if (weights == null || weights.Count == 0)
                throw new ArgumentException("스폰 가중치 목록이 비어 있습니다.", nameof(weights));

            _weights = weights;
            _rng = rng ?? new Random();

            foreach (TileSpawnWeight entry in weights)
                _totalWeight += entry.Weight;

            if (_totalWeight <= 0f)
                throw new ArgumentException("가중치 합이 0입니다. 최소 하나는 양수여야 합니다.", nameof(weights));
        }

        public TileDefinition NextDefinition()
        {
            float roll = (float)(_rng.NextDouble() * _totalWeight);
            foreach (TileSpawnWeight entry in _weights)
            {
                roll -= entry.Weight;
                if (roll < 0f)
                    return entry.Tile;
            }

            return _weights[_weights.Count - 1].Tile; // 부동소수점 오차 방어
        }
    }
}
