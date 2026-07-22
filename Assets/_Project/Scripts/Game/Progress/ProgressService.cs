using ChainRiposte.Core.Progress;
using UnityEngine;

namespace ChainRiposte.Game.Progress
{
    /// <summary>
    /// 진행도 세이브 어댑터 (GDD §9.2). 규칙은 <see cref="StageProgress"/>(Core)가 갖고,
    /// 여기서는 PlayerPrefs에 읽고 쓰는 것만 한다 — 모바일 포함 전 플랫폼 동작.
    /// </summary>
    public static class ProgressService
    {
        // 키는 그대로 두고 저장 형식 안에 버전을 넣는다 — 기존 세이브가 v2로 자동 마이그레이션된다.
        private const string Key = "ChainRiposte.Progress.v1";

        private static StageProgress _current;

        /// <summary>현재 진행도 (최초 접근 시 로드).</summary>
        public static StageProgress Current => _current ??= Load();

        /// <summary>스테이지 클리어 기록. 새로 깬 경우에만 저장한다.</summary>
        public static void MarkCleared(string stageId)
        {
            if (!Current.MarkCleared(stageId))
                return;

            Save();
            Debug.Log($"[Progress] '{stageId}' 클리어 저장. 누적 {Current.ClearedStageIds.Count}개.");
        }

        /// <summary>스테이지 진입 기록. 이 기록이 있어야 월드맵에서 보스·기믹 정보가 공개된다.</summary>
        public static void MarkAttempted(string stageId)
        {
            if (!Current.MarkAttempted(stageId))
                return;

            Save();
            Debug.Log($"[Progress] '{stageId}' 첫 진입 저장 — 월드맵 정보 공개.");
        }

        public static void Save()
        {
            PlayerPrefs.SetString(Key, Current.Serialize());
            PlayerPrefs.Save();
        }

        /// <summary>세이브 삭제 (테스트/디버그용 — Tools ▸ ChainRiposte ▸ Progress).</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
            _current = new StageProgress();
        }

        /// <summary>주어진 스테이지들을 전부 클리어 처리 (디버그용).</summary>
        public static void UnlockAll(params string[] stageIds)
        {
            foreach (string id in stageIds)
                Current.MarkCleared(id);
            Save();
        }

        private static StageProgress Load() =>
            StageProgress.Deserialize(PlayerPrefs.GetString(Key, string.Empty));
    }
}
