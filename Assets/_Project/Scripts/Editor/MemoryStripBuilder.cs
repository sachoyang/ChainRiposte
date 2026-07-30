using ChainRiposte.Game;
using ChainRiposte.Game.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// 삼킨 <b>보스의 기억</b> 아이콘 줄을 세 화면에 얹는 툴 —
    /// 퍼즐 HUD · 준비 화면 · 결과 화면(새로 얻은 것 강조).
    ///
    /// <para><b>비파괴</b>다 — 각 화면에서 <c>MemoryStrip</c>이라는 이름의 오브젝트만 만들거나
    /// 다시 만들고 나머지 자식은 건드리지 않는다. (<c>Build Main Scene UI</c>는 화면을 자식째
    /// 갈아엎어 손으로 꽂은 UI가 날아간다 — 그래서 전용 메뉴로 얹는다.)</para>
    ///
    /// <para>아이콘은 <b>원본 하나</b>(<c>IconTemplate</c>)만 깔아 둔다. 개수는 데이터로 정해지므로
    /// 런타임에 복제된다. 아트가 생기면 이 원본의 Image 스프라이트만 갈아 끼우면 전부 바뀐다.</para>
    /// </summary>
    public static class MemoryStripBuilder
    {
        private const string StripName = "MemoryStrip";

        [MenuItem("Tools/ChainRiposte/Add Memory Strip To Main")]
        private static void Build()
        {
            var hud = Object.FindFirstObjectByType<PuzzleHud>(FindObjectsInactive.Include);
            var intermission = Object.FindFirstObjectByType<IntermissionScreen>(FindObjectsInactive.Include);
            var result = Object.FindFirstObjectByType<ResultScreen>(FindObjectsInactive.Include);
            var manager = Object.FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);

            if (hud == null && intermission == null && result == null)
            {
                EditorUtility.DisplayDialog("기억 아이콘 줄 추가",
                    "이 씬에서 퍼즐 HUD·준비 화면·결과 화면을 하나도 찾지 못했습니다. Main 씬을 열고 다시 실행하세요.",
                    "확인");
                return;
            }

            int made = 0;

            // 퍼즐 HUD — 아래쪽은 하단 바(체력·보스 시계)가 쓰고 있으므로 위쪽 왼편에 얹는다.
            if (hud != null)
                made += Add(FirstChildOrSelf(hud.transform), manager, new Vector2(0f, 1f), new Vector2(24f, -150f),
                    highlightGained: false, withLabel: false) ? 1 : 0;

            // 준비 화면 — 딤에 가린 채 내 빌드를 확인하는 자리. 띠(Band) 안 왼쪽 위.
            if (intermission != null)
            {
                Transform band = FindDeep(intermission.transform, "Band") ?? FirstChildOrSelf(intermission.transform);
                made += Add(band, manager, new Vector2(0f, 1f), new Vector2(28f, -18f),
                    highlightGained: false, withLabel: false) ? 1 : 0;
            }

            // 결과 화면 — 방금 삼킨 기억만 밝게, 나머지는 어둡게. 제목과 버튼 사이가 비어 있다.
            if (result != null)
            {
                Transform panel = FindDeep(result.transform, "Root") ?? FirstChildOrSelf(result.transform);
                made += Add(panel, manager, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f),
                    highlightGained: true, withLabel: true) ? 1 : 0;
            }

            EditorSceneManager.MarkSceneDirty(
                (hud != null ? hud.gameObject : intermission != null ? intermission.gameObject : result.gameObject).scene);

            Debug.Log($"[MemoryStripBuilder] 기억 아이콘 줄 {made}개를 얹었습니다. " +
                      "위치·크기는 씬에서 조절하고, 아이콘 그림은 각 MemoryStrip ▸ IconTemplate 의 Image 에 꽂으세요.");
        }

        /// <summary>한 화면에 줄 하나 — 같은 이름의 옛것만 갈아 끼운다.</summary>
        private static bool Add(
            Transform parent, GameManager manager, Vector2 anchor, Vector2 position,
            bool highlightGained, bool withLabel)
        {
            if (parent == null)
                return false;

            Transform existing = parent.Find(StripName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing.gameObject);

            return CreateStrip(parent, manager, anchor, position, highlightGained, withLabel) != null;
        }

        /// <summary>
        /// 기억 아이콘 줄 하나를 만든다. 가로 정렬은 <see cref="HorizontalLayoutGroup"/>에 맡긴다 —
        /// 아이콘 수가 판마다 달라지므로 좌표를 코드가 계산하면 개수마다 어긋난다.
        ///
        /// <para><paramref name="manager"/>가 null이면(월드맵 등) 컴포넌트가 저장된 런을 직접 읽는다.</para>
        /// </summary>
        internal static MemoryStrip CreateStrip(
            Transform parent, GameManager manager, Vector2 anchor, Vector2 position,
            bool highlightGained, bool withLabel)
        {
            RectTransform strip = EditorUiFactory.NewRect(StripName, parent);
            Undo.RegisterCreatedObjectUndo(strip.gameObject, "Add Memory Strip");
            strip.anchorMin = anchor;
            strip.anchorMax = anchor;
            strip.pivot = new Vector2(anchor.x, anchor.y);
            strip.anchoredPosition = position;
            strip.sizeDelta = new Vector2(560f, 84f);

            RectTransform row = EditorUiFactory.NewRect("Icons", strip);
            row.anchorMin = Vector2.zero;
            row.anchorMax = Vector2.one;
            row.offsetMin = Vector2.zero;
            row.offsetMax = Vector2.zero;

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = anchor.x > 0.4f ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            // 원본 아이콘 — 스프라이트를 비워 두지 않고 사각형을 깔아 둔다. 기억 에셋에 아이콘을 안 꽂아도
            // 무언가 보여야 "내가 뭘 모았는지"를 셀 수 있다(칸이 비면 안 모은 것처럼 읽힌다).
            var iconGo = new GameObject("IconTemplate", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(row, false);
            var icon = iconGo.GetComponent<Image>();
            icon.sprite = EditorUiFactory.Square;
            icon.color = new Color(0.85f, 0.78f, 0.45f);
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.sizeDelta = new Vector2(64f, 64f);
            iconGo.AddComponent<LayoutElement>().preferredWidth = 64f;

            TMP_Text label = null;
            if (withLabel)
            {
                label = EditorUiFactory.Text(strip, "GainedLabel", new Vector2(0f, -84f),
                    new Vector2(0.5f, 0.5f), 40f, TextAlignmentOptions.Center, new Vector2(1100f, 120f));
                label.color = new Color(0.95f, 0.88f, 0.55f);
            }

            var script = strip.gameObject.AddComponent<MemoryStrip>();
            var so = new SerializedObject(script);
            so.FindProperty("gameManager").objectReferenceValue = manager;
            so.FindProperty("container").objectReferenceValue = row;
            so.FindProperty("iconTemplate").objectReferenceValue = icon;
            so.FindProperty("highlightGained").boolValue = highlightGained;
            if (label != null)
                so.FindProperty("gainedLabel").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();

            return script;
        }

        private static Transform FirstChildOrSelf(Transform root) =>
            root.childCount > 0 ? root.GetChild(0) : root;

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                    return child;
            }

            return null;
        }
    }
}
