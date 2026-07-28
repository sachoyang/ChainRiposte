using ChainRiposte.Game;
using ChainRiposte.Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// 퍼즐 보드가 놓일 <b>화면 영역</b>을 씬에 실물로 만들어 <see cref="CameraFit2D"/>에 꽂는 툴.
    ///
    /// <para>보드는 개수가 데이터로 정해진다(5×5든 10×10이든). 그래서 <b>크기는 카메라가 알아서 맞추지만
    /// 놓일 자리는 아무도 안 정해</b> 화면 전체 한가운데로 가고, 결과적으로 상단 HUD와 하단 바 밑에 깔린다.
    /// 이 사각형이 "여기까지가 보드 자리"를 못박는다 — 보드가 커져도 UI를 침범하지 않는다.</para>
    ///
    /// <para><b>비파괴</b>다 — <c>BoardArea</c> 하나만 만들거나 다시 만들고 다른 것은 건드리지 않는다.
    /// 만든 뒤에는 씬 뷰에서 위/아래를 드래그해 눈으로 맞추면 된다(Game 뷰에서 바로 반영된다).</para>
    /// </summary>
    public static class BoardAreaBuilder
    {
        private const string AreaName = "BoardArea";

        [MenuItem("Tools/ChainRiposte/Add Board Area To Main")]
        private static void Build()
        {
            var hud = Object.FindFirstObjectByType<PuzzleHud>(FindObjectsInactive.Include);
            if (hud == null)
            {
                EditorUtility.DisplayDialog("보드 영역 추가",
                    "이 씬에서 PuzzleHud 를 찾지 못했습니다. Main 씬을 열고 다시 실행하세요.", "확인");
                return;
            }

            Transform canvas = hud.transform.childCount > 0 ? hud.transform.GetChild(0) : hud.transform;

            Transform existing = canvas.Find(AreaName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing.gameObject);

            RectTransform area = EditorUiFactory.NewRect(AreaName, canvas);
            Undo.RegisterCreatedObjectUndo(area.gameObject, "Add Board Area");

            // 화면을 꽉 채우되 위/아래를 UI 띠만큼 들여놓는다. 정확한 값은 씬에서 맞추면 된다.
            // 늘어나는 앵커에서는 (anchoredPosition, sizeDelta)가 곧 들여쓰기다:
            //   sizeDelta = offsetMax − offsetMin,  anchoredPosition = (offsetMin + offsetMax) / 2
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.pivot = new Vector2(0.5f, 0.5f);
            area.offsetMin = new Vector2(0f, 340f);   // 하단 바 높이만큼
            area.offsetMax = new Vector2(0f, -300f);  // 상단 HUD 텍스트만큼

            // 가로 화면은 위아래가 짧다 — 띠도 얇으므로 들여쓰기를 줄인다 (아래 130 / 위 110).
            EditorUiFactory.Orient(area, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                new Vector2(0f, 10f), new Vector2(0f, -240f));

            var fit = Object.FindFirstObjectByType<CameraFit2D>(FindObjectsInactive.Include);
            if (fit != null)
            {
                var so = new SerializedObject(fit);
                so.FindProperty("viewportRect").objectReferenceValue = area;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(fit);
            }

            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
            Debug.Log(fit != null
                ? "[BoardAreaBuilder] BoardArea 를 만들고 CameraFit2D 에 꽂았습니다. " +
                  "씬에서 위/아래를 드래그해 보드 자리를 맞추세요."
                : "[BoardAreaBuilder] BoardArea 를 만들었지만 CameraFit2D 를 못 찾았습니다. " +
                  "카메라의 Camera Fit 2D ▸ Viewport Rect 에 직접 드래그하세요.", area);
        }
    }
}
