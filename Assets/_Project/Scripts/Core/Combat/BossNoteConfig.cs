using System;

namespace ChainRiposte.Core.Combat
{
    /// <summary>
    /// 채보의 노트 하나 = 보스의 공격 하나 (GDD §5.2).
    ///
    /// <b>연속기는 별도 타입이 아니다</b> — 노트를 촘촘히 찍으면 그게 연속기다.
    /// "크게 감았다가 3연타"도 첫 노트만 예비동작을 길게 주면 그대로 표현된다.
    /// 그래서 노트가 가진 것은 "언제 때리나"와 "예비동작이 얼마나 긴가" 둘뿐이다.
    ///
    /// 시간은 전부 <b>박(beat)</b> 단위다. BPM을 올리면 예비동작도 같이 짧아져 난이도가 균일하게 오른다.
    /// </summary>
    public sealed class BossNoteConfig
    {
        /// <summary>타격 시점 (패턴 시작 기준 박). 1박 = 정박, 3.5박 = 3박과 4박 사이 엇박.</summary>
        public float Beat { get; }

        /// <summary>예비동작 길이 (박). 길수록 읽기 쉽다.</summary>
        public float TelegraphBeats { get; }

        /// <summary>
        /// 이 노트만의 속도 배율. 1보다 크면 예비동작이 짧아진다 —
        /// "다른 건 그대로인데 이 찌르기만 유독 빠르게"를 만들 때 쓴다.
        /// </summary>
        public float SpeedMultiplier { get; }

        /// <summary>패링 실패 시 피해 (DEF 적용 전).</summary>
        public float Damage { get; }

        /// <summary>배율까지 반영한 실제 예비동작 길이 (박).</summary>
        public float EffectiveTelegraphBeats => TelegraphBeats / SpeedMultiplier;

        /// <summary>예비동작이 시작되는 박. 0보다 작을 수 있다(패턴 시작 전부터 감는 노트).</summary>
        public float TelegraphStartBeat => Beat - EffectiveTelegraphBeats;

        public BossNoteConfig(float beat, float telegraphBeats, float damage, float speedMultiplier = 1f)
        {
            if (beat < 0f)
                throw new ArgumentOutOfRangeException(nameof(beat), "타격 박은 0 이상이어야 합니다.");
            if (telegraphBeats <= 0f)
                throw new ArgumentOutOfRangeException(nameof(telegraphBeats), "예비동작은 0보다 길어야 합니다.");
            if (speedMultiplier <= 0f)
                throw new ArgumentOutOfRangeException(nameof(speedMultiplier), "속도 배율은 0보다 커야 합니다.");
            if (damage < 0f)
                throw new ArgumentOutOfRangeException(nameof(damage), "피해는 음수일 수 없습니다.");

            Beat = beat;
            TelegraphBeats = telegraphBeats;
            SpeedMultiplier = speedMultiplier;
            Damage = damage;
        }

        public override string ToString() =>
            $"{Beat:0.##}박 (예비 {EffectiveTelegraphBeats:0.##}박, 피해 {Damage:0})";
    }
}
