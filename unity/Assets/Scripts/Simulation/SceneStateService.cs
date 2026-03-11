using VlaStudy.UnityHarness.Data;
using VlaStudy.UnityHarness.Robot;
using UnityEngine;

namespace VlaStudy.UnityHarness.Simulation
{
    public class SceneStateService : MonoBehaviour
    {
        private SimulationController _simulationController;
        private IRobotAdapter _robotAdapter;

        public void Configure(SimulationController simulationController, IRobotAdapter robotAdapter)
        {
            _simulationController = simulationController;
            _robotAdapter = robotAdapter;
        }

        public StateResponse BuildStateResponse()
        {
            var snapshot = _robotAdapter != null
                ? _robotAdapter.GetSnapshot()
                : new RobotSnapshot(Vector3.zero, Quaternion.identity, 0f, Vector3.zero, Quaternion.identity, 0f, 0, false, false);

            return new StateResponse
            {
                sim_time = _simulationController != null ? _simulationController.SimTime : 0f,
                step_count = _simulationController != null ? _simulationController.StepCount : 0,
                physics_dt = _simulationController != null ? _simulationController.PhysicsDt : 0.02f,
                policy_period_seconds = _simulationController != null ? _simulationController.PolicyPeriodSeconds : 0.02f,
                steps_per_action = _simulationController != null ? _simulationController.StepsPerAction : 1,
                robot_mode = _robotAdapter != null ? _robotAdapter.RobotMode : "uninitialized",
                current_pose = new PoseData(snapshot.Position, snapshot.Rotation),
                gripper = _robotAdapter != null ? _robotAdapter.CurrentGripper : 0f,
                target_pose = new PoseData(snapshot.TargetPosition, snapshot.TargetRotation),
                target_gripper = snapshot.TargetGripper,
                last_command_id = snapshot.LastCommandId,
                motion_in_progress = snapshot.MotionInProgress,
                last_command_was_clipped = snapshot.LastCommandWasClipped,
            };
        }
    }
}
