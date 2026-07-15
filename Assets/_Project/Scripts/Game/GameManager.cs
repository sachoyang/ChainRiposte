using ChainRiposte.Core.Flow;
using ChainRiposte.Core.Stage;
using ChainRiposte.Game.Config;
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

            StageConfig = stageData.ToConfig();
            Session = new GameSession(statsConfig.ToConfig());
            Session.PhaseChanged += OnPhaseChanged;
        }

        private void Start()
        {
            Session.StartPuzzle();
        }

        private void OnDestroy()
        {
            if (Session != null)
                Session.PhaseChanged -= OnPhaseChanged;
        }

        private void OnPhaseChanged(GamePhase previous, GamePhase next)
        {
            Debug.Log($"[GameFlow] {previous} → {next}");
        }
    }
}
