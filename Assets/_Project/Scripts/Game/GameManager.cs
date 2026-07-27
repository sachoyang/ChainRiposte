using ChainRiposte.Core.Flow;
using ChainRiposte.Core.Progress;
using ChainRiposte.Core.Stage;
using ChainRiposte.Game.Config;
using ChainRiposte.Game.Progress;
using UnityEngine;

namespace ChainRiposte.Game
{
    /// <summary>
    /// 씬 진입점(컴포지션 루트). GameSession을 조립하고 스테이지를 시작한다.
    /// 다른 시스템은 인스펙터 참조로 이 컴포넌트를 받아 Session에 접근한다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameManager : MonoBehaviour
    {
        [SerializeField] private PlayerStatsConfigSO statsConfig;
        [SerializeField] private StageDataSO stageData;

        public GameSession Session { get; private set; }

        /// <summary>이번 씬에서 플레이할 스테이지. 퍼즐/전투 컨트롤러가 공유한다.</summary>
        public StageConfig StageConfig { get; private set; }

        /// <summary>스테이지 데이터 원본 — 순수 C# config에 담을 수 없는 것(보스 스프라이트 등)을 읽을 때 쓴다.</summary>
        public StageDataSO StageData => stageData;

        private void Awake()
        {
            // 월드맵에서 선택하고 들어왔다면 그 스테이지를 우선한다
            if (StageSelection.Selected != null)
                stageData = StageSelection.Selected;

            if (statsConfig == null || stageData == null)
            {
                Debug.LogError($"{nameof(GameManager)}: {nameof(statsConfig)} 또는 {nameof(stageData)}가 비어 있습니다.", this);
                enabled = false;
                return;
            }

            // 진입 자체를 기록한다 — 월드맵은 이 기록이 있어야 보스·기믹을 공개한다 (클리어 못 해도 공개).
            ProgressService.MarkAttempted(stageData.StageId);

            StageConfig = stageData.ToConfig();
            // 저장된 런의 성장을 씨앗으로 이어받는다 — 성장 캐리 (Docs/PROGRESSION.md)
            Session = new GameSession(BuildStatsConfig(), RunStateService.Current.Stats);
            Session.PhaseChanged += OnPhaseChanged;
        }

        private void Start()
        {
            Session.StartPuzzle();
        }

        /// <summary>
        /// 공용 밸런스 + 고른 캐릭터의 특화. 캐릭터가 없으면(Main 단독 실행 등) 공용 값 그대로다.
        /// </summary>
        private Core.Stats.PlayerStatsConfig BuildStatsConfig()
        {
            Core.Stats.PlayerStatsConfig config = statsConfig.ToConfig();

            Characters.PlayerCharacterSO character = Characters.CharacterService.Current;
            if (character != null && character.HasBonuses)
            {
                character.ApplyBonuses(config);
                Debug.Log($"[GameManager] 캐릭터 '{character.CharacterId}' 특화 적용.");
            }

            return config;
        }

        private void OnDestroy()
        {
            if (Session != null)
                Session.PhaseChanged -= OnPhaseChanged;
        }

        private void OnPhaseChanged(GamePhase previous, GamePhase next)
        {
            Debug.Log($"[GameFlow] {previous} → {next}");

            // 클리어하면 다음 스테이지가 열린다 (GDD §9.2)
            if (next == GamePhase.Victory)
            {
                ProgressService.MarkCleared(stageData.StageId);
                SaveRunProgress(cleared: true);
            }
            // 죽으면 빌드는 남기고 사슬 배수만 끊는다 (Docs/PROGRESSION.md §5)
            else if (next == GamePhase.Defeat)
            {
                SaveRunProgress(cleared: false);
            }
        }

        /// <summary>
        /// 런 상태를 세이브에 반영한다. 클리어면 이번 판의 성장을 이어받고 사슬을 한 칸 잇는다.
        /// 패배면 <b>성장은 다시 저장하지 않고</b>(직전 클리어 지점 유지) 사슬만 끊는다 —
        /// 죽은 판에서 파밍한 소울을 은행에 넣지 않기 위해서다(§5 보스 재도전과 이어진다).
        /// </summary>
        private void SaveRunProgress(bool cleared)
        {
            RunState run = RunStateService.Current;
            if (cleared)
            {
                run.UpdateStats(Session.Stats.Capture());
                run.AdvanceChain();
            }
            else
            {
                run.BreakChain();
            }

            RunStateService.Save();
        }
    }
}
