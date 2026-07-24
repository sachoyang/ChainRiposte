using System;
using UnityEngine;

namespace ChainRiposte.Game.Theming
{
    /// <summary>
    /// 컨셉 한 벌. 캐릭터를 고르면 그 캐릭터가 가리키는 테마가 <b>보이는 것</b>을 갈아 끼운다 —
    /// 배경과 보스 겉모습이 바뀌고, <b>난이도(HP·체간·채보·패턴)는 건드리지 않는다.</b>
    ///
    /// <para>컨셉을 늘리려면 이 에셋을 <c>Assets/_Project/Data/Resources/Themes/</c> 에 하나 더 두고
    /// <see cref="Characters.PlayerCharacterSO"/>에서 가리키면 된다 — 코드도 씬 빌더도 건드리지 않는다.</para>
    ///
    /// <para>배경 키는 <b>자유 문자열</b>이다. 지금은 화면 단위(맵/퍼즐/전투)로 크게 잡지만,
    /// 나중에 스테이지별로 쪼개고 싶으면 <c>stage.1-1</c> 같은 키를 추가하기만 하면 된다.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "ChainRiposte/Theme", fileName = "Theme_")]
    public sealed class ThemeSO : ScriptableObject
    {
        /// <summary>월드맵(스테이지 선택) 배경.</summary>
        public const string KeyMap = "map";
        /// <summary>퍼즐 화면 배경.</summary>
        public const string KeyPuzzle = "puzzle";
        /// <summary>전투 화면 배경.</summary>
        public const string KeyCombat = "combat";

        [Serializable]
        public sealed class BackgroundEntry
        {
            [Tooltip("씬의 ThemedSprite에 적는 키 (map / puzzle / combat …)")]
            public string key;
            public Sprite sprite;
        }

        [Serializable]
        public sealed class BossEntry
        {
            [Tooltip("BossDataSO의 Boss Id (비워 두면 그 에셋 이름)")]
            public string bossId;
            [Tooltip("전투 화면에 서는 그림. 비우면 BossDataSO의 그림을 쓴다.")]
            public Sprite sprite;
            [Tooltip("이름의 현지화 키 (CSV에 있어야 한다). 비우면 BossDataSO의 이름을 쓴다.")]
            public string nameKey;
        }

        [Tooltip("비우면 에셋 이름을 쓴다. CharacterId·StageId와 같은 규칙.")]
        [SerializeField] private string themeId;

        [Tooltip("화면별 배경. 씬의 ThemedSprite가 키로 찾아간다.")]
        [SerializeField] private BackgroundEntry[] backgrounds = Array.Empty<BackgroundEntry>();

        [Tooltip("보스의 겉모습만 갈아 끼운다 — 성능은 BossDataSO 그대로다.")]
        [SerializeField] private BossEntry[] bosses = Array.Empty<BossEntry>();

        public string ThemeId => string.IsNullOrWhiteSpace(themeId) ? name : themeId;

        /// <summary>없으면 null — 부르는 쪽은 씬에 꽂아둔 그림을 그대로 둔다.</summary>
        public Sprite GetBackground(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            foreach (BackgroundEntry entry in backgrounds)
            {
                if (entry != null && entry.key == key)
                    return entry.sprite;
            }

            return null;
        }

        /// <summary>이 테마에서의 보스 겉모습. 항목이 없으면 false — 부르는 쪽이 SO 기본값으로 떨어진다.</summary>
        public bool TryGetBoss(string bossId, out BossEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(bossId))
                return false;

            foreach (BossEntry candidate in bosses)
            {
                if (candidate != null && candidate.bossId == bossId)
                {
                    entry = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
