using System;
using System.Collections.Generic;
using ChainRiposte.Core.Board;
using ChainRiposte.Core.Stage;
using UnityEngine;

namespace ChainRiposte.Game.Config
{
    /// <summary>
    /// 스테이지 정의 데이터. 기획자가 코드 수정 없이 인스펙터에서 편집한다.
    /// 보드 형태는 문자열 행 목록으로 마스킹한다 (전용 에디터 툴은 추후 단계).
    /// </summary>
    [CreateAssetMenu(menuName = "ChainRiposte/Stage Data", fileName = "Stage_")]
    public sealed class StageDataSO : ScriptableObject
    {
        private const char CellActive = 'O';
        private const char CellInactive = 'X';
        private const char CellWall = 'W';

        [Header("식별자 — 진행도 세이브 키 (비우면 에셋 이름 사용)")]
        [Tooltip("한 번 정하면 바꾸지 말 것. 바꾸면 기존 세이브의 클리어 기록과 연결이 끊긴다.")]
        [SerializeField] private string stageId = "";

        [Header("보드 형태 — 위 행부터 아래로. O=활성, X=비활성(구멍), W=벽")]
        [Tooltip("모든 행의 길이가 같아야 한다. 예: 하트/해골 모양은 X로 구멍을 뚫는다")]
        [SerializeField]
        private string[] boardRows =
        {
            "OOOOOOO",
            "OOOOOOO",
            "OOOOOOO",
            "OOOOOOO",
            "OOOOOOO",
            "OOOOOOO",
            "OOOOOOO",
            "OOOOOOO",
            "OOOOOOO",
        };

        [Tooltip("초기 배치된 벽(W)의 내구도")]
        [SerializeField, Min(1)] private int wallHp = 3;

        [Header("퍼즐 규칙")]
        [Tooltip("스왑 가능 횟수. <b>0이면 무제한</b>(기본). " +
            "이 게임의 퍼즐은 목표 달성형이 아니라 준비 구간이라, 수를 제한하면 " +
            "퍼즐을 열심히 푼 사람이 처벌받는다. 파밍 상한은 판 시계와 소울 광맥이 이미 맡고 있다.")]
        [SerializeField, Min(0)] private int turnLimit;
        [Tooltip("콤보(연쇄) 1단계당 영혼석 배수 증가량. 배수 = 1 + 증가량 × (콤보-1)")]
        [SerializeField, Min(0f)] private float comboSoulMultiplierStep = 0.5f;
        [Tooltip("물약 타일 1개 매치당 HP 회복량")]
        [SerializeField, Min(0)] private int potionHealAmount = 10;

        [Tooltip("소울 매장량 — 이 스테이지에서 한 런 동안 캘 수 있는 소울 총량(광맥). " +
            "다 캐면 재방문해도 더 안 나온다. 0이면 Run Economy Config 의 기본값으로 떨어진다.")]
        [SerializeField, Min(0)] private int soulBudget;

        [Header("타일 스폰 가중치 (리필 시 추첨 확률)")]
        [SerializeField] private SpawnWeightEntry[] spawnWeights = Array.Empty<SpawnWeightEntry>();

        [Header("보스 난입 — 동적 스포너 (y=스폰 확률 0~1, 두 곡선 중 최댓값 적용)")]
        [Tooltip("x = 누적 영혼석(점수)")]
        [SerializeField] private AnimationCurve bossChanceByScore = AnimationCurve.Linear(100f, 0f, 400f, 0.3f);
        [Tooltip("x = 퍼즐 경과 시간(초)")]
        [SerializeField] private AnimationCurve bossChanceBySeconds = AnimationCurve.Linear(45f, 0f, 180f, 0.3f);

        [Tooltip("판 전체 시계 — 퍼즐 시작 후 이 시간이 지나면 보스 타일과 무관하게 보스전에 돌입한다. " +
            "0이면 시계를 끄고 보스 타일이 내려올 때까지 기다린다. (페널티 없음 — 깎인 HP가 그대로 이어지는 것이 처벌)")]
        [SerializeField, Min(0f)] private float bossEngageSeconds = 90f;
        [Tooltip("보드 위 보스 타일 동시 최대 개수. 스폰 확률은 리필 타일 하나하나에 굴려지므로, " +
            "이 상한이 없으면 한 웨이브에 여러 개가 쏟아져 보드가 도배된다.")]
        [SerializeField, Min(1)] private int maxLiveBossTiles = 1;

        [Header("전투 (7단계)")]
        [Tooltip("이 스테이지에 난입하는 보스")]
        [SerializeField] private BossDataSO bossData;

        [Tooltip("이 판에서 싸울 인살 페이즈 수. <b>0이면 보스 에셋에 적힌 만큼 전부.</b>\n" +
                 "같은 보스를 앞 판에서는 1페이즈로, 뒤 판에서는 2페이즈로 쓰기 위한 손잡이다 " +
                 "— 페이즈 수만 다른 보스 에셋을 복제하면 그 보스의 채보·수치를 두 곳에 똑같이 적어야 한다.")]
        [SerializeField, Min(0)] private int battlePhaseLimit;

