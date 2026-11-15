using System;
using NUnit.Framework;
using UnityEngine;

namespace MineSurvival.Tests
{
    // [역할] 씬을 플레이하지 않고 시점 계산과 외부 보정값의 약속을 확인하는 EditMode 테스트입니다.
    // Test/TestCase가 붙은 함수가 검사 항목이며, Assert는 '실제 결과가 이 조건이어야 한다'는 확인문입니다.
    // 실패하면 예상한 동작과 코드가 달라졌다는 뜻입니다. 테스트가 게임 기능을 구현하는 것은 아닙니다.
    public sealed class CameraContractTests
    {
        GameObject actor, camera;
        FirstPersonLook look;
        CapsuleMovement movement;
        // 각 검사 전에 새 몸과 카메라를 준비합니다. 이전 검사의 회전/보정 상태가 다음 검사에 섞이지 않게 합니다.
        [SetUp] public void SetUp()
        {
            actor = new GameObject("Character"); movement = actor.AddComponent<CapsuleMovement>();
            camera = new GameObject("Camera"); camera.transform.SetParent(actor.transform, false);
            look = camera.AddComponent<FirstPersonLook>(); look.CharacterBody = actor.transform;
        }
        // 검사가 끝나면 임시 몸과 자식 카메라를 함께 정리합니다.
        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(actor);
        // 큰 마우스 입력에도 위아래 각도가 ±85도로 제한되고, 좌우 회전은 몸에 적용되며 화면은 기울지 않아야 합니다.
        [Test] public void PitchClampsWithoutRollingAndYawRotatesBody()
        {
            look.Look(new Vector2(100, 10000));
            Assert.That(look.Pitch, Is.EqualTo(-85));
            Assert.That(actor.transform.eulerAngles.y, Is.EqualTo(8).Within(.001));
            Assert.That(camera.transform.localEulerAngles.z, Is.EqualTo(0).Within(.001));
            look.Look(new Vector2(0, -20000));
            Assert.That(look.Pitch, Is.EqualTo(85));
        }
        // 시점을 잠근 뒤 마우스 입력을 전달해도 몸과 카메라가 돌아가지 않는지 확인합니다.
        [Test] public void LookLockPreservesRotation()
        {
            look.SetLookEnabled(false); look.Look(new Vector2(500, 500));
            Assert.That(actor.transform.rotation, Is.EqualTo(Quaternion.identity));
            Assert.That(look.Pitch, Is.Zero);
        }
        // 한 번에 100만큼 움직인 결과와 10씩 열 번 움직인 결과가 같아야 합니다.
        // 마우스를 여러 프레임에 나누어 움직여도 총 회전량이 달라지지 않는지 확인합니다.
        [Test] public void MouseDeltaPartitionsHaveEqualSensitivity()
        {
            look.Look(new Vector2(100, 100)); float yaw = actor.transform.eulerAngles.y; float pitch = look.Pitch;
            look.Look(new Vector2(-100, -100));
            for (int i = 0; i < 10; i++) look.Look(new Vector2(10, 10));
            Assert.That(actor.transform.eulerAngles.y, Is.EqualTo(yaw).Within(.001));
            Assert.That(look.Pitch, Is.EqualTo(pitch).Within(.001));
        }
        // 같은 검사를 세 보정값으로 반복합니다. 1은 기본, 0.9/0.75는 이동 능력이 줄어든 상황의 예입니다.
        // 외부 장비 등에서 전달한 배수가 속도/높이에 적용되는지 확인합니다.
        [TestCase(1)] [TestCase(.9f)] [TestCase(.75f)]
        public void ExternalModifierScalesSpeedAndJumpHeight(float value)
        {
            movement.SetMovementMultiplier(value);
            Assert.That(movement.EffectiveWalkSpeed, Is.EqualTo(4 * value).Within(.001));
            Assert.That(movement.EffectiveJumpHeight, Is.EqualTo(1.2f * value).Within(.001));
        }
        // 0, NaN, 무한대 같은 잘못된 입력은 예외로 거부하고, 기존의 정상 보정값은 유지해야 합니다.
        [Test] public void InvalidExternalModifiersAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => movement.SetMovementMultiplier(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => movement.SetMovementMultiplier(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => look.Look(new Vector2(float.PositiveInfinity, 0)));
            Assert.That(movement.MovementMultiplier, Is.EqualTo(1));
        }
    }
}
