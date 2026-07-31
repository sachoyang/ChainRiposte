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
        public TileDefinition Definition { get; private set; }
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

        /// <summary>
        /// 같은 타일인 채로 <b>종류만</b> 바꾼다 (몬스터 → 폭탄 등).
        ///
        /// <para><see cref="InstanceId"/>는 그대로 둔다 — 이 값이 뷰와의 바인딩 키라서,
        /// 새 <see cref="Tile"/>을 만들어 갈아 끼우면 <b>이미 기록된 낙하·스폰 기록이 옛 타일을 가리켜</b>
        /// 보드와 화면이 어긋난다(리필 도중 종류가 바뀌는 경우가 정확히 그렇다).
        /// 그래서 갈아 끼우지 않고 제자리에서 바꾼다.</para>
        ///
        /// <para>내구도는 새 종류의 값으로 다시 잡는다 — 벽이 아니게 된 타일이 옛 HP를 들고 있으면
        /// 그 뒤의 판정이 종류와 안 맞는다.</para>
        /// </summary>
        public void ChangeDefinition(TileDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
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
