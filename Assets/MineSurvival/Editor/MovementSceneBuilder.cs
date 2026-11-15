using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MineSurvival.Editor
{
    // [역할] Unity 상단 메뉴에서 조작 테스트 씬을 만들고, 발표용 손전등/환경을 배치하는 편집 도구입니다.
    // Editor 폴더에 있으므로 게임을 실행하는 동안 매 프레임 동작하는 코드가 아닙니다.
    // 플레이어의 실제 동작은 Runtime 폴더의 코드가 담당하고, 이 파일은 그 코드에 필요한 오브젝트를 연결합니다.
    public static class MovementSceneBuilder
    {
        const string Root = "Assets/MineSurvival";
        const string Scene = Root + "/Scenes/MovementTest.unity";
        // [실행 전 확인] 새 테스트 씬을 만드는 메뉴입니다. 기존 MovementTest가 있으면 덮어쓰지 않고 중단합니다.
        // 재질과 TMP 폰트가 아래 경로에 준비되어 있어야 하며, 완료 시 빌드 씬 목록을 MovementTest 하나로 설정합니다.
        [MenuItem("Mine Prototype/Create Movement Test Scene")]
        public static void Create()
        {
            if (File.Exists(Scene)) throw new InvalidOperationException("Open the existing MovementTest scene; it will not be overwritten.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // 미리 준비된 재질/글꼴을 불러와, 생성하는 물체와 조작 안내에 사용합니다.
            var rock = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Rock.mat");
            var floor = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Floor.mat");
            var timber = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Timber.mat");
            var amber = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Amber.mat");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.27f, .3f, .36f);
            RenderSettings.skybox = null;
            // 복도 모양의 바닥과 벽을 만듭니다. Box는 아래에 정의한 '충돌체가 있는 상자 만들기' 보조 함수입니다.
            Box("Floor", new Vector3(0, -.25f, 12), new Vector3(14, .5f, 28), floor);
            Box("LeftWall", new Vector3(-7, 2, 12), new Vector3(1, 4, 28), rock);
            Box("RightWall", new Vector3(7, 2, 12), new Vector3(1, 4, 28), rock);
            Box("BackWall", new Vector3(0, 2, -2), new Vector3(14, 4, 1), rock);
            Box("EndWall", new Vector3(0, 2, 26), new Vector3(14, 4, 1), rock);
            // 일정 간격마다 광산 지지대와 등을 배치해 이동 방향과 거리감을 알아보기 쉽게 합니다.
            for (int z = 4; z < 26; z += 7)
            {
                Box("LeftSupport", new Vector3(-6.2f, 1.7f, z), new Vector3(.35f, 3.4f, .4f), timber);
                Box("RightSupport", new Vector3(6.2f, 1.7f, z), new Vector3(.35f, 3.4f, .4f), timber);
                Box("Crossbeam", new Vector3(0, 3.5f, z), new Vector3(12.8f, .35f, .4f), timber);
                var lamp = new GameObject("MineLamp").AddComponent<Light>();
                lamp.type = LightType.Point; lamp.transform.position = new Vector3(0, 3, z);
                lamp.color = new Color(1, .68f, .36f); lamp.intensity = 4; lamp.range = 12;
            }
            // 점프용 상자, 경사로, 낮은 천장을 배치합니다. PlayMode 테스트도 이 씬의 지형/위치를 사용합니다.
            Box("JumpBlock", new Vector3(3, .3f, 10), new Vector3(2, .6f, 2), timber);
            var slope = Box("Slope", new Vector3(-3, .65f, 13), new Vector3(3, .4f, 6), floor);
            slope.transform.rotation = Quaternion.Euler(-15, 0, 0);
            Box("SlopeLanding", new Vector3(-3, .7f, 18), new Vector3(3, 1.4f, 4), floor);
            Box("CeilingTest", new Vector3(3, 2.25f, 17), new Vector3(3, .4f, 4), timber);
            var sun = new GameObject("AmbientLight").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = .6f; sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(55, -25, 0);
            // 몸의 충돌과 이동을 담당하는 플레이어 루트입니다. 기본 크기를 유지하고 발 위치를 원점으로 맞춥니다.
            var actor = new GameObject("CapsulePlayer", typeof(CharacterController), typeof(CapsuleMovement));
            // 2번은 Unity 기본 Ignore Raycast 레이어입니다. 손전등 획득 검사에서 자기 몸을 먼저 맞히지 않게 합니다.
            actor.layer = 2;
            var controller = actor.GetComponent<CharacterController>();
            controller.height = 1.8f; controller.radius = .3f; controller.center = Vector3.up * .9f;
            controller.stepOffset = .3f; controller.skinWidth = .03f; controller.minMoveDistance = 0;
            // 눈에 보이는 캡슐은 나중에 캐릭터 모델로 교체할 수 있습니다.
            // 별도 충돌체는 제거하여 실제 충돌을 위의 CharacterController 하나가 담당하게 합니다.
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Visual_Capsule_Replaceable"; body.transform.SetParent(actor.transform, false);
            body.transform.localPosition = Vector3.up * .9f; body.transform.localScale = new Vector3(.6f, .9f, .6f);
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().sharedMaterial = amber;
            body.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            // 카메라를 몸의 자식으로 놓습니다. 몸이 움직이거나 좌우로 돌면 카메라도 같이 따라갑니다.
            var camera = new GameObject("PlayerCamera").AddComponent<Camera>();
            camera.transform.SetParent(actor.transform, false); camera.transform.localPosition = Vector3.up * 1.6f;
            camera.tag = "MainCamera"; camera.nearClipPlane = .05f; camera.fieldOfView = 75;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f, .045f, .06f);
            camera.gameObject.AddComponent<AudioListener>();
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            // 카메라에는 시점 코드, 몸에는 입력 코드를 붙이고 서로 연결합니다.
            var look = camera.gameObject.AddComponent<FirstPersonLook>(); look.CharacterBody = actor.transform;
            actor.AddComponent<CapsulePlayerInput>().Look = look;
            actor.transform.position = new Vector3(0, .1f, 1);
            // 화면 전체의 색감과 밝은 부분의 번짐을 설정합니다. 플레이어 조작 자체와는 별개인 화면 효과입니다.
            var volume = new GameObject("LightingVolume").AddComponent<Volume>(); volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.Add<Tonemapping>(true).mode.value = TonemappingMode.ACES;
            var bloom = profile.Add<Bloom>(true); bloom.intensity.value = .5f; bloom.threshold.value = .9f;
            AssetDatabase.CreateAsset(profile, Root + "/Settings/LightingProfile.asset");
            foreach (var effect in profile.components) AssetDatabase.AddObjectToAsset(effect, profile);
            volume.sharedProfile = profile;
            // 화면 하단에 고정되는 조작 안내입니다. 공간에 떠 있는 3D 글자와는 다른 UI입니다.
            var canvas = new GameObject("Controls", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            var controls = new GameObject("ControlsText").AddComponent<TextMeshProUGUI>(); controls.transform.SetParent(canvas.transform, false);
            controls.font = font; controls.fontSize = 18; controls.alignment = TextAlignmentOptions.Center;
            controls.text = "WASD Move   Mouse Look   Shift Sprint   Space Jump   Esc Pause / Resume";
            controls.raycastTarget = false;
            var rect = controls.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0); rect.pivot = new Vector2(.5f, 0);
            rect.anchoredPosition = new Vector2(0, 22); rect.sizeDelta = new Vector2(1240, 36);
            // 손전등과 발표 환경까지 구성한 뒤, 재사용할 플레이어 프리팹과 씬/에셋을 저장합니다.
            PreparePresentation();
            PrefabUtility.SaveAsPrefabAsset(actor, Root + "/Prefabs/CapsulePlayer.prefab");
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), Scene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Scene, true) };
            AssetDatabase.SaveAssets();
        }
        // 기본 Cube는 BoxCollider를 포함합니다. 이름·위치·크기·재질을 지정해 벽이나 발판을 쉽게 만듭니다.
        static GameObject Box(string name, Vector3 position, Vector3 scale, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = name;
            box.transform.position = position; box.transform.localScale = scale; box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }
        // [실행 전 확인] 기존 테스트 씬을 발표용으로 정리하는 메뉴입니다. 실행 중에는 사용할 수 없습니다.
        // 아래 이름의 플레이어/안내 UI/재질 및 URP 설정이 있는 테스트 씬을 전제로 합니다.
        // 이 메뉴만 실행하면 씬을 변경 표시(Dirty)하며, 씬과 프리팹을 자동으로 함께 저장하지는 않습니다.
        // 다른 팀원의 최종 맵에 그대로 실행하는 범용 배치 도구가 아닙니다.
        [MenuItem("Mine Prototype/Prepare Presentation Scene")]
        public static void PreparePresentation()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before editing the scene.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != Scene && scene.path != "") throw new InvalidOperationException("Open MovementTest first.");
            // 찾아낸 3D TextMeshPro 글자 오브젝트를 삭제합니다. 아래에서 수정할 화면 UI(TextMeshProUGUI)는 남깁니다.
            foreach (var text in UnityEngine.Object.FindObjectsByType<TextMeshPro>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(text.gameObject);
            var actor = UnityEngine.Object.FindFirstObjectByType<CapsuleMovement>().gameObject;
            var camera = actor.GetComponentInChildren<Camera>();
            // 이미 손전등 컴포넌트/조명이 있으면 재사용하고, 없을 때만 추가하여 중복 생성을 줄입니다.
            var flashlight = actor.GetComponent<FlashlightInteraction>() ?? actor.AddComponent<FlashlightInteraction>();
            var existing = camera.transform.Find("FlashlightBeam");
            var beam = existing != null ? existing.GetComponent<Light>() : new GameObject("FlashlightBeam").AddComponent<Light>();
            // 조명을 카메라의 자식으로 두면 따로 매 프레임 이동시키지 않아도 시선과 앉기를 따라갑니다.
            beam.transform.SetParent(camera.transform, false);
            beam.transform.localPosition = new Vector3(.12f, -.1f, .15f);
            beam.transform.localRotation = Quaternion.identity;
            beam.type = LightType.Spot; beam.range = 18; beam.spotAngle = 65; beam.innerSpotAngle = 38;
            beam.intensity = 1200; beam.color = new Color(1, .96f, .86f); beam.shadows = LightShadows.Soft; beam.enabled = false;
            // 자기 몸/손에 든 모형(2번 레이어)을 비추는 대신 광산 환경을 비추도록 설정합니다.
            beam.cullingMask = ~(1 << 2);
            actor.transform.Find("Visual_Capsule_Replaceable").GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Amber.mat");
            // 손에 들고 있는 모습은 임시 원기둥입니다. 획득할 바닥 원기둥과는 별개의 오브젝트입니다.
            var held = camera.transform.Find("FlashlightModel");
            if (held == null)
            {
                var model = GameObject.CreatePrimitive(PrimitiveType.Cylinder); model.name = "FlashlightModel";
                held = model.transform; held.SetParent(camera.transform, false);
                UnityEngine.Object.DestroyImmediate(model.GetComponent<Collider>());
                model.GetComponent<Renderer>().sharedMaterial = material;
                model.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            held.localPosition = new Vector3(.24f, -.22f, .45f);
            held.gameObject.layer = 2;
            held.localRotation = Quaternion.Euler(90, 0, 0); held.localScale = new Vector3(.085f, .16f, .085f);
            held.gameObject.SetActive(false);
            // Inspector에서 참조를 끌어 넣는 작업을 편집 코드로 대신합니다.
            // private SerializeField 값에 카메라/조명/모형을 연결해 씬에 저장될 수 있게 합니다.
            var serialized = new SerializedObject(flashlight);
            serialized.FindProperty("view").objectReferenceValue = camera;
            serialized.FindProperty("beam").objectReferenceValue = beam;
            serialized.FindProperty("heldModel").objectReferenceValue = held.gameObject;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            // 바닥에 놓인 획득 대상과 받침대입니다. 원기둥의 Collider와 FlashlightPickup이 같은 오브젝트에 붙습니다.
            if (UnityEngine.Object.FindFirstObjectByType<FlashlightPickup>() == null)
            {
                var pickup = GameObject.CreatePrimitive(PrimitiveType.Cylinder); pickup.name = "FlashlightPickup_Cylinder";
                pickup.transform.position = new Vector3(2, 1.05f, 5);
                pickup.transform.localScale = new Vector3(.38f, .35f, .38f);
                pickup.GetComponent<Renderer>().sharedMaterial = material;
                pickup.AddComponent<FlashlightPickup>();
                Box("FlashlightStand", new Vector3(2, .35f, 5), new Vector3(.9f, .7f, .9f),
                    AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Timber.mat"));
            }
            // 낮은 천장을 앉기 확인용 높이로 조절하고, 손전등 효과가 보이도록 주변 밝기를 맞춥니다.
            var ceiling = GameObject.Find("CeilingTest");
            if (ceiling != null) ceiling.transform.position = new Vector3(3, 1.5f, 17);
            RenderSettings.ambientLight = new Color(.38f, .4f, .44f);
            // 현재 사용 중인 공용 URP 에셋도 수정하므로 같은 에셋을 사용하는 다른 씬의 조명 설정에 영향을 줄 수 있습니다.
            var pipeline = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            pipeline.maxAdditionalLightsCount = 8;
            EditorUtility.SetDirty(pipeline);
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.name == "AmbientLight") light.intensity = .7f;
                if (light.name == "MineLamp") light.intensity = 5;
            }
            var controls = GameObject.Find("ControlsText").GetComponent<TextMeshProUGUI>();
            controls.fontSize = 17;
            controls.text = "WASD Move | Mouse Look | Shift Sprint | Space Jump | Ctrl Crouch | F Pick up | RMB Light | Esc Pause";
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
