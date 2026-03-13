using UnityEngine;
using VlaStudy.UnityHarness.Api;
using VlaStudy.UnityHarness.Camera;
using VlaStudy.UnityHarness.Robot;
using VlaStudy.UnityHarness.Simulation;

namespace VlaStudy.UnityHarness.Bootstrap
{
    [DisallowMultipleComponent]
    public class HarnessSceneBootstrap : MonoBehaviour
    {
        [SerializeField] private HarnessSceneReferences sceneReferences;
        [SerializeField] private MainThreadDispatcher mainThreadDispatcher;
        [SerializeField] private SimulationController simulationController;
        [SerializeField] private CameraRegistry cameraRegistry;
        [SerializeField] private CameraCaptureService cameraCaptureService;
        [SerializeField] private CameraMountService cameraMountService;
        [SerializeField] private RuntimeCameraService runtimeCameraService;
        [SerializeField] private ProxyPoseAdapter proxyPoseAdapter;
        [SerializeField] private ArticulatedRobotAdapter articulatedRobotAdapter;
        [SerializeField] private SceneStateService sceneStateService;
        [SerializeField] private TaskResetService taskResetService;
        [SerializeField] private HttpApiServer httpApiServer;

        private bool _isConfigured;

        private void Reset()
        {
            AutoAssignReferences();
        }

        private void OnValidate()
        {
            AutoAssignReferences();
        }

        private void Awake()
        {
            AutoAssignReferences();

            if (!ValidateConfiguration())
            {
                if (httpApiServer != null)
                {
                    httpApiServer.enabled = false;
                }

                enabled = false;
                return;
            }

            cameraRegistry.Clear();
            cameraMountService.Configure(sceneReferences);
            cameraCaptureService.Configure(cameraRegistry);
            runtimeCameraService.Configure(cameraRegistry, cameraMountService);

            if (articulatedRobotAdapter != null && sceneReferences.RobotBaseFrame != null)
            {
                articulatedRobotAdapter.Configure(
                    sceneReferences.RobotBaseFrame,
                    sceneReferences.RobotEndEffector,
                    sceneReferences.RobotCameraMount);
            }

            if ((articulatedRobotAdapter == null || !articulatedRobotAdapter.IsConfiguredForControl) &&
                proxyPoseAdapter != null &&
                sceneReferences.RobotEndEffector != null)
            {
                proxyPoseAdapter.Configure(
                    sceneReferences.RobotEndEffector,
                    sceneReferences.RobotEndEffector.position,
                    sceneReferences.RobotEndEffector.rotation);
            }

            var robotAdapter = ResolveRobotAdapter();
            if (robotAdapter == null)
            {
                Debug.LogError("HarnessSceneBootstrap could not resolve an active robot adapter.", this);
                httpApiServer.enabled = false;
                enabled = false;
                return;
            }

            simulationController.Configure(robotAdapter);
            sceneStateService.Configure(simulationController, robotAdapter);
            taskResetService.Configure(simulationController, robotAdapter, sceneReferences.TargetObject);
            httpApiServer.Configure(mainThreadDispatcher, simulationController, sceneStateService, cameraCaptureService, cameraRegistry, runtimeCameraService, robotAdapter, taskResetService);
            RegisterAuthoredCameras();

            _isConfigured = true;
        }

        private void Start()
        {
            if (!_isConfigured)
            {
                return;
            }

            Debug.Log($"Harness ready: http://{httpApiServer.Host}:{httpApiServer.Port}/v1/ | physics_dt={simulationController.PhysicsDt:0.###} | policy_period={simulationController.PolicyPeriodSeconds:0.###} | steps_per_action={simulationController.StepsPerAction} | cameras={string.Join(",", GetCameraNames())}");
        }

