using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChainRiposte.Game.Localization
{
    /// <summary>언어 하나에 대응하는 스프라이트. 인스펙터에서 한 줄로 보이도록 드로어가 붙는다.</summary>
    [Serializable]
    public struct LocalizedSprite
    {
        public SystemLanguage language;
        public Sprite sprite;
    }

    /// <summary>
    /// 언어별 이미지 교체 — 글자가 박힌 로고·타이틀 아트 등에 쓴다.
    /// 목록에 없는 언어면 fallback을 쓰고, 그것도 없으면 원래 스프라이트를 그대로 둔다.
    /// </summary>
    [RequireComponent(typeof(Image))]
    [DisallowMultipleComponent]
    public sealed class LocalizedImage : MonoBehaviour
    {
        [SerializeField] private List<LocalizedSprite> sprites = new();
        [Tooltip("목록에 없는 언어에서 쓸 기본 이미지")]
        [SerializeField] private Sprite fallback;

        private Image _image;

        private void Awake() => _image = GetComponent<Image>();

        private void OnEnable()
        {
            Loc.LanguageChanged += Apply;
            Apply();
        }

        private void OnDisable() => Loc.LanguageChanged -= Apply;

        private void Apply()
        {
            if (_image == null)
                _image = GetComponent<Image>();
            if (_image == null)
                return;

            SystemLanguage current = Loc.Language;
            foreach (LocalizedSprite entry in sprites)
            {
                if (entry.language != current || entry.sprite == null)
                    continue;
                _image.sprite = entry.sprite;
                return;
            }

            if (fallback != null)
                _image.sprite = fallback;
        }
    }
}
