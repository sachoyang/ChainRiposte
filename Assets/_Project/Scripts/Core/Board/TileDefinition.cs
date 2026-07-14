using System;

namespace ChainRiposte.Core.Board
{
    /// <summary>
    /// 타일 종류의 불변 정의. Game 레이어의 TileDefinitionSO에서 변환되어 주입된다.
    /// 동일 종류의 타일 인스턴스들은 같은 TileDefinition을 공유한다.
    /// </summary>
    public sealed class TileDefinition
    {
        public string Id { get; }
        public TileCategory Category { get; }

        /// <summary>매치 처치 시 기본 영혼석 (몬스터 전용, 그 외 0).</summary>
        public int BaseSouls { get; }

        /// <summary>내구도형 타일(벽 등)의 최대 HP. 일반 타일은 0.</summary>
        public int MaxHp { get; }

        public TileDefinition(string id, TileCategory category, int baseSouls = 0, int maxHp = 0)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("타일 Id는 비어 있을 수 없습니다.", nameof(id));
            if (baseSouls < 0 || maxHp < 0)
                throw new ArgumentOutOfRangeException(nameof(id), $"타일 '{id}': 음수 값은 허용되지 않습니다.");

            Id = id;
            Category = category;
            BaseSouls = baseSouls;
            MaxHp = maxHp;
        }

        public override string ToString() => $"{Id}({Category})";
    }
}
