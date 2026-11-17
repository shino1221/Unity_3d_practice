using System.Collections;
using MineSurvival;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MinePrototype
{
    public class Pickaxe : Item
    {
        private const float MinSwingInterval = 0.05f; // 허용할 최소 반복 간격(초)
        [SerializeField] private InputActionReference swingAction; // 장착 중 받을 곡괭이 좌클릭 입력
        [SerializeField] private Animation swingAnimation; // 곡괭이 모델의 휘두르기 클립
        [SerializeField, Min(MinSwingInterval)] private float swingInterval = 0.7f; // 좌클릭을 누를 때 휘두르기 사이의 간격(초)
        [SerializeField, Min(0.1f)] private float range = 2.6f; // 카메라 기준 타격 거리(m)
        [SerializeField, Min(1)] private int damage = 1; // 곡괭이의 일반 타격 피해

        private InputAction subscribedAction; // 장착 중 구독한 액션, 미장착 시 null
        private CapsulePlayerInput controls; // 장착 중 메뉴 잠금 상태를 알리는 입력 담당
        private Coroutine swingRoutine; // 좌클릭을 유지하는 동안 실행하는 반복 타격
        private float nextSwingTime; // 연타해도 다음 타격이 가능해지는 게임 시간
        private float repeatInterval; // 반복 대기와 애니메이션에 마지막으로 적용한 간격(초)
        private WaitForSeconds repeatWait; // 간격이 같으면 다음 좌클릭에도 재사용할 대기 객체

        public override void SetEquipped(bool equipped)
        {
            InputAction action = equipped && swingAction != null ? swingAction.action : null; // 장착 시 연결할 입력
            if (subscribedAction == action) return;

            if (subscribedAction != null)
            {
                subscribedAction.performed -= StartSwing;
                subscribedAction.canceled -= EndSwing;
                subscribedAction.Disable();
                if (controls != null) controls.MenuChanged -= OnMenuChanged;
                controls = null;
                StopSwing();
            }
            subscribedAction = action;
            if (action == null)
            {
                nextSwingTime = 0f;
                swingAnimation.Stop();
                swingAnimation.transform.localRotation = Quaternion.identity;
                return;
            }

            transform.localRotation = Quaternion.Euler(0f, 80f, 24f);
            transform.localScale = Vector3.one * 1.5f;
            action.performed += StartSwing;
            action.canceled += EndSwing;
            controls = Player.Instance?.Controls;
            if (controls != null) controls.MenuChanged += OnMenuChanged;
            action.Enable();
        }

        private void StartSwing(InputAction.CallbackContext input)
        {
            Player player = Player.Instance; // 곡괭이를 장착한 플레이어
            if (swingRoutine == null && player != null && player.Inventory.CanUse
                && player.Stamina.Current >= PlayerStamina.PickaxeCost)
                swingRoutine = StartCoroutine(SwingWhileHeld(player));
        }

        private void EndSwing(InputAction.CallbackContext input) => StopSwing();

        private void OnMenuChanged(bool open)
        {
            if (open) StopSwing();
        }

        private void StopSwing()
        {
            if (swingRoutine == null) return;
            StopCoroutine(swingRoutine);
            swingRoutine = null;
        }

        private IEnumerator SwingWhileHeld(Player player)
        {
            Inventory inventory = player.Inventory; // 반복 중 행동 가능 여부를 확인할 인벤토리
            PlayerStamina stamina = player.Stamina; // 타격마다 차감할 공용 스태미나
            Camera camera = player.ViewCamera; // 타격과 약점 판정의 기준 카메라
            Transform view = camera.transform; // 타격 순간의 위치와 방향을 읽을 카메라 Transform
            float interval = Mathf.Max(MinSwingInterval, swingInterval); // 이번에 누르는 동안 사용할 간격(초)
            float remaining = nextSwingTime - Time.time; // 직전 타격 때문에 더 기다려야 하는 시간(초)
            if (remaining > 0f) yield return new WaitForSeconds(remaining);

            if (repeatWait == null || repeatInterval != interval)
            {
                repeatInterval = interval;
                repeatWait = new WaitForSeconds(interval);
                AnimationClip clip = swingAnimation.clip; // 반복 간격이 짧을 때 속도를 맞출 휘두르기 클립
                swingAnimation[clip.name].speed = Mathf.Max(1f, clip.length / interval);
            }

            while (player != null && inventory.CanUse && stamina.TryConsume(PlayerStamina.PickaxeCost))
            {
                Swing(camera, view);
                nextSwingTime = Time.time + interval;
                yield return repeatWait;
            }
            swingRoutine = null;
        }

        private void Swing(Camera camera, Transform view)
        {
            swingAnimation.Rewind();
            swingAnimation.Play();
            if (Physics.Raycast(view.position, view.forward, out RaycastHit hit, range,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                hit.collider.GetComponentInParent<BreakableDebris>()?.TakeHit(damage, hit, camera, range);
        }
    }
}