        [Tooltip("이 판이 사슬의 <b>마지막 고리</b>인가 — 클리어하면 엔딩이 나온다. " +
                 "보통은 비워 둔다: 지도가 '정렬된 마지막 노드'를 알아서 알려 주므로, " +
                 "여기 켜는 것은 지도 순서와 다르게 끝내고 싶을 때(또는 Main 단독 실행 확인용)뿐이다.")]
        [SerializeField] private bool isFinalLink;
        // 보스의 캐릭터별 겉모습은 여기 없다 — 보스 에셋(BossDataSO)의 「캐릭터별 겉모습」이 맡는다.
        // 스테이지마다 적으면 같은 그림을 스테이지 수만큼 되풀이하게 되고, 한 곳만 빠뜨려도 보스가 달라진다.

        [Header("스테이지 기믹 on/off (GDD §3.6) — 목록에 넣은 것만 활성화, 조합 가능")]
        [SerializeField] private GimmickType[] gimmicks = Array.Empty<GimmickType>();

        [Tooltip("위 목록에 있는 기믹만 이 수치를 사용한다")]
        [SerializeField] private GimmickTuning gimmickTuning = new();

        [Serializable]
        private struct SpawnWeightEntry
        {
            public TileDefinitionSO tile;
            [Min(0f)] public float weight;
        }

        /// <summary>기믹 밸런스 수치의 인스펙터 표현 (Core의 GimmickSettings로 변환된다).</summary>
        [Serializable]
        private sealed class GimmickTuning
        {
            [Header("전염 — 부패 타일")]
            [Tooltip("퍼즐 시작 시 뿌려지는 부패 타일 수")]
            [Min(0)] public int corruptionSeeds = 2;
            [Tooltip("부패가 퍼지는 주기(턴). 1이면 매 턴")]
            [Min(1)] public int corruptionSpreadEveryTurns = 1;
            [Tooltip("부패가 이 비율을 넘으면 확산 정지 (완전 데드락 방지)")]
            [Range(0.05f, 1f)] public float maxCorruptionRatio = 0.35f;

            [Header("시한폭탄")]
            [Tooltip("새로 스폰되는 몬스터가 폭탄이 될 확률")]
            [Range(0f, 1f)] public float bombChance = 0.12f;
            [Tooltip("폭발까지의 턴 수")]
            [Min(1)] public int bombTurns = 3;
            [Tooltip("폭발 시 플레이어 HP 피해")]
            [Min(0)] public int bombDamage = 12;

            [Header("사슬 결박")]
            [Tooltip("퍼즐 시작 시 결박된 채로 놓이는 타일 수")]
            [Min(0)] public int chainInitialCount = 3;
            [Tooltip("새로 스폰되는 몬스터가 결박될 확률")]
            [Range(0f, 1f)] public float chainChance = 0.08f;

            [Header("성난 몬스터 (상시 — 위 목록과 무관하게 항상 켜짐)")]
            [Tooltip("잡몹 시계가 한 칸 도는 주기(초). 턴이 아니라 시간으로 돈다 — " +
                "턴으로 세면 가만히 있는 것이 가장 안전한 수가 되어 버린다.")]
            [Min(0.1f)] public float enrageBeatSeconds = 1.6f;
            [Tooltip("한 박마다 몬스터 하나가 새로 성날 확률. 0이면 잡몹 공격이 꺼진다. " +
                "한 박에 최대 하나만 성나므로 갑자기 도배되지 않는다.")]
            [Range(0f, 1f)] public float enrageChance = 0.35f;
            [Tooltip("박이 지날수록 성날 확률에 더해지는 양. 0이면 처음부터 끝까지 같은 압박")]
            [Range(0f, 0.1f)] public float enrageChanceRampPerBeat = 0.01f;
            [Tooltip("성난 뒤 때리기까지의 박 수. 이 안에 매치로 없애면 취소된다")]
            [Min(1)] public int enrageBeats = 3;
            [Tooltip("성난 몬스터가 때리는 기본 피해. 타일 종류가 자기 공격력을 적었으면 그쪽이 이긴다")]
            [Min(0)] public int enrageDamage = 8;
            [Tooltip("동시에 성날 수 있는 최대 수 — 보드가 통째로 성나 손쓸 수 없게 되는 것을 막는다")]
            [Min(0)] public int maxEnragedTiles = 3;

            public GimmickSettings ToSettings() => new()
            {
                CorruptionSeeds = corruptionSeeds,
                CorruptionSpreadEveryTurns = corruptionSpreadEveryTurns,
                MaxCorruptionRatio = maxCorruptionRatio,
                BombChance = bombChance,
                BombTurns = bombTurns,
                BombDamage = bombDamage,
                ChainInitialCount = chainInitialCount,
                ChainChance = chainChance,
                EnrageBeatSeconds = enrageBeatSeconds,
                EnrageChance = enrageChance,
                EnrageChanceRampPerBeat = enrageChanceRampPerBeat,
                EnrageBeats = enrageBeats,
                EnrageDamage = enrageDamage,
                MaxEnragedTiles = maxEnragedTiles,
            };
        }

