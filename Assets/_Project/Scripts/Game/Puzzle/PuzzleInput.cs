using System;
using ChainRiposte.Core.Board;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ChainRiposte.Game.Puzzle
{
    /// <summary>
    /// 포인터(마우스/터치) 드래그를 스왑 요청으로 변환하는 퍼즐 전용 입력.
    /// 규칙 검증은 하지 않는다 — 유효성은 PuzzleEngine.TrySwap이 판정.
    /// </summary>
    public sealed class PuzzleInput : MonoBehaviour
    {
        [SerializeField] private BoardView boardView;
        [Tooltip("스왑으로 인정할 드래그 거리 (셀 단위)")]
        [SerializeField, Min(0.1f)] private float dragThresholdCells = 0.35f;

        public event Action<GridPos, GridPos> SwapRequested;

        /// <summary>
        /// <b>「지금은 이 수만」</b> — 튜토리얼이 거는 체 (<c>Docs/TUTORIAL.md</c> §4.4).
        /// null이면 전부 허용(평소). false를 돌려주면 그 드래그는 <b>아무 일도 없이 씹힌다.</b>
        ///
        /// <para>여기서 막으므로 <b>엔진은 그 스왑을 구경도 못 한다</b> — 규칙 엔진이 튜토리얼이라는
        /// 개념을 알 필요가 없어진다. 그리고 되돌리기가 아니라 씹는 이유: 갔다가 되돌아오면
        /// <b>"틀렸다"</b>로 읽혀 뭘 잘못했는지 찾게 되는데, 튜토리얼에서는 애초에 틀릴 기회를
        /// 주면 안 된다. 씹히면 <b>"지금은 저기"</b>로 읽힌다.</para>
        ///
        /// <para>이 체와 보드의 밝은 칸은 <b>반드시 같은 목록</b>에서 나와야 한다 — 어긋나면
        /// "밝은데 안 눌리는 칸"이 생기고 그건 고장으로 읽힌다.</para>
        /// </summary>
        public Func<GridPos, GridPos, bool> SwapFilter { get; set; }

        private Camera _camera;
        private bool _active;
        private bool _dragging;
        private GridPos _pressedCell;
        private Vector3 _pressedWorld;

        /// <summary>연출 재생 중이거나 퍼즐 페이즈가 아닐 때 꺼둔다.</summary>
        public void SetActive(bool active)
        {
            _active = active;
            if (!active)
                _dragging = false;
        }

        private void Awake() => _camera = Camera.main;

        private void Update()
        {
            if (!_active)
                return;

            Pointer pointer = Pointer.current;
            if (pointer == null)
                return;

            Vector3 world = _camera.ScreenToWorldPoint(pointer.position.ReadValue());
            world.z = 0f;

            if (pointer.press.wasPressedThisFrame)
            {
                // HUD 버튼 위에서 시작한 드래그는 무시
                bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                _dragging = !overUi && boardView.TryWorldToGrid(world, out _pressedCell);
                _pressedWorld = world;
                return;
            }

            if (!_dragging)
                return;

            if (!pointer.press.isPressed)
            {
                _dragging = false;
                return;
            }

            Vector3 delta = world - _pressedWorld;
            if (delta.magnitude < dragThresholdCells)
                return;

            GridPos target = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? (delta.x > 0f ? _pressedCell.Right : _pressedCell.Left)
                : (delta.y > 0f ? _pressedCell.Up : _pressedCell.Down);

            _dragging = false;

            // 허용 목록 밖이면 여기서 끝 — 되돌아오는 연출도 없고 엔진도 모른다.
            if (SwapFilter != null && !SwapFilter(_pressedCell, target))
                return;

            SwapRequested?.Invoke(_pressedCell, target);
        }
    }
}
