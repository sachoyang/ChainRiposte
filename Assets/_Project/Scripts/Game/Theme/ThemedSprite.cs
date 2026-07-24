using ChainRiposte.Game.Characters;
using UnityEngine;
using UnityEngine.UI;

namespace ChainRiposte.Game.Theming
{
    /// <summary>
    /// 씬의 그림 한 장을 <b>현재 테마의 것</b>으로 갈아 끼운다. 현지화의 <c>LocalizedText</c>와 같은 물건이다 —
    /// 그림은 씬에 실물로 두고, 이 컴포넌트는 키 하나로 "어떤 자리인지"만 말한다.
    ///
    /// <para><see cref="Image"/>(UI)와 <see cref="SpriteRenderer"/>(월드) 둘 다 붙는다.</para>
    ///
    /// <para>테마에 그 키가 없으면 <b>씬에 꽂아둔 그림을 그대로 둔다.</b>
    /// 배선을 덜 했다고 화면이 비어 버리면 안 된다.</para>
    ///
    /// <para>어느 화면을 테마로 바꿀지는 코드가 아니라 <b>이 컴포넌트를 어디에 붙였는지</b>가 정한다.
    /// 인트로·타이틀처럼 컨셉과 무관한 화면에는 붙이지 않는다.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThemedSprite : MonoBehaviour
    {
        [Tooltip("테마의 배경 키 (map / puzzle / combat …). ThemeSO의 목록과 같아야 한다.")]
        [SerializeField] private string backgroundKey = ThemeSO.KeyMap;

        [Header("대상 (비우면 같은 오브젝트에서 찾는다)")]
        [SerializeField] private Image image;
        [SerializeField] private SpriteRenderer spriteRenderer;

        private void OnEnable()
        {
            if (image == null)
                image = GetComponent<Image>();
            if (image == null && spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            CharacterService.Changed += OnCharacterChanged;
            Apply();
        }

        private void OnDisable() => CharacterService.Changed -= OnCharacterChanged;

        private void OnCharacterChanged(PlayerCharacterSO character) => Apply();

        private void Apply()
        {
            Sprite sprite = ThemeService.GetBackground(backgroundKey);
            if (sprite == null)
                return; // 테마에 없는 자리 — 씬에 있는 그림을 살려 둔다

            if (image != null)
                image.sprite = sprite;
            else if (spriteRenderer != null)
                spriteRenderer.sprite = sprite;
        }
    }
}
