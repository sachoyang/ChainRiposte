using System.IO;
using ChainRiposte.Game.Characters;
using UnityEditor;
using UnityEngine;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// 기본 캐릭터 2종(기사 / 사무라이)을 Resources 아래에 만들고 그림을 물려 준다.
    /// 이미 있으면 덮어쓰지 않고 <b>비어 있는 슬롯만</b> 채운다 — 손으로 고친 값이 날아가지 않는다.
    ///
    /// 캐릭터를 더 만들려면 이 메뉴가 아니라
    /// <c>Create ▸ ChainRiposte ▸ Player Character</c> 로 같은 폴더에 에셋을 하나 더 두면 된다.
    /// </summary>
    public static class CharacterAssetsMenu
    {
        private const string Folder = "Assets/_Project/Data/Resources/" + CharacterService.ResourcesFolder;
        private const string DotImgs = "Assets/_Project/DotImgs";

        [MenuItem("Tools/ChainRiposte/Create Default Characters")]
        private static void CreateDefaults()
        {
            if (!Directory.Exists(Folder))
            {
                Directory.CreateDirectory(Folder);
                AssetDatabase.Refresh();
            }

            // 특화는 대략 '반 레벨'만큼만 — 고르는 재미는 주되 한쪽이 정답이 되면 안 된다.
            // (DEF 1레벨=2.0 / ATK 1레벨=3.0 / PARRY 1레벨=0.03초)
            Ensure("Character_Knight", "knight", "character.knight", "character.knight.desc", 0,
                "player_knight", "darksouls_saint",
                bonusMaxHp: 12, bonusDamageReduction: 1f, bonusAttackDamage: 0f, bonusParryWindow: 0f);
            Ensure("Character_Sekiro", "sekiro", "character.sekiro", "character.sekiro.desc", 1,
                "player_sekiro", "sekiro_shaman",
                bonusMaxHp: 0, bonusDamageReduction: 0f, bonusAttackDamage: 1.5f, bonusParryWindow: 0.015f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Characters] {Folder} 준비 완료. " +
                      "성녀 그림은 캐릭터마다 다르고, 대장장이는 IntermissionScreen 인스펙터에 직접 꽂습니다.");
        }

        private static void Ensure(
            string assetName, string id, string nameKey, string descriptionKey, int sortOrder,
            string combatTexture, string saintTexture,
            int bonusMaxHp, float bonusDamageReduction, float bonusAttackDamage, float bonusParryWindow)
        {
            string path = $"{Folder}/{assetName}.asset";
            var character = AssetDatabase.LoadAssetAtPath<PlayerCharacterSO>(path);
            bool created = character == null;
            if (created)
            {
                character = ScriptableObject.CreateInstance<PlayerCharacterSO>();
                AssetDatabase.CreateAsset(character, path);
            }

            var so = new SerializedObject(character);
            SetIfEmpty(so, "characterId", id);
            SetIfEmpty(so, "nameKey", nameKey);
            SetIfEmpty(so, "descriptionKey", descriptionKey);
            if (created)
                so.FindProperty("sortOrder").intValue = sortOrder;

            SetSpriteIfEmpty(so, "combatSprite", combatTexture);
            SetSpriteIfEmpty(so, "saintSprite", saintTexture);

            // 특화는 '아직 아무것도 안 건드린 상태'일 때만 깔아 준다 — 손으로 튜닝한 값을 되돌리지 않는다.
            if (BonusesUntouched(so))
            {
                so.FindProperty("bonusMaxHp").intValue = bonusMaxHp;
                so.FindProperty("bonusDamageReduction").floatValue = bonusDamageReduction;
                so.FindProperty("bonusAttackDamage").floatValue = bonusAttackDamage;
                so.FindProperty("bonusParryWindowSeconds").floatValue = bonusParryWindow;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(character);

            Debug.Log($"[Characters] {(created ? "생성" : "갱신")}: {path}");
        }

        private static bool BonusesUntouched(SerializedObject so) =>
            so.FindProperty("bonusMaxHp").intValue == 0 &&
            so.FindProperty("bonusDamageReduction").floatValue == 0f &&
            so.FindProperty("bonusAttackDamage").floatValue == 0f &&
            so.FindProperty("bonusParryWindowSeconds").floatValue == 0f;

        private static void SetIfEmpty(SerializedObject so, string propertyName, string value)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop != null && string.IsNullOrWhiteSpace(prop.stringValue))
                prop.stringValue = value;
        }

        private static void SetSpriteIfEmpty(SerializedObject so, string propertyName, string textureName)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null || prop.objectReferenceValue != null)
                return;

            Sprite sprite = LargestSprite($"{DotImgs}/{textureName}.png");
            if (sprite == null)
            {
                Debug.LogWarning(
                    $"[Characters] '{textureName}.png' 에서 스프라이트를 찾지 못했습니다. " +
                    "텍스처 타입이 Sprite인지, Multiple이라면 슬라이스가 되어 있는지 확인하세요.");
                return;
            }

            prop.objectReferenceValue = sprite;
        }

        /// <summary>
        /// 자동 슬라이스된 시트에는 자잘한 조각이 섞여 있다 — 가장 큰 것이 본체다.
        /// (그게 아니면 에셋 인스펙터에서 직접 다른 조각을 끌어다 놓으면 된다.)
        /// </summary>
        private static Sprite LargestSprite(string texturePath)
        {
            Sprite best = null;
            float bestArea = 0f;

            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(texturePath))
            {
                if (asset is not Sprite sprite)
                    continue;

                float area = sprite.rect.width * sprite.rect.height;
                if (area > bestArea)
                {
                    bestArea = area;
                    best = sprite;
                }
            }

            return best;
        }
    }
}
