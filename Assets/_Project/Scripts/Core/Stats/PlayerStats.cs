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

        /// <summary>
        /// 아무것도 안 찍은 상태의 판정 폭. 뷰가 <b>노트 원의 두께</b>를 이 값에 맞춰 굽는다 —
        /// "기본 상태에서 회색 띠와 흰 원이 같은 두께"가 성립해야 겹침이 곧 판정으로 읽힌다.
        /// </summary>
        public float BaseParryWindowSeconds => _config.BaseParryWindowSeconds;

        /// <summary>최대 체력(캐릭터 특화까지 반영된 값). 판마다 만피로 시작하므로 이 값이 곧 이 런의 체력이다.</summary>
        public int MaxHp => _config.MaxHp;

        /// <summary>공격 커밋 시간 — 스탯이 아닌 고정 템포 값이지만 전투가 참조하는 단일 창구를 유지한다.</summary>
        public float AttackCommitSeconds => _config.AttackCommitSeconds;

        /// <summary>패링 헛침 후딜레이.</summary>
        public float ParryWhiffLockSeconds => _config.ParryWhiffLockSeconds;

        /// <summary>타격 직후에도 패링을 받아 주는 유예 시간.</summary>
        public float ParryLateGraceSeconds => _config.ParryLateGraceSeconds;

        /// <summary>
        /// 들어온 피해에 방어를 적용한 <b>최종 피해</b>. 전투의 노트와 퍼즐의 잡몹·폭탄이 <b>같은 이 함수를</b> 쓴다 —
        /// 규칙이 두 곳에 있으면 한쪽만 고쳐져서 "방어를 올렸는데 퍼즐에서는 그대로 아프다"가 된다(실제로 그랬다).
        ///
        /// <para><b>완전 무효화는 불가</b> — 방어가 피해보다 커도 최소 1은 들어온다.
        /// 0이 되는 순간 그 위협은 없는 것과 같아지고, 방어만 올리면 퍼즐이 무해해진다.</para>
        /// </summary>
        public int ResolveIncomingDamage(float rawDamage) =>
            rawDamage <= 0f ? 0 : Math.Max(1, (int)Math.Round(rawDamage - DamageReduction));

        /// <summary>(현재 영혼석, 다음 레벨 요구량) — XP 바 갱신용.</summary>
        public event Action<int, int> SoulsChanged;

        /// <summary>(새 누적 레벨) — 포인트 적립 시점. 레벨업 연출 훅.</summary>
        public event Action<int> LeveledUp;

        /// <summary>(분배된 스탯, 해당 스탯의 새 레벨).</summary>
        public event Action<StatType, int> StatAllocated;

        public PlayerStats(PlayerStatsConfig config) : this(config, null) { }

        /// <summary>
        /// 저장된 진행(<paramref name="snapshot"/>)을 씨앗으로 복원한다 — 성장 캐리(<c>Docs/PROGRESSION.md</c>).
        /// snapshot이 null이면 새 캐릭터(레벨 1, 소울 0)로 시작한다.
        /// <b>복원은 이벤트를 발행하지 않는다</b> — 아직 아무도 구독하지 않은 생성 시점이라 UI를 흔들 필요가 없다.
        /// </summary>
        public PlayerStats(PlayerStatsConfig config, PlayerStatsSnapshot snapshot)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (snapshot == null)
                return;

            Level = Math.Max(1, snapshot.Level);
            Souls = Math.Max(0, snapshot.Souls);
            PendingPoints = Math.Max(0, snapshot.PendingPoints);
            // TotalSoulsEarned는 복원하지 않는다 — 판 단위 보스 난입 게이지라 매 판 0에서 시작한다.
            for (int i = 0; i < _statLevels.Length && i < snapshot.StatLevels.Length; i++)
                _statLevels[i] = Math.Max(0, snapshot.StatLevels[i]);
        }

        /// <summary>현재 가변 상태를 스냅샷으로 떠낸다 — 스테이지 클리어 시 런 상태에 저장하기 위한 것.</summary>
        public PlayerStatsSnapshot Capture()
        {
            PlayerStatsSnapshot snapshot = new()
            {
                Level = Level,
                Souls = Souls,
                PendingPoints = PendingPoints,
            };
            for (int i = 0; i < _statLevels.Length; i++)
                snapshot.StatLevels[i] = _statLevels[i];
            return snapshot;
        }

        public int GetStatLevel(StatType stat) => _statLevels[(int)stat];

        /// <summary>이 스탯을 한 단계 올리는 데 드는 포인트. 판정치는 더 비싸다.</summary>
        public int GetPointCost(StatType stat) => stat switch
        {
            StatType.Attack => Math.Max(1, _config.AttackPointCost),
            StatType.Defense => Math.Max(1, _config.DefensePointCost),
            _ => Math.Max(1, _config.ParryPointCost),
        };

        /// <summary>상한에 걸려 더 못 올리는가 — '포인트가 모자란 것'과 구분해야 UI가 MAX를 잘못 띄우지 않는다.</summary>
        public bool IsAtCap(StatType stat) =>
            stat == StatType.Parry && GetStatLevel(StatType.Parry) >= _config.ParryLevelHardCap;

        /// <summary>판정치는 하드 캡 도달 이후 선택할 수 없고, 값만큼 포인트가 있어야 한다.</summary>
        public bool CanAllocate(StatType stat) =>
            !IsAtCap(stat) && PendingPoints >= GetPointCost(stat);

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
                    $"스탯 분배 불가: {stat} (포인트 {PendingPoints}/{GetPointCost(stat)}, 현재 레벨 {GetStatLevel(stat)})");

            PendingPoints -= GetPointCost(stat);
            _statLevels[(int)stat]++;
            StatAllocated?.Invoke(stat, GetStatLevel(stat));
        }
    }
}
