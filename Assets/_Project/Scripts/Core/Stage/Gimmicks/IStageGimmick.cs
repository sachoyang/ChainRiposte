using System.Collections.Generic;
using ChainRiposte.Core.Board;
using ChainRiposte.Core.Match;

namespace ChainRiposte.Core.Stage.Gimmicks
{
    /// <summary>
    /// 스테이지 기믹 모듈 (GDD §3.6). StageData의 목록에 담긴 것만 활성화되며 서로 조합 가능하다.
    /// PuzzleEngine이 정해진 순간에 훅을 호출하고, 기믹은 보드를 고치고 GimmickContext에 기록한다.
    /// </summary>
    public interface IStageGimmick
    {
        GimmickType Type { get; }

        /// <summary>초기 배치 직후 — 씨앗 타일(부패/사슬)을 심는다.</summary>
        void OnBoardInitialized(GimmickContext context);

        /// <summary>리필로 새 타일이 나온 직후 — 폭탄/사슬을 붙인다.</summary>
        void OnTilesSpawned(GimmickContext context, IReadOnlyList<TileSpawn> spawns);

        /// <summary>
        /// 매치가 확정되고 <b>제거 직전</b> — cleared를 고쳐 파괴를 취소하거나(사슬 해제)
        /// 파괴 대상을 추가할 수 있다(인접 부패 제거).
        /// </summary>
        void OnMatchesResolving(GimmickContext context, HashSet<GridPos> cleared);

        /// <summary>턴이 소모된 뒤 — 확산/카운트다운. 보드를 바꾸면 엔진이 다시 정착시킨다.</summary>
        void OnTurnEnded(GimmickContext context);

        /// <summary>
        /// 실시간이 흐른 만큼 — <b>플레이어가 아무것도 안 해도</b> 도는 위협이 여기서 자란다.
        /// 턴 훅만 있으면 손을 놓고 있는 것이 가장 안전한 수가 되므로, 압박을 맡은 기믹은 이쪽을 쓴다.
        /// </summary>
        void OnTimeElapsed(GimmickContext context, float deltaSeconds);
    }

    /// <summary>필요한 훅만 재정의하도록 하는 기본 구현.</summary>
    public abstract class StageGimmick : IStageGimmick
    {
        public abstract GimmickType Type { get; }

        public virtual void OnBoardInitialized(GimmickContext context) { }

        public virtual void OnTilesSpawned(GimmickContext context, IReadOnlyList<TileSpawn> spawns) { }

        public virtual void OnMatchesResolving(GimmickContext context, HashSet<GridPos> cleared) { }

        public virtual void OnTurnEnded(GimmickContext context) { }

        public virtual void OnTimeElapsed(GimmickContext context, float deltaSeconds) { }
    }
}
