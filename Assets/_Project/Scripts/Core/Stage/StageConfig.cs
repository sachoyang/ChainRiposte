using System;
using System.Collections.Generic;
using ChainRiposte.Core.Board;
using ChainRiposte.Core.Combat;

namespace ChainRiposte.Core.Stage
{
    /// <summary>
    /// 스테이지 하나의 정의. Game 레이어의 StageDataSO.ToConfig()로 생성된다.
    /// AnimationCurve 등 UnityEngine 타입은 Func&lt;float, float&gt;로 감싸져 주입된다.
    /// </summary>
    public sealed class StageConfig
    {
        // ── 보드 ──
        /// <summary>[x, y] true=Active. y=0이 바닥.</summary>
        public bool[,] ActiveMask;

        /// <summary>초기 배치되는 벽(장애물)의 위치.</summary>
        public IReadOnlyList<GridPos> WallPositions = Array.Empty<GridPos>();

        /// <summary>벽 내구도.</summary>
        public int WallHp = 3;

        // ── 퍼즐 규칙 ──
        public int TurnLimit = 30;

        /// <summary>콤보(연쇄) 1단계당 영혼석 배수 증가량. 배수 = 1 + 증가량 × (콤보 − 1).</summary>
        public float ComboSoulMultiplierStep = 0.5f;

        /// <summary>물약 타일 1개 매치당 HP 회복량.</summary>
        public int PotionHealAmount = 10;

        /// <summary>리필 시 타일 종류별 스폰 가중치.</summary>
        public IReadOnlyList<TileSpawnWeight> SpawnWeights = Array.Empty<TileSpawnWeight>();

        // ── 보스 난입 (6단계에서 사용) ──
        /// <summary>x=누적 영혼석(점수) → y=보스 타일 스폰 확률(0~1).</summary>
        public Func<float, float> BossChanceByScore = _ => 0f;

        /// <summary>x=경과 초 → y=보스 타일 스폰 확률(0~1). 점수 곡선과 둘 중 최댓값 적용.</summary>
        public Func<float, float> BossChanceBySeconds = _ => 0f;

        /// <summary>보스 타일별 듀얼 카운트다운 — 실시간 초.</summary>
        public float BossCountdownSeconds = 20f;

        /// <summary>보스 타일별 듀얼 카운트다운 — 잔여 턴.</summary>
        public int BossCountdownTurns = 8;

        /// <summary>기습 돌입 시 시작 HP 배율 (0.5 = 반토막).</summary>
        public float AmbushHpMultiplier = 0.5f;

        // ── 전투 (7단계에서 사용) ──
        /// <summary>이 스테이지에 난입하는 보스. null이면 전투 없음 (테스트/퍼즐 전용).</summary>
        public BossConfig Boss;

        // ── 스테이지 기믹 (GDD §3.6) ──
        /// <summary>이 스테이지에서 활성화되는 기믹 목록. 조합 가능.</summary>
        public IReadOnlyList<GimmickType> Gimmicks = Array.Empty<GimmickType>();

        /// <summary>기믹 밸런스 수치. 활성화된 기믹만 참조한다.</summary>
        public GimmickSettings GimmickSettings = new();

        /// <summary>ActiveMask로 빈 보드를 생성한다. 초기 벽 배치는 3단계 보드 초기화에서 수행.</summary>
        public BoardGrid CreateBoard() => new(ActiveMask);
    }
}
