using System.Collections.Generic;
using ChainRiposte.Game.Config;
using ChainRiposte.Game.Progress;
using UnityEditor;
using UnityEngine;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// 진행도 세이브(GDD §9.2) 디버그 메뉴. 잠금 흐름을 테스트할 때 쓴다.
    /// 실행: <c>Tools ▸ ChainRiposte ▸ Progress ▸ ...</c>
    /// </summary>
    public static class ProgressMenu
    {
        [MenuItem("Tools/ChainRiposte/Progress/Reset Progress (세이브 삭제)")]
        private static void Reset()
        {
            if (!EditorUtility.DisplayDialog("진행도 초기화",
                    "저장된 클리어 기록을 모두 지웁니다. 1-1만 열린 상태가 됩니다.", "삭제", "취소"))
                return;

            ProgressService.ResetAll();
            Debug.Log("[Progress] 세이브를 삭제했습니다.");
        }

        [MenuItem("Tools/ChainRiposte/Progress/Unlock All Stages (전부 해금)")]
        private static void UnlockAll()
        {
            List<string> ids = AllStageIds();
            ProgressService.UnlockAll(ids.ToArray());
            Debug.Log($"[Progress] 스테이지 {ids.Count}개를 클리어 처리했습니다.");
        }

        [MenuItem("Tools/ChainRiposte/Progress/Log Progress (현재 상태 출력)")]
        private static void LogProgress()
        {
            var cleared = new List<string>(ProgressService.Current.ClearedStageIds);
            Debug.Log(cleared.Count == 0
                ? "[Progress] 클리어 기록 없음."
                : $"[Progress] 클리어 {cleared.Count}개: {string.Join(", ", cleared)}");
        }

        private static List<string> AllStageIds()
        {
            var ids = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:StageDataSO"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                StageDataSO stage = AssetDatabase.LoadAssetAtPath<StageDataSO>(path);
                if (stage != null)
                    ids.Add(stage.StageId);
            }
            return ids;
        }
    }
}
