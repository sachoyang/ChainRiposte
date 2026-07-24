using System.Collections.Generic;
using ChainRiposte.Game;
using ChainRiposte.Game.Config;
using ChainRiposte.Game.Map;
using ChainRiposte.Game.Theming;
using ChainRiposte.Game.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// 월드맵(StageSelect)의 초기 레이아웃을 씬에 <b>실물 오브젝트</b>로 한 번 생성해 주는 에디터 툴.
    /// 생성 후에는 씬 뷰에서 노드/배경/캐릭터/패널을 자유롭게 드래그·교체하면 되고,
    /// StageSelectController는 이 오브젝트들을 참조만 한다(런타임에 아무것도 만들지 않음).
    ///
    /// 실행: 상단 메뉴 <c>Tools ▸ ChainRiposte ▸ Build StageSelect Layout</c>
    /// (StageSelect.unity 를 연 상태에서). 다시 실행하면 기존 생성물을 지우고 새로 깐다.
    /// </summary>
    public static class StageSelectSceneBuilder
    {
        // 초기 레이아웃 값 — 생성 후엔 씬에서 자유롭게 바꾸면 된다.
        private static readonly Vector2[] NodePositions =
        {
            new(-3.0f, -3.2f), new(-0.3f, -2.6f), new(2.6f, -2.0f), // 월드 1
            new(2.6f, 0.4f), new(-0.3f, 1.2f), new(-3.0f, 2.0f),    // 월드 2
        };

        private static readonly string[] StageAssetNames =
        {
            "Stage_1_1", "Stage_1_2", "Stage_1_3", "Stage_2_1", "Stage_2_2", "Stage_2_3",
        };

        private static readonly Color World1Color = new(0.22f, 0.34f, 0.24f);
        private static readonly Color World2Color = new(0.30f, 0.20f, 0.36f);

        private const int PathSort = 1, NodeSort = 2, CharSort = 3;

        [MenuItem("Tools/ChainRiposte/Build StageSelect Layout")]
        private static void Build()
        {
            StageSelectController controller = Object.FindFirstObjectByType<StageSelectController>();
            if (controller == null)
            {
                if (!EditorUtility.DisplayDialog(
                        "StageSelect 레이아웃 생성",
                        "씬에서 StageSelectController를 찾지 못했습니다.\n" +
                        "새 GameObject를 만들어 붙일까요? (StageSelect.unity를 연 상태여야 합니다)",
                        "만들고 진행", "취소"))
                    return;
                var go = new GameObject("StageSelect");
                controller = go.AddComponent<StageSelectController>();
                Undo.RegisterCreatedObjectUndo(go, "Create StageSelect");
            }

            Transform root = controller.transform;
            if (root.childCount > 0 &&
                !EditorUtility.DisplayDialog(
                    "StageSelect 레이아웃 생성",
                    $"'{root.name}'의 기존 자식 {root.childCount}개를 지우고 새로 생성합니다.\n계속할까요?",
                    "생성", "취소"))
                return;

            for (int i = root.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);

            Sprite square = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            StageDataSO[] stages = LoadStages();

            // ── 배경 2층 ── 그림은 테마(고른 캐릭터)가 채운다.
            // 하늘·원경은 화면을 덮고(BackgroundPanner), 길이 놓인 땅은 길에 맞춰 씬에서 배치한다.
            SpriteRenderer sky = CreateSprite(root, "SkyBackground", new Vector3(0f, 0f, 2f), Vector3.one, Color.white, -200, null);
            sky.gameObject.AddComponent<ThemedSprite>(); // 기본 키가 map
            sky.gameObject.AddComponent<BackgroundPanner>();

            SpriteRenderer ground = CreateSprite(root, "ThemedBackground", new Vector3(0f, 0f, 1f), Vector3.one, Color.white, -100, null);
            ground.gameObject.AddComponent<ThemedSprite>(); // 키는 Setup 메뉴가 path 로 바꾼다

            // ── 노드 + 라벨 ──
            var nodes = new MapNode[NodePositions.Length];
            for (int i = 0; i < NodePositions.Length; i++)
            {
                Color color = i < 3 ? World1Color : World2Color;
                Vector3 pos = new(NodePositions[i].x, NodePositions[i].y, 0f);
                SpriteRenderer sr = CreateSprite(root, $"Node_{DisplayName(i)}", pos, Vector3.one * 0.8f, color * 1.8f, NodeSort, square);

                var node = sr.gameObject.AddComponent<MapNode>();
                if (i < stages.Length && stages[i] != null)
                    node.SetStageEditorOnly(stages[i]);
                nodes[i] = node;

                CreateWorldLabel(sr.transform, DisplayName(i));

                // 잠금/클리어 배지 — 씬 오브젝트라 자물쇠·깃발 아트로 그대로 교체 가능
                GameObject lockedBadge = CreateBadge(sr.transform, "LockedBadge", "LOCK",
                    new Vector3(0f, 0f, -0.1f), new Color(0.85f, 0.83f, 0.80f));
                GameObject clearedBadge = CreateBadge(sr.transform, "ClearedBadge", "CLEAR",
                    new Vector3(0f, 0.95f, -0.1f), new Color(0.95f, 0.83f, 0.35f));
                node.SetBadgesEditorOnly(lockedBadge, clearedBadge);
            }

            // ── 경로선 (노드를 잇는 LineRenderer) ──
            LineRenderer pathLine = CreatePathLine(root, nodes);

            // ── 캐릭터 ──
            SpriteRenderer character = CreateSprite(root, "Character",
                new Vector3(NodePositions[0].x, NodePositions[0].y + 0.7f, 0f),
                new Vector3(0.5f, 0.6f, 1f), new Color(0.92f, 0.88f, 0.75f), CharSort, square);

            // ── 정보 패널 (Canvas + TMP) ──
            BuildUi(root, out GameObject infoPanel, out TMP_Text titleText, out TMP_Text infoText,
                out Button startButton, out Image bossPortrait);

            // ── 컨트롤러 참조 배선 ──
            CameraFit2D cameraFit = EnsureCameraFit();
            var so = new SerializedObject(controller);
            SetArray(so, "nodes", nodes);
            so.FindProperty("character").objectReferenceValue = character.transform;
            so.FindProperty("characterRenderer").objectReferenceValue = character;
            so.FindProperty("cameraFit").objectReferenceValue = cameraFit;
            so.FindProperty("pathLine").objectReferenceValue = pathLine;
            so.FindProperty("infoPanel").objectReferenceValue = infoPanel;
            so.FindProperty("titleText").objectReferenceValue = titleText;
            so.FindProperty("infoText").objectReferenceValue = infoText;
            so.FindProperty("startButton").objectReferenceValue = startButton;
            so.FindProperty("bossPortrait").objectReferenceValue = bossPortrait;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(controller);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            Selection.activeGameObject = controller.gameObject;

            Debug.Log("[StageSelectSceneBuilder] 레이아웃 생성 완료. 씬에서 노드/배경/UI를 자유롭게 편집하세요. " +
                      "세로 구성(상단 배경 띠 + 길 스크롤)은 여기서 만들지 않으므로 이어서 " +
                      "Tools ▸ ChainRiposte ▸ Theme ▸ Setup Background In Open Scene 을 실행하세요. " +
                      "(TMP가 처음이면 Window ▸ TextMeshPro ▸ Import TMP Essential Resources 필요)", controller);
        }

        private static StageDataSO[] LoadStages()
        {
            var result = new StageDataSO[StageAssetNames.Length];
            for (int i = 0; i < StageAssetNames.Length; i++)
            {
                string[] guids = AssetDatabase.FindAssets($"{StageAssetNames[i]} t:StageDataSO");
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (System.IO.Path.GetFileNameWithoutExtension(path) != StageAssetNames[i])
                        continue;
                    result[i] = AssetDatabase.LoadAssetAtPath<StageDataSO>(path);
                    break;
                }
                if (result[i] == null)
                    Debug.LogWarning($"[StageSelectSceneBuilder] '{StageAssetNames[i]}' 에셋을 찾지 못했습니다 — 노드에 직접 지정하세요.");
            }
            return result;
        }

        private static SpriteRenderer CreateSprite(Transform parent, string name, Vector3 position, Vector3 scale, Color color, int sortingOrder, Sprite sprite)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Build StageSelect");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = scale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static void CreateWorldLabel(Transform node, string label)
        {
            var go = new GameObject("Label");
            Undo.RegisterCreatedObjectUndo(go, "Build StageSelect");
            go.transform.SetParent(node, false);
            go.transform.localPosition = new Vector3(0f, -1.1f, 0f);
            go.transform.localScale = Vector3.one * 0.35f;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = label;
            tmp.fontSize = 6f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.92f, 0.90f, 0.85f);
        }

        /// <summary>노드 상태 배지(잠금/클리어). 기본은 꺼진 채로 두고 런타임에 MapNode가 켠다.</summary>
        private static GameObject CreateBadge(Transform node, string name, string text, Vector3 localPos, Color color)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Build StageSelect");
            go.transform.SetParent(node, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * 0.35f;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.fontSize = 5f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.fontStyle = FontStyles.Bold;
            go.SetActive(false);
            return go;
        }

        private static LineRenderer CreatePathLine(Transform parent, IReadOnlyList<MapNode> nodes)
        {
            var go = new GameObject("PathLine");
            Undo.RegisterCreatedObjectUndo(go, "Build StageSelect");
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = 0.12f;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.sortingOrder = PathSort;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = line.endColor = new Color(0.75f, 0.70f, 0.58f, 0.85f);
            line.positionCount = nodes.Count;
            for (int i = 0; i < nodes.Count; i++)
                line.SetPosition(i, nodes[i].Position);
            return line;
        }

        private static CameraFit2D EnsureCameraFit()
        {
            CameraFit2D fit = Object.FindFirstObjectByType<CameraFit2D>();
            if (fit != null)
                return fit;
            Camera cam = Camera.main;
            if (cam == null)
                return null;
            fit = Undo.AddComponent<CameraFit2D>(cam.gameObject);
            return fit;
        }

        // ── 정보 패널 UI (TMP) ──

        private static void BuildUi(
            Transform parent, out GameObject infoPanel, out TMP_Text titleText,
            out TMP_Text infoText, out Button startButton, out Image bossPortrait)
        {
            var canvasGo = new GameObject("MapCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Build StageSelect");
            canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<ChainRiposte.Game.UI.OrientationCanvas>(); // 방향별 기준 해상도 (GDD §9.3)

            infoPanel = new GameObject("InfoPanel", typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(infoPanel, "Build StageSelect");
            infoPanel.transform.SetParent(canvasGo.transform, false);
            var panelRect = (RectTransform)infoPanel.transform;
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(0f, 360f);
            infoPanel.GetComponent<Image>().color = new Color(0.10f, 0.09f, 0.13f, 0.92f);

            titleText = CreatePanelText(infoPanel.transform, "Title", new Vector2(50f, -30f), 64f, "STAGE 1-1");
            titleText.fontStyle = FontStyles.Bold;
            infoText = CreatePanelText(infoPanel.transform, "Info", new Vector2(50f, -120f), 42f, "WORLD 1 ...");

            // 보스 초상 — 스프라이트를 꽂기 전까지는 런타임에 꺼진 채로 있는다.
            // 아직 안 가본 스테이지에서는 컨트롤러가 검은 실루엣으로 칠한다.
            var portraitGo = new GameObject("BossPortrait", typeof(RectTransform), typeof(Image));
            Undo.RegisterCreatedObjectUndo(portraitGo, "Build StageSelect");
            portraitGo.transform.SetParent(infoPanel.transform, false);
            var portraitRect = (RectTransform)portraitGo.transform;
            portraitRect.anchorMin = portraitRect.anchorMax = new Vector2(1f, 0.5f);
            portraitRect.pivot = new Vector2(1f, 0.5f);
            portraitRect.anchoredPosition = new Vector2(-390f, 0f);
            portraitRect.sizeDelta = new Vector2(260f, 260f);
            bossPortrait = portraitGo.GetComponent<Image>();
            bossPortrait.preserveAspect = true;
            bossPortrait.raycastTarget = false;

            var buttonGo = new GameObject("StartButton", typeof(RectTransform), typeof(Image), typeof(Button));
            Undo.RegisterCreatedObjectUndo(buttonGo, "Build StageSelect");
            buttonGo.transform.SetParent(infoPanel.transform, false);
            var buttonRect = (RectTransform)buttonGo.transform;
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(1f, 0.5f);
            buttonRect.pivot = new Vector2(1f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(-50f, 0f);
            buttonRect.sizeDelta = new Vector2(300f, 200f);
            buttonGo.GetComponent<Image>().color = new Color(0.55f, 0.16f, 0.18f, 1f);
            startButton = buttonGo.GetComponent<Button>();

            // 가로에서는 하단 바 대신 오른쪽 컬럼으로 세우고, START는 컬럼 아래에 둔다 (GDD §9.3)
            EditorUiFactory.Orient(infoPanel.transform,
                new Vector2(0.62f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            EditorUiFactory.Orient(buttonGo.transform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(380f, 160f));
            EditorUiFactory.Orient(portraitGo.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(340f, 340f));

            TMP_Text label = CreatePanelText(buttonGo.transform, "Label", Vector2.zero, 56f, "START");
            EditorUiFactory.Localize(label, "map.start");
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            label.raycastTarget = false;
        }

        private static TMP_Text CreatePanelText(Transform parent, string name, Vector2 pos, float size, string text)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Build StageSelect");
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(700f, 160f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.color = new Color(0.92f, 0.90f, 0.85f);
            tmp.text = text;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void SetArray(SerializedObject so, string propName, Object[] values)
        {
            SerializedProperty prop = so.FindProperty(propName);
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static string DisplayName(int index) => $"{index / 3 + 1}-{index % 3 + 1}";
    }
}
