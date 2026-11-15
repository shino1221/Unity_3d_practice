using System;
using MineSurvival;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MinePrototype.Editor
{
    // 기존 맵·소품·인벤토리 연결을 보존하면서 플레이어의 이동 담당만 교체하는 편집기 작업입니다.
    public static class IntegratePlayer
    {
        [MenuItem("ABYSS RESCUE/Apply player and stamina integration")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before integration.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            IntegrateScene("Assets/Scenes/MinePrototype.unity");
            IntegrateScene("Assets/_Recovery/0.unity");
            // 원본 저장소의 빌드 목록은 존재하지 않는 SampleScene을 가리키고 있었습니다.
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/MinePrototype.unity", true),
                new EditorBuildSettingsScene("Assets/MineSurvival/Scenes/MovementTest.unity", true)
            };
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene("Assets/Scenes/MinePrototype.unity");
            Debug.Log("ABYSS player/stamina integration saved in main and recovery scenes.");
        }

        static void IntegrateScene(string path)
        {
            var scene = EditorSceneManager.OpenScene(path);
            var players = UnityEngine.Object.FindObjectsByType<Player>(FindObjectsSortMode.None);
            if (players.Length != 1) throw new InvalidOperationException("Expected exactly one player in " + path);
            var player = players[0];
            var actor = player.gameObject;
            var camera = actor.GetComponentInChildren<Camera>();
            if (camera == null) throw new InvalidOperationException("Player camera is missing.");

            var oldInput = actor.GetComponent<PlayerInput>();
            if (oldInput != null) UnityEngine.Object.DestroyImmediate(oldInput);
            var oldBody = actor.GetComponent<Rigidbody>();
            if (oldBody != null) UnityEngine.Object.DestroyImmediate(oldBody);
            var oldCapsule = actor.GetComponent<CapsuleCollider>();
            var controller = actor.GetComponent<CharacterController>();
            if (controller == null)
            {
                controller = actor.AddComponent<CharacterController>();
                controller.height = oldCapsule != null ? oldCapsule.height : 1.8f;
                controller.center = oldCapsule != null ? oldCapsule.center : new Vector3(0, .9f, 0);
                controller.radius = oldCapsule != null ? oldCapsule.radius : .3f;
                controller.skinWidth = .03f;
                controller.stepOffset = .3f;
                controller.slopeLimit = 45;
            }
            if (oldCapsule != null) UnityEngine.Object.DestroyImmediate(oldCapsule);
            if (actor.GetComponent<PlayerStamina>() == null) actor.AddComponent<PlayerStamina>();
            if (actor.GetComponent<CapsuleMovement>() == null) actor.AddComponent<CapsuleMovement>();
            var look = camera.GetComponent<FirstPersonLook>();
            if (look == null) look = camera.gameObject.AddComponent<FirstPersonLook>();
            look.CharacterBody = actor.transform;
            var input = actor.GetComponent<CapsulePlayerInput>();
            if (input == null) input = actor.AddComponent<CapsulePlayerInput>();
            input.Look = look;

            var inventory = new SerializedObject(actor.GetComponent<Inventory>());
            inventory.FindProperty("pickupDistance").floatValue = 2.5f;
            inventory.ApplyModifiedPropertiesWithoutUndo();
            foreach (var label in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (label.name == "Controls Text")
                    label.text = "WASD 이동 · Shift 달리기 · Space 점프 · 왼쪽 Ctrl 앉기 · F 획득 · 우클릭 손전등\nX 2초 아이템 버리기 · Q 2초 배낭 들기 / 떼면 버리기 · 우클릭 취소 · Alt 커서 · Esc 일시정지";
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save " + path);
        }
    }
}
