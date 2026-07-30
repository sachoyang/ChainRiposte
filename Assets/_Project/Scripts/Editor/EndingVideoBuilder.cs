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
    /// 전투 씬에 <b>엔딩 영상 자리</b>를 얹는 툴. 마지막 고리를 끊으면 고른 캐릭터의
    /// <c>PlayerCharacterSO ▸ Ending Video</c>가 여기서 재생된다.
    ///
    /// <para><b>영상 파일은 기획이 넣는다</b> — 이 메뉴가 만드는 것은 재생기와 화면, 넘기기 버튼뿐이다.
    /// 클립을 안 꽂은 캐릭터는 영상 없이 엔딩만 지나간다.</para>
    ///
    /// <para><b>비파괴</b>다 — <c>EndingVideoCanvas</c> 하나만 만들거나 다시 만든다.</para>
    ///
    /// <para>정렬 순서 <b>21</b> — 결과 화면(20)보다 위다. 엔딩은 결과 화면을 덮어야 한다.</para>
    /// </summary>
    public static class EndingVideoBuilder
    {
        private const string CanvasName = "EndingVideoCanvas";

        [MenuItem("Tools/ChainRiposte/Add Ending Video To Main")]
        private static void Build()
        {
            var result = Object.FindFirstObjectByType<ResultScreen>();
            if (result == null)
            {
                EditorUtility.DisplayDialog("엔딩 영상 추가",
                    "이 씬에서 ResultScreen 을 찾지 못했습니다. 전투 씬(Main)을 열고 실행하세요.", "확인");
                return;
            }

            var existing = GameObject.Find(CanvasName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);

            var canvasGo = new GameObject(CanvasName);
            Undo.RegisterCreatedObjectUndo(canvasGo, "Add Ending Video");
            EditorUiFactory.SetupCanvas(canvasGo, sortingOrder: 21);

            EndingVideoPlayer player = BuildPlayer(canvasGo);

            var so = new SerializedObject(result);
            so.FindProperty("endingVideo").objectReferenceValue = player;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(result);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = canvasGo;

            Debug.Log("[EndingVideo] 엔딩 영상 자리를 얹고 ResultScreen 에 배선했습니다. " +
                      "영상은 캐릭터 에셋(Character_*.asset)의 '엔딩 ▸ Ending Video' 에 꽂으세요. " +
                      "안드로이드는 H.264 mp4 만 확실히 재생됩니다. 클립을 안 꽂으면 영상 없이 엔딩만 지나갑니다.");
        }

        /// <summary>
        /// 재생기는 <b>캔버스</b>에 붙인다. 연출 루트는 평소 꺼져 있고 꺼진 오브젝트에서는
        /// <c>Awake</c>가 돌지 않아 넘기기 버튼이 배선되지 않는다.
        /// </summary>
        private static EndingVideoPlayer BuildPlayer(GameObject canvasGo)
        {
            RectTransform root = EditorUiFactory.NewRect("Ending", canvasGo.transform);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;

            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            // 영상 뒤는 완전한 검정 — 비율이 안 맞아 생기는 여백이 게임 화면이면 몰입이 깨진다
            EditorUiFactory.Stretch(root, "Backdrop", Color.black, raycast: true);

            // 영상 자리 = 비율을 지키는 틀 + 그 틀을 잘라 내는 마스크.
            // 마스크를 부모에 두는 이유: Unity UI 의 Mask 는 <b>자식</b>을 자른다 — 자기 자신은 못 자른다.
            RectTransform screenRect = EditorUiFactory.NewRect("Screen", root);
            screenRect.anchorMin = Vector2.zero;
            screenRect.anchorMax = Vector2.one;
            screenRect.offsetMin = screenRect.offsetMax = Vector2.zero;
            // 영상 비율을 지킨다 — 화면비가 제각각인 모바일에서 늘어나 보이면 안 된다
            var fitter = screenRect.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 16f / 9f;

            // 모서리를 자르는 그림. 실제 모양은 EndingVideoPlayer 가 클립 비율에 맞춰 런타임에 굽는다.
            var screenMask = screenRect.gameObject.AddComponent<Image>();
            screenMask.sprite = PlaceholderSprite.Square;
            screenMask.raycastTarget = false;
            var mask = screenRect.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false; // 마스크 그림 자체는 안 보인다 — 자르는 데만 쓴다

            RectTransform videoRect = EditorUiFactory.NewRect("Video", screenRect);
            videoRect.anchorMin = Vector2.zero;
            videoRect.anchorMax = Vector2.one;
            videoRect.offsetMin = videoRect.offsetMax = Vector2.zero;
            var screen = videoRect.gameObject.AddComponent<RawImage>();
            screen.raycastTarget = false;

            // 넘기기: 투명한 전체 화면 버튼이라 모바일 탭도 그대로 먹는다
            Button skip = EditorUiFactory.Button(
                root, "Skip", Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(4000f, 4000f), new Color(0f, 0f, 0f, 0f), string.Empty, 1f,
                out Image skipImage, out TextMeshProUGUI skipLabel);
            skipImage.raycastTarget = true;
            skipLabel.gameObject.SetActive(false);

            TextMeshProUGUI hint = EditorUiFactory.Text(
                root, "SkipHint", new Vector2(0f, 60f), new Vector2(0.5f, 0f), 30f,
                TextAlignmentOptions.Center, new Vector2(800f, 60f));
            hint.color = new Color(0.8f, 0.78f, 0.72f, 0.55f);
            EditorUiFactory.Localize(hint, "ending.skip");

            // 재생기와 소리는 캔버스에 둔다 — 루트가 꺼져 있어도 살아 있어야 한다
            var video = canvasGo.AddComponent<VideoPlayer>();
            video.playOnAwake = false;
            video.renderMode = VideoRenderMode.RenderTexture;
            video.audioOutputMode = VideoAudioOutputMode.AudioSource;
            var audioOut = canvasGo.AddComponent<AudioSource>();
            audioOut.playOnAwake = false;
            video.SetTargetAudioSource(0, audioOut);

            var player = canvasGo.AddComponent<EndingVideoPlayer>();
            var so = new SerializedObject(player);
            so.FindProperty("root").objectReferenceValue = root;
            so.FindProperty("screen").objectReferenceValue = screen;
            so.FindProperty("screenFitter").objectReferenceValue = fitter;
            so.FindProperty("screenMask").objectReferenceValue = screenMask;
            so.FindProperty("video").objectReferenceValue = video;
            so.FindProperty("audioOut").objectReferenceValue = audioOut;
            so.FindProperty("skipButton").objectReferenceValue = skip;
            so.FindProperty("group").objectReferenceValue = group;
            so.ApplyModifiedPropertiesWithoutUndo();

            root.gameObject.SetActive(false);
            return player;
        }
    }
}
