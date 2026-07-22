using System;
using System.Collections.Generic;
using ChainRiposte.Core.Stats;

namespace ChainRiposte.Core.Flow
{
    /// <summary>
    /// 한 스테이지 플레이의 런타임 상태(페이즈 + 플레이어 성장)를 소유하는 도메인 루트.
    /// UnityEngine에 의존하지 않으므로 유닛 테스트가 가능하다.
    /// </summary>
    public sealed class GameSession
    {
        private static readonly Dictionary<GamePhase, GamePhase[]> ValidTransitions = new()
        {
            { GamePhase.None,    new[] { GamePhase.Puzzle } },
            { GamePhase.Puzzle,  new[] { GamePhase.Combat, GamePhase.Defeat } },
            { GamePhase.Combat,  new[] { GamePhase.Victory, GamePhase.Defeat } },
            { GamePhase.Victory, Array.Empty<GamePhase>() },
            { GamePhase.Defeat,  Array.Empty<GamePhase>() },
        };

        public GamePhase Phase { get; private set; } = GamePhase.None;

        /// <summary>퍼즐 페이즈에서 파밍하고 전투 페이즈로 이월되는 플레이어 성장 상태.</summary>
        public PlayerStats Stats { get; }

        /// <summary>퍼즐(물약/기믹)과 전투(피격)가 공유하는 플레이어 HP.</summary>
        public PlayerHealth Health { get; }

        /// <summary>(이전 페이즈, 새 페이즈). 각 페이즈 컨트롤러의 활성/비활성 스위치.</summary>
        public event Action<GamePhase, GamePhase> PhaseChanged;

        public GameSession(PlayerStatsConfig statsConfig)
        {
            Stats = new PlayerStats(statsConfig);
            Health = new PlayerHealth(statsConfig.MaxHp);
        }

        public void StartPuzzle() => TransitionTo(GamePhase.Puzzle);

        /// <summary>보스 타일이 바닥에 닿거나 강제 조우가 발생했을 때 호출된다.</summary>
        /// <summary>보스 돌입 직전의 스탯 분배 시간으로 넘어간다 (시간 제한 없음).</summary>
        public void StartIntermission() => TransitionTo(GamePhase.Intermission);

        public void StartCombat() => TransitionTo(GamePhase.Combat);

        public void EndStage(bool victory) => TransitionTo(victory ? GamePhase.Victory : GamePhase.Defeat);

        private void TransitionTo(GamePhase next)
        {
            if (Array.IndexOf(ValidTransitions[Phase], next) < 0)
                throw new InvalidOperationException($"허용되지 않은 페이즈 전환: {Phase} → {next}");

            GamePhase previous = Phase;
            Phase = next;
            PhaseChanged?.Invoke(previous, next);
        }
    }
}
