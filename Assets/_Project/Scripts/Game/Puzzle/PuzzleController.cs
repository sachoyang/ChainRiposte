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
            if (gameManager.Session.Phase != GamePhase.Puzzle)
                return;

            // 듀얼 카운트다운의 실시간 축은 퍼즐 페이즈 동안 항상 흐른다 (연출 중 포함)
            _intrusion?.Tick(Time.deltaTime);

            // 잡몹 위협은 <b>연출 중에는 멈춘다</b> — 애니메이션이 도는 동안 보드가 바뀌면
            // 화면과 모델이 어긋난다. 대신 그만큼의 시간은 아예 안 흐른 것으로 친다.
            if (!_replaying)
                TickGimmickTime(Time.deltaTime);
        }

        /// <summary>
        /// 턴과 무관하게 도는 위협(성난 몬스터). <b>손을 놓고 있어도 자란다</b> —
        /// 이것이 없으면 가만히 기다리는 것이 가장 안전한 수가 되고, 보스 시계만 흘러
        /// 만피로 보스전에 갈 수 있다.
        /// </summary>
        private void TickGimmickTime(float deltaSeconds)
        {
            if (_engine == null)
                return;

            GimmickPhase phase = _engine.TickTime(deltaSeconds);
            if (phase.IsEmpty && phase.PlayerDamage <= 0)
                return;

            StartCoroutine(ReplayGimmickPhase(phase));
        }

        /// <summary>
        /// 시간이 만든 사건의 연출과 피해 적용.
        ///
        /// <para>보드가 안 바뀌는 사건(카운트 감소·피격)은 <b>입력을 막지 않는다</b> —
        /// 1.6초마다 손이 묶이면 퍼즐을 풀 수가 없다. 낙하·연쇄가 실제로 생긴 경우에만 잠근다.</para>
        /// </summary>
        private IEnumerator ReplayGimmickPhase(GimmickPhase phase)
        {
            bool boardMoves = phase.FallPhases.Count > 0 || phase.Cascades.Count > 0;
            if (boardMoves)
            {
                _replaying = true;
                input.SetActive(false);
            }

            yield return boardView.PlayGimmickPhase(phase);

            if (boardMoves)
                _replaying = false;

            if (TakeGimmickDamage(phase.PlayerDamage))
            {
                gameManager.Session.EndStage(victory: false);
                yield break;
            }

            // 그 사이에 스왑이 시작됐다면 그쪽이 입력의 주인이다 — 여기서 켜면 연출 중에 손이 풀린다.
            if (!_replaying && gameManager.Session.Phase == GamePhase.Puzzle)
                input.SetActive(true);
        }

        /// <summary>
        /// 기믹이 낸 피해를 <b>방어를 적용해</b> 입는다 (전투의 노트와 같은 함수 — <c>PlayerStats.ResolveIncomingDamage</c>).
        /// 예전에는 여기서 생피해를 그대로 넣어서 <b>방어를 올려도 퍼즐에서는 똑같이 아팠다.</b>
        /// </summary>
        /// <returns>이 피해로 죽었으면 true.</returns>
        private bool TakeGimmickDamage(int rawDamage)
        {
            int damage = gameManager.Session.Stats.ResolveIncomingDamage(rawDamage);
            return damage > 0 && gameManager.Session.Health.ApplyDamage(damage);
        }

        private void BeginPuzzle()
        {
            _stageConfig = gameManager.StageConfig;

            _intrusion = new IntrusionSystem(_stageConfig, () => gameManager.Session.Stats.TotalSoulsEarned);
            _engine = new PuzzleEngine(_stageConfig, _intrusion.Spawner);
            _intrusion.AttachBoard(_engine.Board);

            _intrusion.EngageTimerChanged += hud.SetBossTimer;
            _intrusion.Engage += OnBossEngage;

            boardView.Build(_engine.Board);
            cameraFit.FitTo(boardView.WorldBounds);
            hud.Bind(gameManager.Session, _engine, gameManager);
            if (juice != null)
                juice.BindPuzzle(boardView, _intrusion.Spawner);
            input.SetActive(true);
        }

        /// <summary>
        /// 보스전 돌입 — 판 시계 만료(<paramref name="bossTile"/>가 null)이거나 보스 타일이 바닥에 닿았을 때.
        /// <b>어느 쪽도 페널티가 없다.</b> 퍼즐에서 성난 몬스터에게 맞아 깎인 HP가 그대로 전투로 이어지는 것이
        /// 곧 "퍼즐을 못 풀었을 때의 처벌"이다 — 예전의 기습 반토막 규칙을 대체한다.
        /// </summary>
        private void OnBossEngage(Tile bossTile)
        {
            input.SetActive(false);

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

                // 런 경제(인컴 배수 + 사슬 배수)를 통과시킨 뒤 적립한다 (Docs/PROGRESSION.md §2.4)
                gameManager.Session.Stats.AddSouls(gameManager.ScaleSoulIncome(result.TotalSouls));

                // 기믹 피해(성난 몬스터·시한폭탄 등)는 퍼즐 중에도 HP를 깎는다 (GDD §3.6)
                if (TakeGimmickDamage(result.Gimmicks.PlayerDamage))
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
