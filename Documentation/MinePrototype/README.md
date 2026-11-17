# 플레이어·스태미나 통합 안내

2026-11-15에 팀 저장소의 main 커밋 a0002133f5f97e307e841181ae344c1dc52eefbd와 로컬 MineSurvivalPrototype 코드를 통합했습니다. 팀의 광산 맵·아이템·배낭·UI를 유지하고, 이동·시점·공통 스태미나는 로컬 코드를 사용합니다.

## 실행

- Unity **6000.3.24f1**로 이 프로젝트를 엽니다. 팀이 합의한 엔진 버전에 맞춘 통합본입니다.
- 메인 씬: `Assets/Scenes/MinePrototype.unity`
- 이동·손전등 단독 검사 씬: `Assets/MineSurvival/Scenes/MovementTest.unity`
- Play 후 Game 화면을 클릭합니다. Esc는 일시정지/복귀, 왼쪽 Alt는 커서 전환입니다.
- Active Input Handling은 **Both**입니다. 키보드·마우스 버튼은 `Input.GetKey/GetKeyDown/GetKeyUp`, 마우스 이동량·휠·기존 UI 이벤트는 Input System을 사용합니다. GetAxis는 사용하지 않습니다.

## 조작

| 입력 | 동작 |
| --- | --- |
| WASD | 이동 |
| 마우스 이동 | 시점 회전 |
| 왼쪽 Shift | 달리기 |
| Space | 지면에서 점프 |
| 왼쪽 Ctrl 누르기 | 앉기. 낮은 천장에서는 놓아도 일어나지 않습니다. |
| F | 바라보는 아이템 획득. 거리 2.5m 이내, 벽 너머 획득 차단 |
| 마우스 휠 | 기본 소지품 3칸 선택 |
| 우클릭 | 선택한 손전등 켜기·끄기 |
| 왼쪽 Alt | 커서를 풀거나 탐사로 복귀. 배낭 UI가 열렸으면 닫기 |
| I / 가방 아이콘 클릭 | 배낭 열기·닫기 |
| 좌클릭 / 유지 | 장착한 곡괭이 휘두르기 / 반복 타격 |
| 아이템 드래그 | 슬롯 간 이동·교환, UI 밖에 놓으면 버리기 |
| X 2초 유지 | 선택 아이템 한 번 버리기. 놓으면 진행 취소 |
| Q 2초 유지 후 놓기 | 배낭을 앞으로 든 뒤 버리기 |
| 배낭을 든 상태에서 우클릭 | 배낭 버리기 취소. 같은 클릭으로 손전등이 바뀌지 않음 |
| Esc | PAUSE 메뉴 열기·닫기. 게임 창의 포커스를 잃으면 메뉴 열기 |

배낭을 열면 이동·시점·도구 입력을 막지만 시간과 스태미나 회복은 계속됩니다. PAUSE의 두 버튼은 게임 복귀와 종료를 처리합니다. 메뉴를 닫아도 배낭이 열려 있으면 이동 잠금은 유지됩니다. 던질 공간이 막혔으면 아이템을 버리지 않습니다. 버린 배낭은 내용물을 보존합니다.

## 코드 읽는 순서

1. `MineSurvival/Runtime/CapsulePlayerInput.cs`: 키 입력과 배낭·메뉴·행동 잠금의 입구입니다.
2. `MineSurvival/Runtime/CapsuleMovement.cs`: CharacterController를 이용해 이동·앉기·점프를 처리합니다. 스태미나 회복 시간도 여기서 한 번만 진행합니다.
3. `MineSurvival/Runtime/PlayerStamina.cs`: 모든 행동이 공유하는 잔량과 최대치를 관리합니다.
4. `MinePrototype/Runtime/Player.cs`: 팀 코드의 Player.Instance를 위 세 컴포넌트와 연결합니다. 자체 이동 계산은 하지 않습니다.
5. `MinePrototype/Runtime/Inventory.cs`, `Backpack.cs`, `Flashlight.cs`, `InventoryUI.cs`: 팀의 아이템 보관·버리기·손전등·UI를 처리합니다.

