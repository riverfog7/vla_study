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
        [SerializeField] private ProxyPoseAdapter proxyPoseAdapter;
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

            cameraRegistry.Register("main", sceneReferences.MainCamera);
            cameraCaptureService.Configure(cameraRegistry);
            proxyPoseAdapter.Configure(
                sceneReferences.ProxyEndEffector,
                sceneReferences.ProxyEndEffector.position,
                sceneReferences.ProxyEndEffector.rotation);
            simulationController.Configure(proxyPoseAdapter);
            sceneStateService.Configure(simulationController, proxyPoseAdapter);
            taskResetService.Configure(simulationController, proxyPoseAdapter, sceneReferences.TargetObject);
            httpApiServer.Configure(mainThreadDispatcher, simulationController, sceneStateService, cameraCaptureService, proxyPoseAdapter, taskResetService);

            _isConfigured = true;
        }

        private void Start()
        {
            if (!_isConfigured)
            {
                return;
            }

            Debug.Log(
                $"Harness ready: http://{httpApiServer.Host}:{httpApiServer.Port}/v1/ | physics_dt={simulationController.PhysicsDt:0.###} | policy_period={simulationController.PolicyPeriodSeconds:0.###} | steps_per_action={simulationController.StepsPerAction}");
        }

        private void AutoAssignReferences()
        {
            sceneReferences ??= GetComponent<HarnessSceneReferences>();
            mainThreadDispatcher ??= GetComponent<MainThreadDispatcher>();
            simulationController ??= GetComponent<SimulationController>();
            cameraRegistry ??= GetComponent<CameraRegistry>();
            cameraCaptureService ??= GetComponent<CameraCaptureService>();
            proxyPoseAdapter ??= GetComponent<ProxyPoseAdapter>();
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
            isValid &= ValidateComponent(proxyPoseAdapter, nameof(proxyPoseAdapter));
            isValid &= ValidateComponent(sceneStateService, nameof(sceneStateService));
            isValid &= ValidateComponent(taskResetService, nameof(taskResetService));
            isValid &= ValidateComponent(httpApiServer, nameof(httpApiServer));

            return isValid;
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
