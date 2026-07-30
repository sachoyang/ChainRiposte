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
        // 이름의 원천은 아래 「캐릭터별 겉모습」의 nameKey 하나뿐이다(그림과 같은 규칙).
        // 공용 displayName/nameKey 슬롯을 따로 두면 같은 이름을 두 곳에 적게 되고, 실제로 지도만
        // 개발용 생 이름("Mushroom King")을 띄우고 전투는 다른 이름을 띄우는 사고가 났다.

        [Header("겉모습 — 캐릭터마다 다르게 보이는 같은 보스")]
        [Tooltip("고른 캐릭터에 해당하는 줄이 있으면 그 그림·이름을 쓰고, 없으면 <b>맨 위 줄(0번)</b>이 기본값이다. " +
                 "그림의 원천은 이 목록 하나뿐 — 따로 공용 슬롯을 두면 어느 쪽을 채웠는지가 매번 헷갈린다. " +
                 "HP·체간·채보는 아래 수치 하나를 모두가 공유하므로 난이도는 절대 갈리지 않는다.")]
        [SerializeField] private CharacterVisual[] characterVisuals = Array.Empty<CharacterVisual>();

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
        [Tooltip("헛침 1회당 보스가 되찾는 체간. 0이면 헛쳐도 잠금뿐이라 막 눌러도 손해가 없다 " +
                 "— 위의 '패링 성공 1회당 상승량'과 나란히 볼 것. 25 상승에 8이면 헛침 하나가 패링 3분의 1을 무른다.")]
        [SerializeField, Min(0f)] private float whiffPostureRecovery = 8f;
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

        [Header("기억 — 이 보스를 벤 자가 삼키는 것")]
        [Tooltip("인살로 삼키는 보스의 기억(Docs/PROGRESSION.md §2.2). 비우면 이 보스는 소울만 준다.\n" +
                 "기억 1개 = 보스 1개다 — 같은 보스를 쓰는 스테이지가 여럿이라 두 번째 판부터는 소울만 번다.")]
        [SerializeField] private BossMemorySO memory;

        [Header("인살 페이즈 — 비우면 인살 한 번으로 끝나는 보통 보스")]
        [Tooltip("인살 몇 번으로 눕는 보스인가. 두 줄이면 인살 마크가 ◆◆ 로 뜨고, " +
                 "1페이즈를 인살하면 컷씬을 거쳐 HP·체간이 만땅으로 새로 시작한다. " +
                 "겉모습·채보·수치가 페이즈마다 통째로 갈린다.")]
        [SerializeField] private BattlePhaseEntry[] battlePhases = Array.Empty<BattlePhaseEntry>();

        /// <summary>
        /// 한 캐릭터로 이 보스를 만났을 때의 <b>겉모습</b>. 그림과 이름이 한 줄에 같이 있어야
        /// "이 캐릭터에겐 이렇게 생긴 누구"가 한눈에 읽히고, 원천이 둘로 갈리지 않는다.
        /// </summary>
        [Serializable]
        public sealed class CharacterVisual
        {
            [Tooltip("이 겉모습을 쓸 캐릭터. 맨 위 줄(0번)은 비워 둬도 된다 — " +
                     "어느 줄과도 안 맞는 캐릭터는 0번 줄을 기본값으로 쓴다.")]
            public Characters.PlayerCharacterSO character;
            [Tooltip("전투 화면에 서는 그림. 준비 화면의 그림자·처형 컷씬도 이 그림을 쓴다. " +
                     "비우면 아래 인살 페이즈의 그림 → 0번 줄 순으로 떨어진다.")]
            public Sprite battleSprite;
            [Tooltip("이름의 현지화 키(CSV). 비우면 0번 줄 → 위의 Name Key.")]
            public string nameKey;
            [Tooltip("인살 페이즈별 그림. 인살 페이즈를 쓰는 보스만 채운다 — 순서는 위 인살 페이즈와 같다. " +
                     "비거나 모자라면 바로 위 battleSprite 로 떨어진다(페이즈마다 그림을 안 나눈 보스).")]
            public PhaseVisual[] phaseVisuals = Array.Empty<PhaseVisual>();
        }

        /// <summary>
        /// 한 캐릭터로 만난 보스의 <b>한 페이즈</b> 겉모습. 아세프리트의 <c>phase1 / trans / phase2</c> 레이어가
        /// 그대로 여기로 온다 — 겹쳐 그리는 레이어가 아니라 <b>통째로 갈아 끼우는 그림</b>이다.
        /// </summary>
        [Serializable]
        public sealed class PhaseVisual
        {
            [Tooltip("이 페이즈에서 서 있는 그림")]
            public Sprite sprite;
            [Tooltip("이 페이즈로 넘어올 때 컷씬에 뜨는 그림(trans). 0번 페이즈는 넘어올 일이 없으므로 안 쓴다.")]
            public Sprite transitionSprite;
        }

        /// <summary>
        /// 인살 한 번 분량의 보스. <b>비운 칸은 공용 값으로 떨어진다</b> —
        /// 페이즈를 나눴다는 이유만으로 같은 숫자를 두 번 적게 하지 않기 위해서다.
        /// </summary>
        [Serializable]
        private sealed class BattlePhaseEntry
        {
            [Tooltip("인스펙터에서 알아보기 위한 이름. 게임에는 안 나온다.")]
            public string label = "Phase";
            [Tooltip("이 페이즈에서 서 있는 공용 그림. 비우면 위의 공용 Battle Sprite.")]
            public Sprite sprite;
            [Tooltip("이 페이즈로 넘어올 때 컷씬에 뜨는 공용 그림(trans).")]
            public Sprite transitionSprite;
            [Tooltip("전환 컷씬에 띄울 한 줄의 현지화 키(CSV). 비우면 문구 없이 그림만.")]
            public string transitionTextKey;
            [Tooltip("이 페이즈의 HP. 0이면 위의 공용 Max Hp.")]
            [Min(0f)] public float maxHp;
            [Tooltip("이 페이즈의 체간 한계치. 0이면 위의 공용 Max Posture.")]
            [Min(0f)] public float maxPosture;
            [Tooltip("이 페이즈 안에서 도는 HP 구간별 패턴 풀. 비우면 위의 공용 Phases. " +
                     "2페이즈를 더 험한 채보로 만들려면 여기에 따로 짠다.")]
            public PhaseEntry[] hpPhases = Array.Empty<PhaseEntry>();
        }

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

        /// <summary>보스를 가리키는 키. StageId·CharacterId와 같은 규칙 — 비우면 에셋 이름.</summary>
        public string BossId => string.IsNullOrWhiteSpace(bossId) ? name : bossId;

        /// <summary>인살 몇 번으로 눕는 보스인가. 인살 페이즈를 안 짰으면 1.</summary>
        public int BattlePhaseCount => battlePhases != null && battlePhases.Length > 0 ? battlePhases.Length : 1;

        /// <summary>이 보스를 벤 자가 삼키는 기억. 없으면 null(소울만 주는 보스).</summary>
        public BossMemorySO Memory => memory;

        /// <summary>
        /// 이 캐릭터로 만났을 때 <paramref name="phaseIndex"/> 페이즈의 그림. 아무 데도 없으면 null.
        ///
        /// <para>순서: <b>이 캐릭터 줄</b>(페이즈 그림 → 그 줄의 그림) → <b>페이즈의 공용 그림</b>
        /// → <b>0번 줄</b>(페이즈 그림 → 그 줄의 그림).</para>
        ///
        /// <para><b>캐릭터 지정이 페이즈 지정을 이긴다</b> — 페이즈를 안 나눈 캐릭터가 남의 페이즈 그림을 쓰면 안 된다.
        /// 그리고 <b>기본값은 0번 줄</b>이다 — 공용 그림 슬롯을 따로 두면 목록과 슬롯 중 어디를 채웠는지가
        /// 매번 헷갈리고, 결국 같은 그림을 두 곳에 적게 된다.</para>
        /// </summary>
        public Sprite GetBattleSprite(Characters.PlayerCharacterSO character, int phaseIndex = 0)
        {
            Sprite mine = SpriteIn(Find(character), phaseIndex);
            if (mine != null)
                return mine;

            BattlePhaseEntry entry = PhaseEntryAt(phaseIndex);
            if (entry != null && entry.sprite != null)
                return entry.sprite;

            return SpriteIn(DefaultVisual, phaseIndex);
        }

        private static Sprite SpriteIn(CharacterVisual visual, int phaseIndex)
        {
            if (visual == null)
                return null;

            PhaseVisual phase = PhaseVisualAt(visual, phaseIndex);
            return phase != null && phase.sprite != null ? phase.sprite : visual.battleSprite;
        }

        /// <summary>이 페이즈로 <b>넘어올 때</b> 컷씬에 뜨는 그림(trans). 없으면 null — 컷씬이 그림 없이 돈다.</summary>
        public Sprite GetTransitionSprite(Characters.PlayerCharacterSO character, int phaseIndex)
        {
            Sprite mine = TransitionIn(Find(character), phaseIndex);
            if (mine != null)
                return mine;

            BattlePhaseEntry entry = PhaseEntryAt(phaseIndex);
            if (entry != null && entry.transitionSprite != null)
                return entry.transitionSprite;

            return TransitionIn(DefaultVisual, phaseIndex);
        }

        private static Sprite TransitionIn(CharacterVisual visual, int phaseIndex)
        {
            PhaseVisual phase = visual != null ? PhaseVisualAt(visual, phaseIndex) : null;
            return phase != null ? phase.transitionSprite : null;
        }

        /// <summary>
        /// 어느 줄과도 안 맞는 캐릭터가 쓸 기본 겉모습 = <b>맨 위 줄</b>.
        /// 캐릭터를 새로 추가해도 보스가 투명해지지 않는다 — 새 캐릭터 줄을 안 채웠다는 이유로
        /// 보스가 화면에서 사라지는 것은 어떤 경우에도 옳지 않다.
        /// </summary>
        private CharacterVisual DefaultVisual =>
            characterVisuals != null && characterVisuals.Length > 0 ? characterVisuals[0] : null;

        /// <summary>전환 컷씬 한 줄의 현지화 키. 캐릭터와 무관하다 — 문구는 보스의 것이다.</summary>
        public string GetTransitionTextKey(int phaseIndex)
        {
            BattlePhaseEntry entry = PhaseEntryAt(phaseIndex);
            return entry != null && !string.IsNullOrWhiteSpace(entry.transitionTextKey) ? entry.transitionTextKey : null;
        }

        private BattlePhaseEntry PhaseEntryAt(int phaseIndex) =>
            battlePhases != null && phaseIndex >= 0 && phaseIndex < battlePhases.Length ? battlePhases[phaseIndex] : null;

        private static PhaseVisual PhaseVisualAt(CharacterVisual visual, int phaseIndex) =>
            visual.phaseVisuals != null && phaseIndex >= 0 && phaseIndex < visual.phaseVisuals.Length
                ? visual.phaseVisuals[phaseIndex]
                : null;

        /// <summary>
        /// 이 캐릭터로 만났을 때의 이름 키. 없으면 <b>0번 줄</b>, 그것도 없으면 <b>에셋 이름</b>.
        ///
        /// <para>키를 안 적은 보스가 에셋 이름("Boss_01")으로 뜨는 것은 의도다 —
        /// 화면에 키가 그대로 보이는 것이 곧 "번역 누락" 신호라는 이 프로젝트의 규칙과 같다.
        /// 빈 문자열로 두면 보스 이름 칸이 조용히 사라져서 빠뜨린 것을 알 수 없다.</para>
        /// </summary>
        public string GetNameKey(Characters.PlayerCharacterSO character)
        {
            CharacterVisual visual = Find(character);
            if (visual != null && !string.IsNullOrWhiteSpace(visual.nameKey))
                return visual.nameKey;

            CharacterVisual fallback = DefaultVisual;
            return fallback != null && !string.IsNullOrWhiteSpace(fallback.nameKey) ? fallback.nameKey : name;
        }

        private CharacterVisual Find(Characters.PlayerCharacterSO character)
        {
            if (character == null || characterVisuals == null)
                return null;

            foreach (CharacterVisual visual in characterVisuals)
            {
                if (visual != null && visual.character == character)
                    return visual;
            }

            return null;
        }

        /// <param name="battlePhaseLimit">
        /// 이 판에서 싸울 인살 페이즈 수 (0 = 전부). <b>같은 보스를 1페이즈로도 2페이즈로도 쓰기 위한 것</b> —
        /// 스테이지가 정한다(<c>StageDataSO.BattlePhaseLimit</c>). 이 손잡이가 없으면 페이즈 수만 다른
        /// 보스 에셋을 복제해야 하고, 그러면 그 보스의 채보·수치를 두 곳에 똑같이 적게 된다.
        /// </param>
        public BossConfig ToConfig(int battlePhaseLimit = 0)
        {
            List<BossPatternConfig> built = BuildPatterns();
            List<BossPhaseConfig> sharedHpPhases = BuildSharedHpPhases(built);

            return new BossConfig
            {
                BattlePhases = LimitPhases(BuildBattlePhases(built, sharedHpPhases), battlePhaseLimit),
                // 화면에 나오는 이름이 아니다 — 로그·디버그용이라 에셋 이름을 쓴다
                // (보이는 이름은 캐릭터별 목록의 nameKey → 현지화 CSV가 맡는다).
                Name = name,
                MaxHp = maxHp,
                MaxPosture = maxPosture,
                ParryPostureGain = parryPostureGain,
                AttackPostureFactor = attackPostureFactor,
                PostureDecayPerSecond = postureDecayPerSecond,
                WhiffPostureRecovery = whiffPostureRecovery,
                ScaleDecayWithHp = scaleDecayWithHp,
                FirstAttackDelaySeconds = firstAttackDelaySeconds,
                PatternGapSeconds = patternGapSeconds,
                Phases = sharedHpPhases,
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

        /// <summary>공용 HP 구간 풀. 안 짰으면 모든 패턴을 균등하게 쓰는 단일 풀로 둔다.</summary>
        private List<BossPhaseConfig> BuildSharedHpPhases(List<BossPatternConfig> built)
        {
            List<BossPhaseConfig> result = BuildHpPhases(built, phases);
            if (result.Count > 0)
                return result;

            var all = new List<WeightedPattern>();
            foreach (BossPatternConfig pattern in built)
                all.Add(new WeightedPattern(pattern));

            result.Add(new BossPhaseConfig(1f, all));
            return result;
        }

        /// <summary>HP 구간 풀 만들기. 안 짰으면 <b>빈 목록</b>을 돌려준다 — 부르는 쪽이 공용 풀로 떨어진다.</summary>
        private static List<BossPhaseConfig> BuildHpPhases(List<BossPatternConfig> built, PhaseEntry[] source)
        {
            var result = new List<BossPhaseConfig>();

            foreach (PhaseEntry phase in source ?? Array.Empty<PhaseEntry>())
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

            return result;
        }

        /// <summary>
        /// 인살 페이즈 목록. 안 짰으면 <b>빈 목록</b> — <see cref="BossConfig.ResolveBattlePhases"/>가
        /// 공용 값으로 1페이즈를 만든다. 여기서 미리 만들어 두면 "1페이즈 보스"의 규칙이 두 곳에 생긴다.
        /// </summary>
        private List<BossBattlePhase> BuildBattlePhases(
            List<BossPatternConfig> built, List<BossPhaseConfig> sharedHpPhases)
        {
            var result = new List<BossBattlePhase>();
            if (battlePhases == null || battlePhases.Length == 0)
                return result;

            foreach (BattlePhaseEntry entry in battlePhases)
            {
                if (entry == null)
                    continue;

                List<BossPhaseConfig> hpPhases = BuildHpPhases(built, entry.hpPhases);
                result.Add(new BossBattlePhase(
                    entry.maxHp > 0f ? entry.maxHp : maxHp,
                    entry.maxPosture > 0f ? entry.maxPosture : maxPosture,
                    hpPhases.Count > 0 ? hpPhases : sharedHpPhases));
            }

            // 한 줄짜리는 안 적은 것과 결과가 같다 — 2페이즈로 만들려다 한 줄만 넣고 끝낸 경우를 잡는다.
            // 조용히 넘어가면 "인살했는데 그냥 죽는다"로만 보여서 원인을 못 찾는다.
            // (판이 1페이즈로 자르는 것은 정상이므로 자르기 전에 센다 — LimitPhases 참조.)
            if (result.Count == 1)
                Debug.LogWarning(
                    $"{name}: 인살 페이즈가 한 줄뿐이라 보통 보스(인살 1회)와 같습니다. " +
                    "2페이즈 보스로 만들려면 줄을 하나 더 늘리세요 — " +
                    "Tools ▸ ChainRiposte ▸ Setup Two-Phase Boss (2-3) 가 모자란 줄을 채워 줍니다.", this);

            return result;
        }

        /// <summary>
        /// 스테이지가 정한 만큼만 남긴다 — <b>앞에서부터</b>. 뒤 페이즈를 버리는 것이라
        /// 1페이즈로 자른 보스는 예전에 1페이즈 에셋으로 싸웠던 것과 완전히 같은 값이 된다.
        ///
        /// <para>자르기는 <see cref="BuildBattlePhases"/>가 <b>경고를 다 찍은 뒤</b>에 한다 —
        /// 자른 결과를 보고 경고하면 "2-1은 1페이즈로 싸운다"는 정상 설정에서 매번
        /// "페이즈가 한 줄뿐입니다"가 뜬다.</para>
        /// </summary>
        private static List<BossBattlePhase> LimitPhases(List<BossBattlePhase> phases, int limit) =>
            limit <= 0 || phases == null || phases.Count <= limit ? phases : phases.GetRange(0, limit);
    }
}
