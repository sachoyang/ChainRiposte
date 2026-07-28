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
    ///   <item>스테이지별 <b>채굴량</b>(<see cref="GetHarvested"/>) — 그 땅에서 이미 캐 간 소울. 광맥이 마르면 재방문해도 안 나온다.</item>
    /// </list>
    ///
    /// UnityEngine에 의존하지 않는다 — 저장 매체는 Game 레이어의 RunStateService가 담당하고,
    /// 이 클래스는 <see cref="StageProgress"/>와 같은 방식의 문자열 직렬화만 제공한다.
    /// </summary>
    public sealed class RunState
    {
        private const char SectionSeparator = '|';
        private const char ItemSeparator = ';';
        private const char PairSeparator = '=';
        // v1→v2: 판 단위 값이던 TotalSoulsEarned를 스냅샷에서 뺐다. v1 세이브는 칸 수가 달라
        // 오독되므로 버전을 올려 옛 세이브를 새 런으로 떨어뜨린다(자동 초기화).
        // v2→v3: 스테이지별 채굴량(소울 광맥) 칸이 붙었다.
        private const string Version = "v3";

        /// <summary>플레이어 성장 스냅샷 — 다음 판의 <see cref="PlayerStats"/>에 씨앗으로 들어간다.</summary>
        public PlayerStatsSnapshot Stats { get; }

        private readonly List<string> _relicIds;

        /// <summary>스테이지 id → 이 런에서 이미 캐 간 소울. 없는 키는 0(아직 손 안 댄 땅).</summary>
        private readonly Dictionary<string, int> _harvested = new();

        /// <summary>연속 무사망 클리어 수. 소울 배수의 입력.</summary>
        public int ChainStep { get; private set; }

        /// <summary>엔딩 후 재도전 회차(NG+).</summary>
        public int NewGamePlusCount { get; private set; }

        public RunState(
            PlayerStatsSnapshot stats = null,
            IEnumerable<string> relicIds = null,
            int chainStep = 0,
            int newGamePlusCount = 0,
            IEnumerable<KeyValuePair<string, int>> harvested = null)
        {
            Stats = stats ?? new PlayerStatsSnapshot();
            _relicIds = new List<string>();
            foreach (string id in relicIds ?? Array.Empty<string>())
                AddRelic(id);
            ChainStep = Math.Max(0, chainStep);
            NewGamePlusCount = Math.Max(0, newGamePlusCount);
            foreach (KeyValuePair<string, int> entry in harvested ?? Array.Empty<KeyValuePair<string, int>>())
                Harvest(entry.Key, entry.Value);
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

        /// <summary>이 런에서 그 스테이지에서 이미 캐 간 소울. 한 번도 안 간 땅은 0.</summary>
        public int GetHarvested(string stageId) =>
            _harvested.TryGetValue(Sanitize(stageId), out int amount) ? amount : 0;

        /// <summary>
        /// 그 스테이지에서 캔 소울을 광맥에 기록한다 (<c>Docs/PROGRESSION.md</c> §2.4).
        /// <b>클리어할 때만 부른다</b> — 죽으면 그 판의 벌이가 통째로 무효가 되므로 채굴량도 남으면 안 된다
        /// (소울 롤백과 같은 규칙이어야 "죽으면 그 판은 없던 일"이 한 문장으로 유지된다).
        /// </summary>
        public void Harvest(string stageId, int amount)
        {
            string clean = Sanitize(stageId);
            if (string.IsNullOrEmpty(clean) || amount <= 0)
                return;

            _harvested[clean] = GetHarvested(clean) + amount;
        }

        /// <summary>무사망 클리어 — 사슬을 한 칸 잇는다.</summary>
        public void AdvanceChain() => ChainStep++;

        /// <summary>죽음 — 사슬 배수만 끊는다(빌드·넋은 유지).</summary>
        public void BreakChain() => ChainStep = 0;

        /// <summary>엔딩 후 다음 회차로. 넋 유지 여부는 상위(RunStateService)가 정한다(§8 열린 결정).</summary>
        public void EnterNewGamePlus() => NewGamePlusCount++;

        /// <summary>형식: <c>v3|레벨;소울;포인트;S0;S1;S2|넋;넋|사슬;회차|스테이지=캔양;스테이지=캔양</c>.</summary>
        public string Serialize()
        {
            string statsSection = string.Join(ItemSeparator.ToString(),
                Stats.Level, Stats.Souls, Stats.PendingPoints,
                Stats.StatLevels[0], Stats.StatLevels[1], Stats.StatLevels[2]);

            var harvestParts = new List<string>(_harvested.Count);
            foreach (KeyValuePair<string, int> entry in _harvested)
                harvestParts.Add(entry.Key + PairSeparator + entry.Value.ToString(CultureInfo.InvariantCulture));

            return string.Join(SectionSeparator.ToString(),
                Version,
                statsSection,
                string.Join(ItemSeparator.ToString(), _relicIds),
                string.Join(ItemSeparator.ToString(), ChainStep, NewGamePlusCount),
                string.Join(ItemSeparator.ToString(), harvestParts));
        }

        public static RunState Deserialize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new RunState();

            string[] sections = raw.Split(SectionSeparator);
            if (sections.Length < 5 || sections[0] != Version)
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
                ParseInt(meta, 1, 0),
                ParseHarvest(sections[4]));
        }

        private static IEnumerable<KeyValuePair<string, int>> ParseHarvest(string section)
        {
            var result = new List<KeyValuePair<string, int>>();
            foreach (string part in section.Split(ItemSeparator))
            {
                int split = part.IndexOf(PairSeparator);
                if (split <= 0)
                    continue; // 빈 칸이거나 id가 없는 조각 — 조용히 건너뛴다

                string[] pair = { part.Substring(split + 1) };
                result.Add(new KeyValuePair<string, int>(part.Substring(0, split), ParseInt(pair, 0, 0)));
            }

            return result;
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
                : id.Trim()
                    .Replace(SectionSeparator, '_')
                    .Replace(ItemSeparator, '_')
                    .Replace(PairSeparator, '_');
    }
}
