using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace MinePrototype
{
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    [RequireComponent(typeof(PlayerInput), typeof(Inventory))]
    public class Player : MonoBehaviour
    {
        public static Player Instance { get; private set; } // 현재 씬의 단일 플레이어 참조
        public Inventory Inventory => inventory; // 외부에서 접근할 플레이어의 인벤토리
        [SerializeField] private Transform view; // 카메라 Transform
        [SerializeField] private float walkSpeed = 3.5f; // 걷는 속도
        [SerializeField] private float sprintSpeed = 5.5f; // 달리는 속도
        [SerializeField] private float crouchSpeed = 1.75f; // 웅크린 상태 이동속도
        [SerializeField] private float mouseSensitivity = 0.09f; // 마우스 감도
        [SerializeField] private float gravity = -22f; // 중력
        [SerializeField] private float jumpHeight = 1.2f; // 점프 높이
        [SerializeField] private float crouchHeight = 1f; // 웅크릴 때 캡슐 콜라이더 높이
        

        private Rigidbody body; // 이동 속도·중력·몸체 회전을 적용할 Rigidbody
        private CapsuleCollider capsule; // 웅크릴 때 높이와 중심을 바꿀 플레이어 충돌체
        private Inventory inventory; // 같은 게임 오브젝트에 붙은 Inventory 컴포넌트
        private Vector2 movement, look; // movement: 좌우 X·전후 Y 입력(길이≤1), look: 마우스 X·Y 이동량(픽셀)
        private Quaternion yawRotation; // 플레이어 바디의 수직축을 배제한 수평 회전값
        private Vector3 standingCenter, standingView; 
        // standingCenter: 서 있을 때 캡슐 중심의 로컬 좌표, standingView: 서 있을 때 카메라 로컬 위치
        private bool wasOpen, sprint, jump, crouchHeld, crouched; 
        // wasOpen: 직전 배낭 UI 열림 상태, sprint: 달리기 입력, jump: 점프 입력, crouchHeld: C 입력 유지 중인지, crouched: 실제 웅크림 상태
        private float pitch, yaw, standingHeight; 
        // pitch: 카메라 상하 각도(도), yaw: 몸체 좌우 각도(도), standingHeight: 서 있을 때 캡슐 높이(m)
        private bool canMove = true;



        public bool WorldControls => !inventory.IsOpen && Cursor.lockState == CursorLockMode.Locked; // 배낭 UI가 닫히고 커서가 잠겨 있으면 이동·시점 조작 허용



        // Input 시스템 설정
        public void OnMove(InputAction.CallbackContext input) => movement = Vector2.ClampMagnitude(input.ReadValue<Vector2>(), 1f);
        public void OnLook(InputAction.CallbackContext input) => look = input.ReadValue<Vector2>();
        public void OnSprint(InputAction.CallbackContext input) => sprint = input.ReadValueAsButton();
        public void OnCrouch(InputAction.CallbackContext input) => crouchHeld = input.ReadValueAsButton();
        public void OnJump(InputAction.CallbackContext input)
        {
            if (input.performed && WorldControls) jump = true;
        }
        public void OnResume(InputAction.CallbackContext input)
        {
            if (input.performed && !WorldControls) StartCoroutine(ResumeAfterUI());
        }


        private void OnEnable()
        {
            if (Instance != null && Instance != this)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }
            Instance = this;
            body = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
            standingHeight = capsule.height;
            standingCenter = capsule.center;
            standingView = view.localPosition;
            yawRotation = body.rotation;
            yaw = yawRotation.eulerAngles.y;
            pitch = Mathf.DeltaAngle(0f, view.localEulerAngles.x);
            inventory = GetComponent<Inventory>();
            wasOpen = false;
            movement = look = Vector2.zero;
            sprint = jump = crouchHeld = crouched = false;
            inventory.Changed += InventoryChanged;
            CaptureCursor(true);
        }

        private void OnDisable()
        {
            if (Instance != this) return;
            inventory.Changed -= InventoryChanged;
            StopAllCoroutines();
            inventory.ClearTarget();
            capsule.height = standingHeight;
            capsule.center = standingCenter;
            view.localPosition = standingView;
            CaptureCursor(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnApplicationFocus(bool focused) // 배낭을 들던 중 다른 창으로 이동시 원래 상태로 해제
        {
            if (focused) return;
            inventory?.CancelBagHold();
            CaptureCursor(false);
        }



        // 키보드 조작으로 복귀
        private IEnumerator ResumeAfterUI()
        {
            // UI 처리와 겹칠 수도 있기에 한 프레임 건너뜀
            yield return null;
            if (!inventory.IsOpen && !inventory.HoldingBag && !inventory.Dragging
                && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())) CaptureCursor(true);
        }

        private void Update()
        {
            if (!WorldControls)
            {
                inventory.ClearTarget();
                return;
            }
            // 매 프레임 검사하기에 어떻게 최적화 할지 고민 중

            Look();
            inventory.UpdateTarget();
        }


        // 마우스에 따른 시선 변경
        private void Look()
        {
            if (look == Vector2.zero) return;
            Vector2 delta = look * mouseSensitivity;
            yaw += delta.x;
            yawRotation = Quaternion.Euler(0f, yaw, 0f); // 수평 각
            pitch = Mathf.Clamp(pitch - delta.y, -85f, 85f); // 상하 각도 제한
            view.localRotation = Quaternion.Euler(pitch, 0f, 0f); // 카메라는 상하만 판단
        }


        private void FixedUpdate()
        {
            canMove = WorldControls; // 이번 호출에 이동·점프·웅크리기 입력을 처리할 수 있는지 (현재는 가방이 열려있는지로만 판단)
            if (canMove && crouchHeld != crouched) SetCrouch(crouchHeld);


            Vector2 input = canMove ? movement : Vector2.zero; // 움직이는 상태인지 체크
            float speed = crouched ? crouchSpeed : sprint ? sprintSpeed : walkSpeed; // 숙인 상태인가? => 달리는 상태인가? 검토해서 스피드 값
            Vector3 velocity = yawRotation * new Vector3(input.x, 0f, input.y) * speed; // Rigidbody에 벡터 값 적용


            velocity.y = body.linearVelocity.y; // 기존 상승·낙하 속도를 유지하고 점프할 때만 바꾼다.
            // 접지 검사 후 점프
            if (jump && canMove && velocity.y <= 0.1f && Grounded())
                velocity.y = Mathf.Sqrt(-2f * gravity * jumpHeight); // 점프
            jump = false;


            body.linearVelocity = velocity; // 실질적 이동


            body.AddForce(Vector3.up * gravity, ForceMode.Acceleration); // 중력 적용


            if (canMove) body.MoveRotation(yawRotation); // Look >> 수평각 계산 후 몸체에 적용
        }

                
        private void InventoryChanged()
        {
            // 슬롯 변경 때는 Alt로 풀어 둔 커서를 다시 잠그지 않는다.
            if (wasOpen == inventory.IsOpen) return;
            wasOpen = inventory.IsOpen;
            CaptureCursor(!wasOpen);
        }


        private bool Grounded()
        {
            // TransformPont >> 받은 로컬 좌표를 월드 좌표로 전환
            Vector3 foot = transform.TransformPoint(capsule.center - Vector3.up * (capsule.height * 0.5f - capsule.radius)); // 캡슐 하단 반구 중심의 월드 좌표
            return Physics.SphereCast(foot + Vector3.up * 0.05f, capsule.radius * 0.9f, Vector3.down,
                out RaycastHit hit, 0.15f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) 
                && hit.normal.y >= 0.707f; // 노멀(닿은 표면) 경사각이 45도 이상인지 (0.707은 cos 값)
        }


        private void SetCrouch(bool crouch)
        {
            // 일어날 공간이 없으면 웅크린 채 유지하고, 공간이 생겼을 때 다시 시도한다.
            if (!crouch)
            {
                // TransformPont >> 받은 로컬 좌표를 월드 좌표로 전환
                Vector3 top = transform.TransformPoint(capsule.center + Vector3.up * (capsule.height * 0.5f - capsule.radius)); 
                // 현재 캡슐 상단 반구 중심의 월드 좌표
                Vector3 head = transform.TransformPoint(standingCenter + Vector3.up * (standingHeight * 0.5f - capsule.radius)); 
                // 서 있을 때 기준 캡슐 상단 반구 중심의 월드 좌표 (숙였을 때 기준으로 처리)
                if (Physics.CheckCapsule(top, head, capsule.radius - 0.02f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
            }

            crouched = crouch;
            float lower = crouch ? standingHeight - crouchHeight : 0f; // 줄일 캡슐 높이이자 카메라를 내릴 거리(m), 서 있으면 0
            capsule.height = standingHeight - lower;
            capsule.center = standingCenter - Vector3.up * (lower * 0.5f);
            view.localPosition = standingView - Vector3.up * lower;
        }


        public void CaptureCursor(bool capture)
        {
            look = Vector2.zero; // 모든 이동량에 대해 상쇄
            Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !capture;
        }
    }
}
