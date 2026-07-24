using System.IO;
using ChainRiposte.Game;
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

            if (Object.FindFirstObjectByType<IntroController>() != null)
            {
                SetupIntro();
                return;
            }

            EditorUtility.DisplayDialog("배경 배치",
                "Intro / Title / StageSelect 씬을 연 상태에서 실행하세요.", "확인");
        }

        /// <summary>
        /// 인트로는 로고 하나만 떠올랐다 지는 화면이다 — 배경에 아무것도 두지 않고
        /// <b>완전한 검정</b>으로 비워야 로고가 제일 잘 읽힌다.
        /// </summary>
        private static void SetupIntro()
        {
            var background = GameObject.Find("Canvas/Background");
            if (background != null && background.TryGetComponent(out Image image))
            {
                Undo.RecordObject(image, "Intro Background");
                image.sprite = null;
                image.color = Color.black;
            }

            Camera camera = Camera.main;
            if (camera != null)
            {
                Undo.RecordObject(camera, "Intro Background");
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
            }

            var intro = Object.FindFirstObjectByType<IntroController>();
            EditorSceneManager.MarkSceneDirty(intro.gameObject.scene);
            Debug.Log("[Theme] 인트로 배경을 검정으로 맞췄습니다. 로고는 IntroController 의 페이드 시간으로 조절하세요.");
        }

        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 월드맵 배경 — 테마가 갈아 끼우는 자리. SpriteRenderer라서 UI가 아니라 <b>카메라 범위</b>를 덮는다.
        /// 자리만 잡아 두면 그림은 <see cref="ThemedSprite"/>가 채운다.
        /// </summary>
        /// <summary>
        /// 월드맵은 세로와 가로가 아예 다른 화면이다.
        /// <b>세로</b> = 위 배경 띠 / 가운데 길(스크롤) / 아래 정보 띠.
        /// <b>가로</b> = 배경이 화면 전체를 덮고 길 전체가 보이며 정보는 오른쪽 컬럼.
        /// 그래서 배경을 두 벌 두고 방향에 따라 한쪽만 그린다.
        /// </summary>
        private static void SetupStageSelect()
        {
            StageSelectController controller = Object.FindFirstObjectByType<StageSelectController>();
            Transform root = controller.transform;

            // ① 배경(하늘·원경) — 화면을 덮고 길 뒤에 깔린다. 세로·가로 모두.
            ConfigureSkyBackground(EnsureChild(root, "SkyBackground", first: true));

            // ② 길이 놓인 땅 — 배경과 다른 그림이라 키가 다르다. 크기·위치는 씬에서 잡는다.
            ConfigurePathBackground(EnsureChild(root, "ThemedBackground", first: false));

            DisablePlaceholder(root, "World1Bg");
            DisablePlaceholder(root, "World2Bg");

            // ② 세로용 — 화면 위쪽 배경 띠. 이 띠가 길의 윗부분을 가려서 '창'을 만든다.
            var canvas = root.GetComponentInChildren<Canvas>(true);
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("배경 배치",
                    "월드맵 Canvas 를 찾지 못했습니다. Build StageSelect Layout 을 먼저 실행하세요.", "확인");
                return;
            }

            RectTransform topBand = EnsureTopBand(canvas);
            var infoPanel = canvas.transform.Find("InfoPanel") as RectTransform;

            // ③ 카메라 — 세로에서만 스크롤을 맡는다
            Camera camera = Camera.main;
            if (camera == null)
            {
                EditorUtility.DisplayDialog("배경 배치", "MainCamera 태그가 붙은 카메라를 찾지 못했습니다.", "확인");
                return;
            }

            var rig = camera.GetComponent<MapCameraRig>();
            if (rig == null)
                rig = Undo.AddComponent<MapCameraRig>(camera.gameObject);

            var rigSo = new SerializedObject(rig);
            rigSo.FindProperty("cameraFit").objectReferenceValue = camera.GetComponent<CameraFit2D>();
            rigSo.FindProperty("topBand").objectReferenceValue = topBand;
            rigSo.FindProperty("bottomBand").objectReferenceValue = infoPanel;
            rigSo.ApplyModifiedPropertiesWithoutUndo();

            var controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("cameraRig").objectReferenceValue = rig;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            OfferVerticalSpread(root);

            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            Debug.Log("[Theme] 월드맵 구성 완료 — 배경(SkyBackground, 키 map) / 길 그림(ThemedBackground, 키 path) / " +
                      "세로 전용 상단 띠(TopBackground, 키 map). 길 그림은 비어 있으니 " +
                      "Theme_*.asset ▸ Backgrounds ▸ path 에 넣고, 크기·위치는 씬에서 길에 맞춰 잡으세요. " +
                      "확대 정도는 MapCameraRig ▸ portraitViewWidth.");
        }

        /// <summary>
        /// 배경(하늘·원경). 화면을 덮고 길 뒤에 깔린다 — <b>세로·가로 모두</b>.
        /// 상단 띠와 같은 <c>map</c> 그림을 쓰므로 세로에서 띠와 배경이 이어져 보인다.
        /// </summary>
        private static void ConfigureSkyBackground(GameObject go)
        {
            go.transform.localPosition = new Vector3(0f, 0f, 2f);

            var renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null)
                renderer = Undo.AddComponent<SpriteRenderer>(go);
            renderer.sortingOrder = -200; // 길 그림보다도 뒤
            renderer.color = Color.white;
            if (renderer.sprite == null)
                renderer.sprite = LargestSprite($"{BackFolder}/Irithyll.png");

            SetThemeKey(go, ThemeSO.KeyMap);
            EnsureStillPanner(go); // 화면을 덮되 흔들지는 않는다
            SetVisibility(go, portrait: true, landscape: true);
        }

        /// <summary>
        /// 길이 놓인 땅. 배경과 다른 그림이므로 키가 다르고, <b>화면을 덮지 않는다</b> —
        /// 덮어 버리면 뒤의 배경이 아무 의미가 없어진다. 크기·위치는 씬에서 길에 맞춰 잡는다.
        /// </summary>
        private static void ConfigurePathBackground(GameObject go)
        {
            var renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null)
                renderer = Undo.AddComponent<SpriteRenderer>(go);
            renderer.sortingOrder = -100; // 배경보다 앞, 노드·경로보다 뒤
            renderer.color = Color.white;

            SetThemeKey(go, ThemeSO.KeyPath);

            // 예전 배선에서 붙었을 수 있는 '화면 덮기'를 끈다. 지우지는 않는다 — 되돌리기 쉽게.
            var panner = go.GetComponent<BackgroundPanner>();
            if (panner != null && panner.enabled)
            {
                Undo.RecordObject(panner, "Path Background");
                panner.enabled = false;
            }

            SetVisibility(go, portrait: true, landscape: true);
        }

        private static void SetThemeKey(GameObject go, string key)
        {
            var themed = go.GetComponent<ThemedSprite>();
            if (themed == null)
                themed = Undo.AddComponent<ThemedSprite>(go);

            var so = new SerializedObject(themed);
            so.FindProperty("backgroundKey").stringValue = key;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 화면 위쪽 배경 띠 — <b>세로 전용</b>이고, 하는 일은 길의 윗부분을 가려 '창'을 만드는 것이다.
        /// 가로에서는 길 전체가 보여야 하므로 가릴 이유가 없어 꺼진다(그림 자체는 배경이 계속 쓴다).
        ///
        /// <para>그림은 <b>띠 안쪽</b>을 덮어야 하므로 띠(마스크) + 자식 이미지 구조로 만든다 —
        /// BackgroundPanner 는 '부모를 덮는' 물건이라 이미지에 직접 붙이면 캔버스 전체로 커진다.</para>
        /// </summary>
        private static RectTransform EnsureTopBand(Canvas canvas)
        {
            Transform found = canvas.transform.Find("TopBackground");
            GameObject bandGo;
            if (found != null)
            {
                bandGo = found.gameObject;
            }
            else
            {
                bandGo = new GameObject("TopBackground", typeof(RectTransform), typeof(RectMask2D));
                Undo.RegisterCreatedObjectUndo(bandGo, "Top Background");
                bandGo.transform.SetParent(canvas.transform, worldPositionStays: false);
                bandGo.transform.SetAsFirstSibling(); // 정보 패널보다 뒤에

                var bandRect = (RectTransform)bandGo.transform;
                bandRect.anchorMin = new Vector2(0f, 1f);
                bandRect.anchorMax = new Vector2(1f, 1f);
                bandRect.pivot = new Vector2(0.5f, 1f);
                bandRect.anchoredPosition = Vector2.zero;
                bandRect.sizeDelta = new Vector2(0f, 620f); // 세로 1920 기준 약 32%
            }

            Transform imageFound = bandGo.transform.Find("Image");
            GameObject imageGo;
            if (imageFound != null)
            {
                imageGo = imageFound.gameObject;
            }
            else
            {
                imageGo = new GameObject("Image", typeof(RectTransform), typeof(Image));
                Undo.RegisterCreatedObjectUndo(imageGo, "Top Background");
                imageGo.transform.SetParent(bandGo.transform, worldPositionStays: false);
                var imageRect = (RectTransform)imageGo.transform;
                imageRect.anchorMin = Vector2.zero;
                imageRect.anchorMax = Vector2.one;
                imageRect.offsetMin = imageRect.offsetMax = Vector2.zero;
            }

            var image = imageGo.GetComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;
            if (image.sprite == null)
                image.sprite = LargestSprite($"{BackFolder}/Irithyll.png");

            SetThemeKey(imageGo, ThemeSO.KeyMap); // 배경(하늘)과 같은 그림 — 띠와 뒤가 이어져 보인다
            EnsureStillPanner(imageGo);
            SetVisibility(imageGo, portrait: true, landscape: false);

            return (RectTransform)bandGo.transform;
        }

        /// <summary>
        /// 크기만 맞추고 흔들지는 않는 배경. 월드맵은 눈이 길을 따라가야 하는 화면이라
        /// 배경이 계속 움직이면 방해가 된다 (타이틀과 반대).
        /// </summary>
        private static void EnsureStillPanner(GameObject go)
        {
            var panner = go.GetComponent<BackgroundPanner>();
            if (panner == null)
                panner = Undo.AddComponent<BackgroundPanner>(go);

            var so = new SerializedObject(panner);
            so.FindProperty("amplitude").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetVisibility(GameObject go, bool portrait, bool landscape)
        {
            var visibility = go.GetComponent<OrientationVisibility>();
            if (visibility == null)
                visibility = Undo.AddComponent<OrientationVisibility>(go);

            var so = new SerializedObject(visibility);
            so.FindProperty("showInPortrait").boolValue = portrait;
            so.FindProperty("showInLandscape").boolValue = landscape;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject EnsureChild(Transform parent, string name, bool first)
        {
            Transform found = parent.Find(name);
            if (found != null)
                return found.gameObject;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Setup Background");
            go.transform.SetParent(parent, worldPositionStays: false);
            if (first)
                go.transform.SetAsFirstSibling();
            return go;
        }

        /// <summary>
        /// 세로에서 스크롤이 느껴지려면 길이 <b>보이는 창보다 길어야</b> 한다.
        /// 지금 노드 배치는 가로로 퍼져 있어서 세로 화면에는 거의 다 들어와 버리므로 한 번 물어보고 늘린다.
        /// (노드 위치는 사용자 것이라 말없이 바꾸지 않는다.)
        /// </summary>
        private static void OfferVerticalSpread(Transform root)
        {
            MapNode[] nodes = root.GetComponentsInChildren<MapNode>(true);
            if (nodes.Length < 2)
                return;

            if (!EditorUtility.DisplayDialog("월드맵 세로 배치",
                    "세로에서 스크롤이 느껴지려면 길이 화면보다 길어야 합니다.\n" +
                    "노드의 세로 간격을 1.8배로 늘릴까요? (가로 위치는 그대로)\n\n" +
                    "나중에 씬에서 직접 드래그해도 됩니다 — 경로선은 따라옵니다.",
                    "늘리기", "그대로 두기"))
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

            // 없는 키만 뒤에 붙인다 — 이미 채워 둔 슬롯은 건드리지 않고, 키가 늘어도 다시 돌리면 따라온다.
            SerializedProperty backgrounds = so.FindProperty("backgrounds");
            Sprite back = LargestSprite($"{BackFolder}/{backTexture}.png");
            EnsureBackgroundKey(backgrounds, ThemeSO.KeyMap, back);
            EnsureBackgroundKey(backgrounds, ThemeSO.KeyPath, back); // 우선 배경과 같은 그림을 재활용한다
            EnsureBackgroundKey(backgrounds, ThemeSO.KeyPuzzle, null);
            EnsureBackgroundKey(backgrounds, ThemeSO.KeyCombat, null);
            if (back == null)
                Debug.LogWarning($"[Theme] '{backTexture}.png' 에서 스프라이트를 찾지 못했습니다. 텍스처 타입이 Sprite 인지 확인하세요.");

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

        private static void EnsureBackgroundKey(SerializedProperty list, string key, Sprite sprite)
        {
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).FindPropertyRelative("key").stringValue == key)
                    return;
            }

            int index = list.arraySize;
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
