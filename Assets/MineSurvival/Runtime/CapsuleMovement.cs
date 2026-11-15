using System;
using UnityEngine;

namespace MineSurvival
{
    // [역할] 플레이어의 이동, 점프, 달리기, 앉기를 실제 위치 변화로 바꾸는 코드입니다.
    // CapsulePlayerInput이 키보드 입력을 읽어 이 파일의 함수를 호출합니다.
    // CharacterController는 몸의 충돌 범위를 나타내며, 벽을 뚫지 않도록 이동을 처리합니다.
    // 이 파일은 키보드나 인벤토리를 직접 읽지 않으므로 다른 시스템에서도 같은 이동 기능을 사용할 수 있습니다.
    [DisallowMultipleComponent, RequireComponent(typeof(CharacterController), typeof(PlayerStamina))]
    public sealed class CapsuleMovement : MonoBehaviour
    {
        // SerializeField가 붙은 값은 Unity Inspector에서 조절할 수 있습니다.
        // 아래 숫자는 기본값이며, 이미 저장된 씬/프리팹에는 Inspector에 저장한 값이 적용됩니다.
        // 거리 단위는 기본 크기(Scale 1)의 플레이어에서 Unity 1칸을 약 1m로 보고 설정했습니다.
        [SerializeField, Min(.1f)] float walkSpeed = 4;
        // 걷기 속도에 곱할 배수입니다. 기본값에서는 4 × 1.6 = 초당 6.4m로 달립니다.
        [SerializeField, Min(1)] float sprintSpeedMultiplier = 1.6f;
        // jumpHeight는 점프 목표 높이(m), gravity는 아래로 끌어당기는 가속도(m/s²)입니다.
        [SerializeField, Min(.1f)] float jumpHeight = 1.2f;
        [SerializeField, Min(.1f)] float gravity = 20;
        // 최대치나 구역과 무관한 고정 비용입니다. 점프는 실제로 시작할 때만 한 번 차감합니다.
        [SerializeField, Min(0)] float sprintCostPerSecond = 8;
        [SerializeField, Min(0)] float jumpCost = 10;
        // 앉았을 때 몸의 충돌 높이와 이동 속도 배수입니다. 0.5이면 걷기의 절반 속도입니다.
        [SerializeField, Min(.6f)] float crouchHeight = 1.05f;
        [SerializeField, Range(.1f, 1)] float crouchSpeedMultiplier = .5f;
        CharacterController controller;
        Transform view;
        // 서 있을 때의 원래 값을 기억해 두어, 앉았다 일어날 때 그 상태로 정확히 돌아갑니다.
        Vector3 standingCenter, standingViewPosition;
        float standingHeight, standingStepOffset;
        // verticalSpeed: 위아래 속도(양수는 상승, 음수는 하강).
        float verticalSpeed;
        // 한 번 소진되면 잠깐 회복되었다고 바로 달리기를 반복하지 않도록 잠그는 상태입니다.
        bool sprintExhausted;

        // 다른 코드가 읽을 수 있는 현재 상태입니다. private set은 값을 이 클래스 안에서만 바꾼다는 뜻입니다.
        // MovementMultiplier는 이동 속도와 점프 높이에만 적용됩니다. 스태미나 소모와는 별개입니다.
        public float MovementMultiplier { get; private set; } = 1;
        public PlayerStamina Stamina { get; private set; }
        public float EffectiveJumpHeight => jumpHeight * MovementMultiplier;
        public float EffectiveWalkSpeed => walkSpeed * MovementMultiplier * (IsCrouched ? crouchSpeedMultiplier : 1);
        public bool MovementEnabled { get; private set; } = true;
        public bool IsSprinting { get; private set; }
        public bool IsCrouched { get; private set; }
        // 아래 두 값은 CharacterController가 마지막 이동을 처리한 결과를 사용합니다.
        public bool IsGrounded => controller.isGrounded;
        public Vector3 Velocity => controller.velocity;

        // Awake는 Unity가 컴포넌트를 초기화할 때 호출합니다. 필요한 부품과 원래 자세를 확보합니다.
        void Awake()
        {
            controller = GetComponent<CharacterController>();
            Stamina = GetComponent<PlayerStamina>();
            standingHeight = controller.height;
            standingCenter = controller.center;
            standingStepOffset = controller.stepOffset;
            var camera = GetComponentInChildren<Camera>();
            if (camera != null)
            {
                view = camera.transform;
                standingViewPosition = view.localPosition;
            }
        }

