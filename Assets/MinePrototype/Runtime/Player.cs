using MineSurvival;
using UnityEngine;

namespace MinePrototype
{
    // 팀 인벤토리의 Player.Instance 연결을 유지하는 접점입니다.
    // 실제 이동/시점/스태미나는 MineSurvival 컴포넌트만 처리하여 이중 이동·차감을 막습니다.
    [RequireComponent(typeof(Inventory), typeof(CapsuleMovement), typeof(CapsulePlayerInput))]
    public class Player : MonoBehaviour
    {
        public static Player Instance { get; private set; }
        public Inventory Inventory { get; private set; }
        public CapsulePlayerInput Controls { get; private set; }
        public CapsuleMovement Movement { get; private set; }
        public PlayerStamina Stamina => Movement.Stamina;
        public bool WorldControls => Controls.GameplayInputEnabled && Controls.CanUseTools;
        public bool CanManageInventory => Controls.HasFocus && !Controls.MenuOpen && !Controls.ActionInProgress && !Stamina.IsDead;

        void Awake()
        {
            Inventory = GetComponent<Inventory>();
            Controls = GetComponent<CapsulePlayerInput>();
            Movement = GetComponent<CapsuleMovement>();
        }

        void OnEnable()
        {
            if (Instance != null && Instance != this)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Inventory.Changed += InventoryChanged;
        }

        void Start() => Controls.SetInventoryOpen(Inventory.IsOpen);

        void OnDisable()
        {
            if (Instance != this) return;
            Inventory.Changed -= InventoryChanged;
            Inventory.ClearTarget();
            Instance = null;
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused && Inventory.Backpack != null) Inventory.Backpack.CancelThrow();
        }

        void InventoryChanged()
        {
            // 슬롯만 바뀔 때 Alt로 풀어 놓은 커서를 다시 잠그지 않습니다.
            if (Controls.InventoryOpen != Inventory.IsOpen) Controls.SetInventoryOpen(Inventory.IsOpen);
        }

        public void CaptureCursor(bool capture) => Controls.SetCursorCaptured(capture);
    }
}
