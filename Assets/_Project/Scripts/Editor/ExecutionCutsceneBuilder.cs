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
    /// 전투 씬에 <b>처형(인살) 컷씬</b>을 얹는 툴. 위아래 검은 띠가 닫히고 그 사이 창에서
    /// 캐릭터가 보스를 다단으로 베는 연출(포켓몬 비전머신식 화면 분할).
    ///
    /// <para><b>비파괴</b>다 — <c>ExecutionCutsceneCanvas</c> 하나만 만들거나 다시 만들고
    /// 다른 화면은 절대 건드리지 않는다.</para>
    ///
    /// <para><b>페이즈 컷씬과 캔버스를 나눈 이유</b>: <c>Add Phase Cutscene To Main</c>은 자기 캔버스를
    /// 통째로 지우고 다시 만든다. 한 캔버스를 같이 쓰면 한쪽 메뉴를 다시 돌릴 때마다 다른 쪽이 사라진다.
    /// 두 컷씬은 동시에 뜨지도 않으므로(처형 → 전환 순서) 나눠 두는 편이 안전하다.</para>
    ///
    /// <para>정렬 순서 16 — 전투(10)·준비(15) 위, 페이즈 컷씬(17)·일시정지(18)·결과(20) 아래.</para>
    /// </summary>
    public static class ExecutionCutsceneBuilder
    {
        private const string CanvasName = "ExecutionCutsceneCanvas";

        private static readonly Color BandColor = new(0.02f, 0.02f, 0.03f, 1f);
        private static readonly Color WindowColor = new(0.06f, 0.05f, 0.07f, 1f);
        private static readonly Color TextColor = new(0.95f, 0.86f, 0.55f);

        [MenuItem("Tools/ChainRiposte/Add Execution Cutscene To Main")]
        private static void Build()
        {
            var screen = Object.FindFirstObjectByType<CombatScreen>();
            if (screen == null)
            {
                EditorUtility.DisplayDialog("처형 컷씬 추가",
                    "이 씬에서 CombatScreen 을 찾지 못했습니다. 전투 씬(Main)을 열고 실행하세요.", "확인");
                return;
            }

            var existing = GameObject.Find(CanvasName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);

            var canvasGo = new GameObject(CanvasName);
            Undo.RegisterCreatedObjectUndo(canvasGo, "Add Execution Cutscene");
            EditorUiFactory.SetupCanvas(canvasGo, sortingOrder: 16);

            ExecutionCutscene cutscene = BuildCutscene(canvasGo);

            var so = new SerializedObject(screen);
            so.FindProperty("executionCutscene").objectReferenceValue = cutscene;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(screen);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = canvasGo;

            Debug.Log("[ExecutionCutscene] 처형 컷씬을 얹고 CombatScreen 에 배선했습니다. " +
                      "캐릭터·보스 그림은 런타임에 채워지므로(고른 캐릭터 × 그 페이즈 보스) 비워 둡니다. " +
                      "창 높이·검기 굵기 같은 값은 Execution 오브젝트의 ExecutionCutscene 인스펙터에서 조절하세요.");
        }

        /// <summary>
        /// 컴포넌트는 <b>캔버스</b>에 붙인다. 연출 루트(<c>Execution</c>)는 평소 꺼져 있고
        /// 꺼진 오브젝트에서는 <c>Awake</c>가 돌지 않는다 — 거기 붙이면 제자리를 재는 초기화가
        /// 영영 실행되지 않아 처음 처형에서 사람들이 엉뚱한 자리에 선다.
        /// </summary>
        private static ExecutionCutscene BuildCutscene(GameObject canvasGo)
        {
            Transform canvas = canvasGo.transform;

            RectTransform root = EditorUiFactory.NewRect("Execution", canvas);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;

            // 그룹 하나로 통째로 페이드한다 — 조각마다 알파를 만지면 서로 어긋난다
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false; // 넘기기 버튼이 없으므로 입력을 가로챌 이유가 없다

            // ── 창이 먼저, 띠가 나중 ──
            // 형제 순서가 곧 그리는 순서다. 창을 먼저 두면 띠가 그 위에 덮여, 창 밖으로 삐져나온
            // 캐릭터·검기를 띠가 알아서 잘라 준다 (마스크를 따로 쓸 필요가 없다).
            RectTransform window = EditorUiFactory.NewRect("Window", root);
            window.anchorMin = new Vector2(0f, 0.5f);
            window.anchorMax = new Vector2(1f, 0.5f);
            window.pivot = new Vector2(0.5f, 0.5f);
            window.anchoredPosition = Vector2.zero;
            window.sizeDelta = new Vector2(0f, 360f); // 높이는 ExecutionCutscene.windowHeight 가 실제 기준

            Image windowBg = EditorUiFactory.Stretch(window, "WindowBg", WindowColor, raycast: false);
            windowBg.raycastTarget = false;

            Image boss = Figure(window, "Boss", new Vector2(260f, 0f));
            Image character = Figure(window, "Character", new Vector2(-280f, 0f));

            RectTransform slashLayer = EditorUiFactory.NewRect("SlashLayer", window);
            slashLayer.anchorMin = slashLayer.anchorMax = slashLayer.pivot = new Vector2(0.5f, 0.5f);
            slashLayer.anchoredPosition = Vector2.zero;
            slashLayer.sizeDelta = Vector2.zero; // 좌표 기준점일 뿐 — 크기는 의미 없다

            RectTransform slashRect = EditorUiFactory.NewRect("SlashTemplate", slashLayer);
            slashRect.anchorMin = slashRect.anchorMax = slashRect.pivot = new Vector2(0.5f, 0.5f);
            slashRect.sizeDelta = new Vector2(460f, 44f);
            var slash = slashRect.gameObject.AddComponent<Image>();
            // 양 끝이 뾰족하게 모이는 눈 모양. 사각형을 늘리면 끝이 일자로 뚝 끊겨 '막대'로 보인다.
            slash.sprite = PlaceholderSprite.Slash;
            slash.raycastTarget = false;
            slashRect.gameObject.SetActive(false); // 복제 원본

            RectTransform bandTop = Band(root, "BandTop", top: true);
            RectTransform bandBottom = Band(root, "BandBottom", top: false);

            Image flash = EditorUiFactory.Stretch(root, "Flash", Color.clear, raycast: false);
            flash.raycastTarget = false;

            TextMeshProUGUI line = EditorUiFactory.Text(
                root, "Line", new Vector2(0f, -240f), new Vector2(0.5f, 0.5f), 72f,
                TextAlignmentOptions.Center, new Vector2(1200f, 160f), FontStyles.Bold);
            line.color = TextColor;
            // LocalizedText 를 붙이지 않는다 — 문구를 코드가 채우므로 언어 전환 때 서로 덮어쓴다
            // (CLAUDE.md 「화면에 나오는 글씨는 전부 현지화」 참조). 키는 combat.execute 재사용.

            var cutscene = canvasGo.AddComponent<ExecutionCutscene>();
            var so = new SerializedObject(cutscene);
            so.FindProperty("root").objectReferenceValue = root;
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("bandTop").objectReferenceValue = bandTop;
            so.FindProperty("bandBottom").objectReferenceValue = bandBottom;
            so.FindProperty("window").objectReferenceValue = window;
            so.FindProperty("characterImage").objectReferenceValue = character;
            so.FindProperty("bossImage").objectReferenceValue = boss;
            so.FindProperty("slashLayer").objectReferenceValue = slashLayer;
            so.FindProperty("slashTemplate").objectReferenceValue = slash;
            so.FindProperty("flash").objectReferenceValue = flash;
            so.FindProperty("line").objectReferenceValue = line;
            so.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
            return cutscene;
        }

        /// <summary>
        /// 창 안에 서는 사람. 그림은 <b>런타임에</b> 꽂힌다(고른 캐릭터 × 그 페이즈 보스) —
        /// 여기서 채워 두면 조합이 씬에 굳어 버린다.
        /// </summary>
        private static Image Figure(Transform parent, string name, Vector2 position)
        {
            RectTransform rect = EditorUiFactory.NewRect(name, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(300f, 300f);
            var image = rect.gameObject.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = false; // 그림이 없으면 안 그린다 — ExecutionCutscene 이 켠다
            return image;
        }

        /// <summary>
        /// 화면 위(아래)에 붙어 안쪽으로 자라는 띠. 높이가 0에서 시작하므로 처음에는 안 보인다 —
        /// 닫히는 것 자체가 연출의 시작이다.
        /// </summary>
        private static RectTransform Band(Transform parent, string name, bool top)
        {
            float edge = top ? 1f : 0f;
            RectTransform rect = EditorUiFactory.NewRect(name, parent);
            rect.anchorMin = new Vector2(0f, edge);
            rect.anchorMax = new Vector2(1f, edge);
            rect.pivot = new Vector2(0.5f, edge);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 0f);

            var image = rect.gameObject.AddComponent<Image>();
            image.color = BandColor;
            image.raycastTarget = false;
            return rect;
        }
    }
}