        private void AutoAssignReferences()
        {
            sceneReferences ??= GetComponent<HarnessSceneReferences>();
            mainThreadDispatcher ??= GetComponent<MainThreadDispatcher>();
            simulationController ??= GetComponent<SimulationController>();
            cameraRegistry ??= GetComponent<CameraRegistry>();
            cameraCaptureService ??= GetComponent<CameraCaptureService>();
            cameraMountService ??= GetComponent<CameraMountService>();
            runtimeCameraService ??= GetComponent<RuntimeCameraService>();
            proxyPoseAdapter ??= GetComponent<ProxyPoseAdapter>();
            articulatedRobotAdapter ??= GetComponent<ArticulatedRobotAdapter>();
            sceneStateService ??= GetComponent<SceneStateService>();
            taskResetService ??= GetComponent<TaskResetService>();
            httpApiServer ??= GetComponent<HttpApiServer>();
        }

        private bool ValidateConfiguration()
        {
            var isValid = true;

            if (sceneReferences == null || !sceneReferences.IsConfigured())
            {
                Debug.LogError("HarnessSceneBootstrap requires fully configured HarnessSceneReferences.", this);
                isValid = false;
            }

            isValid &= ValidateComponent(mainThreadDispatcher, nameof(mainThreadDispatcher));
            isValid &= ValidateComponent(simulationController, nameof(simulationController));
            isValid &= ValidateComponent(cameraRegistry, nameof(cameraRegistry));
            isValid &= ValidateComponent(cameraCaptureService, nameof(cameraCaptureService));
            isValid &= ValidateComponent(cameraMountService, nameof(cameraMountService));
            isValid &= ValidateComponent(runtimeCameraService, nameof(runtimeCameraService));
            isValid &= ValidateComponent(sceneStateService, nameof(sceneStateService));
            isValid &= ValidateComponent(taskResetService, nameof(taskResetService));
            isValid &= ValidateComponent(httpApiServer, nameof(httpApiServer));

            if (proxyPoseAdapter == null && articulatedRobotAdapter == null)
            {
                Debug.LogError("HarnessSceneBootstrap requires either ProxyPoseAdapter or ArticulatedRobotAdapter.", this);
                isValid = false;
            }

            if (articulatedRobotAdapter != null && proxyPoseAdapter == null && sceneReferences != null && sceneReferences.RobotBaseFrame == null)
            {
                Debug.LogError("HarnessSceneBootstrap requires HarnessSceneReferences.robotBaseFrame when only ArticulatedRobotAdapter is present.", this);
                isValid = false;
            }

            if (sceneReferences != null && !sceneReferences.HasCameraNamed("main"))
            {
                Debug.LogError("HarnessSceneBootstrap requires an authored camera named 'main'.", this);
                isValid = false;
            }

            return isValid;
        }

        private IRobotAdapter ResolveRobotAdapter()
        {
            if (articulatedRobotAdapter != null && articulatedRobotAdapter.IsConfiguredForControl)
            {
                return articulatedRobotAdapter;
            }

            return proxyPoseAdapter;
        }

        private void RegisterAuthoredCameras()
        {
            foreach (var cameraDefinition in sceneReferences.Cameras)
            {
                if (cameraDefinition == null || !cameraDefinition.IsValid())
                {
                    continue;
                }

                var descriptor = new RegisteredCameraDescriptor
                {
                    CameraName = cameraDefinition.cameraName,
                    Camera = cameraDefinition.camera,
                    IsRuntime = false,
                    MountTargetName = string.IsNullOrWhiteSpace(cameraDefinition.mountTarget) ? string.Empty : cameraMountService.NormalizeMountTargetName(cameraDefinition.mountTarget),
                    LocalPositionOffset = cameraDefinition.localPositionOffset,
                    LocalRotationEuler = cameraDefinition.localRotationEuler,
                    TemplateCameraName = cameraDefinition.cameraName,
                };
                cameraRegistry.RegisterAuthored(descriptor);
                cameraMountService.ApplyAuthoredCameraMount(descriptor);
            }
        }

        private string[] GetCameraNames()
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (var cameraDefinition in sceneReferences.Cameras)
            {
                if (cameraDefinition != null && cameraDefinition.IsValid())
                {
                    names.Add(cameraDefinition.cameraName);
                }
            }

            return names.ToArray();
        }

        private bool ValidateComponent(Object component, string fieldName)
        {
            if (component != null)
            {
                return true;
            }

            Debug.LogError($"HarnessSceneBootstrap is missing required reference '{fieldName}'.", this);
            return false;
        }
    }
}
