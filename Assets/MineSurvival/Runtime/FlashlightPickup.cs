using UnityEngine;

namespace MineSurvival
{
    // [역할] 바닥의 원기둥에 붙이는 '이 물체는 획득할 수 있는 손전등'이라는 표시입니다.
    // 직접 입력을 받거나 빛을 켜지는 않습니다. 플레이어의 FlashlightInteraction이 이 표시를 찾습니다.
    // 현재 판정은 충돌체와 같은 오브젝트에서 표시를 찾으므로 Collider와 함께 붙여야 합니다.
    // 내용이 빈 클래스인 이유는 추가 동작 없이 컴포넌트의 존재 자체만으로 대상을 구분하기 때문입니다.
    public sealed class FlashlightPickup : MonoBehaviour { }
}
