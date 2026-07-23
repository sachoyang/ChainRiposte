using System.Collections.Generic;
using System.IO;
using ChainRiposte.Game.Flow;
using ChainRiposte.Game.Localization;
using ChainRiposte.Game.UI;
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
                "문구가 키로 보이면 Tools ▸ ChainRiposte ▸ Localization ▸ Sync From Google Sheet (또는 Create Starter CSV) 를 실행하세요.");
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

            BuildConfirmPanel(canvas, "ConfirmPanel", out GameObject confirmPanel, out TMP_Text confirmText,
                out Button yesButton, out Button noButton);
            GameObject optionsPanel = BuildOptionsPanel(canvas);
            CharacterSelectPanel characterSelect = BuildCharacterSelectPanel(canvas);

            var controllerGo = new GameObject("TitleController", typeof(TitleController));
            SceneManager.MoveGameObjectToScene(controllerGo, scene);
            var so = new SerializedObject(controllerGo.GetComponent<TitleController>());
            so.FindProperty("continueButton").objectReferenceValue = continueButton;
            so.FindProperty("newGameButton").objectReferenceValue = newGameButton;
            so.FindProperty("optionsButton").objectReferenceValue = optionsButton;
            so.FindProperty("quitButton").objectReferenceValue = quitButton;
            so.FindProperty("optionsPanel").objectReferenceValue = optionsPanel;
            so.FindProperty("confirmPanel").objectReferenceValue = confirmPanel;
            so.FindProperty("confirmText").objectReferenceValue = confirmText;
            so.FindProperty("confirmYesButton").objectReferenceValue = yesButton;
            so.FindProperty("confirmNoButton").objectReferenceValue = noButton;
            so.FindProperty("characterSelect").objectReferenceValue = characterSelect;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 새 게임에서 캐릭터를 고르는 화면. 카드 개수는 Resources/Characters 의 에셋 수로 정해지므로
        /// 여기서는 <b>템플릿 카드 하나</b>만 만들고 런타임에 복제한다 (옵션의 언어 버튼과 같은 방식).
        /// </summary>
        private static CharacterSelectPanel BuildCharacterSelectPanel(Transform canvas)
        {
            Image backdrop = EditorUiFactory.Stretch(canvas, "CharacterSelectPanel", new Color(0.05f, 0.04f, 0.07f, 0.97f), raycast: true);
            GameObject panel = backdrop.gameObject;
            var select = panel.AddComponent<CharacterSelectPanel>();

            // 제목은 패널이 Loc.GetText로 채운다 — LocalizedText를 붙이면 서로 덮어쓴다.
            TextMeshProUGUI title = EditorUiFactory.Text(
                panel.transform, "Title", new Vector2(0f, -160f), new Vector2(0.5f, 1f), 72f,
                TextAlignmentOptions.Center, new Vector2(1000f, 100f), FontStyles.Bold);
            title.text = "CHOOSE";

            RectTransform cardParent = EditorUiFactory.NewRect("Cards", panel.transform);
            cardParent.anchorMin = cardParent.anchorMax = cardParent.pivot = new Vector2(0.5f, 0.5f);
            cardParent.anchoredPosition = new Vector2(0f, 120f);
            cardParent.sizeDelta = new Vector2(980f, 620f);
            var layout = cardParent.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 40f;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            Button card = EditorUiFactory.Button(
                cardParent, "CardTemplate", Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(400f, 560f), ButtonColor, string.Empty, 44f, out Image _, out TextMeshProUGUI cardLabel);

            // 기본 라벨을 이름표로 삼는다 — 패널이 이름으로 찾으므로 "Name" 이어야 한다.
            cardLabel.gameObject.name = "Name";
            var nameRect = (RectTransform)cardLabel.transform;
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(1f, 0f);
            nameRect.pivot = new Vector2(0.5f, 0f);
            nameRect.offsetMin = new Vector2(0f, 30f);
            nameRect.offsetMax = new Vector2(0f, 120f);

            RectTransform portraitRect = EditorUiFactory.NewRect("Portrait", card.transform);
            portraitRect.anchorMin = portraitRect.anchorMax = portraitRect.pivot = new Vector2(0.5f, 0.5f);
            portraitRect.anchoredPosition = new Vector2(0f, 50f);
            portraitRect.sizeDelta = new Vector2(300f, 380f);
            var portrait = portraitRect.gameObject.AddComponent<Image>();
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;

            TextMeshProUGUI description = EditorUiFactory.Text(
                panel.transform, "Description", new Vector2(0f, 340f), new Vector2(0.5f, 0f), 38f,
                TextAlignmentOptions.Center, new Vector2(900f, 140f));

            Button confirm = EditorUiFactory.Button(
                panel.transform, "ConfirmButton", new Vector2(0f, 200f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0.5f), new Vector2(460f, 130f), AccentColor,
                "character.select.confirm", 52f, out Image _, out TextMeshProUGUI confirmLabel);
            Localize(confirmLabel, "character.select.confirm");

            Button back = EditorUiFactory.Button(
                panel.transform, "BackButton", new Vector2(0f, 70f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0.5f), new Vector2(360f, 110f), ButtonColor,
                "common.back", 44f, out Image _, out TextMeshProUGUI backLabel);
            Localize(backLabel, "common.back");

            var so = new SerializedObject(select);
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("cardParent").objectReferenceValue = cardParent;
            so.FindProperty("cardTemplate").objectReferenceValue = card;
            so.FindProperty("descriptionText").objectReferenceValue = description;
            so.FindProperty("confirmButton").objectReferenceValue = confirm;
            so.FindProperty("backButton").objectReferenceValue = back;
            so.ApplyModifiedPropertiesWithoutUndo();
            return select;
        }

        /// <summary>옵션 패널. 항목은 위에서부터 볼륨 2개 → 화면 방향 → 언어 → 진행도 초기화 순.</summary>
        private static GameObject BuildOptionsPanel(Transform canvas)
        {
            Image backdrop = EditorUiFactory.Stretch(canvas, "OptionsPanel", new Color(0.05f, 0.04f, 0.07f, 0.96f), raycast: true);
            GameObject panel = backdrop.gameObject;
            var options = panel.AddComponent<OptionsPanel>();

            TextMeshProUGUI title = EditorUiFactory.Text(
                panel.transform, "Title", new Vector2(0f, -80f), new Vector2(0.5f, 1f), 72f,
                TextAlignmentOptions.Center, new Vector2(900f, 100f), FontStyles.Bold);
            EditorUiFactory.Localize(title, "options.title");

            Slider bgm = LabeledSlider(panel.transform, "Bgm", "options.bgm", -260f);
            Slider sfx = LabeledSlider(panel.transform, "Sfx", "options.sfx", -400f);

            // 화면 방향 — 자동 / 세로 / 가로 순서가 OrientationMode enum 순서와 같아야 한다
            RowLabel(panel.transform, "OrientationLabel", "options.orientation", -540f);
            var orientationButtons = new[]
            {
                ChoiceButton(panel.transform, "OrientAuto", "options.orientation.auto", -300f, -620f),
                ChoiceButton(panel.transform, "OrientPortrait", "options.orientation.portrait", 0f, -620f),
                ChoiceButton(panel.transform, "OrientLandscape", "options.orientation.landscape", 300f, -620f),
            };

            // 언어 — 개수가 CSV 컬럼 수로 정해지므로 템플릿 하나를 두고 런타임에 복제한다
            RowLabel(panel.transform, "LanguageLabel", "options.language", -760f);
            RectTransform languageParent = EditorUiFactory.NewRect("LanguageButtons", panel.transform);
            languageParent.anchorMin = languageParent.anchorMax = languageParent.pivot = new Vector2(0.5f, 1f);
            languageParent.anchoredPosition = new Vector2(0f, -840f);
            languageParent.sizeDelta = new Vector2(900f, 120f);
            var layout = languageParent.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 20f;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            Button languageTemplate = EditorUiFactory.Button(
                languageParent, "LanguageTemplate", Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(260f, 100f), ButtonColor, "LANGUAGE", 42f, out Image _, out TextMeshProUGUI _);
            // 라벨은 OptionsPanel이 언어별로 직접 채운다 — LocalizedText를 붙이면 서로 덮어쓴다

            Button reset = EditorUiFactory.Button(
                panel.transform, "ResetProgressButton", new Vector2(0f, -1010f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(520f, 110f), new Color(0.35f, 0.12f, 0.14f, 1f),
                "options.reset", 42f, out Image _, out TextMeshProUGUI resetLabel);
            EditorUiFactory.Localize(resetLabel, "options.reset");

            Button close = EditorUiFactory.Button(
                panel.transform, "CloseButton", new Vector2(0f, 90f), new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0.5f), new Vector2(400f, 120f), ButtonColor,
                "common.back", 48f, out Image _, out TextMeshProUGUI closeLabel);
            EditorUiFactory.Localize(closeLabel, "common.back");

            BuildConfirmPanel(panel.transform, "OptionsConfirmPanel", out GameObject confirmPanel,
                out TMP_Text confirmText, out Button yes, out Button no);

            var so = new SerializedObject(options);
            so.FindProperty("bgmSlider").objectReferenceValue = bgm;
            so.FindProperty("sfxSlider").objectReferenceValue = sfx;
            so.FindProperty("languageButtonParent").objectReferenceValue = languageParent;
            so.FindProperty("languageButtonTemplate").objectReferenceValue = languageTemplate;
            so.FindProperty("resetProgressButton").objectReferenceValue = reset;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.FindProperty("confirmPanel").objectReferenceValue = confirmPanel;
            so.FindProperty("confirmText").objectReferenceValue = confirmText;
            so.FindProperty("confirmYesButton").objectReferenceValue = yes;
            so.FindProperty("confirmNoButton").objectReferenceValue = no;

            SerializedProperty array = so.FindProperty("orientationButtons");
            array.arraySize = orientationButtons.Length;
            for (int i = 0; i < orientationButtons.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = orientationButtons[i];

            so.ApplyModifiedPropertiesWithoutUndo();
            return panel;
        }

        private static void RowLabel(Transform parent, string name, string locKey, float y)
        {
            TextMeshProUGUI label = EditorUiFactory.Text(
                parent, name, new Vector2(0f, y), new Vector2(0.5f, 1f), 44f,
                TextAlignmentOptions.Center, new Vector2(900f, 70f), FontStyles.Bold);
            EditorUiFactory.Localize(label, locKey);
        }

        private static Slider LabeledSlider(Transform parent, string name, string locKey, float y)
        {
            RowLabel(parent, name + "Label", locKey, y);

            RectTransform rect = EditorUiFactory.NewRect(name + "Slider", parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, y - 70f);
            rect.sizeDelta = new Vector2(700f, 50f);

            Image background = EditorUiFactory.Stretch(rect, "Background", new Color(0.12f, 0.11f, 0.15f, 1f), raycast: true);

            RectTransform fillArea = EditorUiFactory.NewRect("Fill Area", rect);
            fillArea.anchorMin = new Vector2(0f, 0.25f);
            fillArea.anchorMax = new Vector2(1f, 0.75f);
            fillArea.offsetMin = fillArea.offsetMax = Vector2.zero;
            Image fill = EditorUiFactory.Stretch(fillArea, "Fill", new Color(0.55f, 0.16f, 0.18f, 1f), raycast: false);

            RectTransform handleArea = EditorUiFactory.NewRect("Handle Slide Area", rect);
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = handleArea.offsetMax = Vector2.zero;
            Image handle = EditorUiFactory.Stretch(handleArea, "Handle", new Color(0.92f, 0.90f, 0.85f, 1f), raycast: true);
            var handleRect = (RectTransform)handle.transform;
            handleRect.anchorMin = handleRect.anchorMax = new Vector2(0f, 0.5f);
            handleRect.sizeDelta = new Vector2(40f, 60f);

            var slider = rect.gameObject.AddComponent<Slider>();
            slider.targetGraphic = handle;
            slider.fillRect = (RectTransform)fill.transform;
            slider.handleRect = handleRect;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            return slider;
        }

        private static Button ChoiceButton(Transform parent, string name, string locKey, float x, float y)
        {
            Button button = EditorUiFactory.Button(
                parent, name, new Vector2(x, y), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(280f, 100f), ButtonColor, locKey, 40f, out Image _, out TextMeshProUGUI label);
            EditorUiFactory.Localize(label, locKey);
            return button;
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
            Transform canvas, string name, out GameObject panel, out TMP_Text text, out Button yes, out Button no)
        {
            Image backdrop = EditorUiFactory.Stretch(canvas, name, new Color(0f, 0f, 0f, 0.88f), raycast: true);
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
            label.gameObject.AddComponent<LocalizedText>().Key = key;

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
