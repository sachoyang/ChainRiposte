using ChainRiposte.Core.Board;

namespace ChainRiposte.Core.Stage.Gimmicks
{
    /// <summary>기믹이 일으킨 사건의 종류. 뷰는 이걸 보고 연출/뱃지를 갱신한다.</summary>
    public enum GimmickEventType
    {
        /// <summary>부패가 인접 몬스터 타일을 감염시켰다 (해당 칸의 타일이 교체됨).</summary>
        CorruptionSpread = 0,

        /// <summary>인접 매치로 부패 타일이 제거됐다.</summary>
        CorruptionCleared = 1,

        /// <summary>새 타일이 폭탄으로 스폰됐다 (Value = 남은 턴).</summary>
        BombArmed = 2,

        /// <summary>폭탄 카운트가 줄었다 (Value = 남은 턴).</summary>
        BombTicked = 3,

        /// <summary>폭탄이 터졌다 — 타일 제거 + 플레이어 피해 (Value = 피해량).</summary>
        BombExploded = 4,

        /// <summary>매치/인접 매치로 사슬이 풀렸다 (타일은 살아남는다).</summary>
        ChainBroken = 5,
    }

    /// <summary>기믹 사건 1건. Core는 이벤트만 기록하고 연출/HP 반영은 상위 레이어가 한다.</summary>
    public sealed class GimmickEvent
    {
        public GimmickEventType Type { get; }
        public GridPos Position { get; }

        /// <summary>사건의 주체가 된 타일 (감염된 경우는 새로 놓인 부패 타일).</summary>
        public Tile Tile { get; }

        /// <summary>폭탄 남은 턴 또는 피해량. 그 외에는 0.</summary>
        public int Value { get; }

        public GimmickEvent(GimmickEventType type, GridPos position, Tile tile, int value = 0)
        {
            Type = type;
            Position = position;
            Tile = tile;
            Value = value;
        }

        public override string ToString() => $"{Type}@{Position}({Value})";
    }
}
