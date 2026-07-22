using TMPro;
using UnityEngine;

namespace ChainRiposte.Game.Localization
{
    /// <summary>
    /// TMP 텍스트에 붙여 키를 지정하면 언어에 맞춰 스스로 갱신한다.
    /// 씬에 실물로 배치한 텍스트에 컴포넌트만 얹는 방식이라 배치·폰트는 그대로 씬에서 편집한다.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [DisallowMultipleComponent]
    public sealed class LocalizedText : MonoBehaviour
    {
        [Tooltip("LocalizationTable의 키. 비우면 아무것도 하지 않는다(직접 세팅하는 텍스트).")]
        [SerializeField] private string key;

        private TMP_Text _text;

        private void Awake() => _text = GetComponent<TMP_Text>();

        private void OnEnable()
        {
            Loc.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => Loc.Changed -= Refresh;

        /// <summary>키를 코드에서 바꿀 때 (예: 상태에 따라 다른 문구).</summary>
        public void SetKey(string value)
        {
            key = value;
            Refresh();
        }

        private void Refresh()
        {
            if (_text == null || string.IsNullOrWhiteSpace(key))
                return;
            _text.text = Loc.Get(key);
        }

#if UNITY_EDITOR
        /// <summary>에디터 빌더 전용 — 생성 시 키를 주입한다.</summary>
        public void SetKeyEditorOnly(string value) => key = value;
#endif
    }
}
