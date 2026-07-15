using System;

namespace ChainRiposte.Core.Combat
{
    /// <summary>
    /// 보스 공격 패턴의 한 수. BossData SO에서 시퀀스로 정의되며,
    /// 텔레그래프 길이를 다르게 섞어 정박/엇박 리듬을 데이터로 설계한다 (GDD §5.2).
    /// </summary>
    public sealed class BossAttackConfig
    {
        /// <summary>텔레그래프(예비 동작) 길이 — 이 시간이 끝나는 순간 타격이 들어온다.</summary>
        public float TelegraphSeconds { get; }

        /// <summary>패링 실패/미입력 시 입는 피해 (DEF 적용 전).</summary>
        public float Damage { get; }

        /// <summary>false면 패링 윈도우 안이어도 튕겨낼 수 없다 (반드시 맞는 압박 수).</summary>
        public bool Parryable { get; }

        /// <summary>타격 후 다음 공격 텔레그래프까지의 후딜레이.</summary>
        public float RecoverySeconds { get; }

        public BossAttackConfig(float telegraphSeconds, float damage, bool parryable, float recoverySeconds)
        {
            if (telegraphSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(telegraphSeconds));
            if (recoverySeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(recoverySeconds));

            TelegraphSeconds = telegraphSeconds;
            Damage = damage;
            Parryable = parryable;
            RecoverySeconds = recoverySeconds;
        }
    }
}
