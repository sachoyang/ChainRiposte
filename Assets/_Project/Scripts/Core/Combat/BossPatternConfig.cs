using System;
using System.Collections.Generic;

namespace ChainRiposte.Core.Combat
{
    /// <summary>
    /// 보스 패턴 하나 = 미니 리듬게임 한 마디 (GDD §5.2).
    /// 정해진 길이(기본 8박) 동안 노트들이 흐르고, 끝나면 다음 패턴을 뽑는다.
    /// <b>노트가 없는 박은 빈 박</b>이며 플레이어의 공격 기회다.
    /// </summary>
    public sealed class BossPatternConfig
    {
        /// <summary>에디터·디버그 표시용 이름.</summary>
        public string Name { get; }

        /// <summary>분당 박 수. 이 하나로 패턴 전체의 속도가 정해진다.</summary>
        public float Bpm { get; }

        /// <summary>패턴 길이 (박). 기본 8박.</summary>
        public float LengthBeats { get; }

        /// <summary>패턴 전체 속도 배율 — 같은 채보를 후반 페이즈에서 1.3배로 재탕할 때 쓴다.</summary>
        public float SpeedMultiplier { get; }

        public IReadOnlyList<BossNoteConfig> Notes { get; }

        /// <summary>한 박의 실제 길이(초). 배율이 반영된 값이다.</summary>
        public float SecondsPerBeat => 60f / (Bpm * SpeedMultiplier);

        /// <summary>패턴 본체의 길이(초). 리드인은 포함하지 않는다.</summary>
        public float DurationSeconds => LengthBeats * SecondsPerBeat;

        /// <summary>
        /// 첫 노트의 예비동작이 0박보다 앞서 시작해야 할 때 필요한 준비 시간(초, 0 이상).
        /// 이걸 앞에 붙여 주므로 1박에 긴 예비동작을 둬도 잘리지 않는다.
        /// </summary>
        public float LeadInSeconds { get; }

        public BossPatternConfig(
            string name, float bpm, float lengthBeats, IReadOnlyList<BossNoteConfig> notes, float speedMultiplier = 1f)
        {
            if (bpm <= 0f)
                throw new ArgumentOutOfRangeException(nameof(bpm), "BPM은 0보다 커야 합니다.");
            if (lengthBeats <= 0f)
                throw new ArgumentOutOfRangeException(nameof(lengthBeats), "패턴 길이는 0보다 커야 합니다.");
            if (speedMultiplier <= 0f)
                throw new ArgumentOutOfRangeException(nameof(speedMultiplier), "속도 배율은 0보다 커야 합니다.");

            Name = string.IsNullOrWhiteSpace(name) ? "Pattern" : name;
            Bpm = bpm;
            LengthBeats = lengthBeats;
            SpeedMultiplier = speedMultiplier;

            var sorted = new List<BossNoteConfig>(notes ?? Array.Empty<BossNoteConfig>());
            sorted.Sort((a, b) => a.Beat.CompareTo(b.Beat)); // 타격 순서대로 — 엔진이 앞에서부터 훑는다
            Notes = sorted;

            float earliest = 0f;
            foreach (BossNoteConfig note in sorted)
                earliest = Math.Min(earliest, note.TelegraphStartBeat);
            LeadInSeconds = -earliest * SecondsPerBeat;
        }

        public override string ToString() =>
            $"{Name} ({Bpm:0}BPM x{SpeedMultiplier:0.##}, {LengthBeats:0.##}박, 노트 {Notes.Count}개)";
    }
}
