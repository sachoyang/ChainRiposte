using UnityEngine;
using UnityEngine.UI;

namespace ChainRiposte.Game.UI
{
    /// <summary>
    /// 화면 방향에 따라 이 오브젝트를 <b>보이거나 감춘다</b>. 세로와 가로에서 같은 자리를
    /// 다른 물건이 맡아야 할 때 쓴다 (예: 배경 — 세로는 상단 띠, 가로는 화면 전체).
    ///
    /// <para><see cref="GameObject.SetActive"/>가 아니라 <b>그리는 컴포넌트만</b> 끈다.
    /// 자기 자신을 비활성화하면 이 스크립트도 같이 멈춰서 다시 켤 방법이 없어지기 때문이다.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OrientationVisibility : MonoBehaviour
    {
        [SerializeField] private bool showInPortrait = true;
        [SerializeField] private bool showInLandscape = true;
        [Tooltip("같이 껐다 켤 것 (없어도 된다)")]
        [SerializeField] private Behaviour[] alsoToggle = System.Array.Empty<Behaviour>();

        private Renderer[] _renderers;
        private Graphic[] _graphics;

        private void OnEnable()
        {
            _renderers = GetComponents<Renderer>();
            _graphics = GetComponents<Graphic>();

            OrientationService.Changed += Apply;
            Apply(OrientationService.Current);
        }

        private void OnDisable() => OrientationService.Changed -= Apply;

        private void Apply(ScreenLayout layout)
        {
            bool visible = layout == ScreenLayout.Landscape ? showInLandscape : showInPortrait;

            foreach (Renderer renderer in _renderers)
                renderer.enabled = visible;
            foreach (Graphic graphic in _graphics)
                graphic.enabled = visible;
            foreach (Behaviour behaviour in alsoToggle)
            {
                if (behaviour != null)
                    behaviour.enabled = visible;
            }
        }
    }
}
