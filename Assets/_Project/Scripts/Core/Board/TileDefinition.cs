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
        /// 성난 뒤 때리기까지의 <b>박 수</b>. 0이면 기믹 설정의 공용값(<c>GimmickSettings.EnrageBeats</c>)을 쓴다.
        ///
        /// <para>초가 아니라 박으로 세는 이유: 모든 몬스터가 <b>같은 맥박</b>(박 길이 1.6초)에 맞춰
        /// 숫자를 줄여야 보드에 뜨는 카운트가 정수로 읽히고 "다음 박에 저놈이 때린다"를 셀 수 있다.
        /// 몬스터마다 제 속도로 흐르면 그 셈이 불가능해진다.</para>
        /// </summary>
        public int AttackBeats { get; }

        public TileDefinition(
            string id, TileCategory category, int baseSouls = 0, int maxHp = 0,
            int attackDamage = 0, int attackBeats = 0)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("타일 Id는 비어 있을 수 없습니다.", nameof(id));
            if (baseSouls < 0 || maxHp < 0 || attackDamage < 0 || attackBeats < 0)
                throw new ArgumentOutOfRangeException(nameof(id), $"타일 '{id}': 음수 값은 허용되지 않습니다.");

            Id = id;
            Category = category;
            BaseSouls = baseSouls;
            MaxHp = maxHp;
            AttackDamage = attackDamage;
            AttackBeats = attackBeats;
        }

        public override string ToString() => $"{Id}({Category})";
    }
}
