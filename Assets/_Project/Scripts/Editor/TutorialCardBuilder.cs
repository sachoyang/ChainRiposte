using ChainRiposte.Game;
using ChainRiposte.Game.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// 전투 씬에 <b>기믹 소개 카드 자리</b>를 얹는 툴 (<c>Docs/TUTORIAL.md</c> §3).
    /// 판이 시작되기 직전, 그 판의 <c>StageDataSO ▸ Introduces</c> 중 아직 안 본 것이 여기서 뜬다.
    ///
    /// <para><b>영상·그림은 기획이 넣는다</b> — 이 메뉴가 만드는 것은 카드 틀과 넘기기 버튼뿐이다.
    /// 항목 에셋(<c>TutorialTopicSO</c>)에 아무것도 안 꽂으면 글씨만으로 뜬다.</para>
    ///
    /// <para><b>비파괴</b>다 — <c>TutorialCanvas</c> 하나만 만들거나 다시 만든다.</para>
    ///
    /// <para>정렬 순서 <b>19</b> — 일시정지(18)보다 위, 결과 화면(20)보다 아래다.
    /// 카드가 떠 있는 동안 일시정지 버튼이 카드 위로 튀어나오면 안 된다.</para>
    /// </summary>
    public static class TutorialCardBuilder
    {
        private const string CanvasName = "TutorialCanvas";

        [MenuItem("Tools/ChainRiposte/Add Tutorial Card To Main")]
        private static void Build()
        {
            var manager = Object.FindFirstObjectByType<GameManager>();
            if (manager == null)
            {
                EditorUtility.DisplayDialog("소개 카드 추가",
                    "이 씬에서 GameManager 를 찾지 못했습니다. 전투 씬(Main)을 열고 실행하세요.", "확인");
                return;
            }

            var existing = GameObject.Find(CanvasName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);

            var canvasGo = new GameObject(CanvasName);
            Undo.RegisterCreatedObjectUndo(canvasGo, "Add Tutorial Card");
            EditorUiFactory.SetupCanvas(canvasGo, sortingOrder: 19);

            TutorialCard card = BuildCard(canvasGo);

            var so = new SerializedObject(manager);
            so.FindProperty("tutorialCard").objectReferenceValue = card;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = canvasGo;

            Debug.Log("[Tutorial] 소개 카드 자리를 얹고 GameManager 에 배선했습니다. " +
                      "무엇을 언제 띄울지는 스테이지 에셋(Stage_*.asset)의 '튜토리얼 ▸ Introduces' 가 정합니다. " +
                      "다시 보려면 Tools ▸ ChainRiposte ▸ Progress ▸ Reset Tutorial.");
        }

        /// <summary>
        /// 카드 본체. 컴포넌트와 <c>VideoPlayer</c>는 <b>캔버스</b>에 붙인다 — 카드 루트는 평소 꺼져 있고
        /// 꺼진 오브젝트에서는 <c>Awake</c>가 돌지 않아 넘기기 버튼이 배선되지 않는다.
        /// </summary>
        private static TutorialCard BuildCard(GameObject canvasGo)
        {
            RectTransform root = EditorUiFactory.NewRect("Card", canvasGo.transform);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            // 딤이 raycast 를 먹는다 — 카드가 떠 있는 동안 뒤의 보드·버튼을 누를 수 없어야 한다.
            EditorUiFactory.Stretch(root, "Dim", new Color(0f, 0f, 0f, 0.78f), raycast: true);

            RectTransform panel = EditorUiFactory.NewRect("Panel", root);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = new Vector2(900f, 1180f);
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.sprite = EditorUiFactory.PixelSprite("panel");
            panelImage.type = Image.Type.Sliced;
            panelImage.raycastTarget = true;
            // 가로 화면에서는 세로로 긴 카드가 화면 밖으로 나간다 (GDD §9.3)
            EditorUiFactory.Orient(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1340f, 840f));

            TextMeshProUGUI title = EditorUiFactory.Text(
                panel, "Title", new Vector2(0f, -60f), new Vector2(0.5f, 1f), 56f,
                TextAlignmentOptions.Center, new Vector2(820f, 90f), FontStyles.Bold);

            // 영상·그림 자리: 바깥 상자가 크기를 정하고, 안쪽이 비율을 지킨다.
            // 비율은 항목마다 다르므로 씬에 굳혀 둘 수 없다 — TutorialCard 가 띄울 때 실어 준다.
            RectTransform mediaBox = EditorUiFactory.NewRect("MediaBox", panel);
            mediaBox.anchorMin = mediaBox.anchorMax = mediaBox.pivot = new Vector2(0.5f, 1f);
            mediaBox.anchoredPosition = new Vector2(0f, -170f);
            mediaBox.sizeDelta = new Vector2(820f, 470f);

            RectTransform media = EditorUiFactory.NewRect("Media", mediaBox);
            media.anchorMin = Vector2.zero;
            media.anchorMax = Vector2.one;
            media.offsetMin = media.offsetMax = Vector2.zero;
            var fitter = media.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 16f / 9f;

            RectTransform videoRect = EditorUiFactory.NewRect("Video", media);
            videoRect.anchorMin = Vector2.zero;
            videoRect.anchorMax = Vector2.one;
            videoRect.offsetMin = videoRect.offsetMax = Vector2.zero;
            var videoScreen = videoRect.gameObject.AddComponent<RawImage>();
            videoScreen.raycastTarget = false;
            videoRect.gameObject.SetActive(false);

            RectTransform imageRect = EditorUiFactory.NewRect("Image", media);
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = imageRect.offsetMax = Vector2.zero;
            var imageView = imageRect.gameObject.AddComponent<Image>();
            imageView.preserveAspect = true;
            imageView.raycastTarget = false;
            imageRect.gameObject.SetActive(false);

            TextMeshProUGUI body = EditorUiFactory.Text(
                panel, "Body", new Vector2(0f, -680f), new Vector2(0.5f, 1f), 40f,
                TextAlignmentOptions.Top, new Vector2(820f, 260f));

            TextMeshProUGUI page = EditorUiFactory.Text(
                panel, "Page", new Vector2(0f, 210f), new Vector2(0.5f, 0f), 32f,
                TextAlignmentOptions.Center, new Vector2(400f, 50f));
            page.color = new Color(0.8f, 0.78f, 0.72f, 0.55f);

            Button next = EditorUiFactory.Button(
                panel, "NextButton", new Vector2(0f, 60f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(420f, 120f), Color.white, string.Empty, 46f,
                out Image _, out TextMeshProUGUI nextLabel);

            // 재생기는 캔버스에 둔다 — 루트가 꺼져 있어도 살아 있어야 한다.
            // 소리는 안 낸다: 카드는 읽는 화면이고, 퍼즐 BGM 위에 겹치면 방해다.
            var video = canvasGo.AddComponent<VideoPlayer>();
            video.playOnAwake = false;
            video.renderMode = VideoRenderMode.RenderTexture;
            video.audioOutputMode = VideoAudioOutputMode.None;
            video.isLooping = true;

            var card = canvasGo.AddComponent<TutorialCard>();
            var so = new SerializedObject(card);
            so.FindProperty("root").objectReferenceValue = root;
            so.FindProperty("titleLabel").objectReferenceValue = title;
            so.FindProperty("bodyLabel").objectReferenceValue = body;
            so.FindProperty("pageLabel").objectReferenceValue = page;
            so.FindProperty("mediaFrame").objectReferenceValue = mediaBox;
            so.FindProperty("mediaFitter").objectReferenceValue = fitter;
            so.FindProperty("videoScreen").objectReferenceValue = videoScreen;
            so.FindProperty("imageView").objectReferenceValue = imageView;
            so.FindProperty("video").objectReferenceValue = video;
            so.FindProperty("nextButton").objectReferenceValue = next;
            so.FindProperty("nextLabel").objectReferenceValue = nextLabel;
            so.FindProperty("group").objectReferenceValue = group;
            so.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
            return card;
        }
    }
}
