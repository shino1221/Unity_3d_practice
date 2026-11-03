using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace MinePrototype
{
    public class InventoryUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Inventory inventory; // 플레이어 인벤토리
        [SerializeField] private Button backpackButton; // 배낭을 장착했을 때 표시하는 배낭 UI
        [SerializeField] private GameObject backpackPanel, holdHint; 
        // backpackPanel: 배낭 5칸 UI, holdHint: 배낭을 들 때 표시할 던지기·취소 안내
        [SerializeField] private Image[] frames = new Image[8], icons = new Image[8]; 
        // frames: 슬롯 테두리, icons: 아이템 아이콘(0~2 소지 칸, 3~7 배낭 칸)
        [SerializeField] private Image dragBorder, dragIcon; 
        // dragBorder: 마우스를 따라가는 테두리, dragIcon: 드래그 아이템 아이콘
        [SerializeField] private Color normal = new(0.3f, 0.34f, 0.37f); // 테두리 기본색
        [SerializeField] private Color selected = new(0.95f, 0.73f, 0.3f); // 테두리 노란색
        private int source = -1; // 드래그 시작 슬롯 번호, -1이면 드래그 중 아님
        private int hover = -1; // 드래그 포인터가 가리키는 슬롯 번호, -1이면 슬롯 밖
        private PointerEventData pointer; // 현재 드래그 포인터의 화면 위치와 UI 레이캐스트 결과
        private Coroutine pulse; // 드래그 테두리 점멸을 중단할 때 쓸 코루틴 실행 참조



        

        private void OnEnable()
        {
            inventory.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            inventory.Changed -= Refresh;
            EndDrag();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && source >= 0) EndDrag();
        }

        public void OnMouseMode(InputAction.CallbackContext input)
        {
            if (!input.performed || inventory.HoldingBag || inventory.Dragging) return;
            if (inventory.IsOpen) inventory.ToggleOpen();
            else Player.Instance.CaptureCursor(!Player.Instance.WorldControls);
        }

        private void Refresh()
        {
            backpackButton.gameObject.SetActive(inventory.Backpack != null);
            backpackButton.interactable = !inventory.HoldingBag;
            backpackPanel.SetActive(inventory.IsOpen);
            holdHint.SetActive(inventory.HoldingBag);
            for (int i = 0; i < frames.Length; i++) // i: 갱신할 슬롯 번호(0~2 소지 칸, 3~7 배낭 칸)
            {
                Item item = inventory.At(i); // 이 슬롯에 들어 있는 아이템, 빈 칸이면 null
                icons[i].sprite = item == null ? null : item.Icon;
                icons[i].enabled = item != null;
                icons[i].color = new Color(1, 1, 1, i == source ? 0.3f : 1f);
                RestoreBorder(i);
            }
        }

        private int SlotAt(Vector2 position)
        {
            for (int i = 0; i < frames.Length; i++) // i: 마우스 위치와 겹치는지 검사할 슬롯 번호
                if (frames[i].gameObject.activeInHierarchy
                    && RectTransformUtility.RectangleContainsScreenPoint(frames[i].rectTransform, position)) return i;
            return -1;
        }

        // 포인터가 게임 오브젝트와 맞닿아 있지 않고 버릴 수 있는 상태라면 버림
        private bool CanDiscard() => pointer.pointerCurrentRaycast.gameObject == null && inventory.CanDrop(inventory.At(source));

        
        public void OnBeginDrag(PointerEventData data)
        {
            if (inventory.HoldingBag || Cursor.lockState == CursorLockMode.Locked) return;
            int slot = SlotAt(data.position); // 드래그를 시작한 화면 위치의 슬롯 번호, 슬롯 밖이면 -1
            Item item = inventory.At(slot); // 마우스로 끌어 표시할 아이템, 빈 칸이면 null


            if (item == null) return;
            source = slot;


            inventory.Dragging = true;
            dragIcon.sprite = item.Icon;
            dragBorder.gameObject.SetActive(true);
            dragBorder.transform.SetAsLastSibling();


            OnDrag(data);
            
            icons[source].color = new Color(1, 1, 1, 0.3f);
            pulse = StartCoroutine(Pulse());
        }

        public void OnDrag(PointerEventData data)
        {
            if (source < 0) return;
            pointer = data;
            dragBorder.rectTransform.position = data.position;
        }

        private IEnumerator Pulse()
        {
            // 드래그 중에만 실행하며, 게임이 일시정지되어도 점멸은 계속한다.
            while (source >= 0)
            {
                float amount = 0.4f + 0.6f * Mathf.PingPong(Time.unscaledTime * 3f, 1f); // 테두리 점멸 색상의 보간 비율(0.4~1)
                int target = SlotAt(pointer.position); // 현재 드래그 포인터가 가리키는 슬롯 번호, 슬롯 밖이면 -1
                
                
                if (hover != target) RestoreBorder(hover);
                hover = target;


                bool canMove = inventory.CanMove(source, target); // 출발 아이템을 이 목적지 슬롯으로 이동·교환할 수 있는지


                if (canMove) frames[target].color = Color.Lerp(normal, Color.yellow, amount);
                else RestoreBorder(target);


                dragBorder.color = canMove ? Color.yellow
                    : CanDiscard() ? Color.Lerp(normal, Color.red, amount) : normal;
                yield return null;
            }
        }

        public void OnEndDrag(PointerEventData data)
        {
            if (source < 0) return;
            pointer = data;
            int target = SlotAt(data.position); // 마우스를 놓은 위치의 슬롯 번호, 슬롯 밖이면 -1
            int from = source; // 드래그 상태를 초기화하기 전에 보존할 출발 슬롯 번호
            bool move = inventory.CanMove(from, target); // 놓은 위치로 아이템을 이동·교환할 수 있는지
            bool discard = !move && CanDiscard(); // 슬롯 이동은 불가하고 UI 밖으로 던질 수 있는지
            // 판정은 초기화 전에, UI 정리는 인벤토리 변경 알림 전에 처리한다.
            EndDrag();
            if (move) inventory.Move(from, target);
            else if (discard) inventory.Drop(from);
        }

        private void EndDrag()
        {
            if (pulse != null) StopCoroutine(pulse);
            pulse = null;
            RestoreBorder(hover);
            hover = -1;
            if (source >= 0) icons[source].color = Color.white;
            source = -1;
            inventory.Dragging = false;
            dragBorder.gameObject.SetActive(false);
        }

        private void RestoreBorder(int slot)
        {
            if (slot >= 0) frames[slot].color = slot == inventory.Selected ? selected : normal;
        }
    }
}
