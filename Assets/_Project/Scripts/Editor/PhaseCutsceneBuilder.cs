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

            TMP_Text marks = BuildDeathblowMarks(canvas);
            BuildCutscene(canvas, out RectTransform cutsceneRoot, out CanvasGroup group,
                out Image image, out TMP_Text text, out Button skip);

            var so = new SerializedObject(screen);
            so.FindProperty("deathblowText").objectReferenceValue = marks;
            so.FindProperty("cutsceneRoot").objectReferenceValue = cutsceneRoot;
            so.FindProperty("cutsceneGroup").objectReferenceValue = group;
            so.FindProperty("cutsceneImage").objectReferenceValue = image;
            so.FindProperty("cutsceneText").objectReferenceValue = text;
            so.FindProperty("cutsceneSkipButton").objectReferenceValue = skip;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(screen);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = canvasGo;

            Debug.Log("[PhaseCutscene] 전환 컷씬과 인살 마크를 얹었습니다. " +
                      "인살 마크는 페이즈가 여럿인 보스에서만 켜집니다(1페이즈 보스에서는 스스로 꺼짐). " +
                      "위치는 씬에서 자유롭게 옮기세요.");
        }

        /// <summary>
        /// 남은 인살 횟수(◆◆). 보스 HP 바 아래에 두는 것을 기본으로 하되 <b>위치는 씬에서 잡는다</b> —
        /// HP 바 자리는 사용자가 옮겼을 수 있어서 코드가 아는 척하면 겹친다.
        /// </summary>
        private static TMP_Text BuildDeathblowMarks(Transform canvas)
        {
            TextMeshProUGUI marks = EditorUiFactory.Text(
                canvas, "DeathblowMarks", new Vector2(0f, -132f), new Vector2(0.5f, 1f), 46f,
                TextAlignmentOptions.Center, new Vector2(600f, 60f), FontStyles.Bold);
            marks.color = MarkColor;
            marks.text = "◆◆";

            // 문구가 아니라 기호라 현지화하지 않는다 — 마크 글자는 CombatScreen 인스펙터에서 바꾼다
            marks.gameObject.SetActive(false); // 켜고 끄는 건 CombatScreen 이 정한다
            return marks;
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
