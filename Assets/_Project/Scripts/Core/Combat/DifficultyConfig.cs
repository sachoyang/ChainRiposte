namespace ChainRiposte.Core.Combat
{
    /// <summary>
    /// 이 판 하나에 적용될 <b>보스 수치 배수</b> (<c>Docs/PROGRESSION.md</c> §2.5).
    /// <see cref="DifficultyCurve"/>가 고리 깊이·NG+ 회차를 받아 만들어 내고,
    /// <see cref="BossConfigScaler"/>가 보스 config에 곱한다.
    ///
    /// <para>배수와 절대값의 역할이 갈린다: <b>절대값은 보스 에셋의 것</b>("이 보스가 어떤 놈인가")이고
    /// 배수는 <b>고리 깊이만큼의 인플레이션</b>("몇 번째 판이라 얼마나 부푸나")이다. 두 축을 섞으면
    /// 물렁할 때 어디를 봐야 할지 알 수 없게 된다.</para>
    /// </summary>
    public sealed class DifficultyConfig
    {
        /// <summary>배수가 전부 1 — 스케일링이 꺼진 상태(월드맵을 안 거친 Main 단독 실행 등).</summary>
        public static readonly DifficultyConfig Identity = new();

        /// <summary>보스 HP 배수. 전투가 <b>길어질</b> 뿐이라 크게 올리지 않는다.</summary>
        public float Hp = 1f;

        /// <summary>체간 한계치 배수 — 인살까지 필요한 <b>패링 횟수</b>가 늘어난다. 승리 속도의 본체.</summary>
        public float Posture = 1f;

        /// <summary>노트 피해 배수 — 한 대 맞는 것이 아파진다. 실수 처벌.</summary>
        public float Damage = 1f;

        /// <summary>
        /// 템포(속도) 배수 — 노트가 빨리 오고 예비동작이 짧아진다.
        /// <b>채보를 안 건드리고 조일 수 있는 유일한 레버</b>이자 사람의 반응 한계에 직접 닿는 값이라
        /// <see cref="DifficultyCurve.MaxTempoMultiplier"/>로 천장을 둔다.
        /// </summary>
        public float Tempo = 1f;

        /// <summary>전부 1인가 — 그렇다면 스케일러가 원본을 그대로 돌려준다(쓸데없이 복사하지 않는다).</summary>
        public bool IsIdentity =>
            Near(Hp) && Near(Posture) && Near(Damage) && Near(Tempo);

        private static bool Near(float value) => value > 0.9995f && value < 1.0005f;

        public override string ToString() =>
            $"HP x{Hp:0.##} 체간 x{Posture:0.##} 피해 x{Damage:0.##} 템포 x{Tempo:0.##}";
    }
}
