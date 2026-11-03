# 광산 프로토타입 수정 위치

씬: `Assets/Scenes/MinePrototype.unity`. Play 후 Game 화면을 클릭합니다.

| 수정할 내용 | Inspector에서 선택할 객체 / 에셋 |
| --- | --- |
| 속도, 감도, 점프 높이, 웅크린 높이·속도 | `Player` → `Player` 컴포넌트 |
| 줍기 거리 | `Player` → `Inventory` 컴포넌트 |
| 이동 물리·충돌체 | `Player` → `Rigidbody`, `CapsuleCollider` |
| 키 설정 | `Assets/InputSystem_Actions.inputactions` → `Player` 맵 |
| 손에 드는 위치·각도·크기 | `Player → Inventory → Hand`에 연결된 Transform |
| 손전등 빛 | `Flashlight/Flashlight Beam` → Light |
| 제목 | `HUD/Title` → TextMeshPro |
| 조준점 | `HUD/Crosshair` → Image |

## 인벤토리 조작

- **E**: 흰 테두리가 표시된 바닥 아이템 획득. 기본 3칸을 먼저 채우고, 배낭이 있으면 추가 5칸에 보관합니다.
- **Space**: 지면에서 점프. **C 누르는 동안**: 웅크리기. 낮은 천장 아래에서는 C를 떼어도 공간이 생길 때까지 웅크립니다.
- **Shift**: 달리기. 웅크린 동안에는 웅크리기 속도로 움직입니다.
- **휠**: 기본 3칸의 선택 변경. **우클릭**: 선택한 아이템 사용·손전등 전환.
- **Alt**: 마우스 모드 전환. 우측 하단 가방 아이콘 클릭으로 배낭을 엽니다.
- **드래그**: 기본 칸과 배낭 칸 사이 이동. 목적지에 아이템이 있으면 교환합니다.
- **노란 테두리**: 이동 가능한 칸. **붉은 점멸**: UI 밖에 놓아 버릴 수 있는 위치.
- **X**: 선택한 아이템을 바라보는 방향으로 가볍게 던집니다.
- **Q 길게**: 배낭을 앞으로 들고, Q를 떼면 던집니다. 짧게 누르면 아무 동작도 하지 않습니다.
- **배낭을 든 상태에서 우클릭**: 던지기 취소. 손전등은 전환되지 않으며, Q를 떼어도 배낭은 버려지지 않습니다.

배낭은 1개만 장착할 수 있습니다. 던진 배낭도 내용물을 유지하며 다시 주울 수 있습니다.
배낭을 드는 동안 기존 손의 아이템은 숨겨지고, UI·드래그·아이템 사용·칸 변경은 차단됩니다.
취소하거나 배낭을 던지면 기존 손의 아이템이 다시 표시됩니다.
벽 등으로 던질 공간이 부족하면 버리지 않습니다.

## 컴포넌트

- `Player`: 씬 내 단일 플레이어(`Player.Instance`), 물리 이동·점프·웅크리기·시점·커서. PlayerInput의 Unity Events에서 각 콜백을 직접 호출합니다.
- `Inventory`: 플레이어의 `Item[3]`, 선택 칸, 장착 배낭 참조와 이동·버리기 처리.
- `Backpack : Item`: 배낭 자체에 `Item[5]`를 보관하고, 장착 중에만 Q·우클릭 입력을 받습니다.
- `Item`: 아이콘·물리 줍기/던지기·흰 외곽선. 선택된 아이템만 사용 입력을 받으며 `Flashlight`는 점등만 추가합니다.
- `InventoryUI`: 씬의 UI 갱신과 Unity 드래그 이벤트. 점멸 코루틴은 드래그 중에만 실행합니다.

`Player → Inventory`에서 손·배낭 위치, 던지는 거리와 속도를 설정합니다.
`HUD/Inventory`에 슬롯 이미지와 버튼이 연결되어 있으며 각 자식 객체를 Inspector에서 수정합니다.
아이템은 `Assets/MinePrototype/Prefabs`의 프리팹을 재사용할 수 있습니다.
흰 테두리 색과 두께는 `Art/PickupOutline.mat`, Q 길게 누르는 시간은
Input Actions의 `DropBackpack → Hold`에서 수정합니다.

## 입력 연결

`Player → PlayerInput → Events`에서 액션과 담당 컴포넌트를 직접 연결합니다.
중앙 switch나 액션 이름에 따른 분기 함수는 없습니다.

| 액션 | 담당 컴포넌트 |
| --- | --- |
| Move / Look / Sprint / Jump / Crouch / Attack | Player |
| Interact / SelectSlot / DropItem | Inventory |
| MouseMode | InventoryUI |
| UseItem | 선택된 Item의 Use Action 참조 |
| DropBackpack / CancelBackpack | 장착된 Backpack의 Drop / Cancel Action 참조 |

아이템 종류를 추가해도 Player의 입력 코드를 수정하지 않습니다.
싱글 플레이 전용이므로 아이템·배낭은 Input Action Reference의 액션을 직접 구독합니다.
현재 인벤토리는 `Player.Instance.Inventory`로 접근하며, 장착 여부만으로 입력 구독을 연결·해제합니다.
Player는 씬과 함께 생성·제거되며 중복 객체는 즉시 비활성화한 뒤 제거합니다.
슬롯 이동·선택은 관련 아이템만 배치하고, UI 점멸은 이전·현재 대상 테두리만 갱신합니다.
이동은 `FixedUpdate`에서 Rigidbody 속도로 처리합니다. 중력은 Player의 Gravity 값으로 적용하며 Rigidbody의 Use Gravity는 끕니다.
회전 고정·보간·연속 충돌과 마찰 없는 물리 머티리얼은 씬의 컴포넌트에 설정되어 있습니다.
접지 검사는 점프 요청 시에만, 머리 위 공간 검사는 일어나려 할 때만 실행합니다.
