using UnityEngine;

namespace MinePrototype
{
    public class Flashlight : Item
    {
        [SerializeField] private Light beam;
        private bool equipped;
        public bool IsOn => beam != null && beam.enabled;

        public override void SetEquipped(bool value) => equipped = value;
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Mouse1)) TryToggle();
        }

        // 가방 취소와 같은 우클릭으로 전원까지 바뀌지 않도록 인벤토리의 취소 프레임도 확인합니다.
        public bool TryToggle()
        {
            var player = Player.Instance;
            if (!equipped || beam == null || player == null || !player.Inventory.CanUse ||
                !player.Controls.CanToggleFlashlight) return false;
            beam.enabled = !beam.enabled;
            return true;
        }
    }
}
