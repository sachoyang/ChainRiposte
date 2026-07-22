using System.Collections.Generic;
using System.IO;
using ChainRiposte.Game.Flow;
using ChainRiposte.Game.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// 앱 흐름 씬(인트로·타이틀)을 실물로 생성하고 빌드 설정에 순서대로 등록한다.
    /// 흐름: 인트로 → 타이틀 → 스테이지선택 → 게임.
    ///
    /// 다른 빌더와 같은 원칙 — 여기서 한 번 깔아주고, 이후 배치·아트는 씬에서 편집한다.
    /// 재실행하면 두 씬을 다시 만든다(수정한 배치는 날아가므로 주의).
    /// 실행: <c>Tools ▸ ChainRiposte ▸ Build App Scenes (Intro + Title)</c>
    /// </summary>
    public static class AppScenesBuilder
    {
        private const string SceneDirectory = "Assets/_Project/Scenes";
        private static readonly Color Background = new(0.06f, 0.05f, 0.08f);
        private static readonly Color ButtonColor = new(0.20f, 0.18f, 0.24f, 1f);
        private static readonly Color AccentColor = new(0.55f, 0.16f, 0.18f, 1f);

        [MenuItem("Tools/ChainRiposte/Build App Scenes (Intro + Title)")]
        private static void Build()
        {
            if (!EditorUtility.DisplayDialog(
                    "앱 씬 생성",
                    "Intro.unity 와 Title.unity 를 새로 만듭니다.\n" +
                    "이미 있으면 덮어쓰므로 그 두 씬에서 편집한 내용은 사라집니다.\n" +
                    "(StageSelect / Main 은 건드리지 않습니다.)",
                    "생성", "취소"))
                return;

            if (!Directory.Exists(SceneDirectory))
                Directory.CreateDirectory(SceneDirectory);

            BuildScene(SceneRouter.Intro, PopulateIntro);
            BuildScene(SceneRouter.Title, PopulateTitle);
            RegisterBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "[AppScenes] Intro / Title 생성 완료. 빌드 설정 순서: Intro → Title → StageSelect → Main.\n" +
                "문구가 키로 보이면 Tools ▸ ChainRiposte ▸ Localization ▸ Create or Update Table 을 실행하세요.");
        }

        /// <summary>
        /// 현재 열려 있는 씬을 건드리지 않도록 새 씬을 <b>Additive</b>로 만들고 저장한 뒤 닫는다.
        /// </summary>
        private static void BuildScene(string sceneName, System.Action<Scene> populate)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            populate(scene);
            EditorSceneManager.SaveScene(scene, $"{SceneDirectory}/{sceneName}.unity");
            EditorSceneManager.CloseScene(scene, removeScene: true);
        }

        private static void PopulateIntro(Scene scene)
        {
            CreateCommon(scene, out Transform canvas);

            Image logo = EditorUiFactory.Stretch(canvas, "Logo", Color.white, raycast: false);
            var logoRect = (RectTransform)logo.transform;
            logoRect.anchorMin = logoRect.anchorMax = new Vector2(0.5f, 0.5f);
            logoRect.pivot = new Vector2(0.5f, 0.5f);
            logoRect.anchoredPosition = Vector2.zero;
            logoRect.sizeDelta = new Vector2(700f, 700f);
            logo.sprite = EditorUiFactory.Square; // 아트가 생기면 이 슬롯만 교체
            logo.preserveAspect = true;
            logo.color = new Color(1f, 1f, 1f, 0f); // 컨트롤러가 페이드로 올린다

            // 로고 아래 임시 문구 — 아트를 넣으면 지워도 된다.
            TextMeshProUGUI caption = EditorUiFactory.Text(
                canvas, "Caption", new Vector2(0f, -420f), new Vector2(0.5f, 0.5f), 44f,
                TextAlignmentOptions.Center, new Vector2(900f, 80f));
            caption.text = "ChainRiposte";

            var controllerGo = new GameObject("IntroController", typeof(IntroController));
            SceneManager.MoveGameObjectToScene(controllerGo, scene);
            var so = new SerializedObject(controllerGo.GetComponent<IntroController>());
            so.FindProperty("logo").objectReferenceValue = logo;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void PopulateTitle(Scene scene)
        {
            CreateCommon(scene, out Transform canvas);

            TextMeshProUGUI title = EditorUiFactory.Text(
                canvas, "Title", new Vector2(0f, -280f), new Vector2(0.5f, 1f), 120f,
                TextAlignmentOptions.Center, new Vector2(1000f, 160f), FontStyles.Bold);
            title.text = "ChainRiposte";
            EditorUiFactory.Orient(title.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(1000f, 140f));

            Button continueButton = MenuButton(canvas, "ContinueButton", "title.continue", 0, AccentColor);
            Button newGameButton = MenuButton(canvas, "NewGameButton", "title.newgame", 1, ButtonColor);
            Button optionsButton = MenuButton(canvas, "OptionsButton", "title.options", 2, ButtonColor);
            Button quitButton = MenuButton(canvas, "QuitButton", "title.quit", 3, ButtonColor);

            BuildConfirmPanel(canvas, out GameObject confirmPanel, out TMP_Text confirmText,
                out Button yesButton, out Button noButton);

            var controllerGo = new GameObject("TitleController", typeof(TitleController));
            SceneManager.MoveGameObjectToScene(controllerGo, scene);
            var so = new SerializedObject(controllerGo.GetComponent<TitleController>());
            so.FindProperty("continueButton").objectReferenceValue = continueButton;
            so.FindProperty("newGameButton").objectReferenceValue = newGameButton;
            so.FindProperty("optionsButton").objectReferenceValue = optionsButton;
            so.FindProperty("quitButton").objectReferenceValue = quitButton;
            so.FindProperty("confirmPanel").objectReferenceValue = confirmPanel;
            so.FindProperty("confirmText").objectReferenceValue = confirmText;
            so.FindProperty("confirmYesButton").objectReferenceValue = yesButton;
            so.FindProperty("confirmNoButton").objectReferenceValue = noButton;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>세로로 쌓이는 메뉴 버튼. 라벨은 키로 붙여 언어 전환을 따라간다.</summary>
        private static Button MenuButton(Transform canvas, string name, string locKey, int row, Color color)
        {
            const float spacing = 180f;
            Button button = EditorUiFactory.Button(
                canvas, name, new Vector2(0f, -260f - row * spacing), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(560f, 140f), color, locKey, 52f,
                out Image _, out TextMeshProUGUI label);

            Localize(label, locKey);

            // 가로에서는 간격을 좁혀 네 개가 다 들어오게 한다 (GDD §9.3)
            EditorUiFactory.Orient(button.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 120f - row * 120f), new Vector2(520f, 100f));
            return button;
        }

        private static void BuildConfirmPanel(
            Transform canvas, out GameObject panel, out TMP_Text text, out Button yes, out Button no)
        {
            Image backdrop = EditorUiFactory.Stretch(canvas, "ConfirmPanel", new Color(0f, 0f, 0f, 0.82f), raycast: true);
            panel = backdrop.gameObject;

            TextMeshProUGUI body = EditorUiFactory.Text(
                panel.transform, "Message", new Vector2(0f, 120f), new Vector2(0.5f, 0.5f), 46f,
                TextAlignmentOptions.Center, new Vector2(900f, 300f));
            body.text = "?";
            text = body;

            yes = EditorUiFactory.Button(
                panel.transform, "YesButton", new Vector2(-170f, -120f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(280f, 120f), AccentColor, "common.yes", 48f,
                out Image _, out TextMeshProUGUI yesLabel);
            Localize(yesLabel, "common.yes");

            no = EditorUiFactory.Button(
                panel.transform, "NoButton", new Vector2(170f, -120f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(280f, 120f), ButtonColor, "common.no", 48f,
                out Image _, out TextMeshProUGUI noLabel);
            Localize(noLabel, "common.no");
        }

        private static void Localize(TMP_Text label, string key) =>
            label.gameObject.AddComponent<LocalizedText>().SetKeyEditorOnly(key);

        /// <summary>카메라 + 이벤트시스템 + 캔버스 — 어느 씬이든 필요한 최소 구성.</summary>
        private static void CreateCommon(Scene scene, out Transform canvas)
        {
            var cameraGo = new GameObject("Main Camera", typeof(Camera));
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.orthographic = true;
            SceneManager.MoveGameObjectToScene(cameraGo, scene);

            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(eventSystemGo, scene);

            var canvasGo = new GameObject("Canvas");
            EditorUiFactory.SetupCanvas(canvasGo, sortingOrder: 0);
            SceneManager.MoveGameObjectToScene(canvasGo, scene);
            canvas = canvasGo.transform;

            // 배경판 — 아트가 생기면 이 이미지의 스프라이트만 갈아 끼우면 된다.
            Image background = EditorUiFactory.Stretch(canvas, "Background", Background, raycast: false);
            background.transform.SetAsFirstSibling();
        }

        /// <summary>빌드 설정을 흐름 순서로 재구성한다. 목록에 없던 씬은 뒤에 남겨 둔다.</summary>
        private static void RegisterBuildSettings()
        {
            string[] ordered =
            {
                $"{SceneDirectory}/{SceneRouter.Intro}.unity",
                $"{SceneDirectory}/{SceneRouter.Title}.unity",
                $"{SceneDirectory}/{SceneRouter.StageSelect}.unity",
                $"{SceneDirectory}/{SceneRouter.Main}.unity",
            };

            var scenes = new List<EditorBuildSettingsScene>();
            foreach (string path in ordered)
            {
                if (File.Exists(path))
                    scenes.Add(new EditorBuildSettingsScene(path, true));
                else
                    Debug.LogWarning($"[AppScenes] 빌드 설정에 넣을 씬을 찾지 못했습니다: {path}");
            }

            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (System.Array.IndexOf(ordered, existing.path) < 0)
                    scenes.Add(existing);
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
