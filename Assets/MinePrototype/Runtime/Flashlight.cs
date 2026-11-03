using UnityEngine;
using UnityEngine.InputSystem;

namespace MinePrototype
{
    public class Flashlight : Item
    {
        [SerializeField] private InputActionReference useAction; // 손전등 전원을 전환하는 입력 액션
        [SerializeField] private Light beam; // 우클릭으로 켜고 끌 손전등 조명 컴포넌트
        private InputAction subscribedAction; // 장착 중 구독한 입력 액션, 미장착 시 null

        public override void SetEquipped(bool equipped)
        {
            InputAction action = equipped ? useAction != null ? useAction.action : null : null; // 장착 시 구독할 액션, 해제 시 null
            if (subscribedAction == action) return;

            if (subscribedAction != null) subscribedAction.performed -= ToggleBeam;
            subscribedAction = action;
            if (subscribedAction != null) subscribedAction.performed += ToggleBeam;
        }

        private void ToggleBeam(InputAction.CallbackContext input)
        {
            if (Player.Instance != null && Player.Instance.Inventory.CanUse)
                beam.enabled = !beam.enabled;
        }
    }
}
