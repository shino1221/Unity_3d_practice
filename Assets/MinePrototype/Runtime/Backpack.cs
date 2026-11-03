using UnityEngine;
using UnityEngine.InputSystem;

namespace MinePrototype
{
    public class Backpack : Item
    {
        [field: SerializeField, HideInInspector] public Item[] Items { get; private set; } = new Item[5]; // 배낭 내부 5칸
        [field: SerializeField] public Transform Storage { get; private set; } // 배낭 아이템을 비활성 상태로 보관할 자식 Transform
        [SerializeField] private InputActionReference dropAction, cancelAction; // dropAction: Q 길게 누르기·놓기, cancelAction: 우클릭 취소 액션 참조
      
      
        private Inventory Inventory => Player.Instance.Inventory; // 단일 플레이어의 인벤토리 참조
        private InputAction drop, cancel; // drop: 구독 중인 Q 액션, cancel: 구독 중인 취소 액션, 미장착 시 둘 다 null
      
      
        private bool restoreMouse; // 배낭 들기 전 마우스 조작 모드였는지, 취소 시 커서 복원에 사용



        public void Bind(bool equipped)
        {
            // 같은 장착 상태에서는 입력을 중복 구독하거나 해제하지 않는다.
            if (equipped == (drop != null)) return;
            if (equipped)
            {
                drop = dropAction.action;
                cancel = cancelAction.action;

                drop.performed += BeginThrow;
                drop.canceled += ReleaseThrow;

                cancel.performed += CancelThrow;
            }
            else
            {
                drop.performed -= BeginThrow;
                drop.canceled -= ReleaseThrow;

                cancel.performed -= CancelThrow;

                drop = cancel = null;
            }
        }

        private void BeginThrow(InputAction.CallbackContext input)
        {
            bool mouseWasFree = !Player.Instance.WorldControls; // 배낭을 들기 직전 마우스 조작 모드였는지
            if (!Inventory.BeginBagHold()) return;
            restoreMouse = mouseWasFree;
            Player.Instance.CaptureCursor(true);
        }

        private void ReleaseThrow(InputAction.CallbackContext input)
        {
            if (Inventory.HoldingBag) Player.Instance.CaptureCursor(Inventory.DropBag() || !restoreMouse);
        }

        private void CancelThrow(InputAction.CallbackContext input)
        {
            if (!Inventory.HoldingBag) return;
            Inventory.CancelBagHold();
            Player.Instance.CaptureCursor(!restoreMouse);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Bind(false);
        }
    }
}
