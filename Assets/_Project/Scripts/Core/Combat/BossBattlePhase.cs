using System;
using System.Collections.Generic;

namespace ChainRiposte.Core.Combat
{
    /// <summary>
    /// <b>인살 한 번 분량</b>의 보스. 체간을 무너뜨려 인살하면 이 페이즈가 끝난다 —
    /// 다음 페이즈가 남아 있으면 컷씬을 거쳐 HP·체간이 <b>만땅으로 새로</b> 시작하고,
    /// 없으면 그것이 승리다. 인살 마크(◆◆)의 개수가 곧 이 목록의 길이다.
    ///
    /// <para><see cref="BossPhaseConfig"/>와 헷갈리지 말 것. 그쪽은 <b>HP 구간별 채보 풀</b>이고
    /// 이 페이즈 <b>안에서</b> 돈다. 즉 2층이다:
    /// 인살 페이즈(겉모습·게이지가 통째로 바뀜) ▸ HP 구간 페이즈(같은 모습으로 채보만 험해짐).</para>
    ///
    /// <para>HP·체간을 페이즈마다 따로 두는 이유: 1페이즈를 짧게 끊고 2페이즈를 무겁게 부는 식의
    /// 완급을 데이터로 잡기 위해서다. 공유하면 같은 보스를 두 번 싸우는 것에 그친다.</para>
    /// </summary>
    public sealed class BossBattlePhase
    {
        public float MaxHp { get; }

        /// <summary>체간 한계치 — 도달 시 인살 가능.</summary>
        public float MaxPosture { get; }

        /// <summary>이 페이즈 안에서 도는 HP 구간별 채보 풀.</summary>
        public IReadOnlyList<BossPhaseConfig> HpPhases { get; }

        public BossBattlePhase(float maxHp, float maxPosture, IReadOnlyList<BossPhaseConfig> hpPhases)
        {
            if (maxHp <= 0f || maxPosture <= 0f)
                throw new ArgumentException("보스 HP/체간 한계치는 0보다 커야 합니다.");
            if (hpPhases == null || hpPhases.Count == 0)
                throw new ArgumentException("인살 페이즈에 채보 풀이 하나도 없습니다.", nameof(hpPhases));

            MaxHp = maxHp;
            MaxPosture = maxPosture;
            HpPhases = hpPhases;
        }

        /// <summary>현재 HP 비율에 해당하는 채보 풀. 조건을 만족하는 것 중 <b>가장 진행된</b> 것을 쓴다.</summary>
        public BossPhaseConfig ResolveHpPhase(float hpRatio)
        {
            BossPhaseConfig best = null;
            foreach (BossPhaseConfig phase in HpPhases)
            {
                if (hpRatio > phase.HpRatioAtOrBelow)
                    continue;
                if (best == null || phase.HpRatioAtOrBelow < best.HpRatioAtOrBelow)
                    best = phase;
            }

            // 전부 조건에 안 맞으면(임계치를 낮게만 잡은 설정) 가장 너그러운 풀로 떨어진다
            if (best != null)
                return best;

            foreach (BossPhaseConfig phase in HpPhases)
                if (best == null || phase.HpRatioAtOrBelow > best.HpRatioAtOrBelow)
                    best = phase;

            return best;
        }
    }
}
