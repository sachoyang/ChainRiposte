using System.IO;
using ChainRiposte.Game.Characters;
using ChainRiposte.Game.Flow;
using ChainRiposte.Game.Map;
using ChainRiposte.Game.Theming;
using ChainRiposte.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// 기본 테마 2종(이루실 / 아시나)을 만들고 캐릭터에 물려 준다.
    /// 캐릭터 메뉴와 같은 규칙 — 이미 있으면 <b>비어 있는 슬롯만</b> 채운다.
    ///
    /// 테마를 더 만들려면 <c>Create ▸ ChainRiposte ▸ Theme</c> 로 같은 폴더에 에셋을 하나 더 두고
    /// 캐릭터 에셋에서 가리키면 된다.
    /// </summary>
    public static class ThemeAssetsMenu
    {
        private const string Folder = "Assets/_Project/Data/Resources/Themes";
        private const string BackFolder = "Assets/_Project/DotImgs/back";

        [MenuItem("Tools/ChainRiposte/Theme/Create Default Themes")]
        private static void CreateDefaults()
        {
            if (!Directory.Exists(Folder))
            {
                Directory.CreateDirectory(Folder);
                AssetDatabase.Refresh();
            }

            ThemeSO irithyll = Ensure("Theme_Irithyll", "irithyll", "Irithyll",
                "boss.irithyll.01", "boss.irithyll.02");
            ThemeSO ashina = Ensure("Theme_Ashina", "ashina", "ashina",
                "boss.ashina.01", "boss.ashina.02");

            AssignTheme("Character_Knight", irithyll);
            AssignTheme("Character_Sekiro", ashina);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Theme] {Folder} 준비 완료. 보스 그림은 비워 뒀습니다 — " +
                      "비어 있으면 BossDataSO의 그림으로 떨어지므로, 컨셉별 보스 아트가 생기면 그때 꽂으면 됩니다.");
        }

        /// <summary>
        /// 지금 열려 있는 씬에 배경을 깔아 준다. 어떤 씬인지는 컨트롤러로 알아본다 —
        /// 씬을 몰래 열고 저장하지 않으므로 작업 중인 것이 날아가지 않는다.
        /// </summary>
        [MenuItem("Tools/ChainRiposte/Theme/Setup Background In Open Scene")]
        private static void SetupBackground()
        {
            if (Object.FindFirstObjectByType<StageSelectController>() != null)
            {
                SetupStageSelect();
                return;
            }

            if (Object.FindFirstObjectByType<TitleController>() != null)
            {
                SetupTitle();
                return;
            }

            EditorUtility.DisplayDialog("배경 배치",
                "StageSelect / Title 씬을 연 상태에서 실행하세요.", "확인");
        }

        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 월드맵 배경 — 테마가 갈아 끼우는 자리. SpriteRenderer라서 UI가 아니라 <b>카메라 범위</b>를 덮는다.
        /// 자리만 잡아 두면 그림은 <see cref="ThemedSprite"/>가 채운다.
        /// </summary>
        private static void SetupStageSelect()
        {
            StageSelectController controller = Object.FindFirstObjectByType<StageSelectController>();
            Transform root = controller.transform;

            Transform existing = root.Find("ThemedBackground");
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject("ThemedBackground");
                Undo.RegisterCreatedObjectUndo(go, "Themed Background");
                go.transform.SetParent(root, worldPositionStays: false);
                go.transform.SetAsFirstSibling();
            }

            ConfigureMapBackground(go);
            DisablePlaceholder(root, "World1Bg");
            DisablePlaceholder(root, "World2Bg");

            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            Debug.Log("[Theme] 월드맵 배경 배치 완료. 색 사각형 World1Bg/World2Bg 는 꺼 뒀습니다(지워도 됩니다). " +
                      "흔들림이 싫으면 BackgroundPanner 의 amplitude 를 0 으로 두세요.");
        }

        private static void ConfigureMapBackground(GameObject go)
        {
            go.transform.localPosition = new Vector3(0f, 0f, 1f);

            var renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null)
                renderer = Undo.AddComponent<SpriteRenderer>(go);
            renderer.sortingOrder = -100; // 노드·경로보다 확실히 뒤
            renderer.color = Color.white;
            if (renderer.sprite == null)
                renderer.sprite = LargestSprite($"{BackFolder}/Irithyll.png"); // 테마가 없을 때 보이는 기본값

            if (go.GetComponent<ThemedSprite>() == null)
                Undo.AddComponent<ThemedSprite>(go); // 기본 키가 map 이라 그대로 둔다
            if (go.GetComponent<BackgroundPanner>() == null)
                Undo.AddComponent<BackgroundPanner>(go);
        }

        private static void DisablePlaceholder(Transform root, string name)
        {
            Transform placeholder = root.Find(name);
            if (placeholder == null || !placeholder.gameObject.activeSelf)
                return;

            Undo.RecordObject(placeholder.gameObject, "Disable Placeholder");
            placeholder.gameObject.SetActive(false);
        }

        /// <summary>
        /// 타이틀 배경은 <b>컨셉과 무관한 공용</b>이다 — 캐릭터를 고르기 전에도 보이는 화면이라
        /// <see cref="ThemedSprite"/>를 붙이지 않고 그림을 직접 꽂는다.
        /// </summary>
        private static void SetupTitle()
        {
            var background = GameObject.Find("Canvas/Background");
            if (background == null)
            {
                EditorUtility.DisplayDialog("배경 배치", "Title 씬에서 Canvas/Background 를 찾지 못했습니다.", "확인");
                return;
            }

            var image = background.GetComponent<Image>();
            if (image == null)
            {
                EditorUtility.DisplayDialog("배경 배치", "Canvas/Background 에 Image 가 없습니다.", "확인");
                return;
            }

            Sprite sprite = LargestSprite($"{BackFolder}/ashina.png");
            if (sprite != null)
            {
                Undo.RecordObject(image, "Title Background");
                image.sprite = sprite;
                image.color = Color.white; // 어둡게 깔고 싶으면 이 색으로 조절
            }

            if (background.GetComponent<BackgroundPanner>() == null)
                Undo.AddComponent<BackgroundPanner>(background);

            EditorSceneManager.MarkSceneDirty(background.scene);
            Debug.Log("[Theme] 타이틀 배경 = ashina + 좌우 왕복. 그림을 바꾸려면 Canvas/Background 의 Source Image 만 교체하면 됩니다.");
        }

        // ─────────────────────────────────────────────────────────────

        private static ThemeSO Ensure(string assetName, string themeId, string backTexture,
            string boss01NameKey, string boss02NameKey)
        {
            string path = $"{Folder}/{assetName}.asset";
            var theme = AssetDatabase.LoadAssetAtPath<ThemeSO>(path);
            bool created = theme == null;
            if (created)
            {
                theme = ScriptableObject.CreateInstance<ThemeSO>();
                AssetDatabase.CreateAsset(theme, path);
            }

            var so = new SerializedObject(theme);
            SerializedProperty id = so.FindProperty("themeId");
            if (string.IsNullOrWhiteSpace(id.stringValue))
                id.stringValue = themeId;

            // 목록은 '아직 비어 있을 때'만 깔아 준다 — 손으로 채운 슬롯을 되돌리지 않는다.
            SerializedProperty backgrounds = so.FindProperty("backgrounds");
            if (backgrounds.arraySize == 0)
            {
                Sprite back = LargestSprite($"{BackFolder}/{backTexture}.png");
                AddBackground(backgrounds, 0, ThemeSO.KeyMap, back);
                AddBackground(backgrounds, 1, ThemeSO.KeyPuzzle, null);
                AddBackground(backgrounds, 2, ThemeSO.KeyCombat, null);
                if (back == null)
                    Debug.LogWarning($"[Theme] '{backTexture}.png' 에서 스프라이트를 찾지 못했습니다. 텍스처 타입이 Sprite 인지 확인하세요.");
            }

            SerializedProperty bosses = so.FindProperty("bosses");
            if (bosses.arraySize == 0)
            {
                // 그림은 비워 둔다 — 비면 BossDataSO 의 그림으로 떨어지므로 이름만 먼저 갈린다.
                AddBoss(bosses, 0, "Boss_01", boss01NameKey);
                AddBoss(bosses, 1, "Boss_02", boss02NameKey);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);
            Debug.Log($"[Theme] {(created ? "생성" : "갱신")}: {path}");
            return theme;
        }

        private static void AddBackground(SerializedProperty list, int index, string key, Sprite sprite)
        {
            list.InsertArrayElementAtIndex(index);
            SerializedProperty element = list.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("key").stringValue = key;
            element.FindPropertyRelative("sprite").objectReferenceValue = sprite;
        }

        private static void AddBoss(SerializedProperty list, int index, string bossId, string nameKey)
        {
            list.InsertArrayElementAtIndex(index);
            SerializedProperty element = list.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("bossId").stringValue = bossId;
            element.FindPropertyRelative("sprite").objectReferenceValue = null;
            element.FindPropertyRelative("nameKey").stringValue = nameKey;
        }

        private static void AssignTheme(string characterAsset, ThemeSO theme)
        {
            string path = $"Assets/_Project/Data/Resources/{CharacterService.ResourcesFolder}/{characterAsset}.asset";
            var character = AssetDatabase.LoadAssetAtPath<PlayerCharacterSO>(path);
            if (character == null)
            {
                Debug.LogWarning($"[Theme] {path} 가 없어 테마를 물리지 못했습니다. Create Default Characters 를 먼저 실행하세요.");
                return;
            }

            var so = new SerializedObject(character);
            SerializedProperty prop = so.FindProperty("theme");
            if (prop.objectReferenceValue != null)
                return; // 손으로 바꿔 둔 것을 되돌리지 않는다

            prop.objectReferenceValue = theme;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(character);
            Debug.Log($"[Theme] {characterAsset} → {theme.name}");
        }

        /// <summary>자동 슬라이스된 시트에는 자잘한 조각이 섞여 있다 — 가장 큰 것이 본체다.</summary>
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
