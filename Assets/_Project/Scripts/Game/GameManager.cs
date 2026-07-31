using System.Collections;
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
        [Tooltip("런 경제(소울 인컴·사슬 배수). 비우면 기본값(배수 1)으로 동작한다 — Main 단독 실행 대비.")]
        [SerializeField] private RunEconomyConfigSO economyConfig;
        [Tooltip("난이도 곡선(고리 깊이·NG+ 회차별 보스 배수). 비우면 배수 1 — 보스 에셋 값 그대로 싸운다.")]
        [SerializeField] private DifficultyCurveSO difficultyCurve;
        [SerializeField] private StageDataSO stageData;
        [Tooltip("판 시작 직전에 뜨는 기믹 소개 카드. 비우면 씬에서 찾고, 그래도 없으면 카드 없이 바로 시작한다 " +
                 "— 안내 자리가 아직 없다고 판에 못 들어가면 안 된다.")]
        [SerializeField] private UI.TutorialCard tutorialCard;

        private RunEconomyConfig _economy;
        // 사슬 배수는 이 판에 들어온 시점 값으로 고정한다 — 판 도중에 바뀌지 않는다(승리 시에만 오른다).
        private int _chainStepAtEntry;
        // 이 판에 들어온 시점의 광맥 잔량. 판 도중에는 이 값에서 깎아 나가고, 클리어해야 세이브에 확정된다.
        private int _remainingSoulsAtEntry;
        private int _harvestedThisRun;

        /// <summary>
        /// 이 스테이지에 아직 남아 있는 소울(광맥 잔량). 무제한이면 <see cref="int.MaxValue"/>.
        /// HUD가 "이 땅의 소울은 모두 거뒀다"를 띄우는 근거다.
        /// </summary>
        public int RemainingStageSouls => Mathf.Max(0, _remainingSoulsAtEntry - _harvestedThisRun);

        /// <summary>이 스테이지에 매장량이 정해져 있는가 — false면 광맥 개념이 꺼진 것이라 UI도 아무 말 안 한다.</summary>
        public bool HasSoulBudget => _remainingSoulsAtEntry != int.MaxValue;

        /// <summary>광맥이 말랐다 — 더 캐도 소울이 안 나온다.</summary>
        public bool StageSoulsDepleted => HasSoulBudget && RemainingStageSouls <= 0;

        /// <summary>광맥 잔량이 바뀔 때 — HUD 갱신 훅. (남은 양)</summary>
        public event System.Action<int> RemainingStageSoulsChanged;

        public GameSession Session { get; private set; }

        /// <summary>이번 씬에서 플레이할 스테이지. 퍼즐/전투 컨트롤러가 공유한다.</summary>
        public StageConfig StageConfig { get; private set; }

        /// <summary>스테이지 데이터 원본 — 순수 C# config에 담을 수 없는 것(보스 스프라이트 등)을 읽을 때 쓴다.</summary>
        public StageDataSO StageData => stageData;

        /// <summary>
        /// 이 판을 깨면 엔딩인가. <b>지도가 알려 준 것이 우선</b>이고(정렬된 마지막 노드),
        /// 스테이지 에셋의 플래그는 지도를 안 거친 경우(Main 단독 실행)와 덮어쓰기용이다.
        /// </summary>
        public bool IsFinalLink { get; private set; }

        /// <summary>
        /// 이 판이 사슬의 몇 번째 고리인가 (첫 판 = 0). 지도가 세어 준 값이고,
        /// 월드맵을 안 거친 Main 단독 실행이면 0이다.
        /// </summary>
        public int LinkDepth { get; private set; }

        /// <summary>
        /// 이 판이 <b>그 보스를 마지막으로 만나는 판</b>인가 — 기억은 여기서만 떨어진다.
        /// 지도가 세어 준 값이고, 단독 실행이면 true다(실험 스테이지에서 바로 확인할 수 있어야 한다).
        /// </summary>
        public bool IsBossFinale { get; private set; }

        /// <summary>
        /// 이 판에 적용된 보스 배수 — 이미 <see cref="StageConfig"/>의 보스에 곱해져 있다.
        /// 디버그·HUD 표시용으로만 읽을 것(다시 곱하면 두 번 부푼다).
        /// </summary>
        public Core.Combat.DifficultyConfig Difficulty { get; private set; } =
            Core.Combat.DifficultyConfig.Identity;

        /// <summary>
        /// 이 판에 들어온 시점에 <b>이미 삼킨</b> 기억 id들 (먹은 순서). 아이콘 줄이 이걸 그린다.
        ///
        /// <para>진입 시점의 사본이다 — <b>이 판의 보스 기억은 여기 없다.</b> 지금 상대하는 보스의 기억을
        /// 그 보스와 싸우는 동안 쓸 수는 없다.</para>
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<string> MemoriesAtEntry { get; private set; } =
            System.Array.Empty<string>();

        /// <summary>삼킨 기억들이 이 전투에 주는 효과의 합. 전투가 이 한 벌만 읽는다.</summary>
        public Core.Combat.BossMemoryConfig MemoryEffects { get; private set; } =
            Core.Combat.BossMemoryConfig.None;

        /// <summary>이 판에서 <b>새로</b> 삼킨 기억. 없거나(이미 가진 보스) 아직 안 이겼으면 null.</summary>
        public BossMemorySO GainedMemory { get; private set; }

        /// <summary>새 기억을 삼킨 순간 — 결과 화면이 그것만 강조하는 근거.</summary>
        public event System.Action<BossMemorySO> MemoryGained;

        private void Awake()
        {
            // 월드맵에서 선택하고 들어왔다면 그 스테이지를 우선한다
            bool fromMap = StageSelection.Selected != null;
            if (fromMap)
                stageData = StageSelection.Selected;

            IsFinalLink = (fromMap && StageSelection.SelectedIsFinalLink)
                || (stageData != null && stageData.IsFinalLink);
            LinkDepth = fromMap ? StageSelection.SelectedLinkDepth : 0;
            // 지도를 안 거친 단독 실행은 그 자리에서 기억을 확인할 수 있어야 한다(실험 스테이지).
            IsBossFinale = !fromMap || StageSelection.SelectedIsBossFinale;

            if (statsConfig == null || stageData == null)
            {
                Debug.LogError($"{nameof(GameManager)}: {nameof(statsConfig)} 또는 {nameof(stageData)}가 비어 있습니다.", this);
                enabled = false;
                return;
            }

            // 진입 자체를 기록한다 — 월드맵은 이 기록이 있어야 보스·기믹을 공개한다 (클리어 못 해도 공개).
            ProgressService.MarkAttempted(stageData.StageId);

            StageConfig = stageData.ToConfig();
            ApplyDifficulty();
            // 저장된 런의 성장을 씨앗으로 이어받는다 — 성장 캐리 (Docs/PROGRESSION.md)
            Session = new GameSession(BuildStatsConfig(), RunStateService.Current.Stats);
            Session.PhaseChanged += OnPhaseChanged;

            // 기억은 세이브에 id로만 남으므로 여기서 에셋으로 되돌려 효과 한 벌로 합친다 (§2.2).
            MemoriesAtEntry = new System.Collections.Generic.List<string>(RunStateService.Current.AcquiredMemoryIds);
            MemoryEffects = Memories.MemoryLibrary.CombinedConfig(MemoriesAtEntry);

            _economy = economyConfig != null ? economyConfig.ToConfig() : new RunEconomyConfig();
            _chainStepAtEntry = RunStateService.Current.ChainStep;
            _remainingSoulsAtEntry = _economy.RemainingSouls(
                stageData.SoulBudget, RunStateService.Current.GetHarvested(stageData.StageId));
            _harvestedThisRun = 0;
        }

        private void Start()
        {
            StartCoroutine(BeginStage());
        }

        /// <summary>
        /// 이 판이 처음 소개하는 기믹이 있으면 <b>퍼즐이 뜨기 전에</b> 카드로 보여 준다
        /// (<c>Docs/TUTORIAL.md</c> §3.1). 대부분의 판에서는 큐가 비어 있어 한 프레임도 안 먹는다.
        ///
        /// <para>기믹이 보드에 실제로 나타나는 순간에 띄우지 않는 이유: 흐름을 끊고, "첫 사슬이 떨어지는
        /// 순간"을 잡아내는 구현이 기믹마다 따로 필요하다. <b>판 시작 직전은 걸리는 지점이 한 곳</b>이라
        /// 기믹이 늘어도 코드가 안 는다.</para>
        /// </summary>
        private IEnumerator BeginStage()
        {
            if (tutorialCard == null)
                tutorialCard = FindFirstObjectByType<UI.TutorialCard>();

            if (tutorialCard != null)
                yield return tutorialCard.Show(stageData.Introduces);

            Session.StartPuzzle();
        }

        /// <summary>
        /// 매치로 번 소울에 런 경제(인컴 배수 + 이 판의 사슬 배수)를 적용하고, <b>남은 광맥만큼으로 자른</b>
        /// 최종 획득량. 퍼즐이 <see cref="Core.Stats.PlayerStats.AddSouls"/>에 넣기 전에 이걸 통과시킨다.
        ///
        /// <para>소울이 지나는 창구가 여기 하나뿐이라 자르는 지점도 하나다 — 매치·콤보·기믹 어디서 왔든
        /// 광맥을 넘길 수 없다. 다만 <b>확정은 클리어할 때</b>다(<see cref="SaveRunProgress"/>) —
        /// 죽으면 그 판의 벌이가 통째로 무효이므로 캔 양도 남으면 안 된다.</para>
        /// </summary>
        public int ScaleSoulIncome(int rawSouls)
        {
            int scaled = (_economy ?? new RunEconomyConfig()).ScaleSoulIncome(rawSouls, _chainStepAtEntry);
            if (!HasSoulBudget)
                return scaled;

            int granted = Mathf.Clamp(scaled, 0, RemainingStageSouls);
            if (granted > 0)
            {
                _harvestedThisRun += granted;
                RemainingStageSoulsChanged?.Invoke(RemainingStageSouls);
            }

            return granted;
        }

        /// <summary>
        /// 고리 깊이·NG+ 회차만큼 보스를 부풀린다 (<c>Docs/PROGRESSION.md</c> §2.5).
        ///
        /// <para><b>자르는 지점이 여기 하나</b>인 것이 요점이다 — 보스 config가 만들어지는 곳도,
        /// 배수가 곱해지는 곳도 하나뿐이라 "두 번 부풀었다"가 생길 수 없다. 전투 쪽은 이미 부푼 값을
        /// 받아 평소처럼 싸울 뿐이고, 스케일링을 아는 코드가 <c>CombatSystem</c>에 없다.</para>
        /// </summary>
        private void ApplyDifficulty()
        {
            if (difficultyCurve == null || StageConfig?.Boss == null)
                return;

            Difficulty = difficultyCurve.ToConfig().Evaluate(LinkDepth, RunStateService.Current.NewGamePlusCount);
            StageConfig.Boss = Core.Combat.BossConfigScaler.Scale(StageConfig.Boss, Difficulty);

            if (!Difficulty.IsIdentity)
                Debug.Log($"[GameManager] 고리 깊이 {LinkDepth} 난이도 — {Difficulty}");
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
        /// 런 상태를 세이브에 반영한다. 클리어면 이번 판의 성장을 이어받고, 캔 소울을 광맥에서 덜어내고,
        /// 사슬을 한 칸 잇는다.
        ///
        /// <para>패배면 <b>아무것도 저장하지 않고</b>(직전 클리어 지점 유지) 사슬만 끊는다 —
        /// 성장도 채굴량도 남지 않으므로 "죽으면 그 판은 없던 일"이 규칙 하나로 유지된다(§5).
        /// 둘 중 하나만 남기면 죽을수록 그 스테이지가 가난해지는 이중 처벌이 된다.</para>
        /// </summary>
        private void SaveRunProgress(bool cleared)
        {
            RunState run = RunStateService.Current;
            if (cleared)
            {
                run.UpdateStats(Session.Stats.Capture());
                run.Harvest(stageData.StageId, _harvestedThisRun);
                run.AdvanceChain();
                SwallowBossMemory(run);
            }
            else
            {
                run.BreakChain();
            }

            RunStateService.Save();
        }

        /// <summary>
        /// <b>엔딩까지 다 봤다 — 이 런을 다음 회차로 넘긴다</b> (<c>Docs/PROGRESSION.md</c> §2.6).
        /// 엔딩 연출이 끝나는 자리(<c>ResultScreen</c>)에서 딱 한 번 부른다.
        ///
        /// <para>빌드(스탯·소울 은행·기억)는 들고 가고, <b>사슬·소울 매장량·클리어 기록</b>이 되돌아간다 —
        /// 그래야 사슬을 처음부터 다시 오르면서 회차 배수(<c>DifficultyCurve</c>의 NG+ 몫)를
        /// 첫 판부터 받는다. 저장이 런과 진행도 둘로 나뉘어 있으므로 <b>여기서 둘 다</b> 넘긴다 —
        /// 한쪽만 넘기면 "회차는 2인데 지도는 다 깨져 있는" 상태가 된다.</para>
        /// </summary>
        public void CompleteRun()
        {
            RunStateService.EnterNewGamePlus();
            ProgressService.BeginNewGamePlus();
        }

        /// <summary>
        /// 벤 보스의 기억을 삼킨다 (<c>Docs/PROGRESSION.md</c> §2.2).
        ///
        /// <para><b>클리어할 때만</b> 부른다 — 성장·채굴량과 같은 규칙이다. 인살한 순간에 저장해 버리면
        /// 2페이즈 보스의 1페이즈만 인살하고 죽어도 기억이 남아, 죽어서 기억만 모으는 길이 열린다.</para>
        ///
        /// <para>그리고 <b>그 보스를 마지막으로 만나는 판</b>에서만 떨어진다(<see cref="IsBossFinale"/>).
        /// 같은 보스가 여러 판에 걸쳐 나오는 것은 상처를 입고 다시 나오는 것이고, 마지막 판이 그 보스의 끝이다
        /// — 첫 만남에 주면 뒤의 두 판은 "이미 다 가진 보스를 또 베는" 판이 된다.</para>
        /// </summary>
        private void SwallowBossMemory(RunState run)
        {
            if (!IsBossFinale)
                return;

            BossMemorySO memory = stageData.BossData != null ? stageData.BossData.Memory : null;
            if (memory == null || !run.AddMemory(memory.MemoryId))
                return;

            GainedMemory = memory;
            Debug.Log($"[Memory] '{memory.MemoryId}' 를 삼켰다.");
            MemoryGained?.Invoke(memory);
        }
    }
}
