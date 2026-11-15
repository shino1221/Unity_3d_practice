using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MineSurvival.Tests
{
    // 실제 프레임을 기다리지 않고 정해진 시간/비용을 전달해 공통 잔량과 회복 규칙을 검사합니다.
    public sealed class PlayerStaminaTests
    {
        GameObject actor;
        PlayerStamina stamina;
        [SetUp] public void SetUp()
        {
            actor = new GameObject("StaminaTest");
            stamina = actor.AddComponent<PlayerStamina>();
        }
        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(actor);

        [Test] public void SharedCostsRejectUnaffordableActionsWithoutSpending()
        {
            Assert.That(stamina.Current, Is.EqualTo(100));
            Assert.That(stamina.TryConsume(60), Is.True);
            Assert.That(stamina.TryConsume(50), Is.False);
            Assert.That(stamina.Current, Is.EqualTo(40).Within(.001f));
            stamina.Consume(1000);
            Assert.That(stamina.Current, Is.Zero);
            Assert.That(stamina.IsExhausted, Is.True);
        }

        [Test] public void RecoveryWaitsThenUsesOnlyTheTimeAfterTheDelay()
        {
            stamina.Consume(50);
            stamina.Recover(2.75f);
            Assert.That(stamina.Current, Is.EqualTo(50).Within(.001f));
            stamina.Recover(.75f); // 남은 대기 0.25초 + 실제 회복 0.5초.
            Assert.That(stamina.Current, Is.EqualTo(55).Within(.001f));
            stamina.Recover(10);
            Assert.That(stamina.Current, Is.EqualTo(stamina.Maximum));
        }

        [Test] public void FurtherUseRestartsDelayButFailedOrZeroCostDoesNot()
        {
            stamina.Consume(20);
            stamina.Recover(2.75f);
            Assert.That(stamina.TryConsume(20), Is.True);
            stamina.Recover(2.75f);
            Assert.That(stamina.Current, Is.EqualTo(60).Within(.001f));
            Assert.That(stamina.TryConsume(80), Is.False);
            Assert.That(stamina.TryConsume(0), Is.True);
            stamina.Recover(.75f);
            Assert.That(stamina.Current, Is.EqualTo(65).Within(.001f));
        }

        [Test] public void ExhaustionHasNoPercentageLockAndCurrentZeroIsNotDeath()
        {
            stamina.Consume(100);
            Assert.That(stamina.IsDead, Is.False);
            stamina.Recover(3.1f);
            Assert.That(stamina.Current, Is.EqualTo(1).Within(.001f));
            Assert.That(stamina.IsExhausted, Is.False);
            Assert.That(stamina.TryConsume(.5f), Is.True);
            Assert.That(stamina.TryConsume(1), Is.False);
        }

        [Test] public void RockfallsReduceMaximumAndBaseRestoresOnlyTheLostCapacity()
        {
            stamina.ReduceMaximum(PlayerStamina.RockfallDamage);
            Assert.That(stamina.Maximum, Is.EqualTo(80));
            Assert.That(stamina.Current, Is.EqualTo(80));
            stamina.Consume(30);
            stamina.SetInBase(true);
            stamina.Recover(2);
            Assert.That(stamina.Maximum, Is.EqualTo(82), "Base recovery begins immediately.");
            Assert.That(stamina.Current, Is.EqualTo(50), "Current recovery still waits three seconds.");
            stamina.Recover(2);
            Assert.That(stamina.Maximum, Is.EqualTo(84));
            Assert.That(stamina.Current, Is.EqualTo(60), "Current recovery remains ten per second.");
            stamina.SetInBase(false);
            stamina.Recover(3);
            Assert.That(stamina.Maximum, Is.EqualTo(84));
            Assert.That(stamina.Current, Is.EqualTo(84));
            stamina.SetInBase(true);
            stamina.Recover(100);
            Assert.That(stamina.Maximum, Is.EqualTo(100), "Base cannot exceed the configured starting maximum.");
        }

        [Test] public void MaximumDamagePreservesLowerCurrentBalanceAndDoesNotRestartRecovery()
        {
            stamina.TryConsume(70);
            stamina.Recover(2);
            stamina.ReduceMaximum(20);
            Assert.That(stamina.Current, Is.EqualTo(30));
            stamina.Recover(2);
            Assert.That(stamina.Current, Is.EqualTo(40));
        }

        [Test] public void MaximumZeroSignalsDeathOnceAndCannotRecoverAtBase()
        {
            int deaths = 0;
            stamina.Died += () => deaths++;
            for (int i = 0; i < 5; i++) stamina.ReduceMaximum(PlayerStamina.RockfallDamage);
            Assert.That(stamina.IsDead, Is.True);
            Assert.That(stamina.Normalized, Is.Zero);
            stamina.ReduceMaximum(20);
            stamina.SetInBase(true);
            stamina.Recover(100);
            Assert.That(stamina.Maximum, Is.Zero);
            Assert.That(stamina.Current, Is.Zero);
            Assert.That(stamina.TryConsume(0), Is.False);
            Assert.That(deaths, Is.EqualTo(1));
        }

        [Test] public void ToolCostsAreSharedAndDeniedActionsLeaveRemainingStamina()
        {
            Assert.That(stamina.TryConsume(PlayerStamina.PickaxeCost), Is.True);
            Assert.That(stamina.TryConsume(PlayerStamina.ShovelCost), Is.True);
            Assert.That(stamina.Current, Is.EqualTo(82));
            stamina.Consume(80);
            Assert.That(stamina.TryConsume(PlayerStamina.ShovelCost), Is.False);
            Assert.That(stamina.Current, Is.EqualTo(2));
        }

        [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidCostAndTimeNeverChangeTheBalance(float value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => stamina.Consume(value));
            Assert.Throws<ArgumentOutOfRangeException>(() => stamina.TryConsume(value));
            Assert.Throws<ArgumentOutOfRangeException>(() => stamina.Recover(value));
            Assert.Throws<ArgumentOutOfRangeException>(() => stamina.ReduceMaximum(value));
            Assert.That(stamina.Normalized, Is.EqualTo(1));
        }

        [Test] public void SavedPlayerPrefabContainsOneSharedStaminaComponent()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MineSurvival/Prefabs/CapsulePlayer.prefab");
            Assert.That(prefab.GetComponents<PlayerStamina>().Length, Is.EqualTo(1));
            var settings = new SerializedObject(prefab.GetComponent<PlayerStamina>());
            Assert.That(settings.FindProperty("recoveryDelay").floatValue, Is.EqualTo(3));
            Assert.That(settings.FindProperty("recoveryPerSecond").floatValue, Is.EqualTo(10));
            Assert.That(settings.FindProperty("baseMaximumRecoveryPerSecond").floatValue, Is.EqualTo(1));
            settings = new SerializedObject(prefab.GetComponent<CapsuleMovement>());
            Assert.That(settings.FindProperty("sprintCostPerSecond").floatValue, Is.EqualTo(8));
            Assert.That(settings.FindProperty("jumpCost").floatValue, Is.EqualTo(10));
        }

        [Test] public void GetKeyBackendAndMouseDeltaBackendAreEnabled()
        {
#if ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
            Assert.Pass();
#else
            Assert.Fail("Active Input Handling must be Both: GetKey buttons and Input System mouse delta.");
#endif
        }
    }
}
