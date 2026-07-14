using System;
using System.Threading;

namespace ChainRiposte.Core.Board
{
    /// <summary>
    /// 보드 위에 존재하는 타일 인스턴스. InstanceId는 뷰(프레젠테이션)와의
    /// 1:1 바인딩 키로 사용된다 (타일이 낙하해도 동일 인스턴스 추적 가능).
    /// </summary>
    public sealed class Tile
    {
        private static long _nextInstanceId;

        public long InstanceId { get; }
        public TileDefinition Definition { get; }
        public TileCategory Category => Definition.Category;

        /// <summary>내구도형 타일(벽 등)의 남은 HP. 일반 타일은 0.</summary>
        public int RemainingHp { get; private set; }

        public Tile(TileDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            InstanceId = Interlocked.Increment(ref _nextInstanceId);
            RemainingHp = definition.MaxHp;
        }

        /// <summary>내구도를 깎는다. 파괴되어야 하면 true.</summary>
        public bool ApplyDamage(int amount)
        {
            if (amount <= 0)
                return false;

            RemainingHp = Math.Max(0, RemainingHp - amount);
            return RemainingHp == 0;
        }

        public override string ToString() => $"{Definition.Id}#{InstanceId}";
    }
}
