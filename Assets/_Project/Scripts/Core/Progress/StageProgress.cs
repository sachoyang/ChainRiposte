using System;
using System.Collections.Generic;

namespace ChainRiposte.Core.Progress
{
    /// <summary>
    /// 월드맵 진행도 (GDD §9.2). "클리어한 스테이지 id 집합"과 "한 번이라도 들어가 본 집합"만 들고 있고,
    /// 잠금 여부는 맵의 노드 순서(orderedStageIds)와 조합해 계산한다 —
    /// 노드를 씬에서 재배치·추가해도 세이브가 깨지지 않는다.
    ///
    /// UnityEngine에 의존하지 않는다. 저장 매체는 Game 레이어의 ProgressService가 담당하고,
    /// 이 클래스는 문자열 직렬화만 제공한다.
    /// </summary>
    public sealed class StageProgress
    {
        private const char Separator = ';';
        private const char SectionSeparator = '|';
        private const string Version2 = "v2";

        private readonly HashSet<string> _cleared;
        private readonly HashSet<string> _attempted;

        public StageProgress(IEnumerable<string> clearedStageIds = null, IEnumerable<string> attemptedStageIds = null)
        {
            _cleared = new HashSet<string>(StringComparer.Ordinal);
            _attempted = new HashSet<string>(StringComparer.Ordinal);

            foreach (string id in clearedStageIds ?? Array.Empty<string>())
            {
                // 깼다면 당연히 들어가 본 것 — v1 세이브에서 넘어올 때도 이 규칙으로 복원된다
                Add(_cleared, id);
                Add(_attempted, id);
            }

            foreach (string id in attemptedStageIds ?? Array.Empty<string>())
                Add(_attempted, id);
        }

        public IReadOnlyCollection<string> ClearedStageIds => _cleared;
        public IReadOnlyCollection<string> AttemptedStageIds => _attempted;

        public bool IsCleared(string stageId) =>
            !string.IsNullOrEmpty(stageId) && _cleared.Contains(stageId);

        /// <summary>한 번이라도 진입한 적이 있는가.</summary>
        public bool IsAttempted(string stageId) =>
            !string.IsNullOrEmpty(stageId) && _attempted.Contains(stageId);

        /// <summary>
        /// 스테이지 정보(보스·기믹)를 공개해도 되는가.
        /// 직접 들어가 본 적이 있어야 공개한다 — 해금만으로는 다음 판을 미리 보여주지 않는다.
        /// </summary>
        public bool IsRevealed(string stageId) => IsAttempted(stageId);

        /// <summary>처음 클리어했으면 true (저장이 필요한지 판단용).</summary>
        public bool MarkCleared(string stageId) => Add(_attempted, stageId) | Add(_cleared, stageId);

        /// <summary>처음 진입했으면 true (저장이 필요한지 판단용).</summary>
        public bool MarkAttempted(string stageId) => Add(_attempted, stageId);

        /// <summary>첫 스테이지는 항상 열려 있고, 그 뒤는 <b>직전 스테이지를 클리어해야</b> 열린다.</summary>
        public bool IsUnlocked(IReadOnlyList<string> orderedStageIds, int index)
        {
            if (orderedStageIds == null)
                throw new ArgumentNullException(nameof(orderedStageIds));
            if (index < 0 || index >= orderedStageIds.Count)
                return false;
            return index == 0 || IsCleared(orderedStageIds[index - 1]);
        }

        /// <summary>열려 있는 노드 중 가장 앞선 것 — 맵 진입 시 캐릭터를 여기에 세운다.</summary>
        public int HighestUnlockedIndex(IReadOnlyList<string> orderedStageIds)
        {
            if (orderedStageIds == null)
                throw new ArgumentNullException(nameof(orderedStageIds));

            int highest = 0;
            for (int i = 1; i < orderedStageIds.Count && IsUnlocked(orderedStageIds, i); i++)
                highest = i;
            return highest;
        }

        /// <summary>형식: <c>v2|클리어;목록|시도;목록</c>. 버전 표시가 없으면 v1(클리어 목록만)로 읽는다.</summary>
        public string Serialize() => string.Join(
            SectionSeparator.ToString(),
            Version2,
            string.Join(Separator.ToString(), _cleared),
            string.Join(Separator.ToString(), _attempted));

        public static StageProgress Deserialize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new StageProgress();

            string[] sections = raw.Split(SectionSeparator);

            // v1 세이브 — 클리어 목록뿐이다. 생성자가 시도 기록까지 복원한다 (마이그레이션).
            if (sections.Length < 3 || sections[0] != Version2)
                return new StageProgress(raw.Split(Separator));

            return new StageProgress(sections[1].Split(Separator), sections[2].Split(Separator));
        }

        private static bool Add(HashSet<string> set, string stageId)
        {
            if (string.IsNullOrWhiteSpace(stageId))
                return false;
            // 구분자가 섞이면 세이브가 깨지므로 방어한다 (id는 에셋 이름 기반).
            string trimmed = stageId.Trim().Replace(Separator, '_').Replace(SectionSeparator, '_');
            return set.Add(trimmed);
        }
    }
}
