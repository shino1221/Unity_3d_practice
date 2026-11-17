using UnityEngine;

namespace MinePrototype
{
    public class BreakableRock : BreakableDebris
    {
        [SerializeField, Min(1)] private int maxHealth = 8; // 바위의 최대 체력과 시작 체력
        [SerializeField] private Transform marker; // 바위 표면에 표시할 X 오브젝트
        [SerializeField, Min(0.01f)] private float weakRadius = 0.085f; // X 중심에서 약점으로 인정하는 거리(m)
        [SerializeField, Min(1)] private int weakMultiplier = 2; // X를 맞혔을 때 기본 피해에 곱할 값
        protected override int MaxHealth => maxHealth; // 부모의 체력 초기화에 전달할 바위 최대 체력

        public override void TakeHit(int damage, RaycastHit hit, Camera camera, float range)
        {
            if (Health == 0) return;

            bool marked = marker.gameObject.activeSelf; // 현재 타격 전에 X가 표시되어 있었는지
            bool weakHit = marked && (hit.point - marker.position).sqrMagnitude <= weakRadius * weakRadius
                && Vector3.Dot(hit.normal, marker.forward) > 0.5f; // X와 가까운 같은 방향의 표면을 맞혔는지
            base.TakeHit(weakHit ? damage * weakMultiplier : damage, hit, camera, range);

            if (Health > 0 && (!marked || weakHit))
                MoveMarker(hit.collider.bounds, camera, range, marked);
        }

        private void MoveMarker(Bounds bounds, Camera camera, float range, bool relocating)
        {
            Vector3 eye = camera.transform.position; // 후보를 확인하는 레이의 출발점
            Vector3 aim = camera.transform.forward; // 화면 중앙을 향하는 현재 조준 방향
            float gapSquared = 4f * weakRadius * weakRadius; // 조준선과 기존 X에서 띄울 최소 거리의 제곱

            // 같은 바위에 먼저 닿는 레이만 받아 보이는 표면에 X를 배치한다.
            for (int i = 0; i < 32; i++)
            {
                Vector3 sample = new(Random.Range(bounds.min.x, bounds.max.x),
                    Random.Range(bounds.min.y, bounds.max.y), Random.Range(bounds.min.z, bounds.max.z)); // 바위 경계 상자 안 후보
                Vector3 viewport = camera.WorldToViewportPoint(sample); // 후보의 화면 위치(0~1)와 카메라 앞 거리
                if (viewport.z <= 0 || viewport.x <= 0.02f || viewport.x >= 0.98f
                    || viewport.y <= 0.02f || viewport.y >= 0.98f) continue;

                if (!Physics.Raycast(eye, sample - eye, out RaycastHit surface, range,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    || surface.collider.GetComponentInParent<BreakableRock>() != this) continue;

                if (Vector3.Cross(aim, surface.point - eye).sqrMagnitude < gapSquared) continue;
                if (relocating && (surface.point - marker.position).sqrMagnitude < gapSquared) continue;

                marker.SetPositionAndRotation(surface.point + surface.normal * 0.008f, Quaternion.LookRotation(surface.normal));
                marker.gameObject.SetActive(true);
                return;
            }

            // 보이는 후보가 없으면 기존 약점을 숨기고 다음 명중에서 다시 찾는다.
            marker.gameObject.SetActive(false);
        }
    }
}
