using System.Collections.Generic;

namespace ChainRiposte.Core.Combat
{
    /// <summary>
    /// 보스 config에 <see cref="DifficultyConfig"/>를 곱해 <b>새 config</b>를 만든다
    /// (<c>Docs/PROGRESSION.md</c> §2.5). 원본은 안 건드린다 — <c>BossDataSO</c>가 만들어 준 config를
    /// 제자리에서 고치면 같은 에셋을 쓰는 다음 판이 이미 부푼 값을 또 부풀린다.
    ///
    /// <para><b>무엇을 곱하고 무엇을 안 곱하나</b>: 곱하는 것은 "이 보스가 얼마나 큰 놈이냐"뿐이다 —
    /// HP·체간 한계치·노트 피해·템포. 체간 경제의 절대값(패링 상승량·자연 회복·헛침 처벌)은
    /// <b>보스 에셋이 계속 주인</b>이다. 상승량까지 같이 부풀리면 체간 배수가 아무 일도 안 한 것이 되고,
    /// 회복만 부풀리면 배수 하나가 두 번 먹혀 예측이 안 된다.</para>
    /// </summary>
    public static class BossConfigScaler
    {
        public static BossConfig Scale(BossConfig source, DifficultyConfig difficulty)
        {
            if (source == null || difficulty == null || difficulty.IsIdentity)
                return source;

            // 패턴·풀은 여러 페이즈가 같은 인스턴스를 공유한다(공용 채보 풀). 한 번 만든 것을 재사용해
            // 원본의 공유 구조를 그대로 옮긴다 — 안 그러면 같은 채보가 페이즈 수만큼 복제된다.
            var patterns = new Dictionary<BossPatternConfig, BossPatternConfig>();
            var hpPhases = new Dictionary<BossPhaseConfig, BossPhaseConfig>();

            return new BossConfig
            {
                Name = source.Name,
                MaxHp = source.MaxHp * difficulty.Hp,
                MaxPosture = source.MaxPosture * difficulty.Posture,

                ParryPostureGain = source.ParryPostureGain,
                AttackPostureFactor = source.AttackPostureFactor,
                PostureDecayPerSecond = source.PostureDecayPerSecond,
                WhiffPostureRecovery = source.WhiffPostureRecovery,
                ScaleDecayWithHp = source.ScaleDecayWithHp,
                FirstAttackDelaySeconds = source.FirstAttackDelaySeconds,
                PatternGapSeconds = source.PatternGapSeconds,

                Phases = ScalePhases(source.Phases, difficulty, patterns, hpPhases),
                BattlePhases = ScaleBattlePhases(source.BattlePhases, difficulty, patterns, hpPhases),
            };
        }

        private static IReadOnlyList<BossBattlePhase> ScaleBattlePhases(
            IReadOnlyList<BossBattlePhase> source,
            DifficultyConfig difficulty,
            Dictionary<BossPatternConfig, BossPatternConfig> patterns,
            Dictionary<BossPhaseConfig, BossPhaseConfig> hpPhases)
        {
            if (source == null || source.Count == 0)
                return source;

            var result = new List<BossBattlePhase>(source.Count);
            foreach (BossBattlePhase phase in source)
            {
                result.Add(new BossBattlePhase(
                    phase.MaxHp * difficulty.Hp,
                    phase.MaxPosture * difficulty.Posture,
                    ScalePhases(phase.HpPhases, difficulty, patterns, hpPhases)));
            }

            return result;
        }

        private static IReadOnlyList<BossPhaseConfig> ScalePhases(
            IReadOnlyList<BossPhaseConfig> source,
            DifficultyConfig difficulty,
            Dictionary<BossPatternConfig, BossPatternConfig> patterns,
            Dictionary<BossPhaseConfig, BossPhaseConfig> hpPhases)
        {
            if (source == null || source.Count == 0)
                return source;

            var result = new List<BossPhaseConfig>(source.Count);
            foreach (BossPhaseConfig phase in source)
            {
                if (hpPhases.TryGetValue(phase, out BossPhaseConfig cached))
                {
                    result.Add(cached);
                    continue;
                }

                var weighted = new List<WeightedPattern>(phase.Patterns.Count);
                foreach (WeightedPattern entry in phase.Patterns)
                    weighted.Add(new WeightedPattern(ScalePattern(entry.Pattern, difficulty, patterns), entry.Weight));

                var scaled = new BossPhaseConfig(phase.HpRatioAtOrBelow, weighted);
                hpPhases[phase] = scaled;
                result.Add(scaled);
            }

            return result;
        }

        /// <summary>
        /// 템포는 <see cref="BossPatternConfig.SpeedMultiplier"/>에 곱한다. BPM은 <b>찍은 사람이 적은 값</b>이라
        /// 그대로 둬야 에디터에서 본 숫자와 로그가 계속 맞는다(체감 속도는 어차피 둘의 곱이다).
        /// </summary>
        private static BossPatternConfig ScalePattern(
            BossPatternConfig source,
            DifficultyConfig difficulty,
            Dictionary<BossPatternConfig, BossPatternConfig> cache)
        {
            if (cache.TryGetValue(source, out BossPatternConfig cached))
                return cached;

            var notes = new List<BossNoteConfig>(source.Notes.Count);
            foreach (BossNoteConfig note in source.Notes)
            {
                notes.Add(new BossNoteConfig(
                    note.Beat, note.TelegraphBeats, note.Damage * difficulty.Damage, note.SpeedMultiplier));
            }

            var scaled = new BossPatternConfig(
                source.Name, source.Bpm, source.LengthBeats, notes, source.SpeedMultiplier * difficulty.Tempo);

            cache[source] = scaled;
            return scaled;
        }
    }
}
