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

        // ── 성난 몬스터 (상시) ──
        /// <summary>
        /// 잡몹의 시계가 한 칸 도는 주기(초). <b>턴이 아니라 시간으로 도는 것이 핵심이다</b> —
        /// 턴으로 세면 손을 놓고 있는 동안 아무 일도 안 일어나서, 가만히 기다리는 것이
        /// 가장 안전한 수가 되어 버린다(보스 시계는 그동안에도 흐르므로 공짜로 보스전에 갈 수 있다).
        /// </summary>
        public float EnrageBeatSeconds = 1.6f;

        /// <summary>
        /// 한 박마다 몬스터 하나가 새로 성날 확률 (0~1). <b>0이면 잡몹 공격이 꺼진다.</b>
        /// 한 박에 최대 하나만 성나므로 갑자기 도배되지 않는다.
        /// </summary>
        public float EnrageChance = 0.35f;

        /// <summary>박이 지날수록 성날 확률에 더해지는 양. 0이면 처음부터 끝까지 같은 압박.</summary>
        public float EnrageChanceRampPerBeat = 0.01f;

        /// <summary>성난 뒤 때리기까지의 박 수. 이 안에 매치로 없애면 취소된다.</summary>
        public int EnrageBeats = 3;

        /// <summary>성난 몬스터가 때리는 기본 피해. 타일 종류가 자기 공격력을 적었으면 그쪽이 이긴다.</summary>
        public int EnrageDamage = 8;

        /// <summary>동시에 성날 수 있는 최대 수 — 보드가 통째로 성나 손쓸 수 없게 되는 것을 막는다.</summary>
        public int MaxEnragedTiles = 3;
    }
}
