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

        /// <summary>true면 보스 HP 비율에 비례해 체간 회복이 느려진다 (HP가 낮을수록 무너지기 쉬움, GDD §5.2).</summary>
        public bool ScaleDecayWithHp = true;

        /// <summary>전투 시작 후 첫 패턴까지의 대기 — 유저가 화면 전환에 적응할 시간.</summary>
        public float FirstAttackDelaySeconds = 1.5f;

        /// <summary>패턴 사이의 숨 고르기 (초). 0이면 쉼 없이 이어진다.</summary>
        public float PatternGapSeconds = 0.6f;

        /// <summary>
        /// HP 구간별 패턴 풀 (GDD §5.2). 보스는 이 패턴들을 조합해 승부한다.
        /// 비어 있으면 안 된다 — CombatSystem이 생성 시 거부한다.
        /// </summary>
        public IReadOnlyList<BossPhaseConfig> Phases = Array.Empty<BossPhaseConfig>();

        /// <summary>현재 HP 비율에 해당하는 페이즈. 조건을 만족하는 것 중 <b>가장 진행된</b> 페이즈를 쓴다.</summary>
        public BossPhaseConfig ResolvePhase(float hpRatio)
        {
            BossPhaseConfig best = null;
            foreach (BossPhaseConfig phase in Phases)
            {
                if (hpRatio > phase.HpRatioAtOrBelow)
                    continue;
                if (best == null || phase.HpRatioAtOrBelow < best.HpRatioAtOrBelow)
                    best = phase;
            }

            // 전부 조건에 안 맞으면(임계치를 낮게만 잡은 설정) 가장 너그러운 페이즈로 떨어진다
            if (best != null)
                return best;

            foreach (BossPhaseConfig phase in Phases)
                if (best == null || phase.HpRatioAtOrBelow > best.HpRatioAtOrBelow)
                    best = phase;

            return best;
        }
    }
}
