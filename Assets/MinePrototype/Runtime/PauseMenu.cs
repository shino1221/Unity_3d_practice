using MineSurvival;
using UnityEngine;
using UnityEngine.UI;

namespace MinePrototype
{
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] private GameObject panel; // 일시정지 중 표시할 전체 화면 메뉴
        [SerializeField] private Player player; // 조작과 커서를 전환할 플레이어
        [SerializeField] private InventoryUI inventoryUI; // 일시정지할 때 드래그를 취소할 인벤토리 UI
        [SerializeField] private Button resumeButton; // 메뉴를 열었을 때 선택할 게임 복귀 버튼
        private CapsulePlayerInput controls; // ESC와 일시정지 상태를 관리하는 팀 플레이어 입력

        private void OnEnable()
        {
            controls = player.GetComponent<CapsulePlayerInput>();
            controls.MenuChanged += ShowMenu;
        }

        private void OnDisable()
        {
            if (controls == null) return;
            controls.MenuChanged -= ShowMenu;
            controls.SetMenuOpen(false);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) controls?.SetMenuOpen(true);
        }

        private void ShowMenu(bool open)
        {
            if (open)
            {
                player.Inventory.Backpack?.CancelThrow();
                inventoryUI.CancelDrag();
            }
            panel.SetActive(open);
            if (open) resumeButton.Select();
        }

        public void Resume() => controls.SetMenuOpen(false);

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
