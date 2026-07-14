using System;
using ChainRiposte.Core.Board;

namespace ChainRiposte.Core.Stage
{
    /// <summary>타일 종류별 스폰 가중치. 리필 시 가중치 비례 확률로 추첨된다.</summary>
    public sealed class TileSpawnWeight
    {
        public TileDefinition Tile { get; }
        public float Weight { get; }

        public TileSpawnWeight(TileDefinition tile, float weight)
        {
            if (weight < 0f)
                throw new ArgumentOutOfRangeException(nameof(weight), $"가중치는 음수일 수 없습니다: {tile}");

            Tile = tile ?? throw new ArgumentNullException(nameof(tile));
            Weight = weight;
        }
    }
}
