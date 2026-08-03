using ChainRiposte.Game.Flow;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// 인트로 씬에 시작 로딩 바를 <b>얹는</b> 툴.
    ///
    /// <para><b>비파괴</b>다 — <c>LoadingCanvas</c> 하나만 만들거나 다시 만들고 로고·카메라는 안 건드린다.
    /// 자리만 깔아 주는 것이고 위치·크기·색은 이후 씬에서 드래그해 맞춘다.</para>
    ///
    /// <para>바는 <b>기본적으로 꺼져 있다</b>(<c>LoadingScreen</c>이 필요할 때만 켠다) —
    /// 씬 뷰에서 편집하려면 잠깐 켜고 보면 된다. 저장할 때 꺼진 상태로 두는 것이 정상이다.</para>
    /// </summary>
    public static class LoadingScreenBuilder
    {
        private const string CanvasName = "LoadingCanvas";
        private static readonly Color FillColor = new(0.78f, 0.62f, 0.28f, 1f);

        [MenuItem("Tools/ChainRiposte/Add Loading Screen To Intro")]
        private static void Build()
        {
            var intro = Object.FindFirstObjectByType<IntroController>();
            if (intro == null &&
                !EditorUtility.DisplayDialog("로딩 화면 추가",
                    "이 씬에서 IntroController 를 찾지 못했습니다. 인트로 씬이 맞나요?\n계속 진행할까요?",
                    "진행", "취소"))
                return;

            var existing = GameObject.Find(CanvasName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);

            var canvasGo = new GameObject(CanvasName);
            Undo.RegisterCreatedObjectUndo(canvasGo, "Add Loading Screen");
            // 로고(인트로 캔버스)보다 위. 인트로에는 다른 UI가 없어 낮은 번호로 충분하다.
            EditorUiFactory.SetupCanvas(canvasGo, sortingOrder: 5);

            var screen = canvasGo.AddComponent<LoadingScreen>();

            // ── 바 묶음 ── 화면 아래쪽. 로고는 가운데 있으므로 겹치지 않는다.
            var barRoot = new GameObject("BarRoot", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(barRoot, "Add Loading Screen");
            var rootRect = (RectTransform)barRoot.transform;
            rootRect.SetParent(canvasGo.transform, false);
            rootRect.anchorMin = rootRect.anchorMax = rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.anchoredPosition = new Vector2(0f, 220f);
            rootRect.sizeDelta = new Vector2(700f, 120f);

            Image fill = EditorUiFactory.Bar(
                rootRect, "Bar",
                pos: Vector2.zero, anchor: new Vector2(0.5f, 0.5f),
                size: new Vector2(640f, 28f), fillColor: FillColor);

            TextMeshProUGUI label = EditorUiFactory.Text(
                rootRect, "Label",
                pos: new Vector2(0f, 34f), anchor: new Vector2(0.5f, 0.5f),
                size: 28f, align: TextAlignmentOptions.Center, sizeDelta: new Vector2(640f, 40f));

            // 코드가 매번 채우는 문구라 LocalizedText 를 붙이지 않는다 —
            // 붙이면 언어 전환 때 둘이 서로 덮어쓴다. LoadingScreen 이 직접 Loc 를 부른다.
            label.text = string.Empty;

            Set(screen, "barRoot", barRoot);
            Set(screen, "fill", fill);
            Set(screen, "label", label);

            barRoot.SetActive(false);

            if (intro != null)
                Set(intro, "loadingScreen", screen);

            Selection.activeGameObject = canvasGo;
            EditorUtility.SetDirty(canvasGo);
            if (intro != null)
                EditorUtility.SetDirty(intro);

            Debug.Log("[LoadingScreenBuilder] LoadingCanvas 를 깔고 IntroController 에 배선했습니다. " +
                      "씬을 저장하세요.");
        }

        /// <summary>비공개 [SerializeField] 배선 — 빌더가 참조를 꽂는 공통 방식.</summary>
        private static void Set(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[LoadingScreenBuilder] '{fieldName}' 필드를 찾지 못했습니다.");
                return;
            }

            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
