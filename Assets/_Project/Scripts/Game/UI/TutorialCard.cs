using System.Collections;
using System.Collections.Generic;
using ChainRiposte.Game.Config;
using ChainRiposte.Game.Localization;
using ChainRiposte.Game.Tutorial;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace ChainRiposte.Game.UI
{
    /// <summary>
    /// <b>기믹 소개 카드</b> — 판이 시작되기 직전에 뜨는 안내 (<c>Docs/TUTORIAL.md</c> §3).
    ///
    /// <para>무엇을 띄울지는 <c>StageDataSO ▸ Introduces</c>가 정하고, 그중 <b>아직 안 본 것만</b>
    /// 고르는 것은 <see cref="TutorialService"/>가 한다. 이 컴포넌트는 <b>보여 주고 닫는 것</b>만 한다 —
    /// 여기에 스테이지 이름이나 기믹 종류가 한 글자도 없어야 기믹이 늘어도 이 코드가 안 는다.</para>
    ///
    /// <para><b>영상 → 그림 → 글씨만</b> 순으로 떨어진다(§3.3). 셋 다 비어도 카드는 뜬다.
    /// 영상은 <b>루프</b>다 — 카드는 사용자가 닫을 때까지 떠 있으므로 한 번 돌고 멈추면
    /// 늦게 읽는 사람은 아무것도 못 본다(엔딩 영상과 다른 점).</para>
    ///
    /// <para>씬 배치는 <c>Tools ▸ ChainRiposte ▸ Add Tutorial Card To Main</c>.</para>
    /// </summary>
    public sealed class TutorialCard : MonoBehaviour
    {
        [Header("씬 참조 (Add Tutorial Card To Main 이 배선)")]
        [Tooltip("카드가 떠 있는 동안만 켜지는 루트")]
        [SerializeField] private RectTransform root;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [Tooltip("여러 장일 때의 '1 / 2'. 한 장뿐이면 비운다.")]
        [SerializeField] private TMP_Text pageLabel;

        [Header("보여 줄 자리")]
        [Tooltip("영상·그림이 들어갈 틀. 둘 다 없는 항목에서는 통째로 꺼서 글씨가 자리를 넓게 쓴다.")]
        [SerializeField] private RectTransform mediaFrame;
        [Tooltip("영상·그림의 비율을 지키는 장치. 비율은 항목마다 다르므로 띄울 때 실어 준다.")]
        [SerializeField] private AspectRatioFitter mediaFitter;
        [SerializeField] private RawImage videoScreen;
        [SerializeField] private Image imageView;
        [SerializeField] private VideoPlayer video;

        [Header("버튼")]
        [SerializeField] private Button nextButton;
        [Tooltip("버튼 글씨. 마지막 장에서는 '시작'으로 바뀐다 — 코드가 채우므로 LocalizedText 를 붙이지 말 것.")]
        [SerializeField] private TMP_Text nextLabel;
        [SerializeField] private CanvasGroup group;

        [Header("연출")]
        [SerializeField, Min(0f)] private float fadeSeconds = 0.25f;
        [Tooltip("영상을 못 여는 경우(파일 손상·플랫폼 미지원)에도 이 시간이 지나면 글씨만으로 띄운다.")]
        [SerializeField, Min(0f)] private float prepareTimeoutSeconds = 3f;

        private bool _advance;
        private bool _frozen;
        private float _scaleBeforeFreeze = 1f;
        private RenderTexture _target;

        private void Awake()
        {
            if (root == null)
                return;

            if (group == null)
                group = root.GetComponent<CanvasGroup>();
            if (nextButton != null)
                nextButton.onClick.AddListener(() => _advance = true);

            root.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            // 멈춘 채로 씬을 넘어가면 다음 씬이 얼어붙는다 (세션 7에 겪은 것 — Docs/TUTORIAL.md §2.2)
            Unfreeze();
            ReleaseTarget();
        }

        private void OnDestroy() => ReleaseTarget();

        /// <summary>
        /// 아직 안 본 항목이 하나라도 있는가 — 호출한 쪽이 "카드가 뜰 판인지"를 미리 알고 싶을 때.
        /// </summary>
        public static bool HasPending(IReadOnlyList<TutorialTopicSO> topics) => Pending(topics).Count > 0;

        /// <summary>
        /// 안 본 항목들을 차례로 보여 준다. <b>이 코루틴이 끝나야</b> 호출한 쪽이 판을 시작한다.
        ///
        /// <para>큐가 비어 있으면(대부분의 판) 그 자리에서 끝난다 — 한 프레임도 안 먹는다.</para>
        /// </summary>
        public IEnumerator Show(IReadOnlyList<TutorialTopicSO> topics)
        {
            List<TutorialTopicSO> queue = Pending(topics);
            if (queue.Count == 0)
                yield break;

            if (root == null || nextButton == null)
            {
                // 배선이 없다고 「봤다」로 기록하면 안 된다 — 배선한 뒤에도 영영 안 뜬다.
                Debug.LogWarning($"{nameof(TutorialCard)}: 배선이 없어 소개 카드 {queue.Count}장을 건너뜁니다. " +
                    "Tools ▸ ChainRiposte ▸ Add Tutorial Card To Main 을 실행하세요.", this);
                yield break;
            }

            Freeze();
            root.gameObject.SetActive(true);
            if (group != null)
                group.alpha = 0f;

            for (int i = 0; i < queue.Count; i++)
            {
                yield return Draw(queue[i], i, queue.Count);

                _advance = false;
                while (!_advance)
                    yield return null;

                // 닫는 순간 기록한다 — 판을 깨야 기록하면, 죽고 다시 들어올 때마다 같은 카드를 또 읽는다.
                TutorialService.MarkSeen(queue[i].TopicId);
            }

            yield return Fade(1f, 0f);
            StopVideo();
            root.gameObject.SetActive(false);
            Unfreeze();
        }

        /// <summary>아직 안 본 것만. 순서는 스테이지에 적힌 순서 그대로다(사슬 → 폭탄).</summary>
        private static List<TutorialTopicSO> Pending(IReadOnlyList<TutorialTopicSO> topics)
        {
            var queue = new List<TutorialTopicSO>();
            if (topics == null)
                return queue;

            foreach (TutorialTopicSO topic in topics)
            {
                if (topic != null && !TutorialService.HasSeen(topic.TopicId) && !queue.Contains(topic))
                    queue.Add(topic);
            }

            return queue;
        }

        private IEnumerator Draw(TutorialTopicSO topic, int index, int total)
        {
            if (titleLabel != null)
                titleLabel.text = Text(topic.TitleKey);
            if (bodyLabel != null)
                bodyLabel.text = Text(topic.BodyKey);

            // 한 장뿐이면 쪽 번호가 정보가 아니라 소음이다.
            if (pageLabel != null)
                pageLabel.text = total > 1 ? Loc.GetText("tutorial.card.page", index + 1, total) : string.Empty;

            if (nextLabel != null)
                nextLabel.text = Loc.GetText(index == total - 1 ? "tutorial.card.start" : "tutorial.card.next");

            yield return DrawMedia(topic);

            if (index == 0)
                yield return Fade(0f, 1f);
        }

        /// <summary>영상 → 그림 → 아무것도 없음. 없는 것은 자리째 접는다.</summary>
        private IEnumerator DrawMedia(TutorialTopicSO topic)
        {
            StopVideo();

            if (topic.Clip != null && video != null && videoScreen != null)
            {
                if (imageView != null)
                    imageView.gameObject.SetActive(false);
                SetMedia(true, (float)topic.Clip.width / Mathf.Max(1u, topic.Clip.height));
                videoScreen.gameObject.SetActive(true);
                yield return PlayLooping(topic.Clip);
                yield break;
            }

            if (videoScreen != null)
                videoScreen.gameObject.SetActive(false);

            if (topic.Image != null && imageView != null)
            {
                Rect rect = topic.Image.rect;
                imageView.sprite = topic.Image;
                imageView.gameObject.SetActive(true);
                SetMedia(true, rect.width / Mathf.Max(1f, rect.height));
                yield break;
            }

            if (imageView != null)
                imageView.gameObject.SetActive(false);
            SetMedia(false, 1f);
        }

        private void SetMedia(bool visible, float aspect)
        {
            if (mediaFitter != null && visible)
                mediaFitter.aspectRatio = Mathf.Max(0.01f, aspect);
            if (mediaFrame != null)
                mediaFrame.gameObject.SetActive(visible);
        }

        /// <summary>
        /// 카드가 닫힐 때까지 <b>돌고 또 돈다</b>. 못 열면 그냥 글씨만 남는다 —
        /// 영상이 없다고 안내가 멈추면 안 된다.
        /// </summary>
        private IEnumerator PlayLooping(VideoClip clip)
        {
            PrepareTarget(clip);

            video.clip = clip;
            video.isLooping = true;
            video.playOnAwake = false;
            // 카드는 읽는 화면이다 — 소리가 나면 퍼즐 BGM 위에 겹친다.
            video.audioOutputMode = VideoAudioOutputMode.None;
            video.Prepare();

            float waited = 0f;
            while (!video.isPrepared)
            {
                if (prepareTimeoutSeconds > 0f && waited >= prepareTimeoutSeconds)
                {
                    Debug.LogWarning($"{nameof(TutorialCard)}: 소개 영상을 여는 데 실패했습니다. " +
                        "안드로이드에서는 H.264 mp4 만 확실히 재생됩니다.", this);
                    SetMedia(false, 1f);
                    yield break;
                }

                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            video.Play();
        }

        private void StopVideo()
        {
            if (video != null && video.clip != null)
            {
                video.Stop();
                video.clip = null;
            }

            ReleaseTarget();
        }

        private void PrepareTarget(VideoClip clip)
        {
            ReleaseTarget();

            int width = Mathf.Max(16, (int)clip.width);
            int height = Mathf.Max(16, (int)clip.height);
            _target = new RenderTexture(width, height, 0);

            video.renderMode = VideoRenderMode.RenderTexture;
            video.targetTexture = _target;
            videoScreen.texture = _target;
        }

        private void ReleaseTarget()
        {
            if (_target == null)
                return;

            if (video != null && video.targetTexture == _target)
                video.targetTexture = null;
            if (videoScreen != null && videoScreen.texture == _target)
                videoScreen.texture = null;

            _target.Release();
            Destroy(_target);
            _target = null;
        }

        /// <summary>
        /// 카드가 떠 있는 동안 판을 멈춘다 (<c>Docs/TUTORIAL.md</c> §2.2). 지금은 판이 시작되기
        /// <b>전</b>이라 멈출 것이 없지만, ②(유도형 튜토리얼)가 플레이 도중에 이 카드를 쓰게 되므로
        /// 멈추는 책임을 처음부터 카드가 진다.
        /// </summary>
        private void Freeze()
        {
            if (_frozen)
                return;

            _scaleBeforeFreeze = Time.timeScale;
            Time.timeScale = 0f;
            _frozen = true;
        }

        private void Unfreeze()
        {
            if (!_frozen)
                return;

            Time.timeScale = _scaleBeforeFreeze;
            _frozen = false;
        }

        private IEnumerator Fade(float from, float to)
        {
            if (group == null || fadeSeconds <= 0f)
            {
                if (group != null)
                    group.alpha = to;
                yield break;
            }

            // 멈춘 시간을 기다리면 영영 안 끝난다 — 카드 자신의 연출은 언제나 unscaled 다.
            for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / fadeSeconds));
                yield return null;
            }

            group.alpha = to;
        }

        // 키를 안 적은 항목도 있을 수 있다 — 그때는 그 줄이 조용히 빈다.
        private static string Text(string key) =>
            string.IsNullOrWhiteSpace(key) ? string.Empty : Loc.GetText(key);
    }
}
