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
            SwapRequested?.Invoke(_pressedCell, target);
        }
    }
}
