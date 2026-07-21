using System;
using System.Collections.Generic;
using ChainRiposte.Core.Board;

namespace ChainRiposte.Core.Stage.Gimmicks
{
    /// <summary>
    /// 기믹 훅에 넘겨지는 작업 공간. 기믹은 보드를 직접 고치고, 일어난 일을 여기에 기록한다.
    /// 엔진은 기록을 걷어 SwapResult로 내보내고(연출), 플레이어 피해는 상위 레이어가 반영한다.
    /// </summary>
    public sealed class GimmickContext
    {
        private readonly List<GimmickEvent> _events = new();

        public BoardGrid Board { get; }
        public Random Rng { get; }
        public GimmickSettings Settings { get; }

        /// <summary>이번 스왑에서 기믹이 누적한 플레이어 피해 (폭발 등).</summary>
        public int PlayerDamage { get; private set; }

        /// <summary>기믹이 보드를 변형했는가 — 엔진이 중력 재정착 여부를 판단한다.</summary>
        public bool BoardChanged { get; private set; }

        public GimmickContext(BoardGrid board, Random rng, GimmickSettings settings)
        {
            Board = board ?? throw new ArgumentNullException(nameof(board));
            Rng = rng ?? new Random();
            Settings = settings ?? new GimmickSettings();
        }

        /// <summary>아직 걷어가지 않은 기록 (엔진이 TakeEvents로 가져가면 비워진다).</summary>
        public IReadOnlyList<GimmickEvent> Events => _events;

        public void Report(GimmickEvent gimmickEvent) => _events.Add(gimmickEvent);

        /// <summary>보드를 고쳤다고 알린다 (타일 제거/교체).</summary>
        public void MarkBoardChanged() => BoardChanged = true;

        public void DealPlayerDamage(int amount) => PlayerDamage += Math.Max(0, amount);

        /// <summary>기록된 이벤트를 꺼내고 버퍼를 비운다 (캐스케이드 단계별로 나눠 담기 위함).</summary>
        public IReadOnlyList<GimmickEvent> TakeEvents()
        {
            if (_events.Count == 0)
                return Array.Empty<GimmickEvent>();

            var taken = _events.ToArray();
            _events.Clear();
            return taken;
        }

        /// <summary>스왑 1회의 시작 — 누적값 초기화.</summary>
        public void BeginTurn()
        {
            _events.Clear();
            PlayerDamage = 0;
            BoardChanged = false;
        }

        /// <summary>보드 위 조건에 맞는 타일 좌표를 모은다 (기믹 공용 헬퍼).</summary>
        public List<GridPos> Collect(Func<Tile, bool> predicate)
        {
            var result = new List<GridPos>();
            foreach (GridPos pos in Board.ActivePositions())
            {
                Tile tile = Board.GetTile(pos);
                if (tile != null && predicate(tile))
                    result.Add(pos);
            }
            return result;
        }
    }
}
