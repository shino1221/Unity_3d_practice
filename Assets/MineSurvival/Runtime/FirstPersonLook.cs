using System;
using UnityEngine;

namespace MineSurvival
{
    // [역할] 마우스 움직임을 1인칭 카메라 회전으로 바꿉니다. 플레이어의 자식 카메라에 붙입니다.
    // 좌우는 플레이어 몸 전체를 돌리고, 위아래는 카메라만 돌려야 이동 방향과 보는 방향이 자연스럽게 맞습니다.
    [DisallowMultipleComponent]
    public sealed class FirstPersonLook : MonoBehaviour
    {
        [SerializeField] Transform characterBody;
        // 마우스 이동량에 곱할 회전 감도입니다. 값이 클수록 조금만 움직여도 많이 회전합니다.
        [SerializeField, Min(.001f)] float sensitivity = .08f;
        // 위아래를 최대 몇 도까지 볼지 정합니다. 90도보다 작게 제한해 카메라가 뒤집히지 않게 합니다.
        [SerializeField, Range(1, 89)] float pitchLimit = 85;
        // pitch는 위아래 각도입니다. 이 코드에서는 음수가 위를, 양수가 아래를 보는 방향입니다.
        float pitch;
        public bool LookEnabled { get; private set; } = true;
        public float Pitch => pitch;
        public Transform CharacterBody { get => characterBody; set => characterBody = value; }

        // 몸을 별도로 지정하지 않았다면 카메라의 부모를 몸으로 사용합니다.
        // Unity의 회전값 350도를 -10도처럼 이해하기 쉬운 범위로 바꿔 초기 각도를 기억합니다.
        void Awake()
        {
            if (characterBody == null) characterBody = transform.parent;
            pitch = Mathf.DeltaAngle(0, transform.localEulerAngles.x);
        }

        // [팀원 연결 지점] UI를 조작할 때 false, 탐사로 돌아갈 때 true로 설정할 수 있습니다.
        // 이 함수는 시점만 막습니다. 이동, 커서 표시, 손전등 입력은 별도로 관리해야 합니다.
        public void SetLookEnabled(bool enabled) => LookEnabled = enabled;

        // CapsulePlayerInput이 전달한 이번 프레임의 마우스 이동량을 적용합니다.
        public void Look(Vector2 mouseDelta)
        {
            if (float.IsNaN(mouseDelta.x) || float.IsNaN(mouseDelta.y) || float.IsInfinity(mouseDelta.x) || float.IsInfinity(mouseDelta.y))
                throw new ArgumentOutOfRangeException(nameof(mouseDelta));
            if (!LookEnabled || characterBody == null) return;
            // 마우스 이동량에는 이미 이번 프레임 동안 움직인 양이 담겨 있습니다.
            // 여기에 deltaTime을 다시 곱하면 화면 갱신 횟수에 따라 감도가 달라지므로 곱하지 않습니다.
            characterBody.Rotate(0, mouseDelta.x * sensitivity, 0);
            // 마우스를 위로 움직였을 때 위를 보도록 y값을 빼고, 위아래 제한 각도를 넘지 않게 합니다.
            pitch = Mathf.Clamp(pitch - mouseDelta.y * sensitivity, -pitchLimit, pitchLimit);
            // 카메라의 좌우 회전은 부모 몸을 따라갑니다. 자체 좌우/기울기 회전은 0으로 두어 화면이 옆으로 기울지 않게 합니다.
            transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        }
    }
}
