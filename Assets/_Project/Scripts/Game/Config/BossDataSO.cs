using System;
using System.Collections.Generic;
using ChainRiposte.Core.Combat;
using UnityEngine;

namespace ChainRiposte.Game.Config
{
    /// <summary>
    /// 보스 전투 밸런스 + 채보 (GDD §5.2).
    ///
    /// 패턴 하나가 미니 리듬게임 한 마디다. 보스는 HP 페이즈별 풀에서 패턴을 뽑아 조합한다.
    /// <b>연속기는 별도 타입이 아니라 촘촘히 찍은 노트</b>이며, 노트가 없는 박은 플레이어의 공격 기회다.
    /// </summary>
    [CreateAssetMenu(menuName = "ChainRiposte/Boss Data", fileName = "Boss_")]
    public sealed class BossDataSO : ScriptableObject
    {
        [Header("표시")]
        [Tooltip("테마가 겉모습을 갈아 끼울 때 쓰는 키. 비우면 에셋 이름. 한 번 정하면 바꾸지 말 것.")]
        [SerializeField] private string bossId;
        [Tooltip("이름의 현지화 키(CSV). 비우면 아래 Display Name을 그대로 쓴다.")]
        [SerializeField] private string nameKey;
        [SerializeField] private string displayName = "Boss";
        [Tooltip("월드맵 정보 패널에 띄울 보스 이미지. 아직 안 가본 스테이지에서는 검은 실루엣으로 나온다.")]
        [SerializeField] private Sprite portrait;
        [Tooltip("전투 화면에 서는 보스 이미지. 비우면 위 초상을 그대로 쓴다.")]
        [SerializeField] private Sprite battleSprite;

        [Header("생존/체간")]
        [SerializeField, Min(1f)] private float maxHp = 120f;
        [Tooltip("체간 한계치 — 도달 시 인살 가능")]
        [SerializeField, Min(1f)] private float maxPosture = 100f;

        [Header("체간 상승/회복")]
        [Tooltip("패링 성공 1회당 체간 상승량 (대폭)")]
        [SerializeField, Min(0f)] private float parryPostureGain = 25f;
        [Tooltip("공격 적중 시 체간 상승량 = ATK × 이 배율 (소폭)")]
        [SerializeField, Min(0f)] private float attackPostureFactor = 0.5f;
        [Tooltip("체간 자연 회복 속도 (초당). 0이면 회복 없음")]
        [SerializeField, Min(0f)] private float postureDecayPerSecond = 6f;
        [Tooltip("체크 시 보스 HP 비율에 비례해 체간 회복이 느려진다 (HP가 낮을수록 무너지기 쉬움)")]
        [SerializeField] private bool scaleDecayWithHp = true;

        [Header("템포")]
        [Tooltip("전투 시작 후 첫 패턴까지의 대기")]
        [SerializeField, Min(0f)] private float firstAttackDelaySeconds = 1.5f;
        [Tooltip("패턴 사이 숨 고르기. 0이면 쉼 없이 이어진다.")]
        [SerializeField, Min(0f)] private float patternGapSeconds = 0.6f;

        [Header("채보")]
        [SerializeField] private PatternEntry[] patterns = Array.Empty<PatternEntry>();
        [Tooltip("HP 구간별 패턴 풀. 비우면 모든 패턴을 균등하게 쓰는 단일 페이즈로 동작한다.")]
        [SerializeField] private PhaseEntry[] phases = Array.Empty<PhaseEntry>();

        [Serializable]
        private sealed class NoteEntry
        {
            [Tooltip("타격 시점 (패턴 시작 기준 박). 3.5 = 엇박")]
            [Min(0f)] public float beat;
            [Tooltip("예비동작 길이 (박). 이게 곧 플레이어의 준비 시간이자 원이 나타나는 거리다")]
            [Min(0.05f)] public float telegraphBeats = 1.5f;
            [Tooltip("이 노트만의 속도 배율. 1보다 크면 예비동작이 짧아진다")]
            [Min(0.05f)] public float speedMultiplier = 1f;
            [Tooltip("패링 실패 시 피해 (DEF 적용 전)")]
            [Min(0f)] public float damage = 12f;
        }

        [Serializable]
        private sealed class PatternEntry
        {
            public string name = "Pattern";
            [Min(20f)] public float bpm = 120f;
            [Tooltip("패턴 길이 (박). 기본 8박")]
            [Min(1f)] public float lengthBeats = 8f;
            [Tooltip("패턴 전체 속도 배율 — 같은 채보를 후반에 1.3배로 재탕할 때")]
            [Min(0.1f)] public float speedMultiplier = 1f;
            public NoteEntry[] notes = Array.Empty<NoteEntry>();
        }

