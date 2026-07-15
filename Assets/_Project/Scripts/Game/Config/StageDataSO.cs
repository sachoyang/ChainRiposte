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
        [SerializeField, Min(1)] private int turnLimit = 30;
        [Tooltip("콤보(연쇄) 1단계당 영혼석 배수 증가량. 배수 = 1 + 증가량 × (콤보-1)")]
        [SerializeField, Min(0f)] private float comboSoulMultiplierStep = 0.5f;
        [Tooltip("물약 타일 1개 매치당 HP 회복량")]
        [SerializeField, Min(0)] private int potionHealAmount = 10;

        [Header("타일 스폰 가중치 (리필 시 추첨 확률)")]
        [SerializeField] private SpawnWeightEntry[] spawnWeights = Array.Empty<SpawnWeightEntry>();

        [Header("보스 난입 — 동적 스포너 (y=스폰 확률 0~1, 두 곡선 중 최댓값 적용)")]
        [Tooltip("x = 누적 영혼석(점수)")]
        [SerializeField] private AnimationCurve bossChanceByScore = AnimationCurve.Linear(100f, 0f, 400f, 0.3f);
        [Tooltip("x = 퍼즐 경과 시간(초)")]
        [SerializeField] private AnimationCurve bossChanceBySeconds = AnimationCurve.Linear(45f, 0f, 180f, 0.3f);

        [Tooltip("보스 타일별 듀얼 카운트다운 — 실시간 초")]
        [SerializeField, Min(1f)] private float bossCountdownSeconds = 20f;
        [Tooltip("보스 타일별 듀얼 카운트다운 — 잔여 턴")]
        [SerializeField, Min(1)] private int bossCountdownTurns = 8;
        [Tooltip("기습 돌입 시 시작 HP 배율 (0.5 = 반토막)")]
        [SerializeField, Range(0.1f, 1f)] private float ambushHpMultiplier = 0.5f;

        [Header("전투 (7단계)")]
        [Tooltip("이 스테이지에 난입하는 보스")]
        [SerializeField] private BossDataSO bossData;

        [Header("스테이지 기믹 on/off (확장 구현 사항, GDD §3.6)")]
        [SerializeField] private GimmickType[] gimmicks = Array.Empty<GimmickType>();

        [Serializable]
        private struct SpawnWeightEntry
        {
            public TileDefinitionSO tile;
            [Min(0f)] public float weight;
        }

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
                BossCountdownSeconds = bossCountdownSeconds,
                BossCountdownTurns = bossCountdownTurns,
                AmbushHpMultiplier = ambushHpMultiplier,
                Boss = bossData != null ? bossData.ToConfig() : null,
                Gimmicks = (GimmickType[])gimmicks.Clone(),
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
