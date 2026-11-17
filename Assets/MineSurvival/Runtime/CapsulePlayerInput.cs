using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace MineSurvival
{
    // [역할] 키보드/마우스 입력을 읽어서 이동·시점·손전등 담당 코드에 전달하는 입구입니다.
    // 읽는 순서: 이 파일의 Update → CapsuleMovement(몸 이동) / FirstPersonLook(시점) / FlashlightInteraction(손전등).
    // 키를 바꾸거나 UI 사용 중 입력을 제한하려면 먼저 이 파일의 흐름을 확인하면 됩니다.
    [DisallowMultipleComponent, RequireComponent(typeof(CapsuleMovement))]
    public sealed class CapsulePlayerInput : MonoBehaviour
    {
        [SerializeField] FirstPersonLook look;
        CapsuleMovement movement;
        FlashlightInteraction flashlight;
        // 게임 창을 사용 중인지 나타냅니다. 다른 프로그램으로 전환했을 때 입력을 받지 않기 위해 사용합니다.
        bool hasFocus = true;
        // 화면 없는 자동 검사에서는 이 읽기만 대체합니다. 실제 게임은 항상 Unity 커서 상태를 확인합니다.
        Func<bool> readCursorLocked = () => Cursor.lockState == CursorLockMode.Locked;
        float timeScaleBeforeMenu = 1;
        bool cursorRequested, cursorBeforeMenu; // 현재 요청된 커서 잠금 상태와 메뉴를 열기 직전의 상태
        int actionTransitionFrame = -1;
        public event Action<bool> MenuChanged; // 메뉴 화면에 일시정지 상태를 알립니다.
        public bool InventoryOpen { get; private set; }
        public bool MenuOpen { get; private set; }
        public bool ActionInProgress { get; private set; }
        public bool HasFocus => hasFocus;
        public bool GameplayInputEnabled => hasFocus && readCursorLocked() &&
            !InventoryOpen && !MenuOpen && !movement.Stamina.IsDead && Time.timeScale > 0;
        public bool CanToggleFlashlight => GameplayInputEnabled && !ActionInProgress && actionTransitionFrame != Time.frameCount;
        // 도구 담당은 이 값이 false일 때 홀드 사용을 실행하지 않습니다.
        public bool CanUseTools => !InventoryOpen && !MenuOpen && !ActionInProgress && !movement.Stamina.IsDead;
        // 테스트 씬 생성 코드에서도 카메라 담당 컴포넌트를 연결할 수 있도록 공개한 속성입니다.
        public FirstPersonLook Look { get => look; set => look = value; }

        // Awake에서 연결을 한 번 찾아 저장합니다. 매 프레임 같은 컴포넌트를 다시 찾을 필요가 없습니다.
        // 이동/손전등은 같은 플레이어 오브젝트, 시점은 자식 카메라에 붙이는 구조입니다.
        void Awake()
        {
            movement = GetComponent<CapsuleMovement>();
            flashlight = GetComponent<FlashlightInteraction>();
            if (look == null) look = GetComponentInChildren<FirstPersonLook>();
        }
        // 시작하면 마우스를 게임 화면에 고정합니다. 창을 벗어나거나 컴포넌트를 끄면 다시 풀어 줍니다.
        void Start() => SetCursorCaptured(!InventoryOpen && !MenuOpen);
        void OnApplicationFocus(bool focused)
        {
            hasFocus = focused;
            if (!focused) ApplyCursor(false);
        }
        void OnDisable()
        {
            // 씬 전환/테스트 종료 때 이 컴포넌트가 건 일시정지를 남기지 않습니다.
            if (MenuOpen) SetMenuOpen(false);
            SetCursorCaptured(false);
        }

        // [인벤토리 담당 연결] 가방은 시간을 멈추지 않습니다. UI 클릭이 게임 복귀 클릭으로 새지 않게 합니다.
        public void SetInventoryOpen(bool open)
        {
            InventoryOpen = open;
            ApplyInputLocks();
        }

        // [메뉴 담당 연결] 메뉴만 게임 시간을 멈춥니다. 닫을 때 열기 전 시간 배율로 복구합니다.
        public void SetMenuOpen(bool open)
        {
            if (MenuOpen == open) return;
            MenuOpen = open;
            if (open) { cursorBeforeMenu = cursorRequested; timeScaleBeforeMenu = Time.timeScale; Time.timeScale = 0; }
            else Time.timeScale = timeScaleBeforeMenu;
            ApplyInputLocks(!open && cursorBeforeMenu);
            MenuChanged?.Invoke(open);
        }

        // [구조/치료 담당 연결] 이동·시점·도구 사용을 잠급니다. Ctrl 앉기는 계속 허용합니다.
        // 진행량 초기화/도구 교체 제한/취소 입력 처리는 해당 행동 담당자가 이 상태와 함께 관리합니다.
        public void SetActionInProgress(bool active)
        {
            if (ActionInProgress != active) actionTransitionFrame = Time.frameCount;
            ActionInProgress = active;
            ApplyInputLocks();
        }

        void ApplyInputLocks(bool captureCursor = true)
        {
            movement.SetMovementEnabled(CanUseTools);
            if (look != null) look.SetLookEnabled(CanUseTools);
            SetCursorCaptured(captureCursor);
        }

        // Unity가 매 프레임 호출합니다. 입력을 읽고 → 동작을 요청하고 → 실제 이동을 처리합니다.
        void Update()
        {
            // 키보드와 버튼은 GetKey 계열로 직접 읽습니다. Horizontal/Vertical 축 설정을 사용하지 않습니다.
            // 마우스 시점만 픽셀 이동량을 읽습니다. 이동량은 버튼 상태인 GetKey로 표현할 수 없습니다.
            var mouse = Mouse.current;
            if (hasFocus && Input.GetKeyDown(KeyCode.Escape)) SetMenuOpen(!MenuOpen);
            if (hasFocus && !InventoryOpen && !MenuOpen && !movement.Stamina.IsDead &&
                Cursor.lockState != CursorLockMode.Locked && Input.GetKeyDown(KeyCode.Mouse0) &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
                SetCursorCaptured(true);
            bool active = GameplayInputEnabled;
            Vector2 input = Vector2.zero;
            // UI/행동 잠금과 실제 Shift 해제를 구분합니다. 잠금을 푼 것만으로 소진 달리기가 재개되면 안 됩니다.
            bool sprint = Input.GetKey(KeyCode.LeftShift), jump = false;
            if (active)
            {
                // 오른쪽(D)/앞(W)은 +1, 왼쪽(A)/뒤(S)는 -1입니다. 반대 키를 함께 누르면 서로 상쇄됩니다.
                input = new Vector2((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
                    (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
                // GetKey는 누르는 동안 true, GetKeyDown은 처음 누른 그 프레임만 true입니다.
                // 달리기는 누르는 동안 유지하고, 점프는 길게 눌러도 자동으로 반복하지 않습니다.
                jump = Input.GetKeyDown(KeyCode.Space);
                // mouse.delta는 이번 프레임에 마우스를 움직인 양입니다. 실제 회전 계산은 FirstPersonLook에 맡깁니다.
                if (look != null && mouse != null) look.Look(mouse.delta.ReadValue());
                // F는 획득 시도, 우클릭은 켜기/끄기입니다. 획득 전 사용 제한과 거리 판정은 손전등 코드가 처리합니다.
                // 행동 중 우클릭은 취소 담당이 처리하도록 남깁니다. 동시에 손전등까지 바꾸지 않습니다.
                if (!ActionInProgress && flashlight != null)
                {
                    if (Input.GetKeyDown(KeyCode.F)) flashlight.TryPickUp();
                    if (Input.GetKeyDown(KeyCode.Mouse1) && CanToggleFlashlight) flashlight.Toggle();
                }
                movement.SetCrouched(Input.GetKey(KeyCode.LeftControl));
            }
            // UI를 열거나 포커스를 잃으면 기존 자세를 유지합니다. 입력이 없다는 이유로 강제로 일어서지 않습니다.
            // 조작 중이 아니어도 Move를 호출해야 중력과 스태미나 회복이 작동합니다. 입력은 0/false입니다.
            movement.Move(input, sprint, jump, Time.timeScale == 0 ? 0 : Time.deltaTime);
        }

        // captured=true: 커서를 잠그고 숨깁니다. false: 커서를 풀고 화면에 보여 줍니다.
        // 이 함수 자체는 커서만 관리합니다. UI 표시 여부는 각 UI 담당자가 처리합니다.
        public void SetCursorCaptured(bool captured)
        {
            cursorRequested = captured && !InventoryOpen && !MenuOpen && !movement.Stamina.IsDead;
            ApplyCursor(cursorRequested && hasFocus);
        }

        static void ApplyCursor(bool captured)
        {
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }
    }
}