        [Serializable]
        private sealed class PatternWeight
        {
            [Tooltip("위 patterns 배열의 인덱스")]
            [Min(0)] public int patternIndex;
            [Min(0f)] public float weight = 1f;
        }

        [Serializable]
        private sealed class PhaseEntry
        {
            [Tooltip("보스 HP 비율이 이 값 이하일 때 이 페이즈를 쓴다. 시작 페이즈는 1")]
            [Range(0f, 1f)] public float hpRatioAtOrBelow = 1f;
            public PatternWeight[] patterns = Array.Empty<PatternWeight>();
        }

        /// <summary>테마의 보스 항목이 찾아올 키. StageId·CharacterId와 같은 규칙 — 비우면 에셋 이름.</summary>
        public string BossId => string.IsNullOrWhiteSpace(bossId) ? name : bossId;

        /// <summary>이름 문구. 현지화 키를 걸었으면 그것, 아니면 생 문자열(구 데이터 호환).</summary>
        public string NameKey => string.IsNullOrWhiteSpace(nameKey) ? displayName : nameKey;

        public string DisplayName => displayName;

        /// <summary>월드맵 표시용 초상. 없으면 컨트롤러가 이미지를 숨긴다.</summary>
        public Sprite Portrait => portrait;

        /// <summary>전투 화면의 보스 본체. 전용 이미지가 없으면 초상으로 대체한다.</summary>
        public Sprite BattleSprite => battleSprite != null ? battleSprite : portrait;

        public BossConfig ToConfig()
        {
            List<BossPatternConfig> built = BuildPatterns();

            return new BossConfig
            {
                Name = displayName,
                MaxHp = maxHp,
                MaxPosture = maxPosture,
                ParryPostureGain = parryPostureGain,
                AttackPostureFactor = attackPostureFactor,
                PostureDecayPerSecond = postureDecayPerSecond,
                ScaleDecayWithHp = scaleDecayWithHp,
                FirstAttackDelaySeconds = firstAttackDelaySeconds,
                PatternGapSeconds = patternGapSeconds,
                Phases = BuildPhases(built),
            };
        }

        private List<BossPatternConfig> BuildPatterns()
        {
            var built = new List<BossPatternConfig>();
            foreach (PatternEntry entry in patterns)
            {
                if (entry == null)
                    continue;

                var notes = new List<BossNoteConfig>();
                foreach (NoteEntry note in entry.notes ?? Array.Empty<NoteEntry>())
                {
                    if (note == null)
                        continue;
                    notes.Add(new BossNoteConfig(
                        note.beat, note.telegraphBeats, note.damage, note.speedMultiplier));
                }

                if (notes.Count == 0)
                    continue; // 노트가 하나도 없는 패턴은 무한 빈 박이 되므로 건너뛴다

                built.Add(new BossPatternConfig(
                    entry.name, entry.bpm, entry.lengthBeats, notes, entry.speedMultiplier));
            }

            if (built.Count > 0)
                return built;

            Debug.LogError(
                $"{name}: 채보가 비어 있습니다. 임시 패턴으로 대체합니다 — " +
                "인스펙터의 Patterns에 노트를 찍어 주세요.", this);
            built.Add(PlaceholderPattern());
            return built;
        }

        /// <summary>채보를 아직 안 찍은 보스도 플레이는 되게 하는 임시 패턴 (정박 4타).</summary>
        private static BossPatternConfig PlaceholderPattern()
        {
            var notes = new List<BossNoteConfig>();
            for (int beat = 1; beat <= 7; beat += 2)
                notes.Add(new BossNoteConfig(beat, telegraphBeats: 1f, damage: 12f));

            return new BossPatternConfig("Placeholder", 120f, 8f, notes);
        }

        private List<BossPhaseConfig> BuildPhases(List<BossPatternConfig> built)
        {
            var result = new List<BossPhaseConfig>();

            foreach (PhaseEntry phase in phases)
            {
                if (phase == null)
                    continue;

                var weighted = new List<WeightedPattern>();
                foreach (PatternWeight reference in phase.patterns ?? Array.Empty<PatternWeight>())
                {
                    if (reference == null || reference.patternIndex < 0 || reference.patternIndex >= built.Count)
                        continue;
                    weighted.Add(new WeightedPattern(built[reference.patternIndex], reference.weight));
                }

                if (weighted.Count > 0)
                    result.Add(new BossPhaseConfig(phase.hpRatioAtOrBelow, weighted));
            }

            if (result.Count > 0)
                return result;

            // 페이즈를 안 짰으면 모든 패턴을 균등하게 쓰는 단일 페이즈로 둔다
            var all = new List<WeightedPattern>();
            foreach (BossPatternConfig pattern in built)
                all.Add(new WeightedPattern(pattern));

            result.Add(new BossPhaseConfig(1f, all));
            return result;
        }
    }
}
