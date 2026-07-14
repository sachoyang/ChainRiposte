using System.Collections.Generic;
using ChainRiposte.Core.Board;
using ChainRiposte.Core.Match;
using ChainRiposte.Core.Stage;

namespace ChainRiposte.Core.Tests
{
    /// <summary>테스트 공용 헬퍼 — 타일 정의, 마스크 파서, 결정적 스포너.</summary>
    internal static class TestUtils
    {
        public static readonly TileDefinition Skull = new("Skull", TileCategory.Monster, baseSouls: 10);
        public static readonly TileDefinition Rat = new("Rat", TileCategory.Monster, baseSouls: 10);
        public static readonly TileDefinition Potion = new("Potion", TileCategory.Potion);

        /// <summary>StageDataSO와 같은 규칙(O/X/W, 첫 행이 최상단)으로 마스크와 벽 위치를 만든다.</summary>
        public static (bool[,] mask, List<GridPos> walls) ParseRows(params string[] rowsTopDown)
        {
            int height = rowsTopDown.Length;
            int width = rowsTopDown[0].Length;
            var mask = new bool[width, height];
            var walls = new List<GridPos>();

            for (int rowIndex = 0; rowIndex < height; rowIndex++)
            {
                int y = height - 1 - rowIndex;
                for (int x = 0; x < width; x++)
                {
                    char c = rowsTopDown[rowIndex][x];
                    if (c == 'O' || c == 'W')
                        mask[x, y] = true;
                    if (c == 'W')
                        walls.Add(new GridPos(x, y));
                }
            }

            return (mask, walls);
        }

        public static StageConfig Config(
            string[] rowsTopDown, int turnLimit = 30, int wallHp = 2, float comboStep = 0.5f)
        {
            (bool[,] mask, List<GridPos> walls) = ParseRows(rowsTopDown);
            return new StageConfig
            {
                ActiveMask = mask,
                WallPositions = walls,
                WallHp = wallHp,
                TurnLimit = turnLimit,
                ComboSoulMultiplierStep = comboStep,
            };
        }
    }

    /// <summary>
    /// 지정한 순서대로 타일을 내놓고, 소진되면 fallback 사이클을 반복하는 결정적 스포너.
    /// 초기 배치 순서는 PuzzleEngine.FillInitialBoard의 순회(아래 행부터, 왼쪽부터)를 따른다.
    /// </summary>
    internal sealed class SequenceSpawner : ITileSpawner
    {
        private readonly Queue<TileDefinition> _sequence;
        private readonly TileDefinition[] _fallbackCycle;
        private int _cycleIndex;

        public SequenceSpawner(IEnumerable<TileDefinition> sequence, params TileDefinition[] fallbackCycle)
        {
            _sequence = new Queue<TileDefinition>(sequence);
            _fallbackCycle = fallbackCycle.Length > 0
                ? fallbackCycle
                : new[] { TestUtils.Potion, TestUtils.Rat, TestUtils.Skull };
        }

        public TileDefinition NextDefinition() =>
            _sequence.Count > 0
                ? _sequence.Dequeue()
                : _fallbackCycle[_cycleIndex++ % _fallbackCycle.Length];
    }
}
