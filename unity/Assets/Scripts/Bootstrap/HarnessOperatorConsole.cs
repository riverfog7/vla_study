using UnityEngine;
using VlaStudy.UnityHarness.Data;
using VlaStudy.UnityHarness.Robot;
using VlaStudy.UnityHarness.Simulation;

namespace VlaStudy.UnityHarness.Bootstrap
{
    [DisallowMultipleComponent]
    public class HarnessOperatorConsole : MonoBehaviour
    {
        [SerializeField] private SimulationController simulationController;
        [SerializeField] private SceneStateService sceneStateService;
        [SerializeField] private TaskResetService taskResetService;
        [SerializeField] private ProxyPoseAdapter proxyPoseAdapter;
        [SerializeField] private ArticulatedRobotAdapter articulatedRobotAdapter;
        [SerializeField] private ControlTimingConfig controlTimingConfig;
        [SerializeField] private Vector3 testPosePosition = new Vector3(0.25f, 1f, 0.2f);
        [SerializeField] private Vector3 testPoseEulerDegrees = Vector3.zero;
        [SerializeField] private float testPoseGripper = 0.5f;

        private void Reset()
        {
            AutoAssignReferences();
        }

        private void OnValidate()
        {
            AutoAssignReferences();
            testPoseGripper = Mathf.Clamp01(testPoseGripper);
        }

        [ContextMenu("Reset Scene")]
        public void ResetScene()
        {
            AutoAssignReferences();

            if (taskResetService == null)
            {
                Debug.LogError("HarnessOperatorConsole is missing TaskResetService.", this);
                return;
            }

            taskResetService.ResetScene();
            Debug.Log("Harness reset complete.", this);
        }

        [ContextMenu("Step One Physics Step")]
        public void StepOnePhysicsStep()
        {
            AutoAssignReferences();

            if (simulationController == null)
            {
                Debug.LogError("HarnessOperatorConsole is missing SimulationController.", this);
                return;
            }

            var steps = controlTimingConfig != null ? controlTimingConfig.DefaultManualStepCount : 1;
            var result = simulationController.StepSimulation(steps, simulationController.PhysicsDt);
            Debug.Log($"Stepped {steps} physics step(s). sim_time={result.SimTime:0.###}, step_count={result.StepCount}", this);
        }

        [ContextMenu("Step One Control Interval")]
        public void StepOneControlInterval()
        {
            AutoAssignReferences();

            if (simulationController == null)
            {
                Debug.LogError("HarnessOperatorConsole is missing SimulationController.", this);
                return;
            }

            var result = simulationController.StepControlInterval();
            Debug.Log($"Stepped one control interval. sim_time={result.SimTime:0.###}, step_count={result.StepCount}", this);
        }

        [ContextMenu("Log Status")]
        public void LogStatus()
        {
            AutoAssignReferences();

            if (sceneStateService == null)
            {
                Debug.LogError("HarnessOperatorConsole is missing SceneStateService.", this);
                return;
            }

            Debug.Log(JsonUtility.ToJson(sceneStateService.BuildStateResponse()), this);
        }

        [ContextMenu("Send Test Pose")]
        public void SendTestPose()
        {
            AutoAssignReferences();

            var robotAdapter = ResolveRobotAdapter();
            if (robotAdapter == null)
            {
                Debug.LogError("HarnessOperatorConsole could not resolve an active robot adapter.", this);
                return;
            }

            var commandId = robotAdapter.ApplyPoseCommand(new PoseCommand
            {
                frame = "world",
                position = new Vector3Data(testPosePosition),
                rotation = new QuaternionData(Quaternion.Euler(testPoseEulerDegrees)),
                gripper = testPoseGripper,
                blocking = false,
            });

            Debug.Log($"Issued test pose command {commandId}.", this);
        }

        private void AutoAssignReferences()
        {
            simulationController ??= GetComponent<SimulationController>();
            sceneStateService ??= GetComponent<SceneStateService>();
            taskResetService ??= GetComponent<TaskResetService>();
            proxyPoseAdapter ??= GetComponent<ProxyPoseAdapter>();
            articulatedRobotAdapter ??= GetComponent<ArticulatedRobotAdapter>();
            controlTimingConfig ??= GetComponent<ControlTimingConfig>();
        }

        private IRobotAdapter ResolveRobotAdapter()
        {
            if (articulatedRobotAdapter != null && articulatedRobotAdapter.isActiveAndEnabled && articulatedRobotAdapter.IsConfiguredForControl)
            {
                return articulatedRobotAdapter;
            }

            return proxyPoseAdapter;
        }
    }
}
