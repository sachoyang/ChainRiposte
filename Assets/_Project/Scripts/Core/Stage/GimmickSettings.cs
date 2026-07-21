namespace ChainRiposte.Core.Stage
{
    /// <summary>
    /// 스테이지 기믹 3종의 밸런스 수치 (GDD §3.6). StageDataSO에서 주입된다.
    /// 기믹이 목록에 없으면 이 값들은 무시된다.
    /// </summary>
    public sealed class GimmickSettings
    {
        // ── 전염되는 타일 ──
        /// <summary>퍼즐 시작 시 뿌려지는 부패 타일 수.</summary>
        public int CorruptionSeeds = 2;

        /// <summary>부패가 퍼지는 주기(턴). 1이면 매 턴.</summary>
        public int CorruptionSpreadEveryTurns = 1;

        /// <summary>부패 타일이 활성 칸 대비 이 비율을 넘으면 확산을 멈춘다 (완전 데드락 방지).</summary>
        public float MaxCorruptionRatio = 0.35f;

        // ── 시한폭탄 몬스터 ──
        /// <summary>새로 스폰되는 몬스터 타일이 폭탄이 될 확률 (0~1).</summary>
        public float BombChance = 0.12f;

        /// <summary>폭탄이 터지기까지의 턴 수.</summary>
        public int BombTurns = 3;

        /// <summary>폭발 시 플레이어가 받는 HP 피해.</summary>
        public int BombDamage = 12;

        // ── 사슬 결박 ──
        /// <summary>퍼즐 시작 시 결박된 채로 놓이는 타일 수.</summary>
        public int ChainInitialCount = 3;

        /// <summary>새로 스폰되는 몬스터 타일이 결박될 확률 (0~1).</summary>
        public float ChainChance = 0.08f;
    }
}
