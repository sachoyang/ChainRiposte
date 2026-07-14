using System;

namespace ChainRiposte.Core.Stats
{
    /// <summary>
    /// 플레이어 HP. 퍼즐 페이즈(물약 회복, 기믹 피해)와 전투 페이즈(피격)가 공유한다.
    /// 방어력에 의한 피해 감산은 전투 로직이 계산한 뒤 최종값만 전달한다.
    /// </summary>
    public sealed class PlayerHealth
    {
        public int Max { get; }
        public int Current { get; private set; }
        public bool IsDead => Current <= 0;

        /// <summary>(현재 HP, 최대 HP)</summary>
        public event Action<int, int> Changed;

        public PlayerHealth(int max)
        {
            if (max <= 0)
                throw new ArgumentOutOfRangeException(nameof(max));

            Max = max;
            Current = max;
        }

        public void Heal(int amount)
        {
            if (amount <= 0 || Current == Max)
                return;

            Current = Math.Min(Max, Current + amount);
            Changed?.Invoke(Current, Max);
        }

        /// <summary>피해를 적용한다. 사망하면 true.</summary>
        public bool ApplyDamage(int amount)
        {
            if (amount <= 0)
                return IsDead;

            Current = Math.Max(0, Current - amount);
            Changed?.Invoke(Current, Max);
            return IsDead;
        }

        /// <summary>기습 돌입 페널티 등 비율 기반 설정 (0~1).</summary>
        public void SetFraction(float fraction)
        {
            Current = Math.Clamp((int)Math.Round(Max * fraction), 0, Max);
            Changed?.Invoke(Current, Max);
        }
    }
}
