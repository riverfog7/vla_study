#if UNITY_EDITOR
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
    [InitializeOnLoad]
    public static class HarnessSceneAuthoring
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string RootName = "VlaHarnessScene";
        private const string WorkspaceName = "WorkspaceTable";
        private const string TargetName = "TargetObject";
        private const string ProxyName = "ProxyEndEffector";
        private const string CameraName = "Main Camera";
        private const string LightName = "Directional Light";

        private static bool _autoRefreshPending = true;

        static HarnessSceneAuthoring()
        {
            EditorApplication.delayCall += TryAutoRefreshMainScene;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Tools/VLA/Refresh Main Scene")]
        public static void RefreshMainSceneMenuItem()
        {
            RefreshMainScene(forceWhenDirty: true);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange stateChange)
        {
            if (stateChange != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }

            _autoRefreshPending = true;
            EditorApplication.delayCall += TryAutoRefreshMainScene;
        }

        private static void TryAutoRefreshMainScene()
        {
            if (!_autoRefreshPending)
            {
                return;
            }

            _autoRefreshPending = false;
            RefreshMainScene(forceWhenDirty: false);
        }

        private static void RefreshMainScene(bool forceWhenDirty)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
            {
                _autoRefreshPending = true;
                return;
            }

            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || activeScene.path != ScenePath)
            {
                return;
            }

            var existingRoot = GameObject.Find(RootName);
            var existingSceneReferences = Object.FindFirstObjectByType<HarnessSceneReferences>();
            var needsInitialAuthoring = existingSceneReferences == null ||
                                        !existingSceneReferences.IsConfigured() ||
                                        SceneRootNeedsComponents(existingRoot);

            if (activeScene.isDirty && !forceWhenDirty && !needsInitialAuthoring)
            {
                _autoRefreshPending = true;
                return;
            }

            var changed = false;
            var root = GetOrCreateGameObject(RootName, ref changed);
            var sceneReferences = GetOrAddComponent<HarnessSceneReferences>(root, ref changed);
            GetOrAddComponent<ControlTimingConfig>(root, ref changed);
            GetOrAddComponent<MainThreadDispatcher>(root, ref changed);
            GetOrAddComponent<SimulationController>(root, ref changed);
            GetOrAddComponent<CameraRegistry>(root, ref changed);
            GetOrAddComponent<CameraCaptureService>(root, ref changed);
            GetOrAddComponent<ProxyPoseAdapter>(root, ref changed);
            GetOrAddComponent<SceneStateService>(root, ref changed);
            GetOrAddComponent<TaskResetService>(root, ref changed);
            GetOrAddComponent<HttpApiServer>(root, ref changed);
            GetOrAddComponent<HarnessSceneBootstrap>(root, ref changed);
            GetOrAddComponent<HarnessOperatorConsole>(root, ref changed);

            var workspaceMaterial = EnsureMaterialAsset("Assets/Materials/WorkspaceTable.mat", new Color(0.55f, 0.55f, 0.58f), ref changed);
            var targetMaterial = EnsureMaterialAsset("Assets/Materials/TargetObject.mat", new Color(0.82f, 0.34f, 0.24f), ref changed);
            var proxyMaterial = EnsureMaterialAsset("Assets/Materials/ProxyEndEffector.mat", new Color(0.16f, 0.54f, 0.86f), ref changed);

            var workspace = EnsurePrimitive(
                WorkspaceName,
                PrimitiveType.Cube,
                new Vector3(0f, 0.5f, 0f),
                new Vector3(1.5f, 0.1f, 1.5f),
                workspaceMaterial,
                ref changed);

            var target = EnsurePrimitive(
                TargetName,
                PrimitiveType.Sphere,
                new Vector3(0.3f, 0.65f, 0.2f),
                Vector3.one * 0.18f,
                targetMaterial,
                ref changed);

            var proxy = EnsurePrimitive(
                ProxyName,
                PrimitiveType.Cube,
                new Vector3(0f, 0.85f, 0f),
                new Vector3(0.12f, 0.12f, 0.12f),
                proxyMaterial,
                ref changed);

            var mainCamera = EnsureMainCamera(workspace.transform, ref changed);
            EnsureDirectionalLight(ref changed);

            if (ApplySceneReferences(sceneReferences, workspace.transform, target.transform, proxy.transform, mainCamera))
            {
                changed = true;
                EditorUtility.SetDirty(sceneReferences);
            }

            if (!changed)
            {
                return;
            }

            EditorSceneManager.MarkSceneDirty(activeScene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(activeScene);
        }

        private static bool ApplySceneReferences(HarnessSceneReferences sceneReferences, Transform workspace, Transform target, Transform proxy, UnityEngine.Camera mainCamera)
        {
            if (sceneReferences == null)
            {
                return false;
            }

            if (sceneReferences.WorkspaceTable == workspace &&
                sceneReferences.TargetObject == target &&
                sceneReferences.ProxyEndEffector == proxy &&
                sceneReferences.MainCamera == mainCamera)
            {
                return false;
            }

            sceneReferences.Configure(workspace, target, proxy, mainCamera);
            return true;
        }

        private static GameObject GetOrCreateGameObject(string name, ref bool changed)
        {
            var gameObject = GameObject.Find(name);
            if (gameObject != null)
            {
                return gameObject;
            }

            changed = true;
            return new GameObject(name);
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

        private static bool SceneRootNeedsComponents(GameObject root)
        {
            if (root == null)
            {
                return true;
            }

            return root.GetComponent<ControlTimingConfig>() == null ||
                   root.GetComponent<MainThreadDispatcher>() == null ||
                   root.GetComponent<SimulationController>() == null ||
                   root.GetComponent<CameraRegistry>() == null ||
                   root.GetComponent<CameraCaptureService>() == null ||
                   root.GetComponent<ProxyPoseAdapter>() == null ||
                   root.GetComponent<SceneStateService>() == null ||
                   root.GetComponent<TaskResetService>() == null ||
                   root.GetComponent<HttpApiServer>() == null ||
                   root.GetComponent<HarnessSceneBootstrap>() == null ||
                   root.GetComponent<HarnessOperatorConsole>() == null;
        }

        private static GameObject EnsurePrimitive(string name, PrimitiveType primitiveType, Vector3 position, Vector3 scale, Material material, ref bool changed)
        {
            var gameObject = GameObject.Find(name);
            if (gameObject == null)
            {
                gameObject = GameObject.CreatePrimitive(primitiveType);
                gameObject.name = name;
                changed = true;
            }

            if (ApplyTransform(gameObject.transform, position, Quaternion.identity, scale))
            {
                changed = true;
            }

            var renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != material)
            {
                renderer.sharedMaterial = material;
                changed = true;
            }

            return gameObject;
        }

        private static UnityEngine.Camera EnsureMainCamera(Transform lookTarget, ref bool changed)
        {
            var mainCamera = UnityEngine.Camera.main;
            if (mainCamera == null)
            {
                var existing = GameObject.Find(CameraName);
                if (existing != null)
                {
                    mainCamera = existing.GetComponent<UnityEngine.Camera>();
                }
            }

            if (mainCamera == null)
            {
                var cameraObject = new GameObject(CameraName);
                cameraObject.tag = "MainCamera";
                mainCamera = cameraObject.AddComponent<UnityEngine.Camera>();
                cameraObject.AddComponent<AudioListener>();
                changed = true;
            }

            if (mainCamera.gameObject.name != CameraName)
            {
                mainCamera.gameObject.name = CameraName;
                changed = true;
            }

            if (mainCamera.gameObject.tag != "MainCamera")
            {
                mainCamera.gameObject.tag = "MainCamera";
                changed = true;
            }

            if (ApplyTransform(mainCamera.transform, new Vector3(1.6f, 1.3f, -1.6f), Quaternion.LookRotation((lookTarget.position + new Vector3(0f, 0.2f, 0f)) - new Vector3(1.6f, 1.3f, -1.6f)), Vector3.one))
            {
                changed = true;
            }

            if (mainCamera.clearFlags != CameraClearFlags.Skybox)
            {
                mainCamera.clearFlags = CameraClearFlags.Skybox;
                changed = true;
            }

            return mainCamera;
        }

        private static void EnsureDirectionalLight(ref bool changed)
        {
            var light = Object.FindFirstObjectByType<Light>();
            if (light == null || light.type != LightType.Directional)
            {
                var lightObject = GameObject.Find(LightName);
                if (lightObject == null)
                {
                    lightObject = new GameObject(LightName);
                    changed = true;
                }

                light = lightObject.GetComponent<Light>();
                if (light == null)
                {
                    light = lightObject.AddComponent<Light>();
                    changed = true;
                }
            }

            if (light.gameObject.name != LightName)
            {
                light.gameObject.name = LightName;
                changed = true;
            }

            if (light.type != LightType.Directional)
            {
                light.type = LightType.Directional;
                changed = true;
            }

            if (ApplyTransform(light.transform, new Vector3(0f, 3f, 0f), Quaternion.Euler(50f, -30f, 0f), Vector3.one))
            {
                changed = true;
            }
        }

        private static Material EnsureMaterialAsset(string path, Color color, ref bool changed)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
                changed = true;
            }

            if (material.color != color)
            {
                material.color = color;
                EditorUtility.SetDirty(material);
                changed = true;
            }

            return material;
        }

        private static bool ApplyTransform(Transform transform, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var changed = false;

            if (transform.localPosition != position)
            {
                transform.localPosition = position;
                changed = true;
            }

            if (transform.localRotation != rotation)
            {
                transform.localRotation = rotation;
                changed = true;
            }

            if (transform.localScale != scale)
            {
                transform.localScale = scale;
                changed = true;
            }

            return changed;
        }
    }
}
#endif
