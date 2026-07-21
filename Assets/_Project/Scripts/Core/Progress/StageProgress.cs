using System;
using System.Collections.Generic;

namespace ChainRiposte.Core.Progress
{
    /// <summary>
    /// 월드맵 진행도 (GDD §9.2). "클리어한 스테이지 id 집합"만 들고 있고,
    /// 잠금 여부는 맵의 노드 순서(orderedStageIds)와 조합해 계산한다 —
    /// 노드를 씬에서 재배치·추가해도 세이브가 깨지지 않는다.
    ///
    /// UnityEngine에 의존하지 않는다. 저장 매체는 Game 레이어의 ProgressService가 담당하고,
    /// 이 클래스는 문자열 직렬화만 제공한다.
    /// </summary>
    public sealed class StageProgress
    {
        private const char Separator = ';';

        private readonly HashSet<string> _cleared;

        public StageProgress(IEnumerable<string> clearedStageIds = null)
        {
            _cleared = new HashSet<string>(StringComparer.Ordinal);
            if (clearedStageIds == null)
                return;
            foreach (string id in clearedStageIds)
                Add(id);
        }

        public IReadOnlyCollection<string> ClearedStageIds => _cleared;

        public bool IsCleared(string stageId) =>
            !string.IsNullOrEmpty(stageId) && _cleared.Contains(stageId);

        /// <summary>처음 클리어했으면 true (저장이 필요한지 판단용).</summary>
        public bool MarkCleared(string stageId) => Add(stageId);

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

        public string Serialize() => string.Join(Separator.ToString(), _cleared);

        public static StageProgress Deserialize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new StageProgress();
            return new StageProgress(raw.Split(Separator));
        }

        private bool Add(string stageId)
        {
            if (string.IsNullOrWhiteSpace(stageId))
                return false;
            // 구분자가 섞이면 세이브가 깨지므로 방어한다 (id는 에셋 이름 기반).
            string trimmed = stageId.Trim().Replace(Separator, '_');
            return _cleared.Add(trimmed);
        }
    }
}
