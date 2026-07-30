using UnityEngine;
using UnityEngine.EventSystems;

namespace ChainRiposte.Game.UI
{
    /// <summary>
    /// 누르는 동안 버튼을 <b>몇 픽셀 아래로</b> 내려 "눌렸다"를 표현한다.
    ///
    /// <para>눌림 그림이 <b>없는</b> 버튼을 위한 것이다 — 전투의 패링·공격 버튼(<c>arrow_*</c>)은
    /// 시트에 <c>_pressed</c> 조각이 없어서 Sprite Swap 을 쓸 수 없다. 색만 어둡게 하면 손가락에 가려
    /// 안 보이므로 <b>움직임</b>으로 알린다.</para>
    ///
    /// <para>내려간 만큼은 <see cref="RectTransform.anchoredPosition"/>에 <b>더하지 않고</b> 기준 위치를 기억해
    /// 되돌린다 — 더하기만 하면 연타할 때마다 버튼이 화면 밖으로 걸어 내려간다.
    /// 방향 전환(<c>OrientationLayout</c>)이 위치를 다시 잡아도 그때의 값을 기준으로 새로 잡는다.</para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class PressOffset : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Tooltip("누르는 동안 내려갈 픽셀. UI 좌표라 기준 해상도(1080×1920) 기준이다.")]
        [SerializeField, Min(0f)] private float pressDownPixels = 6f;

        private RectTransform _rect;
        private Vector2 _restPosition;
        private bool _pressed;

        private void Awake() => _rect = (RectTransform)transform;

        private void OnEnable() => Restore();

        // 누른 채로 꺼지는 경우(공격 커밋으로 버튼이 잠기는 등)에도 제자리로 돌려놓는다.
        private void OnDisable() => Restore();

        public void OnPointerDown(PointerEventData eventData) => SetPressed(true);

        public void OnPointerUp(PointerEventData eventData) => SetPressed(false);

        /// <summary>
        /// 키보드·게임패드 입력에서도 같은 표현을 쓰고 싶을 때 부른다
        /// (마우스·터치는 위 두 핸들러가 알아서 부른다).
        /// </summary>
        public void SetPressed(bool pressed)
        {
            if (_pressed == pressed)
                return;

            if (pressed)
            {
                _restPosition = _rect.anchoredPosition;
                _rect.anchoredPosition = _restPosition - new Vector2(0f, pressDownPixels);
            }
            else
            {
                _rect.anchoredPosition = _restPosition;
            }

            _pressed = pressed;
        }

        private void Restore()
        {
            if (!_pressed)
                return;

            _rect.anchoredPosition = _restPosition;
            _pressed = false;
        }
    }
}
