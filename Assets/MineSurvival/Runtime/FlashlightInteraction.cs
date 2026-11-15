using UnityEngine;

namespace MineSurvival
{
    // [역할] F로 바닥의 손전등을 획득하고, 우클릭으로 빛을 켜고 끄는 시연용 기능입니다.
    // 플레이어에 붙이고 view에는 카메라, beam에는 실제 빛(Light), heldModel에는 손에 보이는 모형을 연결합니다.
    // 빛과 모형은 카메라의 자식으로 배치되어 시점 회전과 앉기를 자동으로 따라갑니다(씬 생성 코드에서 설정).
    // 현재는 손전등 하나의 보유 여부만 관리합니다. 인벤토리 슬롯·장착·버리기와는 아직 연결되어 있지 않습니다.
    [DisallowMultipleComponent]
    public sealed class FlashlightInteraction : MonoBehaviour
    {
        [SerializeField] Camera view;
        [SerializeField] Light beam;
        [SerializeField] GameObject heldModel;
        // 카메라에서 대상의 충돌체 표면까지 허용하는 최대 획득 거리입니다. 기본값은 2.5m입니다.
        [SerializeField, Min(.1f)] float pickupDistance = 2.5f;

        // HasFlashlight는 획득 여부, IsOn은 실제 조명이 켜져 있는지 나타냅니다. 획득과 점등은 서로 다른 상태입니다.
        public bool HasFlashlight { get; private set; }
        public bool IsOn => beam != null && beam.enabled;

        // 처음에는 미획득 상태로 시작하므로 빛과 손에 든 모형을 숨깁니다.
        // 카메라는 자동으로 찾을 수 있지만, 빛과 모형은 씬/프리팹에서 연결해야 합니다.
        void Awake()
        {
            if (view == null) view = GetComponentInChildren<Camera>();
            if (beam != null) beam.enabled = false;
            if (heldModel != null) heldModel.SetActive(false);
        }

        // F를 눌렀을 때 호출합니다. 획득에 성공하면 true, 거리/벽/대상 등의 조건 때문에 실패하면 false를 돌려줍니다.
        public bool TryPickUp()
        {
            // 이미 가지고 있으면 중복 획득을 막습니다. 필수 카메라나 조명이 없을 때도 상태를 바꾸지 않습니다.
            if (HasFlashlight || view == null || beam == null) return false;
            // Raycast는 카메라 정면으로 보이지 않는 선을 쏘아 가장 먼저 닿는 충돌체를 찾는 검사입니다.
            // 첫 대상이 벽이면 손전등까지 도달하지 못하므로 벽 너머 획득이 막힙니다.
            // Ignore Raycast 레이어와 Trigger는 제외합니다. 플레이어 몸은 씬에서 Ignore Raycast 레이어로 설정합니다.
            // [팀원 연결 주의] 맞은 충돌체와 같은 오브젝트에 FlashlightPickup이 있어야 합니다.
            // 실제 에셋의 충돌체가 자식에 있다면 표시 컴포넌트 배치 또는 대상 찾는 방식을 맞춰야 합니다.
            if (!Physics.Raycast(view.transform.position, view.transform.forward, out var hit, pickupDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || !hit.collider.TryGetComponent<FlashlightPickup>(out var pickup)) return false;
            // 획득한 바닥 오브젝트는 비활성화하고, 대신 카메라에 붙어 있는 손전등 모형을 보여 줍니다.
            // 조명을 여기서 켜지 않으므로, 처음 획득한 뒤 우클릭해야 빛이 들어옵니다.
            HasFlashlight = true;
            pickup.gameObject.SetActive(false);
            if (heldModel != null) heldModel.SetActive(true);
            return true;
        }

        // 우클릭 한 번마다 켜짐/꺼짐을 반대로 바꿉니다. !는 현재 true/false 값을 뒤집는 연산자입니다.
        // 획득 전에는 사용할 수 없습니다. 다른 장비 선택이나 버리기에 따른 사용 제한은 추후 인벤토리와 연결해야 합니다.
        public void Toggle()
        {
            if (HasFlashlight && beam != null) beam.enabled = !beam.enabled;
        }
    }
}
