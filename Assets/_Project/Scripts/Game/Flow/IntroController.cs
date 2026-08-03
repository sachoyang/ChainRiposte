using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ChainRiposte.Game.Flow
{
    /// <summary>
    /// 인트로 씬. 로고를 페이드인 → 유지 → 페이드아웃 하고 타이틀로 넘어간다.
    /// 아무 키/터치로 즉시 스킵.
    ///
    /// 로고 이미지는 <b>씬에 실물로 배치</b>하고 여기서는 참조만 받는다 — 아트 교체는 씬에서 드래그.
    /// </summary>
    public sealed class IntroController : MonoBehaviour
    {
        [Header("씬 참조 (빌더가 자동 배선)")]
        [Tooltip("로고 이미지. 스프라이트를 갈아 끼우면 그대로 반영된다.")]
        [SerializeField] private Image logo;
        [Tooltip("시작 로딩. 비워 두면 곧장 타이틀로 간다 — 인트로는 단독으로도 돌아야 한다.")]
        [SerializeField] private LoadingScreen loadingScreen;

        [Header("타이밍(초)")]
        [SerializeField, Min(0f)] private float fadeInSeconds = 0.8f;
        [SerializeField, Min(0f)] private float holdSeconds = 1.2f;
        [SerializeField, Min(0f)] private float fadeOutSeconds = 0.6f;
        [Tooltip("이 시간 안에는 스킵 입력을 무시한다 (씬 진입 클릭이 그대로 먹히는 것 방지)")]
        [SerializeField, Min(0f)] private float skipGuardSeconds = 0.25f;

        private bool _leaving;
        private float _elapsed;

        private void Start()
        {
            if (logo == null)
            {
                Debug.LogWarning($"{nameof(IntroController)}: 로고 참조가 비어 있어 바로 타이틀로 넘어갑니다.", this);
                Leave();
                return;
            }

            StartCoroutine(PlayRoutine());
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;
            if (_leaving || _elapsed < skipGuardSeconds)
                return;

            if (AnyPress())
                Leave();
        }

        private static bool AnyPress()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
                return true;
            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
                return true;
            return Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
        }

        private IEnumerator PlayRoutine()
        {
            yield return FadeLogo(0f, 1f, fadeInSeconds);
            yield return new WaitForSecondsRealtime(holdSeconds);
            yield return FadeLogo(1f, 0f, fadeOutSeconds);
            Leave();
        }

        private IEnumerator FadeLogo(float from, float to, float seconds)
        {
            SetLogoAlpha(from);
            for (float t = 0f; t < seconds && !_leaving; t += Time.unscaledDeltaTime)
            {
                // 선형으로 올리면 시작과 끝이 툭 끊겨 보인다 — 양 끝을 눕혀야 '떠오르고 잠기는' 느낌이 난다.
                SetLogoAlpha(Mathf.SmoothStep(from, to, t / seconds));
                yield return null;
            }

            if (!_leaving)
                SetLogoAlpha(to);
        }

        private void SetLogoAlpha(float alpha)
        {
            Color color = logo.color;
            color.a = alpha;
            logo.color = color;
        }

        private void Leave()
        {
            if (_leaving)
                return;

            _leaving = true;

            // 굽는 일이 남았으면 로딩 화면이 바를 띄우고 기다린 뒤 넘겨 준다.
            // 로고가 도는 동안 이미 끝났으면 그 자리에서 바로 넘어간다(대개 이쪽이다).
            if (loadingScreen != null)
                loadingScreen.LeaveWhenReady(SceneRouter.GoTitle);
            else
                SceneRouter.GoTitle();
        }
    }
}
