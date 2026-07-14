namespace ChainRiposte.Core.Stats
{
    /// <summary>레벨업 시 플레이어가 분배할 수 있는 스탯.</summary>
    public enum StatType
    {
        /// <summary>공격력 — 보스 공격 시 HP·체간 피해 증가.</summary>
        Attack = 0,

        /// <summary>방어력 — 피격 시 받는 피해 감소.</summary>
        Defense = 1,

        /// <summary>판정치 — 패링 성공 윈도우(초) 증가. 하드 캡 존재.</summary>
        Parry = 2,
    }
}
