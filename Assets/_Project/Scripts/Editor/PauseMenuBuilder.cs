using ChainRiposte.Game;
using ChainRiposte.Game.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// 전투 씬 우상단에 일시정지 / 설정 클러스터를 <b>얹는</b> 툴.
    ///
    /// <para><b>비파괴</b>다 — 다른 화면(HUD·전투·결과·준비)을 건드리지 않고 <c>PauseCanvas</c> 하나만
    /// 만들거나 다시 만든다. <c>Build Main Scene UI</c> 처럼 씬을 갈아엎지 않으므로 손으로 꽂은 UI가 안 날아간다.</para>
    ///
    /// 아이콘 스프라이트(일시정지/플레이/설정)는 <b>비워 둔다</b> — 사용자가 인스펙터에서 꽂는다.
    /// </summary>
    public static class PauseMenuBuilder
    {
        private const string CanvasName = "PauseCanvas";
        private static readonly Color Dim = new(0f, 0f, 0f, 0.72f);
        private static readonly Color PanelColor = new(0.08f, 0.07f, 0.11f, 0.98f);
        private static readonly Color ButtonColor = new(0.20f, 0.19f, 0.26f, 1f);
        private static readonly Color AccentColor = new(0.55f, 0.16f, 0.18f, 1f);
        private static readonly Color IconSlotColor = new(0.20f, 0.19f, 0.26f, 1f);

        [MenuItem("Tools/ChainRiposte/Add Pause Menu To Main")]
        private static void Build()
        {
            if (Object.FindFirstObjectByType<GameManager>() == null &&
                !EditorUtility.DisplayDialog("일시정지 메뉴 추가",
                    "이 씬에서 GameManager 를 찾지 못했습니다. 전투 씬(Main)이 맞나요?\n계속 진행할까요?",
                    "진행", "취소"))
                return;

            // 기존 PauseCanvas 는 그 하나만 지우고 새로 만든다 — 다른 화면은 절대 안 건드린다.
            var existing = GameObject.Find(CanvasName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);

            var canvasGo = new GameObject(CanvasName);
            Undo.RegisterCreatedObjectUndo(canvasGo, "Add Pause Menu");
            EditorUiFactory.SetupCanvas(canvasGo, sortingOrder: 18); // 퍼즐(0)·전투(10)·준비(15) 위, 결과(20) 아래
            Transform canvas = canvasGo.transform;

            var pause = canvasGo.AddComponent<PauseMenu>();

            BuildTopRight(canvas, out Button pauseButton, out Image pauseIcon, out Button settingsButton);
            BuildPausePanel(canvas, out GameObject pausePanel, out Button resumeButton, out Button quitButton,
                out GameObject quitConfirm, out Button quitYes, out Button quitNo);
            GameObject optionsPanel = AppScenesBuilder.BuildOptionsPanel(canvas);
            optionsPanel.SetActive(false);

            var so = new SerializedObject(pause);
            so.FindProperty("pauseButton").objectReferenceValue = pauseButton;
            so.FindProperty("pauseButtonIcon").objectReferenceValue = pauseIcon;
            so.FindProperty("settingsButton").objectReferenceValue = settingsButton;
            so.FindProperty("pausePanel").objectReferenceValue = pausePanel;
            so.FindProperty("resumeButton").objectReferenceValue = resumeButton;
            so.FindProperty("quitButton").objectReferenceValue = quitButton;
            so.FindProperty("optionsPanel").objectReferenceValue = optionsPanel;
            so.FindProperty("quitConfirmPanel").objectReferenceValue = quitConfirm;
            so.FindProperty("quitConfirmYes").objectReferenceValue = quitYes;
            so.FindProperty("quitConfirmNo").objectReferenceValue = quitNo;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(pause);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = canvasGo;
            Debug.Log("[PauseMenu] 우상단 일시정지/설정을 얹었습니다. 아이콘 스프라이트(일시정지·플레이·설정)는 " +
                      "PauseCanvas/TopRight 의 버튼 이미지와 PauseMenu 인스펙터(pauseSprite/playSprite)에 꽂으세요.");
        }

        /// <summary>모바일 관행대로 우상단. 아이콘 버튼이라 라벨은 비운다.</summary>
        private static void BuildTopRight(Transform canvas, out Button pauseButton, out Image pauseIcon, out Button settingsButton)
        {
            RectTransform group = EditorUiFactory.NewRect("TopRight", canvas);
            group.anchorMin = group.anchorMax = group.pivot = new Vector2(1f, 1f);
            group.anchoredPosition = new Vector2(-30f, -30f);
            group.sizeDelta = new Vector2(260f, 120f);

            // 일시정지(오른쪽 끝) — 아이콘이 play 로 토글된다
            pauseButton = IconButton(group, "PauseButton", new Vector2(-60f, -60f), out pauseIcon);
            // 설정(그 왼쪽)
            settingsButton = IconButton(group, "SettingsButton", new Vector2(-190f, -60f), out _);
            // 앵커가 우상단이라 세로·가로 모두 자동으로 우상단에 붙는다 — 방향 프리셋이 필요 없다.
        }

        private static Button IconButton(Transform parent, string name, Vector2 pos, out Image icon)
        {
            Button button = EditorUiFactory.Button(
                parent, name, pos, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(110f, 110f),
                IconSlotColor, string.Empty, 40f, out icon, out _);
            return button;
        }

        private static void BuildPausePanel(Transform canvas, out GameObject panel, out Button resume, out Button quit,
            out GameObject quitConfirm, out Button quitYes, out Button quitNo)
        {
            Image dim = EditorUiFactory.Stretch(canvas, "PausePanel", Dim, raycast: true); // 딤이 입력을 막는다
            panel = dim.gameObject;

            RectTransform box = EditorUiFactory.NewRect("Box", panel.transform);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
            box.anchoredPosition = Vector2.zero;
            box.sizeDelta = new Vector2(760f, 720f);
            var boxImg = box.gameObject.AddComponent<Image>();
            boxImg.sprite = EditorUiFactory.Square;
            boxImg.color = PanelColor;

            TMP_Text title = EditorUiFactory.Text(box, "Title", new Vector2(0f, 250f), new Vector2(0.5f, 0.5f), 76f,
                TextAlignmentOptions.Center, new Vector2(700f, 110f), FontStyles.Bold);
            EditorUiFactory.Localize(title, "pause.title");

            resume = LabelButton(box, "ResumeButton", new Vector2(0f, 90f), "pause.resume", AccentColor);
            quit = LabelButton(box, "QuitButton", new Vector2(0f, -70f), "pause.quit", ButtonColor);

            BuildQuitConfirm(panel.transform, out quitConfirm, out quitYes, out quitNo);
            panel.SetActive(false);
        }

        private static void BuildQuitConfirm(Transform parent, out GameObject confirm, out Button yes, out Button no)
        {
            Image dim = EditorUiFactory.Stretch(parent, "QuitConfirmPanel", Dim, raycast: true);
            confirm = dim.gameObject;

            RectTransform box = EditorUiFactory.NewRect("Box", confirm.transform);
            box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
            box.anchoredPosition = Vector2.zero;
            box.sizeDelta = new Vector2(820f, 480f);
            var boxImg = box.gameObject.AddComponent<Image>();
            boxImg.sprite = EditorUiFactory.Square;
            boxImg.color = PanelColor;

            TMP_Text text = EditorUiFactory.Text(box, "Text", new Vector2(0f, 110f), new Vector2(0.5f, 0.5f), 44f,
                TextAlignmentOptions.Center, new Vector2(740f, 200f));
            EditorUiFactory.Localize(text, "pause.quit.confirm");

            yes = LabelButton(box, "YesButton", new Vector2(-200f, -120f), "common.yes", AccentColor);
            no = LabelButton(box, "NoButton", new Vector2(200f, -120f), "common.no", ButtonColor);
            confirm.SetActive(false);
        }

        private static Button LabelButton(Transform parent, string name, Vector2 pos, string locKey, Color color)
        {
            Button button = EditorUiFactory.Button(
                parent, name, pos, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(360f, 120f),
                color, string.Empty, 48f, out _, out TextMeshProUGUI label);
            EditorUiFactory.Localize(label, locKey);
            return button;
        }
    }
}
