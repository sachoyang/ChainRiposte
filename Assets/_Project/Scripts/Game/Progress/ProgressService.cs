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

        /// <summary>
        /// 세이브 삭제 (타이틀 ▸ 새 게임 · 옵션 ▸ 진행도 초기화 · Tools ▸ ChainRiposte ▸ Progress).
        ///
        /// <para><b>튜토리얼 기록도 같이 지운다</b> — 이 함수를 부르는 자리는 전부 "처음부터"라는 뜻이고,
        /// 처음부터 하려고 눌렀는데 안내가 안 나오면 그건 처음이 아니다 (<c>Docs/TUTORIAL.md</c> §2.1).
        /// 지우는 자리를 여기 하나로 모아 둔다 — 부르는 곳이 넷이라 각자 적으면 한 곳은 반드시 빠진다.
        /// (<see cref="BeginNewGamePlus"/>는 반대로 안 건드린다: 2회차에 이미 배운 것을 다시 가르치면 방해다.)</para>
        /// </summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
            _current = new StageProgress();
            Tutorial.TutorialService.ResetAll();
        }

        /// <summary>
        /// 다음 회차(NG+)로 — 클리어 기록만 지우고 <b>진입 기록은 남긴다</b>
        /// (규칙은 <see cref="StageProgress.BeginNewGamePlus"/>).
        /// </summary>
        public static void BeginNewGamePlus()
        {
            Current.BeginNewGamePlus();
            Save();
            Debug.Log("[Progress] 다음 회차 — 클리어 기록을 지웠습니다(보스·기믹 공개는 유지).");
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
