using ChainRiposte.Core.Stats;
using UnityEngine;

namespace ChainRiposte.Game.Characters
{
    /// <summary>
    /// 플레이어가 새 게임에서 고르는 캐릭터. 그림과(나중에) 성능 특화를 한곳에 묶는다.
    ///
    /// 캐릭터를 늘리려면 이 에셋을 <c>Assets/_Project/Data/Resources/Characters/</c> 에 하나 더 두면 된다 —
    /// 선택 화면은 폴더를 훑어 만들어지므로 코드도 씬 빌더도 건드릴 필요가 없다.
    /// </summary>
    [CreateAssetMenu(menuName = "ChainRiposte/Player Character", fileName = "Character_")]
    public sealed class PlayerCharacterSO : ScriptableObject
    {
        [Tooltip("세이브 키. 비우면 에셋 이름을 쓴다. 한 번 정하면 바꾸지 말 것 — 바꾸면 저장된 선택이 풀린다.")]
        [SerializeField] private string characterId;
        [Tooltip("이름 문구의 현지화 키 (CSV에 있어야 한다)")]
        [SerializeField] private string nameKey;
        [Tooltip("한 줄 설명의 현지화 키. 비워도 된다.")]
        [SerializeField] private string descriptionKey;
        [Tooltip("선택 화면에서의 정렬 순서 (작을수록 왼쪽)")]
        [SerializeField] private int sortOrder;

        [Header("컨셉")]
        [Tooltip("이 캐릭터를 고르면 적용될 테마 — 배경과 보스 겉모습만 바뀐다(난이도는 공유). 비워도 된다.")]
        [SerializeField] private Theming.ThemeSO theme;

        [Header("그림")]
        [Tooltip("선택 화면 초상화. 비우면 전투 그림을 대신 쓴다.")]
        [SerializeField] private Sprite portrait;
        [Tooltip("전투 화면의 플레이어 본체")]
        [SerializeField] private Sprite combatSprite;
        [Tooltip("이 캐릭터를 따라다니는 성녀 — 준비 화면에서 공/방 강화에 반응한다")]
        [SerializeField] private Sprite saintSprite;
        [Tooltip("월드맵에서 걸어다니는 그림. 비우면 전투 그림을 대신 쓴다.")]
        [SerializeField] private Sprite mapSprite;

        [Header("특화 — 공용 밸런스에 더해지는 '아주 조금'")]
        [Tooltip("최대 체력 가산")]
        [SerializeField] private int bonusMaxHp;
        [Tooltip("피해 감소 가산 (DEF 1레벨 = 2.0)")]
        [SerializeField] private float bonusDamageReduction;
        [Tooltip("공격력 가산 (ATK 1레벨 = 3.0)")]
        [SerializeField] private float bonusAttackDamage;
        [Tooltip("패링 판정 폭 가산(초) (PARRY 1레벨 = 0.03)")]
        [SerializeField] private float bonusParryWindowSeconds;

        /// <summary>세이브 키. StageDataSO.StageId와 같은 규칙 — 비우면 에셋 이름 폴백.</summary>
        public string CharacterId => string.IsNullOrWhiteSpace(characterId) ? name : characterId;

        /// <summary>보이는 것(배경·보스 겉모습)을 정하는 컨셉. 안 걸었으면 null이고 씬의 그림이 그대로 남는다.</summary>
        public Theming.ThemeSO Theme => theme;

        public string NameKey => string.IsNullOrWhiteSpace(nameKey) ? $"character.{CharacterId}" : nameKey;
        public string DescriptionKey => descriptionKey;
        public int SortOrder => sortOrder;

        /// <summary>초상화가 없으면 전투 그림으로 대신한다 — 자리가 비어 보이는 것보다 낫다.</summary>
        public Sprite Portrait => portrait != null ? portrait : combatSprite;
        public Sprite CombatSprite => combatSprite;
        public Sprite SaintSprite => saintSprite;

        /// <summary>월드맵 아바타. 전용 그림이 없으면 전투 그림으로 대신한다.</summary>
        public Sprite MapSprite => mapSprite != null ? mapSprite : combatSprite;

        /// <summary>특화가 하나라도 걸려 있는가 (로그·표시용).</summary>
        public bool HasBonuses =>
            bonusMaxHp != 0 || bonusDamageReduction != 0f ||
            bonusAttackDamage != 0f || bonusParryWindowSeconds != 0f;

        /// <summary>
        /// 공용 밸런스 위에 캐릭터 차이를 얹는다.
        /// <b>덮어쓰지 않고 더한다</b> — 밸런스 원천은 <see cref="Config.PlayerStatsConfigSO"/> 하나로 유지해야
        /// 나중에 기본값을 고칠 때 캐릭터 수만큼 따라 고치지 않는다.
        /// </summary>
        public void ApplyBonuses(PlayerStatsConfig config)
        {
            if (config == null)
                return;

            config.MaxHp += bonusMaxHp;
            config.BaseDamageReduction += bonusDamageReduction;
            config.BaseAttackDamage += bonusAttackDamage;
            config.BaseParryWindowSeconds += bonusParryWindowSeconds;
        }
    }
}