        /// <summary>진행도 세이브가 이 스테이지를 가리키는 이름 (GDD §9.2).</summary>
        public string StageId => string.IsNullOrWhiteSpace(stageId) ? name : stageId;

        /// <summary>
        /// 이 스테이지의 소울 매장량(광맥). 0이면 <see cref="Core.Progress.RunEconomyConfig.DefaultStageSoulBudget"/>로 떨어진다 —
        /// 스테이지마다 적지 않아도 경제가 돌게 하기 위한 폴백이다.
        /// </summary>
        public int SoulBudget => soulBudget;

        /// <summary>월드맵 정보 표시용 — 보스 초상/이름을 읽는다 (Core의 BossConfig에는 스프라이트를 담을 수 없다).</summary>
        public BossDataSO BossData => bossData;

        /// <summary>
        /// 이 판에서 싸울 인살 페이즈 수. 0이면 보스 에셋에 적힌 만큼 전부.
        /// <para>"2페이즈로 싸울지"는 보스가 아니라 <b>판</b>이 정한다 — 그래서 2-1·2-2·2-3이
        /// 보스 에셋 하나를 공유하면서도 2-3만 인살 두 번이 된다.</para>
        /// </summary>
        public int BattlePhaseLimit => battlePhaseLimit;

        /// <summary>
        /// 이 판을 깨면 엔딩인가. <b>대개는 지도가 정한다</b>(<see cref="StageSelection"/>) —
        /// 이 플래그는 지도 순서를 거스르고 싶을 때만 켜는 덮어쓰기다.
        /// </summary>
        public bool IsFinalLink => isFinalLink;

        /// <summary>이 스테이지에 켜진 기믹 — 월드맵에서 '어떤 기믹이 나오는지' 표시에 쓴다.</summary>
        public IReadOnlyList<GimmickType> Gimmicks => gimmicks;

        public StageConfig ToConfig()
        {
            ParseBoardRows(out bool[,] activeMask, out List<GridPos> wallPositions);

            var weights = new List<TileSpawnWeight>(spawnWeights.Length);
            foreach (SpawnWeightEntry entry in spawnWeights)
            {
                if (entry.tile == null)
                    throw new InvalidOperationException($"{name}: 스폰 가중치 목록에 빈 타일 참조가 있습니다.");
                weights.Add(new TileSpawnWeight(entry.tile.ToDefinition(), entry.weight));
            }

            return new StageConfig
            {
                ActiveMask = activeMask,
                WallPositions = wallPositions,
                WallHp = wallHp,
                TurnLimit = turnLimit,
                ComboSoulMultiplierStep = comboSoulMultiplierStep,
                PotionHealAmount = potionHealAmount,
                SpawnWeights = weights,
                BossChanceByScore = bossChanceByScore.Evaluate,
                BossChanceBySeconds = bossChanceBySeconds.Evaluate,
                BossEngageSeconds = bossEngageSeconds,
                MaxLiveBossTiles = maxLiveBossTiles,
                // 페이즈 수는 보스가 아니라 이 판이 정한다 — 같은 보스 에셋을 1페이즈·2페이즈로 나눠 쓴다.
                Boss = bossData != null ? bossData.ToConfig(battlePhaseLimit) : null,
                Gimmicks = (GimmickType[])gimmicks.Clone(),
                // 이 필드가 없던 시절의 에셋도 열 수 있게 방어 (순수 C# 클래스라 ?? 사용 가능)
                GimmickSettings = (gimmickTuning ?? new GimmickTuning()).ToSettings(),
            };
        }

        /// <summary>인스펙터의 위→아래 행 순서를 y=0이 바닥인 좌표계로 변환한다.</summary>
        private void ParseBoardRows(out bool[,] activeMask, out List<GridPos> wallPositions)
        {
            if (boardRows == null || boardRows.Length == 0)
                throw new InvalidOperationException($"{name}: boardRows가 비어 있습니다.");

            int height = boardRows.Length;
            int width = boardRows[0].Length;
            activeMask = new bool[width, height];
            wallPositions = new List<GridPos>();

            for (int rowIndex = 0; rowIndex < height; rowIndex++)
            {
                string row = boardRows[rowIndex];
                if (string.IsNullOrEmpty(row) || row.Length != width)
                    throw new InvalidOperationException(
                        $"{name}: {rowIndex}번째 행의 길이가 다릅니다 (기대 {width}, 실제 {row?.Length ?? 0}).");

                int y = height - 1 - rowIndex; // 첫 행이 보드 최상단
                for (int x = 0; x < width; x++)
                {
                    switch (char.ToUpperInvariant(row[x]))
                    {
                        case CellActive:
                            activeMask[x, y] = true;
                            break;
                        case CellInactive:
                            break;
                        case CellWall:
                            activeMask[x, y] = true;
                            wallPositions.Add(new GridPos(x, y));
                            break;
                        default:
                            throw new InvalidOperationException(
                                $"{name}: 알 수 없는 셀 문자 '{row[x]}' (행 {rowIndex}, 열 {x}). O/X/W만 허용됩니다.");
                    }
                }
            }
        }
    }
}
