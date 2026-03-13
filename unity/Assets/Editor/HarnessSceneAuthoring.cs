#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VlaStudy.UnityHarness.Api;
using VlaStudy.UnityHarness.Bootstrap;
using VlaStudy.UnityHarness.Camera;
using VlaStudy.UnityHarness.Robot;
using VlaStudy.UnityHarness.Simulation;

namespace VlaStudy.UnityHarness.EditorTools
{
    public static class HarnessSceneAuthoring
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string RootName = "VlaHarnessScene";
        private const string WorkspaceName = "WorkspaceTable";
        private const string TargetName = "TargetObject";
        private const string ProxyName = "ProxyEndEffector";
        private const string ProxyCameraMountName = "ProxyCameraMount";
        private const string CameraName = "Main Camera";

        [MenuItem("Tools/VLA/Refresh Main Scene")]
        public static void RefreshMainSceneMenuItem()
        {
            RefreshMainScene(forceWhenDirty: true);
        }

        public static void RefreshMainSceneAssetForBatch()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || activeScene.path != ScenePath)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            RefreshMainScene(forceWhenDirty: true);
        }

        private static void RefreshMainScene(bool forceWhenDirty)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
            {
                return;
            }

            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || activeScene.path != ScenePath)
            {
                return;
            }

            if (activeScene.isDirty && !forceWhenDirty)
            {
                return;
            }

            var changed = false;
            var root = GameObject.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                changed = true;
            }

            var sceneReferences = GetOrAddComponent<HarnessSceneReferences>(root, ref changed);
            GetOrAddComponent<ControlTimingConfig>(root, ref changed);
            GetOrAddComponent<MainThreadDispatcher>(root, ref changed);
            GetOrAddComponent<SimulationController>(root, ref changed);
            GetOrAddComponent<CameraRegistry>(root, ref changed);
            GetOrAddComponent<CameraCaptureService>(root, ref changed);
            GetOrAddComponent<CameraMountService>(root, ref changed);
            GetOrAddComponent<RuntimeCameraService>(root, ref changed);
            GetOrAddComponent<ProxyPoseAdapter>(root, ref changed);
            GetOrAddComponent<ArticulatedRobotAdapter>(root, ref changed);
            GetOrAddComponent<SceneStateService>(root, ref changed);
            GetOrAddComponent<TaskResetService>(root, ref changed);
            GetOrAddComponent<HttpApiServer>(root, ref changed);
            GetOrAddComponent<HarnessSceneBootstrap>(root, ref changed);
            GetOrAddComponent<HarnessOperatorConsole>(root, ref changed);

            var workspace = GameObject.Find(WorkspaceName)?.transform;
            var target = GameObject.Find(TargetName)?.transform;
            var proxy = GameObject.Find(ProxyName)?.transform;
            var proxyCameraMount = proxy != null ? proxy.Find(ProxyCameraMountName) : null;
            var mainCamera = GameObject.Find(CameraName)?.GetComponent<UnityEngine.Camera>() ?? UnityEngine.Camera.main;

            var missing = new List<string>();
            if (workspace == null) missing.Add(WorkspaceName);
            if (target == null) missing.Add(TargetName);
            if (proxy == null) missing.Add(ProxyName);
            if (proxyCameraMount == null) missing.Add(ProxyCameraMountName);
            if (mainCamera == null) missing.Add(CameraName);

            if (missing.Count > 0)
            {
                Debug.LogError($"Refresh Main Scene aborted. Missing required scene objects: {string.Join(", ", missing)}");
                return;
            }

            if (sceneReferences.RobotBaseFrame == null &&
                (sceneReferences.WorkspaceTable != workspace ||
                 sceneReferences.TargetObject != target ||
                 sceneReferences.ProxyEndEffector != proxy ||
                 sceneReferences.ProxyCameraMount != proxyCameraMount))
            {
                sceneReferences.ConfigureBaseReferences(workspace, target, proxy, proxyCameraMount);
                changed = true;
            }

            if (sceneReferences.EnsureCameraDefinition("main", mainCamera, enabled: mainCamera.enabled))
            {
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            EditorUtility.SetDirty(sceneReferences);
            EditorSceneManager.MarkSceneDirty(activeScene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(activeScene);
        }

        private static T GetOrAddComponent<T>(GameObject gameObject, ref bool changed) where T : Component
        {
            var component = gameObject.GetComponent<T>();
            if (component != null)
            {
                return component;
            }

            changed = true;
            return gameObject.AddComponent<T>();
        }
    }
}
#endif
