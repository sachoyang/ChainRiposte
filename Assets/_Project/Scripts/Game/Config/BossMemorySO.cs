using ChainRiposte.Core.Combat;
using UnityEngine;

namespace ChainRiposte.Game.Config
{
    /// <summary>
    /// <b>보스의 기억</b> 하나 (설계: <c>Docs/PROGRESSION.md</c> §2.2).
    /// 인살로 삼키면 이 런이 끝날 때까지 남는 영구 패시브가 된다.
    ///
    /// <para><b>기억 1개 = 보스 1개.</b> 같은 보스를 쓰는 스테이지가 여러 개라 판마다 주지 않는다 —
    /// 기억을 늘리는 길은 슬롯을 늘리는 것이 아니라 <b>보스를 늘리는 것</b>이다.</para>
    ///
    /// <para>효과 칸은 <see cref="BossMemoryConfig"/>와 1:1이다. 새 효과를 만들 때 여기와 그쪽에
    /// 칸을 하나씩 더하면 전투 코드는 안 늘어난다 — <see cref="CombatSystem"/>은 합산된 수치만 읽는다.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ChainRiposte/Boss Memory", fileName = "Memory_")]
    public sealed class BossMemorySO : ScriptableObject
    {
        [Header("표시")]
        [Tooltip("세이브에 남는 키. 비우면 에셋 이름. <b>한 번 정하면 바꾸지 말 것</b> — " +
                 "바꾸면 이미 삼킨 기억이 세이브에서 사라진다.")]
        [SerializeField] private string memoryId;
        [Tooltip("이름의 현지화 키(CSV).")]
        [SerializeField] private string nameKey;
        [Tooltip("효과 설명의 현지화 키(CSV).")]
        [SerializeField] private string descriptionKey;
        [Tooltip("아이콘 줄에 뜨는 작은 그림. 비우면 씬에 꽂아 둔 기본 그림이 그대로 남는다 " +
                 "— 아이콘을 안 넣었다고 줄이 비어 보이면 안 된다.")]
        [SerializeField] private Sprite icon;

        [Header("효과 — 하나만 채워도 된다 (평타 파워업 금지: §2.2)")]
        [Tooltip("패링 성공 1회당 체간을 이만큼 더 깎는다. 보스의 '패링 성공 1회당 상승량'에 가산.")]
        [SerializeField, Min(0f)] private float bonusParryPostureGain;
        [Tooltip("헛침 잠금 시간 배수. 1 = 그대로, 0.8 = −20%. " +
                 "0으로 두면 '효과 없음'으로 읽는다(안 채운 칸이 처벌을 없애 버리면 안 되므로). " +
                 "기억을 다 모아도 0.4배 아래로는 안 내려간다.")]
        [SerializeField, Range(0f, 1f)] private float whiffLockMultiplier = 1f;
        [Tooltip("연속 패링 N회를 채우면 다음 피격 1회를 무효로 만든다. 0이면 이 효과 없음. " +
                 "쓰고 나면 다시 N회를 쌓아야 한다.")]
        [SerializeField, Min(0)] private int perfectStreakGuard;

        /// <summary>세이브에 남는 키. 비우면 에셋 이름 (StageId·CharacterId와 같은 규칙).</summary>
        public string MemoryId => string.IsNullOrWhiteSpace(memoryId) ? name : memoryId;

        public string NameKey => nameKey;
        public string DescriptionKey => descriptionKey;
        public Sprite Icon => icon;

        /// <summary>전투에 주입할 순수 C# 수치 한 벌.</summary>
        public BossMemoryConfig ToConfig() => new()
        {
            BonusParryPostureGain = bonusParryPostureGain,
            WhiffLockMultiplier = whiffLockMultiplier,
            PerfectStreakGuard = perfectStreakGuard,
        };
    }
}
