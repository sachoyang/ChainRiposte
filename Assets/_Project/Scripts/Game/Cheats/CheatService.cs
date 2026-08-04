using System.Collections.Generic;
using ChainRiposte.Core.Progress;
using ChainRiposte.Core.Stats;
using ChainRiposte.Game.Config;
using ChainRiposte.Game.Memories;
using ChainRiposte.Game.Progress;
using UnityEngine;

namespace ChainRiposte.Game.Cheats
{
    /// <summary>
    /// 치트 — 스탯을 상한까지 찍고, 기억을 다 삼키고, <b>최종 고리 직전까지</b> 클리어 처리한다.
    /// 엔딩·최종 보스를 볼 때 앞의 다섯 판을 매번 다시 깨지 않으려는 용도다.
    ///
    /// <para><b>최종 고리는 일부러 안 깬다</b> — 거기서 엔딩이 나오므로 깨 놓으면 정작 보려던 것을
    /// 못 본다. 마지막 판만 열린 채 남는다.</para>
    ///
    /// <para><b>순수 런타임 코드다</b>(<c>UnityEditor</c> 참조 없음) — 에디터 메뉴와 옵션 화면의
    /// 치트 버튼이 <b>이 한 곳</b>을 부른다. 예전에는 에디터에만 있어서 빌드로는 확인할 수 없었고,
    /// 규칙을 두 곳에 적으면 한쪽만 고쳐진다.</para>
    /// </summary>
    public static class CheatService
    {
        private static CheatConfigSO _config;
        private static bool _looked;

        /// <summary>도메인 리로드를 꺼둔 환경에서 지난 플레이의 참조가 남지 않게 한다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStatics()
        {
            _config = null;
            _looked = false;
        }

        /// <summary>
        /// 치트를 쓸 수 있는가 = <c>Resources</c>에 설정 에셋이 있는가.
        /// 화면은 이 값이 false면 버튼을 아예 안 보여 준다 — 눌러도 아무 일 없는 버튼은 고장으로 읽힌다.
        /// </summary>
        public static bool IsAvailable => Config != null;

        private static CheatConfigSO Config
        {
            get
            {
                if (_looked)
                    return _config;

                _looked = true;
                _config = Resources.Load<CheatConfigSO>(CheatConfigSO.ResourceName);
                return _config;
            }
        }

        /// <summary>
        /// 치트를 적용하고 세이브까지 굳힌다. 성공하면 true + 한 줄 요약.
        /// <b>확인 창은 부르는 쪽의 몫이다</b> — 에디터는 모달, 게임은 옵션의 확인 패널로 서로 다르다.
        /// </summary>
        public static bool Apply(out string summary)
        {
            summary = string.Empty;

            CheatConfigSO config = Config;
            if (config == null)
            {
                Debug.LogError(
                    $"[치트] Resources/{CheatConfigSO.ResourceName} 에셋을 못 찾았습니다. " +
                    "Create ▸ ChainRiposte ▸ Cheat Config 로 만들어 Resources 폴더에 두세요.");
                return false;
            }

            PlayerStatsConfig stats = config.StatsConfig != null
                ? config.StatsConfig.ToConfig()
                : Warned();

            // 판정치는 하드 캡이 진짜 상한이고, 공격·방어는 상한이 없어 설정값에서 끊는다.
            int parry = Mathf.Max(1, stats.ParryLevelHardCap);
            int attack = config.UncappedStatLevel;
            int defense = config.UncappedStatLevel;

            int spent = attack * Mathf.Max(1, stats.AttackPointCost)
                        + defense * Mathf.Max(1, stats.DefensePointCost)
                        + parry * Mathf.Max(1, stats.ParryPointCost);

            var snapshot = new PlayerStatsSnapshot
            {
                Level = spent + 1,          // 레벨당 1포인트 — 쓴 만큼 레벨이 올라 있어야 앞뒤가 맞는다
                Souls = 0,
                PendingPoints = config.SparePoints,
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

            StageDataSO[] ordered = config.OrderedStages;
            var cleared = new List<string>();
            for (int i = 0; i < ordered.Length - 1; i++) // 마지막 판은 남긴다
            {
                if (ordered[i] != null)
                    cleared.Add(ordered[i].StageId);
            }

            // 먼저 지운다 — 안 지우면 <b>이미 깨 둔 최종 고리가 그대로 남아</b> 엔딩을 다시 볼 수 없다.
            // 치트는 "이 상태로 만들어 줘"이므로 결과가 실행 전 세이브에 따라 달라지면 안 된다.
            ProgressService.ResetAll();
            ProgressService.UnlockAll(cleared.ToArray());

            // 마지막 판의 보스·기믹도 미리 공개해 둔다(정보 패널이 ??? 로 뜨면 확인이 불편하다)
            StageDataSO last = ordered.Length > 0 ? ordered[ordered.Length - 1] : null;
            if (last != null)
            {
                ProgressService.Current.MarkAttempted(last.StageId);
                ProgressService.Save();
            }

            while (run.ChainStep < cleared.Count)
                run.AdvanceChain();

            RunStateService.Save();

            summary =
                $"Lv {snapshot.Level} · ATK{attack}/DEF{defense}/PARRY{parry}(상한) · " +
                $"미분배 {config.SparePoints}P · 기억 {memories}개 · 사슬 {run.ChainStep} · " +
                $"클리어 {cleared.Count}판 → 남은 판: {(last != null ? last.StageId : "(없음)")}";
            Debug.Log($"[치트] {summary}");
            return true;
        }

        private static PlayerStatsConfig Warned()
        {
            Debug.LogWarning("[치트] 설정에 PlayerStatsConfigSO 가 비어 있어 기본값으로 계산합니다.");
            return new PlayerStatsConfig();
        }
    }
}
