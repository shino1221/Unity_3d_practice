using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MineSurvival.PlayTests
{
    // [역할] MovementTest 씬에서 실제 충돌·이동·앉기·손전등을 확인하는 PlayMode 테스트입니다.
    // 키보드를 직접 누르는 대신 함수에 입력을 전달합니다. 실제 UI 클릭이나 팀원 시스템 통합까지 검사하지는 않습니다.
    // Assert는 기대 조건을 확인하고, Within은 물리 계산의 작은 오차를 허용합니다.
    // Move에 전달하는 .02f는 계산상 0.02초입니다. yield return null은 Unity가 다음 프레임을 처리하도록 기다립니다.
    public sealed class MovementPlayTests
    {
        CapsuleMovement movement;
        [UnityTearDown] public IEnumerator TearDown()
        {
            movement.GetComponent<CapsulePlayerInput>().SetMenuOpen(false);
            Time.timeScale = 1;
            yield return null;
        }
        // 매 검사마다 테스트 씬을 새로 불러오고 실제 키보드 입력을 꺼, 사용자의 조작이 검사에 섞이지 않게 합니다.
        [UnitySetUp] public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("MovementTest");
            movement = Object.FindFirstObjectByType<CapsuleMovement>();
            Assert.That(movement.Stamina, Is.Not.Null, "The saved test scene must include shared stamina.");
            movement.GetComponent<CapsulePlayerInput>().enabled = false;
            movement.Teleport(new Vector3(0, .1f, 1));
            // 시작 지점에서 중력을 몇 번 적용해 바닥에 안정적으로 닿은 상태로 검사를 시작합니다.
            for (int i = 0; i < 8; i++) { movement.Move(Vector2.zero, false, false, .02f); yield return null; }
        }
        // 0.75배 속도에서 1초 동안 3m 이동하고, 오른쪽 벽을 향해 계속 걸어도 벽 앞에서 멈춰야 합니다.
        [UnityTest] public IEnumerator WalkModifierAndWallCollision()
        {
            movement.SetMovementMultiplier(.75f);
            float start = movement.transform.position.z;
            for (int i = 0; i < 50; i++) { movement.Move(Vector2.up, false, false, .02f); yield return null; }
            Assert.That(movement.transform.position.z - start, Is.EqualTo(3).Within(.1f));
            movement.Teleport(new Vector3(0, .05f, 2));
            for (int i = 0; i < 150; i++) { movement.Move(Vector2.right, false, false, .02f); yield return null; }
            Assert.That(movement.transform.position.x, Is.InRange(6.1f, 6.3f));
        }
        // 보정된 점프 높이에 도달하고, 공중에서 다시 점프를 요청해도 한 번 더 뛰지 않으며 바닥에 돌아와야 합니다.
        [UnityTest] public IEnumerator JumpUsesModifierCannotDoubleJumpAndLands()
        {
            movement.SetMovementMultiplier(.75f);
            float ground = movement.transform.position.y, peak = ground;
            movement.Move(Vector2.zero, false, true, .02f);
            Assert.That(movement.Stamina.Current, Is.EqualTo(90).Within(.001f));
            for (int i = 0; i < 65; i++)
            {
                // 여덟 번째 인덱스에서 점프를 다시 요청해 공중 점프가 막히는지도 함께 확인합니다.
                movement.Move(Vector2.zero, false, i == 8, .02f);
                peak = Mathf.Max(peak, movement.transform.position.y); yield return null;
            }
            Assert.That(peak - ground, Is.InRange(.75f, 1.01f));
            Assert.That(movement.transform.position.y, Is.EqualTo(ground).Within(.08f));
            Assert.That(movement.Stamina.Current, Is.EqualTo(90).Within(.001f), "Airborne requests must not pay again.");
        }
        // 이동 잠금 중에는 수평 이동/달리기가 막혀야 합니다. 해제 후 대각선 이동도 직선과 같은 총 속도여야 합니다.
        [UnityTest] public IEnumerator MovementLockAndDiagonalNormalization()
        {
            Vector3 start = movement.transform.position;
            movement.SetMovementEnabled(false);
            movement.Move(Vector2.one, true, true, .1f); yield return null;
            Assert.That(movement.transform.position.x, Is.EqualTo(start.x));
            Assert.That(movement.transform.position.z, Is.EqualTo(start.z));
            Assert.That(movement.IsSprinting, Is.False);
            Assert.That(movement.Stamina.Current, Is.EqualTo(100), "Locked jump and sprint must be free.");
            movement.SetMovementEnabled(true);
            for (int i = 0; i < 25; i++) { movement.Move(Vector2.one, false, false, .02f); yield return null; }
            Vector3 delta = movement.transform.position - start; delta.y = 0;
            Assert.That(delta.magnitude, Is.EqualTo(2).Within(.08f));
        }
        // 속도 보정은 달리기 비용을 바꾸지 않습니다. 소진 뒤 Shift를 계속 잡아도 자동 재시작하지 않아야 합니다.
        [UnityTest] public IEnumerator SprintUsesSharedStaminaAndRequiresRecoveryAndRelease()
        {
            movement.SetMovementMultiplier(.75f);
            for (int i = 0; i < 50; i++) { movement.Move(Vector2.up, true, false, .02f); yield return null; }
            Assert.That(movement.IsSprinting, Is.True);
            Assert.That(movement.Stamina.Current, Is.EqualTo(92).Within(.01f));
            movement.Stamina.Consume(91.99f);
            movement.Move(Vector2.up, true, false, .02f);
            Assert.That(movement.IsSprinting, Is.False);
            Assert.That(movement.Stamina.Current, Is.EqualTo(.01f).Within(.001f), "Unaffordable sprint must not spend the remainder.");
            for (int i = 0; i < 160; i++) { movement.Move(Vector2.zero, true, false, .02f); yield return null; }
            movement.Move(Vector2.down, true, false, .02f);
            Assert.That(movement.Stamina.Current, Is.InRange(1f, 5f));
            Assert.That(movement.IsSprinting, Is.False, "Holding Shift must not restart an exhausted sprint.");
            movement.Move(Vector2.zero, false, false, .02f);
            movement.Move(Vector2.down, true, false, .02f);
            Assert.That(movement.IsSprinting, Is.True);
        }

        // 장비가 같은 컴포넌트의 잔량을 쓰면 달리기도 즉시 영향을 받아야 합니다.
        [UnityTest] public IEnumerator ToolConsumptionBlocksSprintAndLockedMovementStillRecovers()
        {
            var shared = movement.GetComponent<PlayerStamina>();
            Assert.That(shared, Is.SameAs(movement.Stamina));
            Assert.That(shared.TryConsume(100), Is.True);
            movement.Move(Vector2.up, true, false, .02f);
            Assert.That(movement.IsSprinting, Is.False);
            movement.SetMovementEnabled(false);
            for (int i = 0; i < 170; i++) { movement.Move(Vector2.zero, true, false, .02f); yield return null; }
            Assert.That(shared.Current, Is.InRange(1f, 6f));
            movement.SetMovementEnabled(true);
            movement.Move(Vector2.up, true, false, .02f);
            Assert.That(movement.IsSprinting, Is.False, "Unlocking movement is not a Shift release.");
            movement.Move(Vector2.zero, false, false, .02f);
            movement.Move(Vector2.up, true, false, .02f);
            Assert.That(movement.IsSprinting, Is.True);
        }

        [UnityTest] public IEnumerator StandingAndCrouchingDoNotSpendStaminaAndZeroTimeDoesNothing()
        {
            var shared = movement.Stamina;
            movement.Move(Vector2.zero, true, false, .1f);
            Assert.That(shared.Current, Is.EqualTo(100));
            movement.SetCrouched(true);
            movement.Move(Vector2.up, true, true, .1f);
            Assert.That(shared.Current, Is.EqualTo(100));
            Assert.That(movement.IsSprinting, Is.False);
            shared.Consume(50);
            movement.Move(Vector2.up, true, true, 0);
            Assert.That(shared.Current, Is.EqualTo(50).Within(.001f));
            for (int i = 0; i < 175; i++) { movement.Move(Vector2.up, true, false, .02f); yield return null; }
            Assert.That(shared.Current, Is.EqualTo(55).Within(.1f));
        }

        [UnityTest] public IEnumerator UnaffordableJumpDoesNotExecuteOrResetRecovery()
        {
            movement.Stamina.Consume(95);
            Vector3 start = movement.transform.position;
            for (int i = 0; i < 100; i++) { movement.Move(Vector2.zero, false, true, .02f); yield return null; }
            Assert.That(movement.Stamina.Current, Is.EqualTo(5));
            Assert.That(movement.transform.position.y, Is.EqualTo(start.y).Within(.08f));
            for (int i = 0; i < 60; i++) { movement.Move(Vector2.zero, false, false, .02f); yield return null; }
            Assert.That(movement.Stamina.Current, Is.EqualTo(7).Within(.01f));
        }

        [UnityTest] public IEnumerator ShiftReleaseBeforeRecoveryStillAllowsTheNextPress()
        {
            movement.Stamina.Consume(100);
            movement.Move(Vector2.up, true, false, .02f);
            movement.Move(Vector2.zero, false, false, .02f);
            movement.Stamina.Recover(3.1f);
            movement.Move(Vector2.up, true, false, .02f);
            Assert.That(movement.IsSprinting, Is.True, "No 25 percent threshold, and an earlier release still counts.");
            yield return null;
        }

        [UnityTest] public IEnumerator ReducedMaximumDoesNotScaleSprintCostAndDeathStopsControl()
        {
            var stamina = movement.Stamina;
            stamina.ReduceMaximum(PlayerStamina.RockfallDamage);
            stamina.Consume(10);
            stamina.SetInBase(true);
            for (int i = 0; i < 50; i++) { movement.Move(Vector2.up, true, false, .02f); yield return null; }
            Assert.That(stamina.Current, Is.EqualTo(62).Within(.01f));
            Assert.That(stamina.Maximum, Is.EqualTo(81).Within(.01f), "Base capacity recovery also runs while sprinting.");
            stamina.ReduceMaximum(100);
            Vector3 start = movement.transform.position;
            movement.SetCrouched(true);
            movement.Move(Vector2.one, true, true, .1f);
            Assert.That(movement.transform.position.x, Is.EqualTo(start.x));
            Assert.That(movement.transform.position.z, Is.EqualTo(start.z));
            Assert.That(movement.IsCrouched, Is.False);
            Assert.That(movement.IsSprinting, Is.False);
        }

        [UnityTest] public IEnumerator UiAndActionLocksComposeAndOnlyTheMenuPausesTime()
        {
            var input = movement.GetComponent<CapsulePlayerInput>();
            var look = input.Look;
            input.SetActionInProgress(true);
            Assert.That(input.CanUseTools, Is.False);
            Assert.That(movement.MovementEnabled, Is.False);
            Assert.That(look.LookEnabled, Is.False);
            movement.SetCrouched(true);
            Assert.That(movement.IsCrouched, Is.True, "Rescue and treatment allow crouching.");
            movement.Stamina.Consume(50);
            input.SetInventoryOpen(true);
            Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            for (int i = 0; i < 160; i++) { movement.Move(Vector2.zero, false, false, .02f); yield return null; }
            Assert.That(movement.Stamina.Current, Is.EqualTo(52).Within(.01f));
            input.SetMenuOpen(true);
            yield return null;
            Assert.That(Time.timeScale, Is.Zero);
            float before = movement.Stamina.Current;
            movement.Move(Vector2.up, true, true, Time.deltaTime);
            Assert.That(movement.Stamina.Current, Is.EqualTo(before));
            input.SetMenuOpen(false);
            Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(movement.MovementEnabled, Is.False, "Closing menu cannot unlock an open inventory/action.");
            input.SetInventoryOpen(false);
            Assert.That(movement.MovementEnabled, Is.False);
            input.SetActionInProgress(false);
            Assert.That(input.CanUseTools, Is.True);
            Assert.That(movement.MovementEnabled, Is.True);
            Assert.That(look.LookEnabled, Is.True);
        }

        // 앉았을 때 몸/눈높이/속도가 바뀌고 발 위치가 유지되는지, 달리기/점프가 막히는지 확인합니다.
        // 천장 아래에서는 일어서지 못하지만, 넓은 공간으로 나오면 원래 눈높이로 돌아와야 합니다.
        [UnityTest] public IEnumerator CrouchLowersViewAndKeepsFeetAndCannotStandThroughCeiling()
        {
            var camera = movement.GetComponentInChildren<Camera>();
            var controller = movement.GetComponent<CharacterController>();
            float standingEye = camera.transform.localPosition.y;
            float feet = controller.center.y - controller.height * .5f;
            movement.SetCrouched(true);
            Assert.That(controller.height, Is.EqualTo(1.05f).Within(.001f));
            Assert.That(camera.transform.localPosition.y, Is.EqualTo(standingEye - .75f).Within(.001f));
            Assert.That(controller.center.y - controller.height * .5f, Is.EqualTo(feet).Within(.001f));
            float start = movement.transform.position.z;
            for (int i = 0; i < 20; i++) { movement.Move(Vector2.up, true, true, .02f); yield return null; }
            Assert.That(movement.transform.position.z - start, Is.EqualTo(.8f).Within(.08f));
            Assert.That(movement.IsSprinting, Is.False);
            Assert.That(movement.IsGrounded, Is.True);
            // 순간이동한 위치를 물리 엔진에 즉시 반영한 뒤, 낮은 천장과의 겹침을 검사합니다.
            movement.Teleport(new Vector3(3, .03f, 17));
            Physics.SyncTransforms(); movement.SetCrouched(false);
            Assert.That(movement.IsCrouched, Is.True, "Low ceiling must prevent standing.");
            movement.Teleport(new Vector3(0, .03f, 3));
            Physics.SyncTransforms(); movement.SetCrouched(false);
            Assert.That(movement.IsCrouched, Is.False);
            Assert.That(camera.transform.localPosition.y, Is.EqualTo(standingEye).Within(.001f));
        }

        // 미획득 사용 금지 → 먼 거리 획득 실패 → 벽 뒤 획득 실패 → 정상 획득 순서로 확인합니다.
        // 이어서 중복 획득 방지, 켜기/끄기, 시선 방향 추적, 앉았을 때 조명 높이 변화까지 확인합니다.
        [UnityTest] public IEnumerator FlashlightNeedsClearReachablePickupAndFollowsCamera()
        {
            var flashlight = movement.GetComponent<FlashlightInteraction>();
            var camera = movement.GetComponentInChildren<Camera>();
            var pickup = Object.FindFirstObjectByType<FlashlightPickup>();
            flashlight.Toggle(); Assert.That(flashlight.IsOn, Is.False);
            movement.Teleport(new Vector3(2, .03f, 0));
            camera.transform.LookAt(pickup.transform.position); Physics.SyncTransforms();
            Assert.That(flashlight.TryPickUp(), Is.False, "Distant items cannot be collected.");
            movement.Teleport(new Vector3(2, .03f, 3));
            camera.transform.LookAt(pickup.transform.position); Physics.SyncTransforms();
            // 카메라와 손전등 사이에 임시 벽을 놓아 거리만 맞으면 벽을 무시하는 오류가 없는지 검사합니다.
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.transform.position = (camera.transform.position + pickup.transform.position) * .5f;
            blocker.transform.localScale = Vector3.one * .5f; Physics.SyncTransforms();
            Assert.That(flashlight.TryPickUp(), Is.False, "Walls must block collection.");
            // Destroy는 즉시 삭제가 아니므로 한 프레임 기다린 뒤 벽이 없어진 상태에서 다시 획득합니다.
            Object.Destroy(blocker); yield return null; Physics.SyncTransforms();
            Assert.That(flashlight.TryPickUp(), Is.True);
            Assert.That(pickup.gameObject.activeSelf, Is.False);
            Assert.That(flashlight.IsOn, Is.False);
            Assert.That(flashlight.TryPickUp(), Is.False);
            flashlight.Toggle(); Assert.That(flashlight.IsOn, Is.True);
            camera.GetComponent<FirstPersonLook>().Look(new Vector2(400, -180));
            var beam = camera.transform.Find("FlashlightBeam");
            Assert.That(Vector3.Angle(beam.forward, camera.transform.forward), Is.LessThan(.01f));
            movement.SetCrouched(true);
            Assert.That(beam.position.y, Is.LessThan(1.2f));
            flashlight.Toggle(); Assert.That(flashlight.IsOn, Is.False);
        }
    }
}
