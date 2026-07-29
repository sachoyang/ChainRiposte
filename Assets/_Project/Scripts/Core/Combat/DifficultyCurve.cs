using System;

namespace ChainRiposte.Core.Combat
{
    /// <summary>
    /// 사슬의 <b>고리 깊이 → 보스 배수</b> 표 (<c>Docs/PROGRESSION.md</c> §2.5 · §3).
    /// Game 레이어의 <c>DifficultyCurveSO.ToConfig()</c>로 만들어진다.
    ///
    /// <para><b>왜 필요한가</b>: 보스 에셋은 스테이지들이 돌려 쓴다(1-1~1-3이 같은 보스, 2-1~2-3이 같은 보스).
    /// 절대값만으로는 1-3의 보스가 1-1과 완전히 같은 세기인데, 플레이어는 성장 캐리로 계속 강해지므로
    /// <b>실질 난이도가 뒤로 갈수록 내려간다.</b> 이 표가 그 인플레이션분을 메운다.</para>
    ///
    /// <para><b>왜 등차인가</b>: "한 고리당 +N%" 하나면 스테이지를 몇 개로 늘리든 곡선이 저절로 이어진다.
    /// 깊이별 표를 손으로 적으면 스테이지를 추가할 때마다 그 표를 늘려야 하고, 늘리는 걸 잊으면
    /// 새 판만 조용히 물렁해진다.</para>
    /// </summary>
    public sealed class DifficultyCurve
    {
        /// <summary>고리 하나당 HP 증가율. 0.04 = 고리당 +4%.</summary>
        public float HpPerLink = 0.04f;

        /// <summary>고리 하나당 체간 한계치 증가율. 인살까지의 패링 횟수가 늘어난다.</summary>
        public float PosturePerLink = 0.08f;

        /// <summary>고리 하나당 노트 피해 증가율.</summary>
        public float DamagePerLink = 0.06f;

        /// <summary>고리 하나당 템포 증가율. 여기만은 조금씩 — 속도는 반응 한계에 직접 닿는다.</summary>
        public float TempoPerLink = 0.03f;

        /// <summary>NG+ 한 회차당 HP 증가율. 회차 배수는 고리 배수 <b>위에 곱해진다</b>.</summary>
        public float HpPerNewGamePlus = 0.25f;
        public float PosturePerNewGamePlus = 0.25f;
        public float DamagePerNewGamePlus = 0.3f;
        public float TempoPerNewGamePlus = 0.08f;

        /// <summary>
        /// 템포 배수의 천장. HP·체간·피해는 부풀어도 사람이 대응할 수 있지만
        /// <b>속도는 어느 지점을 넘으면 실력이 아니라 운이 된다</b> — 예비동작이 반응 시간보다 짧아진다.
        /// NG+를 여러 회차 돌아도 여기서 멈춘다.
        /// </summary>
        public float MaxTempoMultiplier = 1.5f;

        /// <summary>
        /// <paramref name="linkDepth"/>번째 고리(1-1 = 0)를 <paramref name="newGamePlusCount"/>회차에
        /// 도는 판의 배수. 음수 깊이·회차는 0으로 본다(월드맵을 안 거친 Main 단독 실행 등).
        /// </summary>
        public DifficultyConfig Evaluate(int linkDepth, int newGamePlusCount = 0)
        {
            int depth = Math.Max(0, linkDepth);
            int cycle = Math.Max(0, newGamePlusCount);

            return new DifficultyConfig
            {
                Hp = Combine(HpPerLink, depth, HpPerNewGamePlus, cycle),
                Posture = Combine(PosturePerLink, depth, PosturePerNewGamePlus, cycle),
                Damage = Combine(DamagePerLink, depth, DamagePerNewGamePlus, cycle),
                Tempo = Math.Min(
                    Math.Max(1f, MaxTempoMultiplier),
                    Combine(TempoPerLink, depth, TempoPerNewGamePlus, cycle)),
            };
        }

        /// <summary>
        /// 고리분과 회차분은 <b>더하지 않고 곱한다</b> — 2회차는 "1회차 전체가 한 단계 위"여야지,
        /// 초반 고리에만 크게 걸리고 후반에 묻히면 회차의 의미가 흐려진다.
        /// 배수가 0 이하로 내려가면 보스가 사라지므로 바닥을 둔다.
        /// </summary>
        private static float Combine(float perLink, int depth, float perCycle, int cycle) =>
            Math.Max(0.01f, (1f + perLink * depth) * (1f + perCycle * cycle));
    }
}