        // true는 앉기, false는 일어서기 요청입니다. Ctrl을 떼어도 천장이 낮으면 앉은 자세를 유지합니다.
        public void SetCrouched(bool crouched)
        {
            // 구조·치료 중 이동 잠금과 앉기는 별개입니다. 사망/비활성 충돌체에서는 자세를 바꾸지 않습니다.
            if (Stamina.IsDead || !controller.enabled || crouched == IsCrouched) return;
            if (!crouched)
            {
                // 서 있는 크기의 가상 캡슐을 놓아 보고, 천장이나 장애물과 겹치는지 먼저 확인합니다.
                // skinWidth는 충돌에 사용하는 작은 여유 폭이며, 바닥 접촉 때문에 일어서기가 막히는 일을 줄입니다.
                float radius = controller.radius - controller.skinWidth;
                Vector3 center = transform.TransformPoint(standingCenter);
                Vector3 offset = transform.up * (standingHeight * .5f - controller.radius);
                // Trigger(통과 가능한 감지 구역)와 자기 몸/자식 오브젝트는 일어서기를 막는 장애물에서 제외합니다.
                foreach (var obstacle in Physics.OverlapCapsule(center - offset, center + offset, radius,
                    Physics.AllLayers, QueryTriggerInteraction.Ignore))
                    if (!obstacle.transform.IsChildOf(transform)) return;
            }
            // 충돌체 높이는 지름보다 작아질 수 없습니다. Clamp는 값을 최소~최대 범위 안으로 제한합니다.
            float height = crouched ? Mathf.Clamp(crouchHeight, controller.radius * 2, standingHeight) : standingHeight;
            float drop = standingHeight - height;
            // stepOffset은 자동으로 넘을 수 있는 턱 높이입니다. 앉은 몸에 비해 너무 커지지 않게 제한합니다.
            controller.stepOffset = Mathf.Min(standingStepOffset, height * .5f);
            controller.height = height;
            // 높이가 줄어든 양의 절반만큼 중심을 내리면 발바닥 위치는 그대로 유지됩니다.
            controller.center = standingCenter - Vector3.up * (drop * .5f);
            // 눈높이는 줄어든 높이만큼 내립니다. 현재 설정은 몸 1.8 → 1.05m, 눈높이는 0.75m 내려갑니다.
            if (view != null) view.localPosition = standingViewPosition - Vector3.up * drop;
            IsCrouched = crouched;
            if (crouched) IsSprinting = false;
        }

        // [팀원 연결 지점] 장비 등의 이동 보정값을 전달합니다. 삭제된 깊이 시스템과는 연결하지 않습니다.
        // 예: 0.75이면 걷기 3m/s, 점프 높이 0.9m입니다. 달리기 스태미나 소모는 바뀌지 않습니다.
        public void SetMovementMultiplier(float multiplier)
        {
            RequireFinite(multiplier, nameof(multiplier));
            if (multiplier <= 0) throw new ArgumentOutOfRangeException(nameof(multiplier));
            MovementMultiplier = multiplier;
        }

        // [팀원 연결 지점] false로 수평 이동·점프·달리기를 막고, true로 다시 허용합니다. 앉기는 별개입니다.
        // 중력은 계속 적용됩니다. 시점 회전과 F/우클릭까지 막는 함수는 아니므로 UI 연동 시 별도 처리가 필요합니다.
        public void SetMovementEnabled(bool enabled)
        {
            MovementEnabled = enabled;
            if (!enabled) IsSprinting = false;
        }