메인 씬에는 기존 Rigidbody 이동과 PlayerInput 이벤트를 제거했습니다. **이동 담당을 하나만 사용해야** 이중 이동이나 스태미나 중복 차감이 발생하지 않습니다. MovementTest의 FlashlightInteraction은 단독 시연용입니다. 인벤토리로 손전등을 관리하는 메인 Player에는 추가하지 않습니다.

## 수치와 수정 위치

| 수정할 내용 | 위치 / 현재 값 |
| --- | --- |
| 걷기·달리기·점프·앉기 | Player의 CapsuleMovement Inspector |
| 시점 감도 | 플레이어 자식 카메라의 FirstPersonLook |
| 충돌 크기 | Player의 CharacterController |
| 획득·던지기 거리 | Player의 Inventory |
| 손·착용 배낭·던질 배낭 위치 | Inventory에 연결된 Transform |
| 스태미나 | Player의 PlayerStamina |
| 최대 스태미나 | 100 |
| 달리기 / 점프 | 초당 8 / 실제 점프 1회당 10 |
| 회복 | 마지막 소모 뒤 3초 대기, 초당 10 |
| 곡괭이 / 삽 비용 | PlayerStamina.PickaxeCost 12 / ShovelCost 6 |
| 곡괭이 반복 간격 | Pickaxe 프리팹 → Swing Interval |
| 낙석 / 거점 | 최대치 20 감소 / 거점에서 최대치 초당 1 복구 |

잔량 부족으로 달리기가 중단되면 Shift를 놓았다가 다시 눌러야 합니다. 잔량 0은 사망이 아니며, **최대치 0**일 때 사망합니다. 최대치 감소 시 현재 잔량은 새 최대치를 넘는 부분만 줄이고, 최대치 복구는 초기 최대치까지만 허용합니다.

## 다른 담당자가 연결할 부분

| 담당 기능 | 연결 방법 |
| --- | --- |
| 곡괭이·삽 | 곡괭이는 타격마다 공용 스태미나를 사용합니다. 삽 사용 시에도 Controls.CanUseTools와 Stamina.TryConsume(비용)을 확인합니다. |
| 낙석 | 실제 피격 한 번당 Stamina.ReduceMaximum(PlayerStamina.RockfallDamage) |
| 거점 회복 | 복구된 거점 안이면 Stamina.SetInBase(true), 모든 거점에서 벗어나면 false |
| 구조·치료 | 시작 시 Controls.SetActionInProgress(true), 완료·취소 시 false. 이동·시점·장비 변경은 차단하고 앉기는 허용 |
| 메뉴 | CapsulePlayerInput이 일시정지 상태를 관리하고 PauseMenu가 화면을 갱신합니다. |
| 스태미나 HUD | Stamina.Current, Maximum, Normalized 읽기 |
| 사망·게임 종료 | Stamina.Died 이벤트에 종료 처리 연결 |

Recover()를 장비나 UI에서 또 호출하지 않습니다. 구조·치료의 진행 시간, 행동 취소와 초기화, 도구 타격, 낙석 충돌, 거점 범위와 복구 여부, 사망 화면 자체는 각 기능 담당 구현이 필요합니다.

## 통합 범위와 남은 차이

- 인벤토리는 **기본 3칸 + 배낭 3칸**입니다. 배낭 슬롯의 UI와 실제 저장 공간이 같은 수로 동작합니다.
- 선로의 바위는 체력 15입니다. 곡괭이 일반 타격은 피해 1, X 타격은 피해 2이며 X를 맞히면 위치가 바뀝니다.
- 메인 씬의 손전등은 기존처럼 아이템을 주워 선택 슬롯에 들어야 사용합니다. 시작 시 기본 지급, 배터리, 왼손 손전등과 오른손 도구 동시 사용은 이번 통합에 포함되지 않았습니다.
- 깊이 시스템과 PDA는 추가하지 않았습니다.
- 원본 로컬 프로젝트는 별도 폴더에 보존했습니다. 이후 공동 개발은 이 통합 프로젝트를 기준으로 진행하면 됩니다.

## 검증

2026-11-15, Unity 6000.3.24f1에서 **EditMode 21/21, PlayMode 21/21** 통과했습니다. Windows 64비트 빌드도 성공했습니다. 실행 파일 시작 검사와 파일 보존 검사 결과는 `Verification.txt`에 기록합니다.

