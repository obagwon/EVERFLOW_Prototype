using System;
using System.Linq;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EverflowMap.Editor
{
    public static class EFMapCinemachineSetup
    {
        public const string ScenePath = "Assets/Scenes/Everflow_Map_v2_test.unity";

        [MenuItem("Tools/EVERFLOW/V2 Exploration/Install Cinemachine Camera")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
            var host = UnityEngine.Object.FindFirstObjectByType<EFMapExploration>();
            var player = host.player;
            var camera = player.view;
            if (camera == null || !camera.CompareTag("MainCamera")) throw new InvalidOperationException("Keep the existing tagged rendering camera.");
            // Remove the entire legacy owner (orbit, follow, collision and ride assist).
            player.follow = null;
            foreach (var old in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EFMapCamera>(true)))
                UnityEngine.Object.DestroyImmediate(old);
            var brain = camera.GetComponent<CinemachineBrain>() ?? camera.gameObject.AddComponent<CinemachineBrain>();
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0);
            var target = player.transform.Find("CameraTarget");
            if (target == null) { target = new GameObject("CameraTarget").transform; target.SetParent(player.transform, false); }
            // Root capsule is centred at the player's origin, with feet at y=-1.
            target.localPosition = new Vector3(0, .65f, 0);
            target.localRotation = Quaternion.identity;
            var rig = player.cinemachineRig;
            var go = rig != null ? rig.gameObject : new GameObject("FreeLook Camera");
            var vcam = go.GetComponent<CinemachineCamera>() ?? go.AddComponent<CinemachineCamera>();
            vcam.Priority = 10;
            vcam.Follow = target; vcam.LookAt = target;
            vcam.Lens.FieldOfView = 60; vcam.Lens.NearClipPlane = .1f; vcam.Lens.FarClipPlane = camera.farClipPlane;
            var orbit = go.GetComponent<CinemachineOrbitalFollow>() ?? go.AddComponent<CinemachineOrbitalFollow>();
            orbit.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            orbit.Radius = 6;
            orbit.TrackerSettings.BindingMode = BindingMode.WorldSpace;
            orbit.TrackerSettings.PositionDamping = new Vector3(.15f, .22f, .15f);
            orbit.RecenteringTarget = CinemachineOrbitalFollow.ReferenceFrames.AxisCenter;
            orbit.HorizontalAxis = new InputAxis { Value = host.spawnPoint.eulerAngles.y, Center = 180, Range = new Vector2(-180, 180), Wrap = true };
            orbit.VerticalAxis = new InputAxis { Value = 18, Center = 18, Range = new Vector2(-30, 65), Wrap = false };
            orbit.RadialAxis = new InputAxis { Value = 1, Center = 1, Range = Vector2.one };
            var composer = go.GetComponent<CinemachineRotationComposer>() ?? go.AddComponent<CinemachineRotationComposer>();
            composer.Damping = new Vector2(.05f, .08f);
            composer.Composition.ScreenPosition = new Vector2(-.08f, -.08f);
            composer.Composition.DeadZone.Enabled = false;
            composer.Composition.HardLimits.Enabled = false;
            var deoccluder = go.GetComponent<CinemachineDeoccluder>() ?? go.AddComponent<CinemachineDeoccluder>();
            deoccluder.CollideAgainst = 1; // Solid imported terrain is Default; exclude player (Ignore Raycast), water, UI and FX.
            deoccluder.MinimumDistanceFromTarget = .25f;
            deoccluder.AvoidObstacles.Enabled = true;
            deoccluder.AvoidObstacles.Strategy = CinemachineDeoccluder.ObstacleAvoidance.ResolutionStrategy.PullCameraForward;
            deoccluder.AvoidObstacles.CameraRadius = .25f;
            deoccluder.AvoidObstacles.DampingWhenOccluded = 0;
            deoccluder.AvoidObstacles.Damping = .35f;
            deoccluder.AvoidObstacles.SmoothingTime = .05f;
            deoccluder.AvoidObstacles.MinimumOcclusionTime = 0;
            var input = go.GetComponent<CinemachineInputAxisController>() ?? go.AddComponent<CinemachineInputAxisController>();
            input.ScanRecursively = false;
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/EverflowMap/V2Camera.inputactions");
            var look = actions.FindAction("Player/Look", true);
            const string referencePath = "Assets/EverflowMap/V2CameraLook.asset";
            var reference = AssetDatabase.LoadAssetAtPath<InputActionReference>(referencePath);
            if (reference == null) { reference = InputActionReference.Create(look); AssetDatabase.CreateAsset(reference, referencePath); }
            reference.Set(look);
            input.SynchronizeControllers();
            foreach (var controller in input.Controllers)
            {
                bool x = controller.Name == "Look Orbit X", y = controller.Name == "Look Orbit Y";
                controller.Enabled = x || y;
                controller.Input.InputAction = x || y ? reference : null;
                controller.Input.Gain = x ? .14f : -.119f;
                controller.Input.LegacyInput = ""; // Both is enabled; avoid a second mouse source.
                controller.Input.CancelDeltaTime = true;
                controller.Driver.AccelTime = controller.Driver.DecelTime = 0;
            }
            rig = go.GetComponent<EFMapCinemachineRig>() ?? go.AddComponent<EFMapCinemachineRig>();
            rig.player = player; rig.brain = brain; rig.virtualCamera = vcam; rig.orbit = orbit; rig.input = input;
            player.cinemachineRig = rig;
            PrefabUtility.RecordPrefabInstancePropertyModifications(player);
            vcam.PreviousStateIsValid = false;
            vcam.InternalUpdateCameraState(Vector3.up, -1);
            brain.ManualUpdate();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed.");
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ScenePath);
            Validate();
            Debug.Log("V2_CINEMACHINE_SAVED_AND_REOPENED");
        }

        public static void Validate()
        {
            var host = UnityEngine.Object.FindFirstObjectByType<EFMapExploration>();
            host.ValidateConfiguration();
            var rig = host.player.cinemachineRig;
            var all = host.gameObject.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            if (all.Any(t => t.GetComponent<EFMapCamera>() != null)) throw new Exception("Legacy camera still attached.");
            if (all.Any(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0)) throw new Exception("Missing script.");
            if (rig == null || rig.virtualCamera.Follow != host.player.transform.Find("CameraTarget") ||
                rig.virtualCamera.LookAt != rig.virtualCamera.Follow || rig.input.Controllers.Count < 2 ||
                rig.input.Controllers.Take(2).Any(c => c.Input.InputAction == null || !c.Input.CancelDeltaTime) ||
                rig.orbit.TrackerSettings.BindingMode != BindingMode.WorldSpace || rig.orbit.HorizontalAxis.Recentering.Enabled ||
                rig.orbit.VerticalAxis.Recentering.Enabled || (rig.GetComponent<CinemachineDeoccluder>().CollideAgainst & (1 << host.player.gameObject.layer)) != 0)
                throw new Exception("Cinemachine reference/configuration validation failed.");
            Debug.Log("V2_CINEMACHINE_REFERENCES_PASS");
        }
    }
}
