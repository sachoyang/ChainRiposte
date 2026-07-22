using System;

namespace ChainRiposte.Core.Stats
{
    /// <summary>
    /// 퍼즐 페이즈에서 파밍한 영혼석(경험치)과 스탯 레벨을 관리한다.
    /// 레벨업 시 스탯이 즉시 오르는 대신 '미할당 포인트'로 적립되고,
    /// 플레이어가 ATK/DEF/판정치 중 하나를 선택해 소비한다.
    /// </summary>
    public sealed class PlayerStats
    {
        private readonly PlayerStatsConfig _config;
        private readonly int[] _statLevels = new int[3]; // StatType 값을 인덱스로 사용

        /// <summary>현재 경험치 바에 쌓인 영혼석. 레벨업 시 요구량만큼 차감된다.</summary>
        public int Souls { get; private set; }

        /// <summary>누적 레벨 (1부터 시작).</summary>
        public int Level { get; private set; } = 1;

        /// <summary>아직 분배하지 않은 스탯 포인트.</summary>
        public int PendingPoints { get; private set; }

        /// <summary>누적 획득 영혼석 = '점수'. 보스 난입 스폰 확률 곡선의 입력 (GDD §4.1).</summary>
        public int TotalSoulsEarned { get; private set; }

        public int SoulsToNextLevel =>
            _config.BaseSoulsToLevel + _config.SoulsToLevelGrowth * (Level - 1);

        public float AttackDamage =>
            _config.BaseAttackDamage + _config.AttackDamagePerLevel * GetStatLevel(StatType.Attack);

        public float DamageReduction =>
            _config.BaseDamageReduction + _config.DamageReductionPerLevel * GetStatLevel(StatType.Defense);

        public float ParryWindowSeconds =>
            _config.BaseParryWindowSeconds + _config.ParryWindowPerLevelSeconds * GetStatLevel(StatType.Parry);

        /// <summary>공격 커밋 시간 — 스탯이 아닌 고정 템포 값이지만 전투가 참조하는 단일 창구를 유지한다.</summary>
        public float AttackCommitSeconds => _config.AttackCommitSeconds;

        /// <summary>패링 헛침 후딜레이.</summary>
        public float ParryWhiffLockSeconds => _config.ParryWhiffLockSeconds;

        /// <summary>타격 직후에도 패링을 받아 주는 유예 시간.</summary>
        public float ParryLateGraceSeconds => _config.ParryLateGraceSeconds;

        /// <summary>(현재 영혼석, 다음 레벨 요구량) — XP 바 갱신용.</summary>
        public event Action<int, int> SoulsChanged;

        /// <summary>(새 누적 레벨) — 포인트 적립 시점. 레벨업 연출 훅.</summary>
        public event Action<int> LeveledUp;

        /// <summary>(분배된 스탯, 해당 스탯의 새 레벨).</summary>
        public event Action<StatType, int> StatAllocated;

        public PlayerStats(PlayerStatsConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public int GetStatLevel(StatType stat) => _statLevels[(int)stat];

        /// <summary>판정치는 하드 캡 도달 이후 선택할 수 없다.</summary>
        public bool CanAllocate(StatType stat) =>
            PendingPoints > 0 &&
            (stat != StatType.Parry || GetStatLevel(StatType.Parry) < _config.ParryLevelHardCap);

        /// <summary>매치 결과로 획득한 영혼석을 적립하고, 요구량을 넘길 때마다 레벨업한다.</summary>
        public void AddSouls(int amount)
        {
            if (amount <= 0)
                return;

            TotalSoulsEarned += amount;
            Souls += amount;
            while (Souls >= SoulsToNextLevel)
            {
                Souls -= SoulsToNextLevel;
                Level++;
                PendingPoints++;
                LeveledUp?.Invoke(Level);
            }

            SoulsChanged?.Invoke(Souls, SoulsToNextLevel);
        }

        public void Allocate(StatType stat)
        {
            if (!CanAllocate(stat))
                throw new InvalidOperationException(
                    $"스탯 분배 불가: {stat} (포인트 {PendingPoints}, 현재 레벨 {GetStatLevel(stat)})");

            PendingPoints--;
            _statLevels[(int)stat]++;
            StatAllocated?.Invoke(stat, GetStatLevel(stat));
        }
    }
}
