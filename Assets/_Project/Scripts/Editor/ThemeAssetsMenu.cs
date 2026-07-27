using System.IO;
using ChainRiposte.Game;
using ChainRiposte.Game.Characters;
using ChainRiposte.Game.Combat;
using ChainRiposte.Game.Flow;
using ChainRiposte.Game.Map;
using ChainRiposte.Game.Theming;
using ChainRiposte.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
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

            // 전투·퍼즐 화면은 평소 꺼져 있으므로 꺼진 것까지 찾아야 Main 씬인 줄 안다.
            if (Object.FindFirstObjectByType<CombatScreen>(FindObjectsInactive.Include) != null ||
                Object.FindFirstObjectByType<PuzzleHud>(FindObjectsInactive.Include) != null)
            {
                SetupMain();
                return;
            }

            EditorUtility.DisplayDialog("배경 배치",
                "Intro / Title / StageSelect / Main 씬을 연 상태에서 실행하세요.", "확인");
        }

        /// <summary>
        /// 전투 씬(퍼즐 + 보스전)의 배경 두 자리. <b>비파괴</b>다 — 배경 오브젝트만 만들거나 배선하고
        /// 다른 UI 는 건드리지 않는다.
        ///
        /// <para>퍼즐과 전투는 <b>층이 달라 방식도 다르다</b>: 판(보드)은 월드 스프라이트라 그 배경도
        /// 월드여야 뒤에 깔리고, 전투 화면은 화면을 덮는 Overlay 캔버스라 그 배경도 <b>캔버스 안</b>이어야 한다.
        /// (Overlay 캔버스는 언제나 월드 스프라이트 위에 그려지므로 월드 배경으로는 전투 화면 뒤에 못 간다.)</para>
        /// </summary>
        private static void SetupMain()
        {
            GameObject puzzle = SetupPuzzleBackground();
            GameObject combat = SetupCombatBackground();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[Theme] 전투 씬 배경 배선 완료 — " +
                      $"{(puzzle != null ? "퍼즐(PuzzleBackground, 월드, puzzle)" : "퍼즐 배경 실패")} / " +
                      $"{(combat != null ? "전투(CombatScreen ▸ Root/Background, UI, combat)" : "전투 배경 실패")}. " +
                      "그림은 테마의 puzzle·combat 키가 채웁니다. 너무 밝거나 어지러우면 그 Image·SpriteRenderer 의 색으로 눌러 주세요.");
        }

        /// <summary>
        /// 전투 씬의 배경 두 자리를 <b>도로 걷어낸다</b>. 그림을 정하기 전까지는 자리만 남아 있어도
        /// 화면을 가리므로, 붙이는 메뉴와 짝으로 둔다. 다른 UI 는 건드리지 않는다.
        /// </summary>
        [MenuItem("Tools/ChainRiposte/Theme/Remove Screen Backgrounds From Main")]
        private static void RemoveMainBackgrounds()
        {
            Transform puzzle = FindRootObject("PuzzleBackground");
            if (puzzle != null)
                Undo.DestroyObjectImmediate(puzzle.gameObject);

            var screen = Object.FindFirstObjectByType<CombatScreen>(FindObjectsInactive.Include);
            Transform combat = screen != null ? screen.transform.Find("Root/Background") : null;
            if (combat != null)
                Undo.DestroyObjectImmediate(combat.gameObject);

            if (puzzle == null && combat == null)
            {
                Debug.Log("[Theme] 지울 배경이 없습니다 (Main 씬을 연 상태인지 확인하세요).");
                return;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[Theme] 전투 씬 배경을 걷어냈습니다. 다시 깔려면 Setup Background In Open Scene 을 실행하세요.");
        }

        /// <summary>
        /// 판 뒤에 깔리는 배경. 카메라가 보는 범위를 덮되 <b>흔들지는 않는다</b> —
        /// 판 위에서 눈이 타일을 훑는 화면이라 뒤가 움직이면 방해가 된다.
        /// </summary>
        private static GameObject SetupPuzzleBackground()
        {
            Transform found = FindRootObject("PuzzleBackground");
            bool created = found == null;

            GameObject go;
            if (created)
            {
                go = new GameObject("PuzzleBackground");
                Undo.RegisterCreatedObjectUndo(go, "Setup Background");
                go.transform.position = new Vector3(0f, 0f, 2f); // 카메라(z −10)보다 앞
            }
            else
            {
                go = found.gameObject;
            }

            var renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null)
                renderer = Undo.AddComponent<SpriteRenderer>(go);
            renderer.sortingOrder = -300; // 배경 셀(−10)·타일보다 뒤

            if (created)
            {
                renderer.color = BackgroundTint; // 처음 깔 때만 — 손으로 맞춘 색을 되돌리지 않는다
                renderer.sprite = LargestSprite($"{BackFolder}/Irithyll.png");
            }

            SetThemeKey(go, ThemeSO.KeyPuzzle);
            EnsureStillPanner(go); // 화면은 덮되 amplitude 0
            return go;
        }

        /// <summary>
        /// 전투 화면의 배경. <see cref="CombatScreen"/> 의 <c>Root</c> 는 불투명한 단색이라
        /// 그 <b>자식</b>으로 넣어야 색 위에 그려진다(uGUI 는 자식이 부모 위).
        /// </summary>
        private static GameObject SetupCombatBackground()
        {
            var screen = Object.FindFirstObjectByType<CombatScreen>(FindObjectsInactive.Include);
            if (screen == null)
                return null;

            // UnityEngine.Object 에 ?? 를 쓰면 '가짜 null' 을 못 걸러낸다 — 직접 확인한다.
            Transform root = screen.transform.Find("Root");
            if (root == null)
            {
                var canvas = screen.GetComponentInChildren<Canvas>(true);
                if (canvas == null)
                    return null;
                root = canvas.transform;
            }

            Transform found = root.Find("Background");
            bool created = found == null;

            GameObject go;
            if (created)
            {
                Image image = EditorUiFactory.Stretch(root, "Background", BackgroundTint, raycast: false);
                image.sprite = LargestSprite($"{BackFolder}/Irithyll.png");
                image.type = Image.Type.Simple;
                go = image.gameObject;
            }
            else
            {
                go = found.gameObject;
            }

            go.transform.SetAsFirstSibling(); // 보스·플레이어·게이지보다 뒤
            SetThemeKey(go, ThemeSO.KeyCombat);
            // 늘려서 뭉개지 않고 <원본 비율 그대로> 화면을 덮게 한다. 흔들지는 않는다 —
            // 눈이 다가오는 패링 원을 좇는 화면이라 뒤가 움직이면 방해가 된다.
            EnsureStillPanner(go);
            return go;
        }

        /// <summary>배경은 눌러서 깐다 — 그 위의 판·게이지·글씨가 먼저 읽혀야 한다.</summary>
        private static readonly Color BackgroundTint = new(0.45f, 0.45f, 0.55f, 1f);

        private static Transform FindRootObject(string name)
        {
            foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.name == name)
                    return go.transform;
            }

            return null;
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

            // ③ 화면 아래 빈 공간을 채우는 땅. 있으면 배선만 한다(없으면 만들지 않는다 — 선택 요소).
            Transform bottom = root.Find("BottomBackground");
            if (bottom != null)
                ConfigureBottomBackground(bottom.gameObject);

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

            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            Debug.Log("[Theme] 월드맵 구성 완료 — 배경(SkyBackground, map) / 길 그림(ThemedBackground, path) / " +
                      "상단 띠(TopBackground, map, 세로 전용)" +
                      (root.Find("BottomBackground") != null ? " / 아래 땅(BottomBackground, map, −150)" : "") +
                      ". 길 그림은 크기·위치를 씬에서 길에 맞춰 잡으세요. 확대 정도는 MapCameraRig ▸ portraitViewWidth.");
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

            // 월드맵 배경은 화면에 맞춰 늘리지 않는다. 노드를 그림 위에 찍어서 배치하는데
            // 실행할 때 그림이 카메라에 맞춰 움직이면 찍어 둔 자리와 그림이 어긋난다.
            DisablePanner(go);
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
            DisablePanner(go);
            SetVisibility(go, portrait: true, landscape: true);
        }

        /// <summary>
        /// 화면 덮기를 끈다(지우지는 않는다 — 되돌리기 쉽게).
        /// 월드맵의 그림은 <b>씬에서 잡은 자리에 가만히 있어야</b> 한다. 카메라를 따라 움직이면
        /// 그림 위에 찍어 둔 노드와 그림이 실행할 때 어긋난다.
        /// </summary>
        private static void DisablePanner(GameObject go)
        {
            var panner = go.GetComponent<BackgroundPanner>();
            if (panner == null || !panner.enabled)
                return;

            Undo.RecordObject(panner, "Static Background");
            panner.enabled = false;
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
        /// 화면 아래 빈 공간을 채우는 땅. 길(<c>ThemedBackground</c>) <b>뒤</b>에 두어(sortingOrder −150)
        /// 겹치는 곳은 길이 덮고 빈 아래에서만 삐져나와 보인다. 월드 스프라이트라서 위로 올라가면
        /// 저절로 화면 밖으로 밀려 사라진다(따로 가리는 코드가 필요 없다).
        ///
        /// <para>그림은 상단 띠·하늘과 같은 <c>map</c> 키를 공유한다(사용자 결정) — 테마를 바꾸면 같이 바뀐다.
        /// 위치·크기는 씬에서 잡은 그대로 둔다(여기서 옮기지 않는다).</para>
        /// </summary>
        private static void ConfigureBottomBackground(GameObject go)
        {
            var renderer = go.GetComponent<SpriteRenderer>();
            if (renderer == null)
                renderer = Undo.AddComponent<SpriteRenderer>(go);
            renderer.sortingOrder = -150; // 하늘(−200)보다 앞, 길(−100)보다 뒤
            renderer.color = Color.white;
            if (renderer.sprite == null)
                renderer.sprite = LargestSprite($"{BackFolder}/Irithyll.png");

            SetThemeKey(go, ThemeSO.KeyMap); // 상단 띠와 같은 그림
            DisablePanner(go);
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
        /// 노드의 세로 간격을 벌린다. 세로 화면에서 스크롤이 느껴지려면 길이 보이는 창보다 길어야 하는데,
        /// 기본 배치는 가로로 퍼져 있어 세로에는 거의 다 들어와 버린다.
        ///
        /// <para><b>배경 배치와 분리된 메뉴인 이유</b>: 노드 위치는 사용자가 찍어 둔 것이라
        /// 다른 작업에 딸려 움직이면 안 된다. 한 번 찍은 뒤에는 이걸 다시 부르지 말 것.</para>
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
