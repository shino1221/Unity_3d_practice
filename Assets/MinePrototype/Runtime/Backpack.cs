using System;
using UnityEngine;

namespace MinePrototype
{
    public class Backpack : Item
    {
        [field: SerializeField, HideInInspector] public Item[] Items { get; private set; } = new Item[5];
        [field: SerializeField] public Transform Storage { get; private set; }
        private bool bound, restoreMouse, attemptedHold;
        private float heldSeconds;
        private Inventory Inventory => Player.Instance.Inventory;

        // 장착 중인 배낭만 Q 입력을 처리합니다. Input Action 구독과 GetKey를 중복 사용하지 않습니다.
        public void Bind(bool equipped)
        {
            bound = equipped;
            heldSeconds = 0;
            attemptedHold = false;
        }

        void Update() => AdvanceThrowInput(Input.GetKey(KeyCode.Q), Input.GetKeyUp(KeyCode.Q),
            Input.GetKeyDown(KeyCode.Mouse1), Time.deltaTime);

        // Q 2초 유지 → 앞으로 들기 → Q 해제 시 버리기. 우클릭 취소 후에는 Q를 다시 눌러야 합니다.
        public void AdvanceThrowInput(bool held, bool released, bool cancel, float seconds)
        {
            if (seconds < 0 || float.IsNaN(seconds) || float.IsInfinity(seconds))
                throw new ArgumentOutOfRangeException(nameof(seconds));
            if (!bound || Player.Instance == null) return;
            if (!Player.Instance.CanManageInventory || cancel)
            {
                CancelThrow();
                heldSeconds = 0;
                attemptedHold = held;
                return;
            }
            if (!held)
            {
                if (released && Inventory.HoldingBag)
                    Player.Instance.CaptureCursor(Inventory.DropBag() || !restoreMouse);
                heldSeconds = 0;
                attemptedHold = false;
                return;
            }
            if (attemptedHold) return;
            heldSeconds += seconds;
            if (heldSeconds < 2) return;
            attemptedHold = true;
            bool mouseWasFree = !Player.Instance.WorldControls;
            if (!Inventory.BeginBagHold()) return;
            restoreMouse = mouseWasFree;
            Player.Instance.CaptureCursor(true);
        }

        public void CancelThrow()
        {
            // 다른 창으로 전환해 Update가 멈춰도 이전 Q 홀드가 복귀 후 이어지지 않게 합니다.
            heldSeconds = 0;
            attemptedHold = true;
            if (Player.Instance == null || !Inventory.HoldingBag) return;
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
