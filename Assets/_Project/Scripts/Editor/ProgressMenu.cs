using System.Collections.Generic;
using ChainRiposte.Core.Progress;
using ChainRiposte.Core.Stats;
using ChainRiposte.Game.Config;
using ChainRiposte.Game.Memories;
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
            Debug.Log("[Run] 현재 캐릭터의 런(스탯·소울·기억·사슬)을 초기화했습니다.");
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
        /// <b>치트</b> — 스탯을 상한까지 찍고, 기억을 다 삼키고, <b>최종 고리 직전까지</b> 클리어 처리한다.
        /// 엔딩·최종 보스를 볼 때 앞의 다섯 판을 매번 다시 깨지 않으려는 용도다.
        ///
        /// <para><b>최종 고리는 일부러 안 깬다</b> — 거기서 엔딩이 나오므로, 깨 놓으면 정작 보려던 것을
        /// 못 본다. 마지막 판만 열린 채 남는다.</para>
        ///
        /// <para>스탯 상한: 판정치는 <c>PlayerStatsConfig.ParryLevelHardCap</c>이 진짜 상한이지만
        /// 공격·방어는 상한이 없다 — 그래서 <see cref="UncappedCheatLevel"/>까지만 올린다.
        /// 레벨은 <b>쓴 포인트에서 거꾸로 계산</b>한다(레벨당 1포인트) — 숫자가 서로 안 맞으면
        /// HUD가 "Lv 1인데 스탯이 만렙"인 이상한 상태를 보여 준다.</para>
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

            ApplyCheat();
        }

        /// <summary>치트의 실제 내용 — 확인 창과 분리해 둔다(자동 실행·검증에서 모달이 에디터를 멈춘다).</summary>
        private static void ApplyCheat()
        {
            PlayerStatsConfig config = FindStatsConfig();
            int parry = Mathf.Max(1, config.ParryLevelHardCap);
            int attack = UncappedCheatLevel;
            int defense = UncappedCheatLevel;

            int spent = attack * Mathf.Max(1, config.AttackPointCost)
                        + defense * Mathf.Max(1, config.DefensePointCost)
                        + parry * Mathf.Max(1, config.ParryPointCost);

            var snapshot = new PlayerStatsSnapshot
            {
                Level = spent + 1,          // 레벨당 1포인트 — 쓴 만큼 레벨이 올라 있어야 앞뒤가 맞는다
                Souls = 0,
                PendingPoints = SpareCheatPoints,
            };
            snapshot.StatLevels[(int)StatType.Attack] = attack;
            snapshot.StatLevels[(int)StatType.Defense] = defense;
            snapshot.StatLevels[(int)StatType.Parry] = parry;

            RunState run = RunStateService.Current;
            run.UpdateStats(snapshot);

            int memories = 0;
            foreach (BossMemorySO memory in Resources.LoadAll<BossMemorySO>(MemoryLibrary.ResourcesFolder))
            {
                if (memory != null && run.AddMemory(memory.MemoryId))
                    memories++;
            }

            List<StageDataSO> ordered = OrderedStages();
            var cleared = new List<string>();
            for (int i = 0; i < ordered.Count - 1; i++) // 마지막 판은 남긴다
                cleared.Add(ordered[i].StageId);

            // 먼저 지운다 — 안 지우면 <b>이미 깨 둔 최종 고리가 그대로 남아</b> 엔딩을 다시 볼 수 없다.
            // 치트는 "이 상태로 만들어 줘"이므로 결과가 실행 전 세이브에 따라 달라지면 안 된다.
            ProgressService.ResetAll();
            ProgressService.UnlockAll(cleared.ToArray());

            // 마지막 판의 보스·기믹도 미리 공개해 둔다(정보 패널이 ??? 로 뜨면 확인이 불편하다)
            if (ordered.Count > 0)
            {
                ProgressService.Current.MarkAttempted(ordered[ordered.Count - 1].StageId);
                ProgressService.Save();
            }

            while (run.ChainStep < cleared.Count)
                run.AdvanceChain();

            RunStateService.Save();

            string last = ordered.Count > 0 ? ordered[ordered.Count - 1].StageId : "(없음)";
            Debug.Log($"[치트] Lv {snapshot.Level} · ATK{attack}/DEF{defense}/PARRY{parry}(상한) · " +
                      $"미분배 {SpareCheatPoints}P · 기억 {memories}개 · 사슬 {run.ChainStep} · " +
                      $"클리어 {cleared.Count}판 → 남은 판: {last}");
        }

        /// <summary>공격·방어는 하드 캡이 없다 — 치트가 무한히 올릴 수는 없으니 여기서 끊는다.</summary>
        private const int UncappedCheatLevel = 10;

        /// <summary>준비 화면의 분배 버튼도 눌러 볼 수 있게 남겨 두는 포인트.</summary>
        private const int SpareCheatPoints = 5;

        private static PlayerStatsConfig FindStatsConfig()
        {
            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(PlayerStatsConfigSO)}"))
            {
                var so = AssetDatabase.LoadAssetAtPath<PlayerStatsConfigSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (so != null)
                    return so.ToConfig();
            }

            Debug.LogWarning("[치트] PlayerStatsConfigSO 를 못 찾아 기본값으로 계산합니다.");
            return new PlayerStatsConfig();
        }

        /// <summary>
        /// 스테이지를 <b>에셋 이름 순</b>으로. 지도의 노드 순서를 에디터에서 알 방법이 없어서 쓰는 근사인데,
        /// 이름이 <c>Stage_1_1 … Stage_2_3</c> 규칙이라 순서가 맞는다. 치트라서 이 정도면 충분하다.
        /// </summary>
        private static List<StageDataSO> OrderedStages()
        {
            var stages = new List<StageDataSO>();
            foreach (string guid in AssetDatabase.FindAssets("t:StageDataSO"))
            {
                var stage = AssetDatabase.LoadAssetAtPath<StageDataSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (stage != null)
                    stages.Add(stage);
            }

            stages.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return stages;
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
