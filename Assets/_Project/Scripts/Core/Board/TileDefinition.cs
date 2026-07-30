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

        /// <summary>
        /// 이 몬스터가 성났을 때 때리는 피해. 0이면 기믹 설정의 공용값을 쓴다 —
        /// 종류별로 차등을 주고 싶을 때만 적으면 되고, 안 적어도 잡몹 공격이 돌아간다.
        /// </summary>
        public int AttackDamage { get; }

        /// <summary>
        /// 성난 뒤 <b>때리기까지의 시간(초)</b>. 0이면 기믹 설정의 공용값(박 수 × 박 길이)을 쓴다.
        ///
        /// <para>실제 카운트다운은 <b>박 단위</b>로 돈다(모든 몬스터가 같은 맥박에 맞춰 숫자를 줄인다).
        /// 그래서 이 값은 성날 때 <c>반올림(초 ÷ 박 길이)</c>로 박 수가 되고, 최소 1박이다 —
        /// 예고 없이 때리는 몬스터는 만들 수 없다.</para>
        /// </summary>
        public float AttackSeconds { get; }

        public TileDefinition(
            string id, TileCategory category, int baseSouls = 0, int maxHp = 0,
            int attackDamage = 0, float attackSeconds = 0f)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("타일 Id는 비어 있을 수 없습니다.", nameof(id));
            if (baseSouls < 0 || maxHp < 0 || attackDamage < 0 || attackSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(id), $"타일 '{id}': 음수 값은 허용되지 않습니다.");

            Id = id;
            Category = category;
            BaseSouls = baseSouls;
            MaxHp = maxHp;
            AttackDamage = attackDamage;
            AttackSeconds = attackSeconds;
        }

        public override string ToString() => $"{Id}({Category})";
    }
}
