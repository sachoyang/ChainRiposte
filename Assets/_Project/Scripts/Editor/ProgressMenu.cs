using System.Collections.Generic;
using ChainRiposte.Game.Cheats;
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
                    "저장된 클리어 기록을 모두 지웁니다. 1-1만 열린 상태가 됩니다.\n" +
                    "튜토리얼 「봤다」 기록도 같이 지워집니다.", "삭제", "취소"))
                return;

            ProgressService.ResetAll();
            RunStateService.ResetCurrent();
            Debug.Log("[Progress] 세이브를 삭제했습니다 (진행도 + 현재 캐릭터의 런 + 튜토리얼 기록).");
        }

        [MenuItem("Tools/ChainRiposte/Progress/Reset Run Only (성장 캐리만 초기화)")]
        private static void ResetRun()
        {
            RunStateService.ResetCurrent();
            Debug.Log("[Run] 현재 캐릭터의 런(스탯·소울·기억·사슬)을 초기화했습니다.");
        }

        /// <summary>
        /// 소개 카드만 다시 보게 만든다 — 진행도·런은 그대로다. 카드 문구·영상을 손볼 때
        /// 판을 다시 깨지 않고 확인하기 위한 것이다(<c>Reset Progress</c>는 진행도까지 날린다).
        /// </summary>
        [MenuItem("Tools/ChainRiposte/Progress/Reset Tutorial (소개 카드 다시 보기)")]
        private static void ResetTutorial()
        {
            Game.Tutorial.TutorialService.ResetAll();
            Debug.Log("[Tutorial] 「봤다」 기록을 지웠습니다 — 소개 카드가 다시 뜹니다.");
        }

        [MenuItem("Tools/ChainRiposte/Progress/Log Tutorial (본 항목 출력)")]
        private static void LogTutorial()
        {
            var seen = new List<string>(Game.Tutorial.TutorialService.SeenIds);
            Debug.Log($"[Tutorial] 본 항목 {seen.Count}개: {Join(seen)}");
        }

        [MenuItem("Tools/ChainRiposte/Progress/Log Run (현재 런 출력)")]
        private static void LogRun()
        {
            var run = RunStateService.Current;
            Debug.Log(
                $"[Run] Lv {run.Stats.Level} · 소울 {run.Stats.Souls} · 미분배 {run.Stats.PendingPoints}P · " +
                $"ATK{run.Stats.StatLevels[0]}/DEF{run.Stats.StatLevels[1]}/PARRY{run.Stats.StatLevels[2]} · " +
                $"기억 {run.AcquiredMemoryIds.Count} · 사슬 {run.ChainStep} · NG+{run.NewGamePlusCount}");
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

        /// <summary>
        /// <b>치트</b> — 내용은 <see cref="CheatService"/>가 갖는다(게임 안 옵션의 치트 버튼과 같은 코드).
        /// 여기서는 확인 창만 띄운다: 에디터는 모달, 게임은 옵션의 확인 패널로 서로 다르기 때문이다.
        /// 재료(스탯 설정 · 지도 순서의 스테이지 목록)는 <c>Resources/CheatConfig</c> 에셋에 있다.
        /// </summary>
        [MenuItem("Tools/ChainRiposte/Progress/Cheat: Max Stats + Clear To Final (치트)")]
        private static void Cheat()
        {
            if (!EditorUtility.DisplayDialog("치트",
                    "진행도와 현재 캐릭터의 런을 덮어씁니다.\n\n" +
                    "· 스탯을 상한까지 분배\n· 기억 전부 획득\n" +
                    "· 최종 고리 직전까지 클리어 (마지막 판은 열린 채 남는다 — 엔딩을 보려고)\n\n" +
                    "계속할까요?", "적용", "취소"))
                return;

            CheatService.Apply(out _);
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
