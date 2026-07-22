using System.Collections;
using ChainRiposte.Core.Board;
using ChainRiposte.Core.Flow;
using ChainRiposte.Core.Intrusion;
using ChainRiposte.Core.Match;
using ChainRiposte.Core.Stage;
using ChainRiposte.Game.Config;
using ChainRiposte.Game.UI;
using UnityEngine;

namespace ChainRiposte.Game.Puzzle
{
    /// <summary>
    /// 퍼즐 페이즈의 Unity 측 오케스트레이터.
    /// 입력 → 엔진 → 결과 재생 → 세션(영혼석/패배) 반영을 연결한다.
    /// </summary>
    public sealed class PuzzleController : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private BoardView boardView;
        [SerializeField] private PuzzleInput input;
        [SerializeField] private CameraFit2D cameraFit;
        [SerializeField] private PuzzleHud hud;
        [SerializeField] private Juice.JuiceDirector juice;

        private PuzzleEngine _engine;
        private IntrusionSystem _intrusion;
        private StageConfig _stageConfig;
        private bool _replaying;

        private void Awake()
        {
            input.SwapRequested += OnSwapRequested;
            boardView.Shuffling += OnShuffling;
            gameManager.Session.PhaseChanged += OnPhaseChanged;
        }

        private void OnDestroy()
        {
            input.SwapRequested -= OnSwapRequested;
            if (boardView != null)
                boardView.Shuffling -= OnShuffling;
            if (gameManager != null && gameManager.Session != null)
                gameManager.Session.PhaseChanged -= OnPhaseChanged;
        }

        /// <summary>둘 수 있는 수가 없어 보드를 섞는 중 — 플레이어에게 무슨 일이 났는지 알린다.</summary>
        private void OnShuffling()
        {
            if (hud != null)
                hud.FlashBanner("puzzle.banner.noMoves", 1.2f);
        }

        private void OnPhaseChanged(GamePhase previous, GamePhase next)
        {
            if (next == GamePhase.Puzzle)
                BeginPuzzle();
            else
                input.SetActive(false);
        }

        private void Update()
        {
            // 듀얼 카운트다운의 실시간 축은 퍼즐 페이즈 동안 항상 흐른다 (연출 중 포함)
            if (_intrusion != null && gameManager.Session.Phase == GamePhase.Puzzle)
                _intrusion.Tick(Time.deltaTime);
        }

        private void BeginPuzzle()
        {
            _stageConfig = gameManager.StageConfig;

            _intrusion = new IntrusionSystem(_stageConfig, () => gameManager.Session.Stats.TotalSoulsEarned);
            _engine = new PuzzleEngine(_stageConfig, _intrusion.Spawner);
            _intrusion.AttachBoard(_engine.Board);

            _engine.TurnsChanged += _intrusion.OnTurnConsumed;
            _intrusion.CountdownChanged += boardView.UpdateBossCountdown;
            _intrusion.Engage += OnBossEngage;

            boardView.Build(_engine.Board);
            cameraFit.FitTo(boardView.WorldBounds);
            hud.Bind(gameManager.Session, _engine);
            if (juice != null)
                juice.BindPuzzle(boardView, _intrusion.Spawner);
            input.SetActive(true);
        }

        private void OnBossEngage(Tile bossTile, bool ambush)
        {
            input.SetActive(false);

            if (ambush)
            {
                // 기습 페널티: 현재 HP를 배율만큼 깎는다 (기본 0.5 = 반토막)
                var health = gameManager.Session.Health;
                int damage = Mathf.CeilToInt(health.Current * (1f - _stageConfig.AmbushHpMultiplier));
                if (health.ApplyDamage(damage))
                {
                    gameManager.Session.EndStage(victory: false);
                    return;
                }
            }

            // 곧바로 전투로 넘기지 않는다 — 파밍한 포인트를 쓸 시간을 먼저 준다 (시간 제한 없음)
            gameManager.Session.StartIntermission();
        }

        private void OnSwapRequested(GridPos a, GridPos b)
        {
            if (_replaying || _engine == null)
                return;

            SwapResult result = _engine.TrySwap(a, b);
            StartCoroutine(ReplayAndSettle(result));
        }

        private IEnumerator ReplayAndSettle(SwapResult result)
        {
            _replaying = true;
            input.SetActive(false);

            yield return boardView.PlaySwapResult(result);

            if (result.Success)
            {
                if (result.TotalPotions > 0)
                    gameManager.Session.Health.Heal(result.TotalPotions * _stageConfig.PotionHealAmount);

                gameManager.Session.Stats.AddSouls(result.TotalSouls);

                // 기믹 피해(시한폭탄 폭발 등)는 퍼즐 중에도 HP를 깎는다 (GDD §3.6)
                if (result.Gimmicks.PlayerDamage > 0 &&
                    gameManager.Session.Health.ApplyDamage(result.Gimmicks.PlayerDamage))
                {
                    _replaying = false;
                    gameManager.Session.EndStage(victory: false);
                    yield break;
                }

                _intrusion.OnBoardSettled(); // 새 보스 타일 추적 + 바닥 도달(정상 돌입) 판정
            }

            _replaying = false;

            // 기습/정상 돌입으로 이미 퍼즐이 끝났으면 여기서 종료
            if (gameManager.Session.Phase != GamePhase.Puzzle)
                yield break;

            if (_engine.OutOfTurns)
                gameManager.Session.EndStage(victory: false);
            else
                input.SetActive(true);
        }
    }
}
