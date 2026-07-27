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
            RunStateService.ResetCurrent();
            Debug.Log("[Progress] 세이브를 삭제했습니다 (진행도 + 현재 캐릭터의 런).");
        }

        [MenuItem("Tools/ChainRiposte/Progress/Reset Run Only (성장 캐리만 초기화)")]
        private static void ResetRun()
        {
            RunStateService.ResetCurrent();
            Debug.Log("[Run] 현재 캐릭터의 런(스탯·소울·넋·사슬)을 초기화했습니다.");
        }

        [MenuItem("Tools/ChainRiposte/Progress/Log Run (현재 런 출력)")]
        private static void LogRun()
        {
            var run = RunStateService.Current;
            Debug.Log(
                $"[Run] Lv {run.Stats.Level} · 소울 {run.Stats.Souls} · 미분배 {run.Stats.PendingPoints}P · " +
                $"ATK{run.Stats.StatLevels[0]}/DEF{run.Stats.StatLevels[1]}/PARRY{run.Stats.StatLevels[2]} · " +
                $"넋 {run.AcquiredRelicIds.Count} · 사슬 {run.ChainStep} · NG+{run.NewGamePlusCount}");
        }

        [MenuItem("Tools/ChainRiposte/Progress/Unlock All Stages (전부 해금)")]
        private static void UnlockAll()
        {
            List<string> ids = AllStageIds();
            ProgressService.UnlockAll(ids.ToArray());
            Debug.Log($"[Progress] 스테이지 {ids.Count}개를 클리어 처리했습니다.");
        }

        [MenuItem("Tools/ChainRiposte/Progress/Reveal All Stages (정보만 공개)")]
        private static void RevealAll()
        {
            List<string> ids = AllStageIds();
            foreach (string id in ids)
                ProgressService.Current.MarkAttempted(id);
            ProgressService.Save();
            Debug.Log($"[Progress] 스테이지 {ids.Count}개를 '진입함'으로 표시했습니다 (해금은 그대로).");
        }

        [MenuItem("Tools/ChainRiposte/Progress/Log Progress (현재 상태 출력)")]
        private static void LogProgress()
        {
            var cleared = new List<string>(ProgressService.Current.ClearedStageIds);
            var attempted = new List<string>(ProgressService.Current.AttemptedStageIds);
            Debug.Log(
                $"[Progress] 클리어 {cleared.Count}개: {Join(cleared)}\n" +
                $"[Progress] 진입(정보 공개) {attempted.Count}개: {Join(attempted)}");
        }

        private static string Join(List<string> ids) => ids.Count == 0 ? "(없음)" : string.Join(", ", ids);

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
