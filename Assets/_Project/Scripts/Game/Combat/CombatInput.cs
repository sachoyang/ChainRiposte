using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ChainRiposte.Game.Combat
{
    /// <summary>
    /// 2버튼 전투 입력 (GDD §5.1). 프로토타입: 키보드 ←=패링 / →=공격, 마우스 좌=패링 / 우=공격.
    /// 화면의 큰 버튼(CombatScreen)도 PressParry/PressAttack을 호출한다.
    /// 판정은 하지 않는다 — 유효성은 CombatSystem이 상태로 결정.
    /// </summary>
    public sealed class CombatInput : MonoBehaviour
    {
        public event Action ParryPressed;
        public event Action AttackPressed;

        private bool _active;

        /// <summary>전투 페이즈 동안만 켠다.</summary>
        public void SetActive(bool active) => _active = active;

        /// <summary>UI 버튼용 진입점.</summary>
        public void PressParry()
        {
            if (_active)
                ParryPressed?.Invoke();
        }

        /// <summary>UI 버튼용 진입점.</summary>
        public void PressAttack()
        {
            if (_active)
                AttackPressed?.Invoke();
        }

        private void Update()
        {
            if (!_active)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.wasPressedThisFrame)
                    ParryPressed?.Invoke();
                if (keyboard.rightArrowKey.wasPressedThisFrame)
                    AttackPressed?.Invoke();
            }

            // 마우스 — UI 버튼 위 클릭은 버튼 쪽에서 처리하므로 중복 발화를 막는다
            Mouse mouse = Mouse.current;
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (mouse != null && !overUi)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                    ParryPressed?.Invoke();
                if (mouse.rightButton.wasPressedThisFrame)
                    AttackPressed?.Invoke();
            }
        }
    }
}
