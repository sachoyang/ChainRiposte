using ChainRiposte.Game;
using ChainRiposte.Game.Combat;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// 전투 씬에 <b>페이즈 전환 컷씬 + 인살 마크</b>를 얹는 툴 (2페이즈 보스용).
    ///
    /// <para><b>비파괴</b>다 — <c>PhaseCutsceneCanvas</c> 하나만 만들거나 다시 만들고
    /// 다른 화면은 절대 건드리지 않는다. <c>Build Main Scene UI</c> 처럼 자식을 갈아엎지 않으므로
    /// 손으로 꽂은 UI가 안 날아간다.</para>
    ///
    /// <para>정렬 순서 17 — 전투(10)·준비(15) 위, <b>일시정지(18) 아래</b>다.
    /// 컷씬 도중에 일시정지를 눌러도 메뉴가 컷씬에 가리면 안 된다.</para>
    /// </summary>
    public static class PhaseCutsceneBuilder
    {
        private const string CanvasName = "PhaseCutsceneCanvas";

        private static readonly Color DimColor = new(0.02f, 0.02f, 0.03f, 0.94f);
        private static readonly Color TextColor = new(0.92f, 0.90f, 0.85f);
        private static readonly Color MarkColor = new(0.90f, 0.25f, 0.28f);

        [MenuItem("Tools/ChainRiposte/Add Phase Cutscene To Main")]
        private static void Build()
        {
            var screen = Object.FindFirstObjectByType<CombatScreen>();
            if (screen == null)
            {
                EditorUtility.DisplayDialog("페이즈 컷씬 추가",
                    "이 씬에서 CombatScreen 을 찾지 못했습니다. 전투 씬(Main)을 열고 실행하세요.", "확인");
                return;
            }

            var existing = GameObject.Find(CanvasName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);

            var canvasGo = new GameObject(CanvasName);
            Undo.RegisterCreatedObjectUndo(canvasGo, "Add Phase Cutscene");
            EditorUiFactory.SetupCanvas(canvasGo, sortingOrder: 17);
            Transform canvas = canvasGo.transform;

            BuildCutscene(canvas, out RectTransform cutsceneRoot, out CanvasGroup group,
                out Image image, out TMP_Text text, out Button skip);

            BuildBossMarks(screen, out RectTransform markRoot, out Image markTemplate, out Image executeMark);

            var so = new SerializedObject(screen);
            so.FindProperty("deathblowMarkRoot").objectReferenceValue = markRoot;
            so.FindProperty("deathblowMarkTemplate").objectReferenceValue = markTemplate;
            so.FindProperty("executeMark").objectReferenceValue = executeMark;
            so.FindProperty("cutsceneRoot").objectReferenceValue = cutsceneRoot;
            so.FindProperty("cutsceneGroup").objectReferenceValue = group;
            so.FindProperty("cutsceneImage").objectReferenceValue = image;
            so.FindProperty("cutsceneText").objectReferenceValue = text;
            so.FindProperty("cutsceneSkipButton").objectReferenceValue = skip;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(screen);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = canvasGo;

            Debug.Log("[PhaseCutscene] 전환 컷씬 + 인살 게이지(보스 왼쪽 위) + 인살 대기 마크(보스 중앙)를 얹었습니다. " +
                      "인살 게이지는 페이즈가 여럿인 보스에서만 켜집니다(1페이즈 보스에서는 스스로 꺼짐). " +
                      "위치·크기는 씬에서 잡고, 실제 아트가 생기면 각 Image 의 스프라이트만 갈아 끼우세요.");
        }

        /// <summary>
        /// 보스에 붙는 두 가지 빨간 원 (세키로식).
        ///
        /// <para><b>인살 게이지</b>는 보스 <b>왼쪽 위</b>. 보스 몸의 자식이 아니라 <b>형제</b>다 —
        /// 보스는 왼쪽을 보려고 좌우로 뒤집혀 있어서, 자식으로 넣으면 게이지가 오른쪽으로 뒤집혀 나온다.</para>
        ///
        /// <para><b>인살 대기 마크</b>는 보스 몸의 <b>자식</b>. 보스가 어디에 있든 그 위에 떠야 하고,
        /// 원이라 좌우 반전은 눈에 안 띈다.</para>
        ///
        /// <para>둘 다 기존 자식은 안 지우고 <b>같은 이름의 것만</b> 갈아 끼운다.</para>
        /// </summary>
        private static void BuildBossMarks(
            CombatScreen screen, out RectTransform markRoot, out Image markTemplate, out Image executeMark)
        {
            markRoot = null;
            markTemplate = null;
            executeMark = null;

            var so = new SerializedObject(screen);
            var bossBody = so.FindProperty("bossBody").objectReferenceValue as RectTransform;
            if (bossBody == null)
            {
                Debug.LogWarning("[PhaseCutscene] CombatScreen 의 bossBody 가 비어 있어 인살 원을 얹지 못했습니다. " +
                                 "Build Main Scene UI 로 전투 화면을 만든 뒤 다시 실행하세요.");
                return;
            }

            markRoot = ReplaceChild("DeathblowMarks", bossBody.parent);
            markRoot.anchorMin = markRoot.anchorMax = markRoot.pivot = bossBody.pivot;
            // 보스 왼쪽 위. 크기를 반쯤 물어 두면 보스를 옮겨도 대체로 따라간다(정밀 배치는 씬에서).
            markRoot.anchoredPosition = bossBody.anchoredPosition
                + new Vector2(-bossBody.sizeDelta.x * 0.5f, bossBody.sizeDelta.y * 0.45f);
            markRoot.sizeDelta = new Vector2(200f, 48f);

            RectTransform markRect = EditorUiFactory.NewRect("MarkTemplate", markRoot);
            markRect.anchorMin = markRect.anchorMax = markRect.pivot = new Vector2(0f, 0.5f);
            markRect.anchoredPosition = Vector2.zero;
            markRect.sizeDelta = new Vector2(40f, 40f);
            markTemplate = markRect.gameObject.AddComponent<Image>();
            markTemplate.sprite = PlaceholderSprite.Annulus(0f); // 꽉 찬 원
            markTemplate.color = MarkColor;
            markTemplate.raycastTarget = false;
            markRect.gameObject.SetActive(false); // 복제 원본

            RectTransform executeRect = ReplaceChild("ExecuteMark", bossBody);
            executeRect.anchorMin = executeRect.anchorMax = executeRect.pivot = new Vector2(0.5f, 0.5f);
            executeRect.anchoredPosition = Vector2.zero;
            executeRect.sizeDelta = new Vector2(200f, 200f);
            executeMark = executeRect.gameObject.AddComponent<Image>();
            executeMark.sprite = PlaceholderSprite.Annulus(0f);
            executeMark.color = MarkColor;
            executeMark.raycastTarget = false;
            executeRect.gameObject.SetActive(false); // 켜고 끄는 건 CombatScreen 이 정한다
        }

        /// <summary>같은 이름의 자식이 있으면 그것만 지우고 새로 만든다 — 형제들은 건드리지 않는다.</summary>
        private static RectTransform ReplaceChild(string name, Transform parent)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing.gameObject);

            return EditorUiFactory.NewRect(name, parent);
        }

        private static void BuildCutscene(
            Transform canvas, out RectTransform root, out CanvasGroup group,
            out Image image, out TMP_Text text, out Button skip)
        {
            root = EditorUiFactory.NewRect("Cutscene", canvas);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;

            // 그룹 하나로 통째로 페이드한다 — 조각마다 알파를 만지면 서로 어긋난다
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            EditorUiFactory.Stretch(root, "Dim", DimColor, raycast: true);

            RectTransform imageRect = EditorUiFactory.NewRect("TransSprite", root);
            imageRect.anchorMin = imageRect.anchorMax = imageRect.pivot = new Vector2(0.5f, 0.5f);
            imageRect.anchoredPosition = new Vector2(0f, 90f);
            imageRect.sizeDelta = new Vector2(560f, 560f);
            image = imageRect.gameObject.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = false; // 그림이 없으면 안 그린다 — CombatScreen 이 켠다

            TextMeshProUGUI line = EditorUiFactory.Text(
                root, "Line", new Vector2(0f, -300f), new Vector2(0.5f, 0.5f), 46f,
                TextAlignmentOptions.Center, new Vector2(1000f, 200f));
            line.color = TextColor;
            line.textWrappingMode = TextWrappingModes.Normal;
            // LocalizedText 를 붙이지 않는다 — 문구를 보스마다 코드가 채우므로 서로 덮어쓴다
            text = line;

            // 아무 데나 눌러 넘기기. 투명한 전체 화면 버튼이라 모바일 탭도 그대로 먹는다.
            skip = EditorUiFactory.Button(
                root, "Skip", Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(4000f, 4000f), new Color(0f, 0f, 0f, 0f), string.Empty, 1f,
                out Image skipImage, out TextMeshProUGUI skipLabel);
            skipImage.raycastTarget = true;
            skipLabel.gameObject.SetActive(false);

            root.gameObject.SetActive(false);
        }
    }
}
