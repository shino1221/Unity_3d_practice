using System.Collections;
using System.Reflection;
using MineSurvival;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MinePrototype.Tests
{
    // 원래 팀 맵을 직접 열어 이동 코드와 인벤토리 사이의 실제 참조/충돌/입력 잠금을 검사합니다.
    public sealed class PlayerIntegrationTests
    {
        Player player;
        Inventory inventory;
        CapsuleMovement movement;

        [UnitySetUp] public IEnumerator SetUp()
        {
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("MinePrototype");
            yield return null;
            player = Player.Instance;
            Assert.That(player, Is.Not.Null);
            inventory = player.Inventory;
            movement = player.Movement;
            player.Controls.enabled = false; // 실사용자의 키 입력을 검사에 섞지 않습니다.
            // 배치 에디터는 Cursor.lockState를 None으로 강제합니다. OS 커서 읽기만 대체하고
            // 배낭·메뉴·행동·사망·포커스 잠금은 모두 실제 코드로 검사합니다.
            typeof(CapsulePlayerInput).GetField("readCursorLocked", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(player.Controls, (System.Func<bool>)(() => true));
            player.CaptureCursor(true);
            for (int i = 0; i < 8; i++) { movement.Move(Vector2.zero, false, false, .02f); yield return null; }
            ResumeWorldInput();
        }

        // 배치 실행은 게임 창 포커스가 없으므로, 키 입력 대신 포커스 복귀와 행동 요청을 직접 전달합니다.
        void ResumeWorldInput()
        {
            player.Controls.SendMessage("OnApplicationFocus", true);
            player.CaptureCursor(true);
            Assert.That(player.WorldControls, Is.True,
                $"World input unavailable: cursor={Cursor.lockState}, menu={player.Controls.MenuOpen}, bag={inventory.IsOpen}, time={Time.timeScale}");
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            player.Controls.SetMenuOpen(false);
            Time.timeScale = 1;
            yield return null;
        }

        TestItem MakeItem(float distance = 1.8f)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = "Integration test item";
            obj.transform.localScale = Vector3.one * .2f;
            var view = player.GetComponentInChildren<Camera>().transform;
            obj.transform.position = view.position + view.forward * distance;
            obj.AddComponent<Rigidbody>().isKinematic = true;
            var item = obj.AddComponent<TestItem>();
            Physics.SyncTransforms();
            return item;
        }

        Backpack AddBag()
        {
            var bag = Object.FindFirstObjectByType<Backpack>();
            Assert.That(inventory.Add(bag), Is.True);
            return bag;
        }

        Flashlight AddLight()
        {
            var light = Object.FindFirstObjectByType<Flashlight>();
            Assert.That(inventory.Add(light), Is.True);
            return light;
        }

        [UnityTest] public IEnumerator TeamSceneUsesOneMotorAndOneSharedStamina()
        {
            Assert.That(player.GetComponent<Rigidbody>(), Is.Null);
            Assert.That(player.GetComponent<CapsuleCollider>(), Is.Null);
            Assert.That(player.GetComponent<PlayerInput>(), Is.Null);
            Assert.That(player.GetComponents<CharacterController>().Length, Is.EqualTo(1));
            Assert.That(player.GetComponents<PlayerStamina>().Length, Is.EqualTo(1));
            Assert.That(player.Stamina, Is.SameAs(movement.Stamina));
            Assert.That(player.GetComponent<FlashlightInteraction>(), Is.Null, "The inventory owns the team flashlight.");
            float start = player.transform.position.z;
            for (int i = 0; i < 25; i++) { movement.Move(Vector2.up, true, false, .02f); yield return null; }
            Assert.That(player.transform.position.z - start, Is.EqualTo(3.2f).Within(.1f));
            Assert.That(player.Stamina.Current, Is.EqualTo(96).Within(.01f));
            movement.Move(Vector2.zero, false, true, .02f);
            Assert.That(player.Stamina.Current, Is.EqualTo(86).Within(.01f));
        }

        [UnityTest] public IEnumerator PickupRequiresTwoPointFiveMetersAndAnUnblockedRay()
        {
            var item = MakeItem(2.8f);
            Assert.That(inventory.TryPickUpTarget(), Is.False);
            var view = player.GetComponentInChildren<Camera>().transform;
            item.transform.position = view.position + view.forward * 2.4f;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = view.position + view.forward;
            wall.transform.localScale = Vector3.one * .5f;
            Physics.SyncTransforms();
            Assert.That(inventory.TryPickUpTarget(), Is.False);
            Object.Destroy(wall);
            yield return null;
            Physics.SyncTransforms();
            ResumeWorldInput();
            Assert.That(inventory.TryPickUpTarget(), Is.True);
            Assert.That(inventory.At(0), Is.SameAs(item));
            Assert.That(item.IsCarried, Is.True);
            Assert.That(inventory.TryPickUpTarget(), Is.False, "The same pickup cannot be duplicated.");
        }

        [UnityTest] public IEnumerator ItemSwapAndDropPreserveInventoryContents()
        {
            var bag = AddBag();
            var first = MakeItem();
            var second = MakeItem();
            Assert.That(inventory.Add(first), Is.True);
            Assert.That(inventory.Add(second), Is.True);
            Assert.That(inventory.Move(0, 3), Is.True);
            Assert.That(bag.Items[0], Is.SameAs(first));
            Assert.That(inventory.Move(3, 1), Is.True);
            Assert.That(bag.Items[0], Is.SameAs(second));
            Assert.That(inventory.At(1), Is.SameAs(first));
            Assert.That(inventory.Drop(1), Is.True);
            Assert.That(first.IsCarried, Is.False);
            Assert.That(inventory.At(1), Is.Null);
            Assert.That(bag.Items[0], Is.SameAs(second));
            yield return null;
        }

        [UnityTest] public IEnumerator OpenBagLocksMovementButAllowsRecoveryAndKeepsTheLightState()
        {
            AddBag();
            var light = AddLight();
            Assert.That(light.TryToggle(), Is.True);
            bool on = light.IsOn;
            inventory.ToggleOpen();
            Assert.That(inventory.IsOpen, Is.True);
            Assert.That(player.Controls.InventoryOpen, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(player.Controls.Look.LookEnabled, Is.False);
            Assert.That(light.TryToggle(), Is.False);
            player.Stamina.Consume(50);
            Vector3 start = player.transform.position;
            for (int i = 0; i < 175; i++) { movement.Move(Vector2.one, true, true, .02f); yield return null; }
            Assert.That(player.transform.position.x, Is.EqualTo(start.x));
            Assert.That(player.transform.position.z, Is.EqualTo(start.z));
            Assert.That(player.Stamina.Current, Is.EqualTo(55).Within(.02f));
            Assert.That(light.IsOn, Is.EqualTo(on));
            player.Controls.SetMenuOpen(true);
            Assert.That(Time.timeScale, Is.Zero);
            player.Controls.SetMenuOpen(false);
            Assert.That(movement.MovementEnabled, Is.False, "Closing menu must leave the bag lock active.");
            inventory.ToggleOpen();
            Assert.That(movement.MovementEnabled, Is.True);
            Assert.That(light.IsOn, Is.EqualTo(on));
        }

        [UnityTest] public IEnumerator BagHoldNeedsTwoSecondsAndCancelDoesNotToggleTheLight()
        {
            var bag = AddBag();
            var light = AddLight();
            bool on = light.IsOn;
            bag.AdvanceThrowInput(true, false, false, 1.99f);
            Assert.That(inventory.HoldingBag, Is.False);
            bag.AdvanceThrowInput(true, false, false, .02f);
            Assert.That(inventory.HoldingBag, Is.True);
            bag.AdvanceThrowInput(true, false, true, 0);
            Assert.That(inventory.HoldingBag, Is.False);
            Assert.That(light.TryToggle(), Is.False, "The cancelling right click cannot reach the restored flashlight.");
            Assert.That(light.IsOn, Is.EqualTo(on));
            bag.AdvanceThrowInput(true, false, false, 3);
            Assert.That(inventory.HoldingBag, Is.False, "Holding Q after cancellation cannot restart the throw.");
            bag.AdvanceThrowInput(false, true, false, 0);
            Assert.That(inventory.Backpack, Is.SameAs(bag));
            yield return null;
            ResumeWorldInput();
            Assert.That(light.TryToggle(), Is.True);
        }

        [UnityTest] public IEnumerator LosingFocusCancelsBagHoldAndRequiresRelease()
        {
            var bag = AddBag();
            var light = AddLight();
            bool on = light.IsOn;
            bag.AdvanceThrowInput(true, false, false, 1.5f);
            player.Controls.SendMessage("OnApplicationFocus", false);
            Assert.That(inventory.CanAct, Is.False);
            Assert.That(player.CanManageInventory, Is.False);
            ResumeWorldInput();
            bag.AdvanceThrowInput(true, false, false, 3);
            Assert.That(inventory.HoldingBag, Is.False, "Focus return cannot resume the old held key.");
            bag.AdvanceThrowInput(false, true, false, 0);
            bag.AdvanceThrowInput(true, false, false, 2);
            Assert.That(inventory.HoldingBag, Is.True);
            player.Controls.SendMessage("OnApplicationFocus", false);
            Assert.That(inventory.HoldingBag, Is.False);
            Assert.That(inventory.Backpack, Is.SameAs(bag));
            Assert.That(light.IsOn, Is.EqualTo(on));
            yield return null;
        }

        [UnityTest] public IEnumerator DroppedBagKeepsItsItemsWhenPickedUpAgain()
        {
            var bag = AddBag();
            var item = MakeItem();
            inventory.Add(item);
            inventory.Move(0, 3);
            bag.AdvanceThrowInput(true, false, false, 2);
            Assert.That(inventory.HoldingBag, Is.True);
            bag.AdvanceThrowInput(false, true, false, 0);
            Assert.That(inventory.Backpack, Is.Null);
            Assert.That(bag.Items[0], Is.SameAs(item));
            Assert.That(inventory.Add(bag), Is.True);
            Assert.That(inventory.At(3), Is.SameAs(item));
            yield return null;
        }

        [UnityTest] public IEnumerator ItemDropHoldCancelsAndExecutesOnlyOnce()
        {
            var item = MakeItem();
            inventory.Add(item);
            inventory.AdvanceDropHold(true, 1.9f);
            inventory.AdvanceDropHold(false, 0);
            inventory.AdvanceDropHold(true, 1.9f);
            Assert.That(inventory.At(0), Is.SameAs(item));
            inventory.AdvanceDropHold(true, .11f);
            Assert.That(inventory.At(0), Is.Null);
            inventory.Add(item);
            inventory.AdvanceDropHold(true, 3);
            Assert.That(inventory.At(0), Is.SameAs(item), "A held key cannot drop a newly acquired item again.");
            yield return null;
        }

        [UnityTest] public IEnumerator ActionAndDeathBlockInventoryButCrouchRemainsAllowedDuringRescue()
        {
            AddBag();
            var light = AddLight();
            player.Controls.SetActionInProgress(true);
            movement.SetCrouched(true);
            Assert.That(movement.IsCrouched, Is.True);
            inventory.Select(1);
            Assert.That(inventory.Selected, Is.Zero);
            inventory.ToggleOpen();
            Assert.That(inventory.IsOpen, Is.False);
            Assert.That(inventory.BeginBagHold(), Is.False);
            Assert.That(inventory.Drop(0), Is.False);
            player.Controls.SetActionInProgress(false);
            Assert.That(light.TryToggle(), Is.False, "Completing/cancelling an action must not also toggle the lamp.");
            yield return null;
            ResumeWorldInput();
            Assert.That(light.TryToggle(), Is.True);
            player.Stamina.ReduceMaximum(100);
            Assert.That(inventory.CanAct, Is.False);
            Assert.That(inventory.Drop(0), Is.False);
            Assert.That(light.TryToggle(), Is.False);
        }
    }

    public sealed class TestItem : Item { }
}
