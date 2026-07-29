using System;
using System.Collections.Generic;

namespace ChainRiposte.Core.Combat
{
    /// <summary>
    /// 보스 하나의 전투 밸런스. Game 레이어의 BossDataSO.ToConfig()로 생성된다.
    /// (Core는 ScriptableObject를 모른다.)
    /// </summary>
    public sealed class BossConfig
    {
        public string Name = "Boss";

        public float MaxHp = 120f;

        /// <summary>체간 한계치 — 도달 시 인살(처형) 가능 상태가 된다.</summary>
        public float MaxPosture = 100f;

        /// <summary>패링 성공 1회당 보스 체간 상승량 (대폭 — 패링이 주 승리 수단).</summary>
        public float ParryPostureGain = 25f;

        /// <summary>공격 적중 시 체간 상승량 = ATK × 이 배율 (소폭 — 공격은 보조 수단).</summary>
        public float AttackPostureFactor = 0.5f;

        /// <summary>체간 자연 회복 속도 (초당). 0이면 회복 없음.</summary>
        public float PostureDecayPerSecond = 6f;

        /// <summary>
        /// <b>헛침 1회당 보스가 되찾는 체간</b> (Docs/PROGRESSION.md §2.5 — 실패 처벌).
        /// 0이면 옛 동작(헛쳐도 잠금뿐).
        ///
        /// <para>헛침의 비용이 0.35초 잠금뿐이면 <b>막 눌러도 손해가 없다</b> — 패링이 읽기가 아니라
        /// 연타 게임이 된다. 되찾는 양은 <see cref="ParryPostureGain"/>과 나란히 읽을 것:
        /// 25 상승에 8 회복이면 헛침 하나가 패링 3분의 1을 무른다.</para>
        /// </summary>
        public float WhiffPostureRecovery;

        /// <summary>true면 보스 HP 비율에 비례해 체간 회복이 느려진다 (HP가 낮을수록 무너지기 쉬움, GDD §5.2).</summary>
        public bool ScaleDecayWithHp = true;

        /// <summary>전투 시작 후 첫 패턴까지의 대기 — 유저가 화면 전환에 적응할 시간.</summary>
        public float FirstAttackDelaySeconds = 1.5f;

        /// <summary>패턴 사이의 숨 고르기 (초). 0이면 쉼 없이 이어진다.</summary>
        public float PatternGapSeconds = 0.6f;

        /// <summary>
        /// HP 구간별 패턴 풀 (GDD §5.2). 보스는 이 패턴들을 조합해 승부한다.
        /// <b>인살이 한 번뿐인 보스</b>는 이것만 채우면 된다 — <see cref="BattlePhases"/>가 비면
        /// 위의 HP·체간과 이 풀로 1페이즈짜리 보스를 자동으로 만든다.
        /// </summary>
        public IReadOnlyList<BossPhaseConfig> Phases = Array.Empty<BossPhaseConfig>();

        /// <summary>
        /// 인살 페이즈 목록 — <b>인살 몇 번으로 끝나는 보스인가</b>. 비우면 위 값으로 1페이즈.
        /// 페이즈마다 HP·체간·채보 풀이 통째로 갈리고, 넘어갈 때 컷씬이 낀다.
        /// </summary>
        public IReadOnlyList<BossBattlePhase> BattlePhases = Array.Empty<BossBattlePhase>();

        /// <summary>
        /// 실제로 싸울 페이즈 목록. 인살 페이즈를 안 짠 보스(대부분)를 위해 1페이즈짜리로 감싸 준다 —
        /// <see cref="CombatSystem"/>이 "페이즈가 있는 보스"와 "없는 보스"를 나눠 처리하지 않게 하기 위한 것이다.
        /// 나누기 시작하면 두 갈래가 서로 다르게 굳는다.
        /// </summary>
        public IReadOnlyList<BossBattlePhase> ResolveBattlePhases()
        {
            if (BattlePhases != null && BattlePhases.Count > 0)
                return BattlePhases;

            return new[] { new BossBattlePhase(MaxHp, MaxPosture, Phases) };
        }
    }
}
