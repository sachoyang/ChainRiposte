using System.Collections;
using System.Collections.Generic;
using ChainRiposte.Core.Board;
using ChainRiposte.Core.Combat;
using ChainRiposte.Core.Flow;
using ChainRiposte.Core.Match;
using ChainRiposte.Game.Combat;
using ChainRiposte.Game.Config;
using ChainRiposte.Game.Puzzle;
using ChainRiposte.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ChainRiposte.Game.Tutorial
{
    /// <summary>
    /// <b>첫 등반 튜토리얼</b> — 유도형 (<c>Docs/TUTORIAL.md</c> §4). 씬을 새로 만들지 않고
    /// <b>Main 그대로</b>에서 돈다. 가르치려는 것이 바로 그 화면이기 때문이다.
    ///
    /// <para>이 컴포넌트는 <b>옆에서 지켜보다 끼어들 뿐</b>이다 — 퍼즐·전투 코드에는 튜토리얼을 아는
    /// 곳이 없고, 여기서 구독하는 것도 이미 있던 이벤트뿐이다. 그래서 튜토리얼을 통째로 지워도
    /// 게임이 그대로 돈다.</para>
    ///
    /// <para><b>단계 1(왜 오르는가)은 여기 없다.</b> 그것은 판 시작 직전의 글 카드라
    /// <c>Stage_Tutorial ▸ Introduces</c>에 넣으면 그만이다(①과 같은 길). 여기가 맡는 것은
    /// 플레이가 얽히는 <b>2~8단계</b>다.</para>
    /// </summary>
    public sealed class TutorialDirector : MonoBehaviour
    {
        /// <summary>「첫 등반을 마쳤다」의 세이브 키. 단계마다 남기지 않는 이유는 <see cref="TutorialCard.ShowOnce"/> 참조.</summary>
        public const string TopicId = "climb.first";

        [Header("참조 (Add Tutorial Director To Main 이 배선)")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private PuzzleController puzzle;
        [SerializeField] private CombatController combat;
        [SerializeField] private BoardView boardView;
        [SerializeField] private PuzzleInput puzzleInput;
        [Tooltip("카드가 떠 있는 동안 잠근다 — 전투 입력은 uGUI를 안 거치므로 딤이 못 막는다.")]
        [SerializeField] private CombatInput combatInput;
        [SerializeField] private TutorialCard card;
        [Tooltip("건너뛰기. 없으면 개발자 자신이 매번 다시 본다(§4.7).")]
        [SerializeField] private Button skipButton;

        [Header("단계 카드 — 순서대로 (비운 단계는 조용히 건너뛴다)")]
        [Tooltip("2 — 3매치 = 처치 + 소울. 이 카드만 정해진 한 수를 밝힌다.")]
        [SerializeField] private TutorialTopicSO stepMatch;
        [Tooltip("3 — 성난 몬스터. 붉은 놈이 처음 나타난 순간에 뜬다.")]
        [SerializeField] private TutorialTopicSO stepEnrage;
        [Tooltip("4 — 보스 타일이 내려오면 전투.")]
        [SerializeField] private TutorialTopicSO stepBossTile;
        [Tooltip("5 — 패링. 이 게임의 심장이다.")]
        [SerializeField] private TutorialTopicSO stepParry;
        [Tooltip("6 — 헛치면 손해다. 실제로 헛친 순간에만 뜬다.")]
        [SerializeField] private TutorialTopicSO stepWhiff;
        [Tooltip("7 — 체간을 무너뜨리면 인살.")]
        [SerializeField] private TutorialTopicSO stepExecute;
        [Tooltip("8 — 죽어도 스탯은 남는다(사슬). 판이 끝난 뒤에 뜬다.")]
        [SerializeField] private TutorialTopicSO stepChain;

        private PuzzleEngine _engine;
        private CombatSystem _combat;
        private bool _running;
        private bool _matchDone;
        private bool _parryDone;
        private bool _whiffShown;
        private bool _brokenShown;
        private Coroutine _cardRoutine;

        private void Awake()
        {
            if (!ShouldRun())
            {
                // 이미 봤거나 튜토리얼 판이 아니다 — 아무 일도 없다.
                // 단 건너뛰기 버튼은 <b>다른 캔버스에 산다</b>(TutorialCanvas/SkipTutorial).
                // 자기만 꺼서는 그 버튼이 화면에 남으므로 여기서 같이 내린다.
                if (skipButton != null)
                    skipButton.gameObject.SetActive(false);

                gameObject.SetActive(false);
                return;
            }

            _running = true;

            if (puzzle != null)
                puzzle.PuzzleBegun += OnPuzzleBegun;
            if (combat != null)
                combat.CombatBegun += OnCombatBegun;
            if (gameManager != null && gameManager.Session != null)
                gameManager.Session.PhaseChanged += OnPhaseChanged;
            if (skipButton != null)
                skipButton.onClick.AddListener(Skip);
        }

        private void OnDestroy()
        {
            if (puzzle != null)
                puzzle.PuzzleBegun -= OnPuzzleBegun;
            if (combat != null)
                combat.CombatBegun -= OnCombatBegun;
            if (gameManager != null && gameManager.Session != null)
                gameManager.Session.PhaseChanged -= OnPhaseChanged;
            Unhook();
        }

        /// <summary>
        /// 이 판에서 튜토리얼이 도는가. <b>판이 정하고</b>(<c>StageDataSO ▸ 첫 등반 튜토리얼</c>)
        /// 본 적이 있으면 안 돈다 — 코드에 스테이지 이름을 적지 않는다는 규칙은 여기서도 같다.
        /// </summary>
        private bool ShouldRun() =>
            gameManager != null && gameManager.StageData != null
            && gameManager.StageData.RunsFirstClimbTutorial
            && !TutorialService.HasSeen(TopicId);

        // ── 퍼즐 ──────────────────────────────────────────────────────

        private void OnPuzzleBegun(PuzzleEngine engine)
        {
            _engine = engine;
            StartCoroutine(PuzzleRoutine());
        }

        /// <summary>
        /// 2 → 3 → 4단계. 각 단계는 <b>조건이 찰 때까지</b> 기다리므로, 플레이어가 늦게 이해해도
        /// 튜토리얼이 앞서 나가지 않는다.
        /// </summary>
        private IEnumerator PuzzleRoutine()
        {
            // ── 2. 정해진 한 수만 ──
            // 좌표를 코드에 적지 않는다 — 지금 보드에서 실제로 매치가 되는 수를 엔진에게 물어
            // 그 두 칸만 밝힌다. 보드를 다시 짜도, 씨앗을 바꿔도 그대로 맞는다.
            if (MoveFinder.TryFindMove(_engine.Board, out GridPos a, out GridPos b))
            {
                yield return Card(stepMatch);
                Focus(a, b);

                _matchDone = false;
                boardView.StepCleared += OnStepCleared;
                while (_running && !_matchDone)
                    yield return null;
                boardView.StepCleared -= OnStepCleared;

                Focus(); // 밝기와 입력을 한 번에 되돌린다
            }

            if (!_running)
                yield break;

            // ── 3. 성난 몬스터 ──
            // 붉은 놈이 <b>실제로 나타난 순간</b>에 설명한다. 여기서는 수를 가두지 않는다 —
            // 없애는 길이 여럿이라 한 수만 밝히면 오히려 거짓말이 된다.
            while (_running && !AnyEnraged())
                yield return null;

            if (!_running)
                yield break;

            yield return Card(stepEnrage);

            // ── 4. 보스 타일 ──
            // 전투로 넘어가기 전에 한 번. 보스 타일이 내려오는 것을 보고 나서 읽어야 뜻이 통한다.
            while (_running && !AnyBossTile() && gameManager.Session.Phase == GamePhase.Puzzle)
                yield return null;

            if (_running && gameManager.Session.Phase == GamePhase.Puzzle)
                yield return Card(stepBossTile);
        }

        private void OnStepCleared(CascadeStep step) => _matchDone = true;

        private bool AnyEnraged() => AnyTile(tile => tile.Status.IsEnraged);

        private bool AnyBossTile() => AnyTile(tile => tile.Category == TileCategory.Boss);

        private bool AnyTile(System.Func<Tile, bool> predicate)
        {
            if (_engine == null)
                return false;

            foreach (GridPos pos in _engine.Board.ActivePositions())
            {
                Tile tile = _engine.Board.GetTile(pos);
                if (tile != null && predicate(tile))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// <b>밝은 칸과 누를 수 있는 칸을 한 번에</b> 정한다 (<c>Docs/TUTORIAL.md</c> §4.5).
        /// 인자가 없으면 스포트라이트를 끄고 입력을 전부 연다.
        ///
        /// <para>두 곳에 따로 적으면 반드시 어긋나고, 그때 생기는 "밝은데 안 눌리는 칸"은
        /// 플레이어에게 고장으로 읽힌다. 그래서 <b>목록이 하나</b>다.</para>
        /// </summary>
        private void Focus(params GridPos[] cells)
        {
            if (cells == null || cells.Length == 0)
            {
                boardView.SetSpotlight(null);
                puzzleInput.SwapFilter = null;
                return;
            }

            var allowed = new HashSet<GridPos>(cells);
            boardView.SetSpotlight(allowed);
            // 두 칸이 모두 목록에 있어야 한다 — 밝은 칸에서 어두운 칸으로 미는 것도 막는다.
            puzzleInput.SwapFilter = (from, to) => allowed.Contains(from) && allowed.Contains(to);
        }

        // ── 전투 ──────────────────────────────────────────────────────

        private void OnCombatBegun(CombatSystem system)
        {
            _combat = system;
            _combat.Whiffed += OnWhiffed;
            _combat.BossBroken += OnBossBroken;
            StartCoroutine(CombatRoutine());
        }

        /// <summary>
        /// 5단계. 6·7단계는 시점이 플레이어에게 달려 있어(헛치는 순간·무너뜨리는 순간)
        /// 여기서 순서대로 기다리지 않고 <b>이벤트에서</b> 띄운다.
        /// </summary>
        private IEnumerator CombatRoutine()
        {
            yield return Card(stepParry);

            _parryDone = false;
            _combat.AttackParried += OnParried;
            while (_running && !_parryDone)
                yield return null;
            _combat.AttackParried -= OnParried;
        }

        private void OnParried(BossNoteConfig note) => _parryDone = true;

        private void OnWhiffed()
        {
            // 헛칠 때마다 설명하면 잔소리다 — 처음 한 번만.
            if (_whiffShown || !_running)
                return;

            _whiffShown = true;
            ShowLater(stepWhiff);
        }

        private void OnBossBroken()
        {
            if (_brokenShown || !_running)
                return;

            _brokenShown = true;
            ShowLater(stepExecute);
        }

        // ── 끝 ────────────────────────────────────────────────────────

        private void OnPhaseChanged(GamePhase previous, GamePhase next)
        {
            if (!_running || (next != GamePhase.Victory && next != GamePhase.Defeat))
                return;

            // 8단계는 이겼든 졌든 뜬다 — 오히려 <b>졌을 때</b> 가장 필요한 말이다(스탯은 남는다).
            ShowLater(stepChain);
            Finish();
        }

        /// <summary>
        /// 지금 도는 카드가 있으면 갈아치우지 않고 그것을 멈추고 새로 띄운다. 이벤트에서 부르므로
        /// 코루틴을 직접 시작할 수 없는 자리를 위한 것이다.
        /// </summary>
        private void ShowLater(TutorialTopicSO topic)
        {
            if (topic == null)
                return;

            if (_cardRoutine != null)
                StopCoroutine(_cardRoutine);
            _cardRoutine = StartCoroutine(Card(topic));
        }

        /// <summary>
        /// 카드 한 장. 떠 있는 동안 <b>전투 입력을 잠근다</b> — 전투 입력은 uGUI를 안 거치므로
        /// 카드의 딤이 못 막는다. 안 잠그면 카드를 읽는 동안 누른 것이 헛침으로 처리된다.
        /// (퍼즐 입력은 딤이 raycast를 먹어 저절로 막힌다.)
        /// </summary>
        private IEnumerator Card(TutorialTopicSO topic)
        {
            if (topic == null || card == null)
                yield break;

            bool lockCombat = combatInput != null && _combat != null && !_combat.Finished;
            if (lockCombat)
                combatInput.SetActive(false);

            yield return card.ShowOnce(topic);

            if (lockCombat)
                combatInput.SetActive(true);
        }

        /// <summary>건너뛰기 — 지금 걸린 것을 전부 풀고 「봤다」로 남긴다.</summary>
        private void Skip()
        {
            if (!_running)
                return;

            Debug.Log("[Tutorial] 첫 등반 튜토리얼을 건너뛰었습니다.");
            Finish();
        }

        private void Finish()
        {
            _running = false;
            TutorialService.MarkSeen(TopicId);
            Focus();
            Unhook();

            if (skipButton != null)
                skipButton.gameObject.SetActive(false);
        }

        private void Unhook()
        {
            if (boardView != null)
                boardView.StepCleared -= OnStepCleared;

            if (_combat == null)
                return;

            _combat.Whiffed -= OnWhiffed;
            _combat.BossBroken -= OnBossBroken;
            _combat.AttackParried -= OnParried;
        }
    }
}
