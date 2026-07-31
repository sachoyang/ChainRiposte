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

        /// <summary>그 칸이 <b>폭탄 타일로 교체</b>됐다 (Value = 남은 턴). 뷰는 타일을 다시 만든다.</summary>
        BombArmed = 2,

        /// <summary>폭탄 카운트가 줄었다 (Value = 남은 턴).</summary>
        BombTicked = 3,

        /// <summary>폭탄이 터졌다 — 타일 제거 + 플레이어 피해 (Value = 피해량).</summary>
        BombExploded = 4,

        /// <summary>매치/인접 매치로 사슬이 풀렸다 (타일은 살아남는다).</summary>
        ChainBroken = 5,

        /// <summary>
        /// <b>인접 매치로 폭탄을 해체했다</b> — 타일은 사라지지만 피해는 없다.
        /// <see cref="BombExploded"/>와 반드시 따로 둔다: 하나는 이득이고 하나는 손해인데
        /// 같은 사건이면 화면에서 구분할 방법이 없다(폭발 연출이 붙는 쪽은 터진 쪽뿐이다).
        /// </summary>
        BombDefused = 10,

        /// <summary>몬스터가 성났다 — 공격 예고 (Value = 남은 턴).</summary>
        EnrageStarted = 6,

        /// <summary>성난 몬스터의 카운트가 줄었다 (Value = 남은 턴).</summary>
        EnrageTicked = 7,

        /// <summary>성난 몬스터가 플레이어를 때렸다 (Value = 피해량). <b>타일은 남아서 재장전한다.</b></summary>
        EnrageAttacked = 8,

        /// <summary>매치로 성난 몬스터를 없앴다 — 공격이 취소됐다.</summary>
        EnrageCleared = 9,
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