MineSurvival/Tests는 기존 이동·충돌·앉기·입력 잠금·스태미나 회귀 검사입니다. MinePrototype/Tests/PlayMode/PlayerIntegrationTests.cs의 9개 검사는 메인 맵을 직접 열어 단일 이동 담당, 획득 거리·벽 차단, 슬롯 이동, 배낭 내용물 보존, 입력 잠금·취소·창 전환을 확인합니다. 두 씬의 컴포넌트 연결과 에셋 GUID 중복 없음도 확인했습니다.

씬·프리팹의 누락 스크립트, URP 설정의 누락 타입, 머티리얼 셰이더를 검사하고 Direct3D11로 메인 맵을 렌더링해 확인했습니다. 배치 에디터는 실제 커서를 잠글 수 없어 통합 검사에서 OS 커서 상태 읽기만 대체했습니다. 실제 키보드·마우스의 손맛과 UI 클릭은 수동 플레이 검증이 남아 있습니다. 빌드 중 선택 패키지의 라이선스 조회 경고가 기록되었지만 검사 및 빌드는 성공했습니다.

작업 브랜치: compat/unity-6000.3.24f1. 원격 main은 변경하지 않았습니다. 실행 파일은 Builds/Integrated/ABYSS_RESCUE.exe이며, Git에 포함하지 않는 로컬 빌드입니다.

씬의 연결은 이미 저장되어 있습니다. 편집기 메뉴 ABYSS RESCUE/Apply player and stamina integration은 이전 팀 씬에 같은 연결을 다시 적용할 때만 사용합니다.

## 6000.3.24f1로 이전한 내용

기존 6000.6 통합 프로젝트를 복사한 뒤 이 폴더에서만 작업했습니다. 기존 `Unity_3d_practice`와 최초 로컬 `MineSurvivalPrototype`은 보존했습니다. 이후 팀 작업에는 이 폴더의 Assets, Packages, ProjectSettings를 함께 사용합니다. ProjectVersion.txt만 변경하면 패키지와 저장 형식의 차이가 남습니다.

| 패키지 | 이전 버전 | 현재 버전 |
| --- | --- | --- |
| Universal RP | 17.6.0 | 17.3.0 |
| uGUI / TextMeshPro | 2.6.0 | 2.0.0 |
| Timeline | 6.6.0 | 1.8.13 |
| Animation Rigging | 6.6.0 | 1.4.1 |
| Test Framework | 1.8.0 | 1.6.0 |

Input System 1.20.0 등 공통 호환 패키지는 유지했습니다. 6000.6 전용 내장 모듈 참조는 6000.3 구성에 맞췄습니다. 패키지 목록과 잠금 파일을 함께 저장했습니다.

- URP 전역 설정을 6000.3에서 읽을 수 있는 리소스 형식으로 교체했습니다. 기존 에셋 GUID와 리플렉션 프로브 회전 설정은 유지했습니다.
- Physics 레이어 충돌 행렬은 기존 비트 값을 유지하고 저장 형식만 바꿨습니다. 새 버전의 매핑 형식을 그대로 두면 바닥·벽 충돌이 적용되지 않아 달리기·점프도 실패했습니다. 수정 후 전체 PlayMode 검사를 통과했습니다.
- Unity가 씬·렌더 설정을 해당 버전 형식으로 다시 저장했습니다. 동적 한글 폰트는 같은 원본 TTF를 유지하며 필요한 글자를 실행 중 생성합니다.
- 세 씬의 위치·회전·크기와 UI 배치를 비교했고, 실행 스크립트는 통합본과 동일합니다. 엔진 차이에 따른 모든 화면 효과의 픽셀 단위 동일성이나 성능은 비교하지 않았습니다.

확인한 공식 문서: [Layer-based collision detection](https://docs.unity3d.com/6000.3/Documentation/Manual/LayerBasedCollision.html), [Physics settings](https://docs.unity3d.com/6000.3/Documentation/Manual/class-PhysicsManager.html). 레이어 충돌 행렬의 의미를 확인했고, 실제 저장 형식과 수정 결과는 두 프로젝트 파일 및 PlayMode 결과로 검증했습니다.
