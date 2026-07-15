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

        /// <summary>전투 시작 후 첫 텔레그래프까지의 대기 — 유저가 화면 전환에 적응할 시간.</summary>
        public float FirstAttackDelaySeconds = 1.5f;

        /// <summary>공격 시퀀스. 끝까지 수행하면 처음부터 반복한다.</summary>
        public IReadOnlyList<BossAttackConfig> Pattern = Array.Empty<BossAttackConfig>();
    }
}
