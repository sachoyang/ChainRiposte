using TMPro;
using UnityEngine;

namespace ChainRiposte.Game.Localization
{
    /// <summary>
    /// TMP 텍스트에 키를 물려 언어에 맞춰 스스로 갱신한다.
    /// 배치·폰트·크기는 씬에서 그대로 편집한다 — 이 컴포넌트는 문자열만 채운다.
    ///
    /// 구독은 <c>OnEnable</c>, 해제는 <c>OnDisable</c> 쌍. static 이벤트는 씬을 넘어 살아있으므로
    /// 해제하지 않으면 파괴된 오브젝트가 계속 호출되어 MissingReferenceException이 난다.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [DisallowMultipleComponent]
    public sealed class LocalizedText : MonoBehaviour
    {
        [Tooltip("Localization.csv 의 Key. 비우면 아무것도 하지 않는다(코드가 직접 채우는 텍스트).")]
        [SerializeField] private string key;

        private TMP_Text _text;

        /// <summary>런타임에 바꾸면 즉시 반영된다.</summary>
        public string Key
        {
            get => key;
            set
            {
                key = value;
                Apply();
            }
        }

        /// <summary>
        /// 문구를 <b>키로</b> 갈아 끼운다. 같은 TMP를 여러 문구가 돌려 쓸 때 쓴다
        /// (확인 대화창 하나를 「새 게임」·「진행도 초기화」·「지도로 나가기」가 나눠 쓰는 것처럼).
        ///
        /// <para><c>text</c>에 직접 넣으면 안 된다 — 붙어 있는 <see cref="LocalizedText"/>가
        /// <c>OnEnable</c>에서 제 키로 <b>덮어쓴다</b>. 패널을 켜는 순간이 그 시점이라
        /// "코드로 넣었는데 다른 문구가 뜬다"가 된다.</para>
        ///
        /// <para>컴포넌트가 없으면 문자열을 그대로 넣는다 — 프리팹마다 붙어 있고 없고가
        /// 다를 수 있으므로 부르는 쪽이 그것을 알 필요가 없게 한다.</para>
        /// </summary>
        public static void SetKey(TMP_Text text, string key)
        {
            if (text == null)
                return;

            if (text.TryGetComponent(out LocalizedText localized))
                localized.Key = key;
            else
                text.text = Loc.GetText(key);
        }

        private void Reset() => _text = GetComponent<TMP_Text>();

        private void Awake() => _text = GetComponent<TMP_Text>();

        private void OnEnable()
        {
            Loc.LanguageChanged += Apply;
            Apply(); // 구독 직후 즉시 1회 — 이벤트는 '변경 시점'에만 오므로 시작 시엔 아무도 안 채워준다
        }

        private void OnDisable() => Loc.LanguageChanged -= Apply;

        private void Apply()
        {
            if (_text == null)
                _text = GetComponent<TMP_Text>();
            if (_text == null || string.IsNullOrWhiteSpace(key))
                return;

            _text.text = Loc.GetText(key);
        }
    }
}
