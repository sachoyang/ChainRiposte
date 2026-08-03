using System;
using System.Collections;
using ChainRiposte.Game.Localization;
using ChainRiposte.Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChainRiposte.Game.Flow
{
    /// <summary>
    /// 시작 로딩. <b>인트로 로고가 도는 동안 뒤에서 미리 굽고</b>, 로고가 끝났는데도 남아 있으면
    /// 그때만 바를 보여 준다.
    ///
    /// <para>그래서 대개는 <b>바가 안 뜨는 것이 정상</b>이다 — 로고 2.6초 안에 끝나면
    /// 플레이어는 로딩이 있었다는 사실도 모른다. 굽는 일을 로고 뒤에 세우면 그만큼
    /// 대기가 그대로 늘어나므로, 겹쳐 두는 것이 요점이다.</para>
    ///
    /// <para>진행률은 <b>실제 작업 단위</b>로 찬다. 시간으로 흘리면 기기가 느릴수록
    /// 바가 다 찬 채로 멈춰 있게 되고, 그건 멈춘 것처럼 보인다.</para>
    /// </summary>
    public sealed class LoadingScreen : MonoBehaviour
    {
        [Header("씬 참조 (빌더가 자동 배선)")]
        [Tooltip("바와 글씨를 묶은 뿌리. 필요할 때만 켜진다.")]
        [SerializeField] private GameObject barRoot;
        [Tooltip("차오르는 쪽 이미지 — Filled 로 강제된다")]
        [SerializeField] private Image fill;
        [Tooltip("지금 무엇을 하는지. 비워도 동작한다.")]
        [SerializeField] private TMP_Text label;

        [Header("연출")]
        [Tooltip("바가 튀지 않게 따라가는 속도(초당 비율). 0이면 즉시")]
        [SerializeField, Min(0f)] private float fillLerpPerSecond = 3f;

        private float _target;
        private float _shown;
        private string _labelKey;

        /// <summary>굽기가 끝났는가. 인트로가 이걸 보고 넘어간다.</summary>
        public bool Ready => Prewarmer.Done;

        private void Awake()
        {
            UiGauge.EnsureFilled(fill);

            // 시작은 숨긴 채로 — 인트로 로고를 가리면 안 된다.
            if (barRoot != null)
                barRoot.SetActive(false);

            if (fill != null)
                fill.fillAmount = 0f;
        }

        private void Start() => StartCoroutine(Prewarmer.Run(OnProgress));

        private void OnProgress(float progress, string labelKey)
        {
            _target = Mathf.Clamp01(progress);
            _labelKey = labelKey;
        }

        private void Update()
        {
            if (fill == null)
                return;

            _shown = fillLerpPerSecond <= 0f
                ? _target
                : Mathf.MoveTowards(_shown, _target, fillLerpPerSecond * Time.unscaledDeltaTime);

            fill.fillAmount = _shown;
        }

        /// <summary>
        /// 인트로가 끝났다. 굽기가 남았으면 바를 띄우고 기다린 뒤 넘어간다.
        /// <paramref name="onReady"/>는 <b>정확히 한 번</b> 불린다.
        /// </summary>
        public void LeaveWhenReady(Action onReady)
        {
            if (Ready)
            {
                onReady?.Invoke();
                return;
            }

            StartCoroutine(WaitRoutine(onReady));
        }

        private IEnumerator WaitRoutine(Action onReady)
        {
            if (barRoot != null)
                barRoot.SetActive(true);

            while (!Ready)
            {
                RefreshLabel();
                yield return null;
            }

            // 바가 90%에서 툭 끊기지 않게 끝까지 차는 것을 보여 준다.
            while (_shown < 0.999f)
            {
                RefreshLabel();
                yield return null;
            }

            onReady?.Invoke();
        }

        /// <summary>
        /// 매 프레임 다시 그린다 — 코드가 채우는 문구라 <see cref="LocalizedText"/>를 붙이면 안 된다
        /// (언어를 바꾸는 순간 서로 덮어쓴다). 로딩 중에는 언어가 안 바뀌지만 규칙은 같게 둔다.
        /// </summary>
        private void RefreshLabel()
        {
            if (label == null || string.IsNullOrEmpty(_labelKey))
                return;

            label.text = Loc.GetText(_labelKey);
        }
    }
}
