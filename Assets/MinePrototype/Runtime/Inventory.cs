using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MinePrototype
{
    public class Inventory : MonoBehaviour
    {
        [SerializeField] private Backpack backpack;
        [SerializeField] private Transform hand, storage, wornBag, heldBag, view; 
        // hand: 손 위치, storage: 미선택 소지품 위치, wornBag: 착용 배낭 위치, heldBag: 던지기 전 배낭 위치, view: 카메라 위치
        [SerializeField] private float throwDistance = 1.1f; // 아이템 버릴 때 카메라 앞 위치
        [SerializeField] private float throwSpeed = 2.5f; // 던지는 속도
        [SerializeField] private float pickupDistance = 2.8f; // pickup 가능 거리


        public Backpack Backpack => backpack; // inventoryui 에서 getter로 참조
        public event Action Changed; // 칸 내용, 선택 칸, 배낭 UI, 배낭 들기 등등 UI에 알리는 이벤트


        private Item[] pockets = new Item[3]; // 포켓 배열
        private Item target; // 획득 가능한 타겟 아이템


        private int bagCancelFrame = -1; // 배낭 취소 프레임 번호, 같은 프레임의 아이템 사용 차단에 사용
        public int Selected { get; private set; } // 손에 들 아이템의 기본 소지 칸 번호(0~2)


        public bool CanAct => Player.Instance != null && Player.Instance.WorldControls && !HoldingBag && !Dragging; 
        // 이동·시점 모드이고 배낭 들기·아이템 드래그 중이 아니면 true
        public bool CanUse => CanAct && Time.frameCount != bagCancelFrame; 
        // 기본 행동이 가능하고 배낭을 취소한 프레임이 아니면 true


        private bool wasOpen; // UI 열려있었는지 (UI 열린 상태에서 holding 할 경우를 대비했는데 삭제 고려 중)
        public bool IsOpen { get; private set; } // 배낭 UI가 열려 있는지
        public bool HoldingBag { get; private set; } // 던지기 전에 배낭을 카메라 앞으로 들고 있는지
        public bool Dragging { get; set; } // 인벤토리 아이템을 마우스로 끌고 있는지
        

        

        private void OnEnable()
        {
            Selected = 0;
            IsOpen = HoldingBag = Dragging = false;
            for (int i = 0; i < 3; i++) UpdateSlot(i); // i: 초기 배치를 복원할 기본 소지 칸 번호(0~2)
            if (backpack != null)
            {
                Place(backpack, wornBag, true);
                backpack.Bind(true);
                foreach (Item item in backpack.Items) Place(item, backpack.Storage, false); // item: 배낭 내부에 숨겨 보관할 아이템
            }
            Changed?.Invoke();
        }


        private void OnDisable()
        {
            ClearTarget();
            pockets[Selected]?.SetEquipped(false);
            if (backpack != null) backpack.Bind(false);
        }


        public void OnPickUp(InputAction.CallbackContext input)
        {
            if (!input.started || !CanAct) return;
            Add(target);
            ClearTarget();
        }

        public void OnSelect(InputAction.CallbackContext input)
        {
            if (!input.performed || !CanAct) return;
            float wheel = input.ReadValue<float>(); // 마우스 휠 회전량, 양수는 이전 칸·음수는 다음 칸
            if (wheel != 0) Select(wheel > 0 ? -1 : 1);
        }

        public void OnDrop(InputAction.CallbackContext input)
        {
            if (input.performed && CanAct) Drop(Selected);
        }

        public void UpdateTarget()
        {
            Item item = null; // 이번 조준 검사에서 찾을 아이템, 못 찾으면 null
            if (!HoldingBag && Physics.Raycast(view.position, view.forward, out RaycastHit hit, // hit: 카메라 전방 레이가 처음 맞힌 충돌체 정보
                pickupDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) hit.collider.TryGetComponent(out item);
            SetTarget(CanAdd(item) ? item : null);
        }

        public void ClearTarget() => SetTarget(null);
        private void SetTarget(Item item)
        {
            if (target == item) return;
            if (target != null) target.Highlight(false);
            target = item;
            if (target != null) target.Highlight(true);
        }

        private bool Valid(int slot) => slot >= 0 && slot < (backpack == null ? 3 : 8);
        public Item At(int slot) => !Valid(slot) ? null : slot < 3 ? pockets[slot] : backpack.Items[slot - 3];
        private void Put(int slot, Item item)
        {
            if (slot < 3) pockets[slot] = item;
            else backpack.Items[slot - 3] = item;
        }

        public bool CanAdd(Item item)
        {
            if (item == null || item.IsCarried || HoldingBag || Dragging) return false;
            return item is Backpack ? backpack == null : Array.IndexOf(pockets, null) >= 0
                || (backpack != null && Array.IndexOf(backpack.Items, null) >= 0);
        }

        public bool Add(Item item)
        {
            if (!CanAdd(item)) return false;
            if (item is Backpack bag) // bag: 획득할 아이템을 Backpack으로 확인한 배낭 참조
            {
                backpack = bag;
                bag.PickUp(wornBag);
                bag.Bind(true);
                foreach (Item content in bag.Items) Place(content, bag.Storage, false); // content: 주운 배낭 안에 보존되어 있던 아이템
            }
            else
            {
                int slot = pockets[Selected] == null ? Selected : Array.IndexOf(pockets, null); // 새 아이템을 넣을 칸 번호, 소지 칸이 꽉 찼으면 아래에서 배낭 칸 검사
                if (slot < 0) slot = 3 + Array.IndexOf(backpack.Items, null);
                item.PickUp(storage);
                Put(slot, item);
                UpdateSlot(slot);
            }
            // 새 아이템은 위에서 이미 배치했다. 기존 손의 아이템은 다시 배치하지 않는다.
            Changed?.Invoke();
            return true;
        }

        public void Select(int direction)
        {
            if (HoldingBag || Dragging) return;
            int previous = Selected; // 선택을 바꾸기 전 기본 소지 칸 번호
            Selected = (Selected + direction + 3) % 3;
            UpdateSlot(previous);
            UpdateSlot(Selected);
            Changed?.Invoke();
        }

        public void OnToggleInventory(InputAction.CallbackContext input)
        {
            if (input.performed) ToggleOpen();
        }

        public void ToggleOpen()
        {
            if (backpack == null || HoldingBag || Dragging) return;
            IsOpen = !IsOpen;
            Changed?.Invoke();
        }

        public bool CanMove(int from, int to) => !HoldingBag && Valid(to) && from != to && At(from) != null;
        public bool Move(int from, int to)
        {
            if (!CanMove(from, to)) return false;
            Item item = At(from); // 목적지 아이템과 교환할 출발 칸 아이템
            Put(from, At(to));
            Put(to, item);
            UpdateSlot(from);
            UpdateSlot(to);
            Changed?.Invoke();
            return true;
        }

        public bool CanDrop(Item item)
        {
            if (item == null) return false;
            
            Vector3 origin = view.position, direction = view.forward; // origin: 카메라 월드 위치, direction: 카메라 전방 벡터
            
            // 플레이어 기준으로 던질 수 있는지 계산
            // 발사 시작 위치, 구 반지름, 발사 방향, 충돌 정보 출력할 변수, 최대 거리, 레이어 마스크(어느 레이어까지 허용할지), 트리거 오브젝트 무시
            return !Physics.SphereCast(origin, item.Radius, direction, out _, throwDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            && !Physics.CheckSphere(origin + direction * throwDistance, item.Radius,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            // 구의 월드 좌표, 구의 반지름, 그 후 동일
            // 드롭 되는 아이템 기준으로도 한번 더 계산
            // 레이어 마스크의 경우 LayMask.GetMask()로 레이어들을 수동으로 추가해서 지정 할 수도 있음
        }

        public bool Drop(int slot)
        {
            Item item = At(slot); // 이 슬롯에서 꺼내 던질 아이템
            if (HoldingBag || !CanDrop(item)) return false;
            Put(slot, null);
            item.SetEquipped(false);
            item.Drop(view, throwDistance, throwSpeed);
            Changed?.Invoke();
            return true;
        }

        public bool BeginBagHold()
        {
            if (backpack == null || HoldingBag || Dragging) return false;
            wasOpen = IsOpen;
            IsOpen = false;
            HoldingBag = true;
            RefreshHand();
            return true;
        }

        public void CancelBagHold()
        {
            if (!HoldingBag) return;
            // 같은 우클릭으로 배낭 취소 직후 복구된 손전등까지 켜지는 것을 막는다.
            bagCancelFrame = Time.frameCount;
            HoldingBag = false;
            IsOpen = wasOpen;
            RefreshHand();
        }

        public bool DropBag()
        {
            if (!HoldingBag) return false;
            if (!CanDrop(backpack)) { CancelBagHold(); return false; }
            Backpack bag = backpack; // 인벤토리 연결을 끊은 뒤에도 던질 배낭 참조 보존
            bag.Bind(false);
            backpack = null;
            HoldingBag = IsOpen = false;
            bag.Drop(view, throwDistance, throwSpeed);
            RefreshHand();
            return true;
        }

        // 아이템 장착시 좌표 지정 (해당 아이템, 기준이 될 부모 좌표, 아이템 보이게 할지 말지)
        private static void Place(Item item, Transform parent, bool visible)
        {
            if (item == null) return;
            Transform itemTransform = item.transform; // 부모·로컬 위치·크기를 갱신할 아이템 Transform 캐시
            if (itemTransform.parent != parent)
            {
                itemTransform.SetParent(parent, false);
                itemTransform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                itemTransform.localScale = Vector3.one;
            }
            if (item.gameObject.activeSelf != visible) item.gameObject.SetActive(visible);
        }

        private void UpdateSlot(int slot)
        {
            Item item = At(slot); // 표시 위치와 사용 입력을 갱신할 슬롯 아이템
            if (item == null) return;
            bool equipped = slot == Selected && !HoldingBag; // 선택한 기본 소지 칸이며 배낭을 들고 있지 않으면 true
            Place(item, equipped ? hand : slot < 3 ? storage : backpack.Storage, equipped);
            item.SetEquipped(equipped);
        }

        private void RefreshHand()
        {
            UpdateSlot(Selected);
            if (backpack != null) Place(backpack, HoldingBag ? heldBag : wornBag, true);
            Changed?.Invoke();
        }
    }
}