        // 입력 담당이 매 프레임(화면 갱신마다) 한 번 호출하는 실제 이동 함수입니다.
        // input.x는 좌우, input.y는 앞뒤 방향입니다. sprint는 달리기 요청, jump는 이번 프레임의 점프 요청입니다.
        // seconds에는 이번 프레임에 지난 시간(Time.deltaTime)을 전달합니다. 테스트에서는 정해진 시간을 전달합니다.
        public void Move(Vector2 input, bool sprint, bool jump, float seconds)
        {
            // 잘못된 숫자가 위치 계산에 들어가면 캐릭터가 사라지거나 물리가 깨질 수 있어 먼저 거부합니다.
            RequireFinite(input.x, nameof(input)); RequireFinite(input.y, nameof(input));
            RequireFinite(seconds, nameof(seconds));
            if (seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (seconds == 0 || !controller.enabled) return;
            // 시간을 먼저 진행하고 이번 행동의 비용을 차감합니다. 이동 잠금 중에도 회복은 계속됩니다.
            Stamina.Recover(seconds);
            // 이동 잠금/앉기로 달리기를 막는 것과, 사용자가 실제로 Shift를 놓는 것은 구별합니다.
            bool sprintReleased = !sprint;
            if (sprintReleased) sprintExhausted = false;
            if (!MovementEnabled || Stamina.IsDead) { input = Vector2.zero; sprint = jump = false; }
            if (IsCrouched) sprint = jump = false;
            // W+D처럼 두 방향을 동시에 눌러도 직선 이동보다 빨라지지 않도록 입력의 길이를 최대 1로 맞춥니다.
            input = Vector2.ClampMagnitude(input, 1);
            // 착지 후에는 작은 하강 속도를 유지해 바닥 접촉을 이어갑니다. 낙하 속도를 계속 쌓지는 않습니다.
            if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            // 바닥에 있을 때만 점프합니다. sqrt(2 × 중력 × 높이)는 목표 높이에 필요한 출발 속도 공식입니다.
            bool jumped = jump && controller.isGrounded && Stamina.TryConsume(jumpCost);
            if (jumped) verticalSpeed = Mathf.Sqrt(2 * gravity * EffectiveJumpHeight);
            // 점프 시작 프레임에는 점프 비용만 냅니다. 정지·공중·잠금 중에는 달리기 비용을 내지 않습니다.
            IsSprinting = false;
            if (sprint && !jumped && input.sqrMagnitude > .01f && controller.isGrounded && !sprintExhausted)
            {
                IsSprinting = Stamina.TryConsume(sprintCostPerSecond * seconds);
                // 비용 부족으로 멈춘 뒤에는 잔량이 회복돼도 Shift를 놓기 전까지 재시도하지 않습니다.
                if (!IsSprinting || Stamina.IsExhausted) sprintExhausted = true;
            }
            verticalSpeed -= gravity * seconds;
            // 몸이 바라보는 방향을 기준으로 앞뒤/좌우 속도를 만들고, 여기에 위아래 속도를 합칩니다.
            Vector3 velocity = (transform.right * input.x + transform.forward * input.y) * EffectiveWalkSpeed * (IsSprinting ? sprintSpeedMultiplier : 1);
            velocity.y = verticalSpeed;
            // 속도(m/s)에 시간(초)을 곱해 이번에 이동할 거리를 구합니다. 실제 벽 충돌은 CharacterController가 처리합니다.
            var flags = controller.Move(velocity * seconds);
            // 점프 중 머리가 천장에 닿으면 남은 상승 속도를 없애, 천장을 계속 밀어 올리지 않게 합니다.
            if ((flags & CollisionFlags.Above) != 0) verticalSpeed = Mathf.Min(0, verticalSpeed);
        }

        // 테스트 시작 위치나 복귀 지점으로 즉시 옮길 때 사용할 함수입니다.
        // 충돌체를 잠시 껐다가 원래 상태로 되돌려 위치를 바꾸고, 이전의 낙하/상승 속도를 지웁니다.
        // 앉은 상태와 공통 스태미나는 초기화하지 않으므로 완전한 부활 처리를 대신하는 함수는 아닙니다.
        public void Teleport(Vector3 position)
        {
            RequireFinite(position.x, nameof(position)); RequireFinite(position.y, nameof(position)); RequireFinite(position.z, nameof(position));
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            transform.position = position;
            controller.enabled = wasEnabled;
            verticalSpeed = 0;
            IsSprinting = false;
        }

        // NaN은 계산 불가능한 값, Infinity는 무한대입니다. 잘못된 값을 조용히 적용하지 않고 원인을 알립니다.
        static void RequireFinite(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(name);
        }
    }
}
