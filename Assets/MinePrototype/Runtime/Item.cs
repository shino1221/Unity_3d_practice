using UnityEngine;

namespace MinePrototype
{
    [RequireComponent(typeof(Collider), typeof(Rigidbody))]
    public abstract class Item : MonoBehaviour
    {
        [field: SerializeField] public Sprite Icon { get; private set; } // 인벤토리 슬롯과 드래그 중에 표시할 아이콘
        [SerializeField] private GameObject pickupOutline; // 획득 가능할 때 켜는 흰 외곽선 표시 객체
        private Collider itemCollider; // 바닥에서는 켜고 소지 중에는 끄는 아이템 충돌체
        private Rigidbody body; // 줍기 시 물리를 멈추고 버릴 때 속도를 적용할 Rigidbody
        public bool IsCarried => !itemCollider.enabled; // 아이템 충돌체가 꺼져 있으면 소지 상태로 판정
        public float Radius { get; private set; } // 콜라이더 경계 상자 대각선의 절반(m), 던질 공간 검사에 사용

        private void Awake()
        {
            itemCollider = GetComponent<Collider>();
            body = GetComponent<Rigidbody>();
            Radius = itemCollider.bounds.extents.magnitude;
        }

        public virtual void SetEquipped(bool equipped) { }

        protected virtual void OnDisable() => SetEquipped(false);

        public virtual void PickUp(Transform parent)
        {
            Highlight(false);
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.isKinematic = true;
            itemCollider.enabled = false;
            transform.SetParent(parent, false);
            transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;
        }

        public void Drop(Transform view, float distance, float speed)
        {
            Vector3 direction = view.forward; // 던질 순간 카메라가 바라보는 월드 기준 벡터
            gameObject.SetActive(true);
            transform.SetParent(null, true);
            transform.SetPositionAndRotation(view.position + direction * distance, view.rotation);
            transform.localScale = Vector3.one;
            itemCollider.enabled = true;
            body.isKinematic = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = direction * speed + Vector3.up * 0.8f;
            body.angularVelocity = Vector3.zero;
        }

        public void Highlight(bool visible)
        {
            if (pickupOutline != null) pickupOutline.SetActive(visible);
        }
    }
}
