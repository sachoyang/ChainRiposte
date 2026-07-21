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

        /// <summary>이 타일에 걸린 기믹 상태 (GDD §3.6). 기믹이 없으면 전부 기본값.</summary>
        public TileStatus Status { get; } = new();

        /// <summary>움직이지 않는 타일인가 — 벽이거나 사슬에 결박된 타일. 중력/슬라이드가 이 값을 본다.</summary>
        public bool IsFixed => Category == TileCategory.Wall || Status.Chained;

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
