using System;
using System.Collections.Generic;

namespace ChainRiposte.Core.Combat
{
    /// <summary>가중치가 붙은 패턴 하나. 가중치가 클수록 자주 뽑힌다.</summary>
    public sealed class WeightedPattern
    {
        public BossPatternConfig Pattern { get; }
        public float Weight { get; }

        public WeightedPattern(BossPatternConfig pattern, float weight = 1f)
        {
            Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
            Weight = Math.Max(0f, weight);
        }
    }

    /// <summary>
    /// 보스의 한 페이즈 — HP 구간마다 쓸 수 있는 패턴 풀 (GDD §5.2).
    /// 체력이 깎일수록 험한 패턴 풀로 넘어가고, 같은 페이즈 안에서는 가중 무작위로 뽑아
    /// 순서를 외우는 게임이 되지 않게 한다.
    /// </summary>
    public sealed class BossPhaseConfig
    {
        /// <summary>보스 HP 비율이 이 값 <b>이하</b>일 때 이 페이즈가 활성화된다. 시작 페이즈는 1.0.</summary>
        public float HpRatioAtOrBelow { get; }

        public IReadOnlyList<WeightedPattern> Patterns { get; }

        public BossPhaseConfig(float hpRatioAtOrBelow, IReadOnlyList<WeightedPattern> patterns)
        {
            if (patterns == null || patterns.Count == 0)
                throw new ArgumentException("페이즈에 패턴이 하나도 없습니다.", nameof(patterns));

            HpRatioAtOrBelow = Math.Clamp(hpRatioAtOrBelow, 0f, 1f);
            Patterns = patterns;
        }

        public float TotalWeight
        {
            get
            {
                float sum = 0f;
                foreach (WeightedPattern entry in Patterns)
                    sum += entry.Weight;
                return sum;
            }
        }
    }
}
