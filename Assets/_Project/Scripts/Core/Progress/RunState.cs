using System;
using System.Collections.Generic;
using System.Globalization;
using ChainRiposte.Core.Stats;

namespace ChainRiposte.Core.Progress
{
    /// <summary>
    /// 한 런(사슬 오르기 한 회차)의 <b>누적 상태</b>. 성장 캐리의 핵심 — 스테이지를 넘어 유지되는 것은
    /// 전부 여기 모인다 (설계: <c>Docs/PROGRESSION.md</c>).
    ///
    /// <list type="bullet">
    ///   <item><see cref="Stats"/> — 레벨·소울 은행·미분배 포인트·스탯 레벨 (<see cref="PlayerStats"/>의 씨앗).</item>
    ///   <item><see cref="AcquiredRelicIds"/> — 인살로 흡수한 넋(영구 패시브)들.</item>
    ///   <item><see cref="ChainStep"/> — 죽지 않고 연속 클리어한 수. <b>죽으면 0</b>(빌드는 유지, 배수만 끊김).</item>
    ///   <item><see cref="NewGamePlusCount"/> — 엔딩 후 회차. 난이도 곡선의 입력.</item>
    /// </list>
    ///
    /// UnityEngine에 의존하지 않는다 — 저장 매체는 Game 레이어의 RunStateService가 담당하고,
    /// 이 클래스는 <see cref="StageProgress"/>와 같은 방식의 문자열 직렬화만 제공한다.
    /// </summary>
    public sealed class RunState
    {
        private const char SectionSeparator = '|';
        private const char ItemSeparator = ';';
        // v1→v2: 판 단위 값이던 TotalSoulsEarned를 스냅샷에서 뺐다. v1 세이브는 칸 수가 달라
        // 오독되므로 버전을 올려 옛 세이브를 새 런으로 떨어뜨린다(자동 초기화).
        private const string Version2 = "v2";

        /// <summary>플레이어 성장 스냅샷 — 다음 판의 <see cref="PlayerStats"/>에 씨앗으로 들어간다.</summary>
        public PlayerStatsSnapshot Stats { get; }

        private readonly List<string> _relicIds;

        /// <summary>연속 무사망 클리어 수. 소울 배수의 입력.</summary>
        public int ChainStep { get; private set; }

        /// <summary>엔딩 후 재도전 회차(NG+).</summary>
        public int NewGamePlusCount { get; private set; }

        public RunState(
            PlayerStatsSnapshot stats = null,
            IEnumerable<string> relicIds = null,
            int chainStep = 0,
            int newGamePlusCount = 0)
        {
            Stats = stats ?? new PlayerStatsSnapshot();
            _relicIds = new List<string>();
            foreach (string id in relicIds ?? Array.Empty<string>())
                AddRelic(id);
            ChainStep = Math.Max(0, chainStep);
            NewGamePlusCount = Math.Max(0, newGamePlusCount);
        }

        public IReadOnlyList<string> AcquiredRelicIds => _relicIds;

        public bool HasRelic(string relicId) =>
            !string.IsNullOrEmpty(relicId) && _relicIds.Contains(Sanitize(relicId));

        /// <summary>넋을 추가한다. 이미 가진 것이면 무시하고 false. (같은 넋을 두 번 흡수하지 않는다)</summary>
        public bool AddRelic(string relicId)
        {
            string clean = Sanitize(relicId);
            if (string.IsNullOrEmpty(clean) || _relicIds.Contains(clean))
                return false;
            _relicIds.Add(clean);
            return true;
        }

        /// <summary>
        /// 스테이지 클리어 시 최신 성장으로 스냅샷을 갱신한다 — 넋·사슬·회차는 건드리지 않는다.
        /// (성장만 이어지고 나머지 런 상태는 각자의 규칙으로 움직인다)
        /// </summary>
        public void UpdateStats(PlayerStatsSnapshot snapshot)
        {
            if (snapshot == null)
                return;

            Stats.Level = Math.Max(1, snapshot.Level);
            Stats.Souls = Math.Max(0, snapshot.Souls);
            Stats.PendingPoints = Math.Max(0, snapshot.PendingPoints);
            for (int i = 0; i < Stats.StatLevels.Length && i < snapshot.StatLevels.Length; i++)
                Stats.StatLevels[i] = Math.Max(0, snapshot.StatLevels[i]);
        }

        /// <summary>무사망 클리어 — 사슬을 한 칸 잇는다.</summary>
        public void AdvanceChain() => ChainStep++;

        /// <summary>죽음 — 사슬 배수만 끊는다(빌드·넋은 유지).</summary>
        public void BreakChain() => ChainStep = 0;

        /// <summary>엔딩 후 다음 회차로. 넋 유지 여부는 상위(RunStateService)가 정한다(§8 열린 결정).</summary>
        public void EnterNewGamePlus() => NewGamePlusCount++;

        /// <summary>형식: <c>v2|레벨;소울;포인트;S0;S1;S2|넋;넋|사슬;회차</c>.</summary>
        public string Serialize()
        {
            string statsSection = string.Join(ItemSeparator.ToString(),
                Stats.Level, Stats.Souls, Stats.PendingPoints,
                Stats.StatLevels[0], Stats.StatLevels[1], Stats.StatLevels[2]);

            return string.Join(SectionSeparator.ToString(),
                Version2,
                statsSection,
                string.Join(ItemSeparator.ToString(), _relicIds),
                string.Join(ItemSeparator.ToString(), ChainStep, NewGamePlusCount));
        }

        public static RunState Deserialize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new RunState();

            string[] sections = raw.Split(SectionSeparator);
            if (sections.Length < 4 || sections[0] != Version2)
                return new RunState();

            string[] s = sections[1].Split(ItemSeparator);
            PlayerStatsSnapshot stats = new()
            {
                Level = Math.Max(1, ParseInt(s, 0, 1)),
                Souls = ParseInt(s, 1, 0),
                PendingPoints = ParseInt(s, 2, 0),
            };
            stats.StatLevels[0] = ParseInt(s, 3, 0);
            stats.StatLevels[1] = ParseInt(s, 4, 0);
            stats.StatLevels[2] = ParseInt(s, 5, 0);

            string[] meta = sections[3].Split(ItemSeparator);
            return new RunState(
                stats,
                sections[2].Split(ItemSeparator),
                ParseInt(meta, 0, 0),
                ParseInt(meta, 1, 0));
        }

        private static int ParseInt(string[] parts, int index, int fallback) =>
            index >= 0 && index < parts.Length
            && int.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? Math.Max(0, value)
                : fallback;

        // id에 구분자가 섞이면 세이브가 깨진다 (StageProgress와 같은 방어).
        private static string Sanitize(string id) =>
            string.IsNullOrWhiteSpace(id)
                ? string.Empty
                : id.Trim().Replace(SectionSeparator, '_').Replace(ItemSeparator, '_');
    }
}
