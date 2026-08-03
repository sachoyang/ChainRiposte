using System.IO;
using ChainRiposte.Game.Characters;
using ChainRiposte.Game.Map;
using ChainRiposte.Game.Theming;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// 테마(캐릭터별 컨셉) 에셋을 만들고 손보는 툴.
    /// 캐릭터 메뉴와 같은 규칙 — 이미 있으면 <b>비어 있는 슬롯만</b> 채운다.
    ///
    /// <para>테마를 더 만들려면 <c>Create ▸ ChainRiposte ▸ Theme</c> 로 같은 폴더에 에셋을 하나 더 두고
    /// 캐릭터 에셋에서 가리키면 된다.</para>
    ///
    /// <para><b>씬에 배경을 까는 메뉴는 걷어냈다.</b> 배경 오브젝트는 이미 각 씬에 실물로 있고
    /// 그림은 <see cref="ThemedSprite"/>가 런타임에 채운다. 다시 까는 메뉴를 남겨 두면
    /// 손으로 맞춘 위치·크기·색을 되돌릴 뿐이다.</para>
    /// </summary>
    public static class ThemeAssetsMenu
    {
        private const string Folder = "Assets/_Project/Data/Resources/Themes";
        private const string BackFolder = "Assets/_Project/DotImgs/back";

        /// <summary>Resources.LoadAll 로 테마를 찾을 때 쓰는 폴더 이름 (인스펙터 툴이 공유).</summary>
        public const string ThemeResourcesFolder = "Themes";

        [MenuItem("Tools/ChainRiposte/Theme/Create Default Themes")]
        private static void CreateDefaults()
        {
            if (!Directory.Exists(Folder))
            {
                Directory.CreateDirectory(Folder);
                AssetDatabase.Refresh();
            }

            ThemeSO irithyll = Ensure("Theme_Irithyll", "irithyll", "Irithyll");
            ThemeSO ashina = Ensure("Theme_Ashina", "ashina", "ashina");

            AssignTheme("Character_Knight", irithyll);
            AssignTheme("Character_Sekiro", ashina);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Theme] {Folder} 준비 완료. 비어 있던 슬롯만 채웠습니다(배경·이름 키). " +
                      "보스 그림은 이 툴이 고르지 않습니다 — 캐릭터별 보스는 " +
                      "Stage_*.asset ▸ 캐릭터별 보스 겉모습 에서 지정하세요.");
        }

        /// <summary>
        /// 에디트 모드에서는 <see cref="ThemedSprite"/>가 안 돌므로, 씬 뷰에 해당 테마의 배경·길 그림을
        /// 직접 꽂아 보여 준다. 어느 테마를 편집 중인지 눈으로 확인하며 노드를 찍기 위한 미리보기다.
        /// (플레이하면 런타임에 다시 테마대로 채워지므로 여기서 꽂은 것은 편집용일 뿐이다.)
        /// </summary>
        public static void PreviewThemeInSceneEditorOnly(ThemeSO theme)
        {
            if (theme == null)
                return;

            var controller = Object.FindFirstObjectByType<StageSelectController>();
            if (controller == null)
                return;

            SetPreviewSprite(controller.transform.Find("SkyBackground"), theme.GetBackground(ThemeSO.KeyMap));
            SetPreviewSprite(controller.transform.Find("ThemedBackground"), theme.GetBackground(ThemeSO.KeyPath));
            SetPreviewSprite(controller.transform.Find("BottomBackground"), theme.GetBackground(ThemeSO.KeyMap));

            var canvas = controller.GetComponentInChildren<Canvas>(true);
            if (canvas != null)
            {
                Transform image = canvas.transform.Find("TopBackground/Image");
                if (image != null && image.TryGetComponent(out Image topImage))
                {
                    Sprite sprite = theme.GetBackground(ThemeSO.KeyMap);
                    if (sprite != null)
                    {
                        Undo.RecordObject(topImage, "Preview Theme");
                        topImage.sprite = sprite;
                    }
                }
            }
        }

        private static void SetPreviewSprite(Transform transform, Sprite sprite)
        {
            if (transform == null || sprite == null)
                return;
            if (!transform.TryGetComponent(out SpriteRenderer renderer))
                return;

            Undo.RecordObject(renderer, "Preview Theme");
            renderer.sprite = sprite;
        }

        /// <summary>
        /// 노드의 세로 간격을 벌린다. 세로 화면에서 스크롤이 느껴지려면 길이 보이는 창보다 길어야 하는데,
        /// 기본 배치는 가로로 퍼져 있어 세로에는 거의 다 들어와 버린다.
        ///
        /// <para><b>한 번 찍은 뒤에는 다시 부르지 말 것.</b> 노드 위치는 사용자가 찍어 둔 것이라
        /// 다른 작업에 딸려 움직이면 안 된다 — 그래서 확인 창을 반드시 거친다.</para>
        /// </summary>
        [MenuItem("Tools/ChainRiposte/Theme/Spread Map Nodes Vertically")]
        private static void SpreadMapNodes()
        {
            var controller = Object.FindFirstObjectByType<StageSelectController>();
            if (controller == null)
            {
                EditorUtility.DisplayDialog("노드 벌리기", "StageSelect 씬을 연 상태에서 실행하세요.", "확인");
                return;
            }

            MapNode[] nodes = controller.GetComponentsInChildren<MapNode>(true);
            if (nodes.Length < 2)
                return;

            if (!EditorUtility.DisplayDialog("노드 벌리기",
                    $"노드 {nodes.Length}개의 <세로> 간격을 1.8배로 늘립니다. (가로 위치는 그대로)\n\n" +
                    "이미 손으로 찍어 배치했다면 그 배치가 흐트러집니다.",
                    "늘리기", "취소"))
                return;

            float sum = 0f;
            foreach (MapNode node in nodes)
                sum += node.transform.position.y;
            float center = sum / nodes.Length;

            foreach (MapNode node in nodes)
            {
                Undo.RecordObject(node.transform, "Spread Map Nodes");
                Vector3 position = node.transform.position;
                position.y = center + (position.y - center) * 1.8f;
                node.transform.position = position;
            }

            controller.RefreshPathLineEditorOnly();
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        }

        // ─────────────────────────────────────────────────────────────

        private static ThemeSO Ensure(string assetName, string themeId, string backTexture)
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

            // 없는 키는 붙이고, 있는데 비어 있는 슬롯만 채운다 — 손으로 꽂아 둔 그림은 그대로 둔다.
            SerializedProperty backgrounds = so.FindProperty("backgrounds");
            Sprite back = LargestSprite($"{BackFolder}/{backTexture}.png");
            EnsureBackgroundKey(backgrounds, ThemeSO.KeyMap, back);
            // 길·퍼즐·전투는 아직 전용 아트가 없어 <같은 그림을 재활용>한다. 화면마다 키가 따로 있으므로
            // 전용 아트가 생기면 그 슬롯만 갈아 끼우면 되고 코드는 안 바뀐다.
            EnsureBackgroundKey(backgrounds, ThemeSO.KeyPath, back);
            // 퍼즐·전투는 <칸만> 만들어 둔다. 어떤 그림을 깔지는 기획이라 툴이 고르지 않는다.
            EnsureBackgroundKey(backgrounds, ThemeSO.KeyPuzzle, null);
            EnsureBackgroundKey(backgrounds, ThemeSO.KeyCombat, null);
            if (back == null)
                Debug.LogWarning($"[Theme] '{backTexture}.png' 에서 스프라이트를 찾지 못했습니다. 텍스처 타입이 Sprite 인지 확인하세요.");

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(theme);
            Debug.Log($"[Theme] {(created ? "생성" : "갱신")}: {path}");
            return theme;
        }

        private static void EnsureBackgroundKey(SerializedProperty list, string key, Sprite sprite)
        {
            SerializedProperty element = FindOrAdd(list, "key", key);
            SerializedProperty slot = element.FindPropertyRelative("sprite");
            if (slot.objectReferenceValue == null && sprite != null)
                slot.objectReferenceValue = sprite;
        }

        /// <summary>식별자가 같은 항목을 찾고, 없으면 끝에 하나 붙여서 돌려준다.</summary>
        private static SerializedProperty FindOrAdd(SerializedProperty list, string idField, string id)
        {
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                if (element.FindPropertyRelative(idField).stringValue == id)
                    return element;
            }

            int index = list.arraySize;
            list.InsertArrayElementAtIndex(index);
            SerializedProperty added = list.GetArrayElementAtIndex(index);
            Clear(added); // Unity 의 배열 삽입은 <직전 항목을 복제>한다 — 비우지 않으면 남의 값이 딸려 온다
            added.FindPropertyRelative(idField).stringValue = id;
            return added;
        }

        private static void Clear(SerializedProperty element)
        {
            SerializedProperty iterator = element.Copy();
            SerializedProperty end = element.GetEndProperty();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                enterChildren = false;
                switch (iterator.propertyType)
                {
                    case SerializedPropertyType.String:
                        iterator.stringValue = string.Empty;
                        break;
                    case SerializedPropertyType.ObjectReference:
                        iterator.objectReferenceValue = null;
                        break;
                }
            }
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
