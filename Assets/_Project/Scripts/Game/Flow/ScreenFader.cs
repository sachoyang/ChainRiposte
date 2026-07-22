using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ChainRiposte.Game.Flow
{
    /// <summary>
    /// 씬 전환용 검은 페이드 오버레이. 씬에 배치하지 않고 부팅 시 자동 생성되며 씬을 넘어 살아남는다
    /// (OrientationService와 같은 방식 — 연출 인프라라 씬 오서링 대상이 아니다).
    /// 색·시간 같은 값은 <see cref="SceneRouter"/>에서 조절한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScreenFader : MonoBehaviour
    {
        private static ScreenFader _instance;

        private CanvasGroup _group;
        private Coroutine _running;
        private bool _fadeInOnNextScene;

        public static ScreenFader Instance => _instance != null ? _instance : Create();

        /// <summary>도메인 리로드를 꺼둔 환경에서도 정적 참조가 남지 않도록 부팅 시 초기화한다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStatics() => _instance = null;

        private static ScreenFader Create()
        {
            var go = new GameObject("~ScreenFader");
            DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue; // 무엇보다 위에 덮인다

            var imageGo = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
            imageGo.transform.SetParent(go.transform, false);
            var rect = (RectTransform)imageGo.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            imageGo.GetComponent<Image>().color = Color.black;

            _instance = go.AddComponent<ScreenFader>();
            _instance._group = go.AddComponent<CanvasGroup>();
            _instance._group.alpha = 0f;
            _instance._group.blocksRaycasts = false;
            return _instance;
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        /// <summary>검게 덮은 뒤 씬을 바꾸고, 새 씬에서 다시 걷어낸다.</summary>
        public void LoadWithFade(string sceneName, float seconds)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                throw new ArgumentException("씬 이름이 비어 있습니다.", nameof(sceneName));

            Restart(LoadRoutine(sceneName, seconds));
        }

        /// <summary>연출용 단독 페이드 (씬 전환 없음). 완료 콜백은 선택.</summary>
        public void FadeTo(float targetAlpha, float seconds, Action onDone = null) =>
            Restart(FadeRoutine(targetAlpha, seconds, onDone));

        private void Restart(IEnumerator routine)
        {
            if (_running != null)
                StopCoroutine(_running);
            _running = StartCoroutine(routine);
        }

        private IEnumerator LoadRoutine(string sceneName, float seconds)
        {
            yield return FadeRoutine(1f, seconds, null);
            _fadeInOnNextScene = true;
            SceneManager.LoadScene(sceneName);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_fadeInOnNextScene)
                return;

            _fadeInOnNextScene = false;
            Restart(FadeRoutine(0f, SceneRouter.FadeSeconds, null));
        }

        private IEnumerator FadeRoutine(float targetAlpha, float seconds, Action onDone)
        {
            _group.blocksRaycasts = true; // 페이드 중 입력이 새어 들어가지 않게

            float start = _group.alpha;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                // 히트스톱(Time.timeScale) 중에도 전환이 멈추지 않도록 unscaled 시간을 쓴다
                _group.alpha = Mathf.Lerp(start, targetAlpha, t / seconds);
                yield return null;
            }

            _group.alpha = targetAlpha;
            _group.blocksRaycasts = targetAlpha > 0.01f;
            _running = null;
            onDone?.Invoke();
        }
    }
}
