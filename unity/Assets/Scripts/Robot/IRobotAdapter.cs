using UnityEngine;
using VlaStudy.UnityHarness.Data;

namespace VlaStudy.UnityHarness.Robot
{
    public interface IRobotAdapter
    {
        string RobotMode { get; }
        float CurrentGripper { get; }
        RobotSnapshot GetSnapshot();
        int ApplyPoseCommand(PoseCommand command);
        void AdvanceMotion(float dt);
        void ResetRobot();
        void ClearTransientState();
    }

    public struct RobotSnapshot
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float Gripper;
        public Vector3 TargetPosition;
        public Quaternion TargetRotation;
        public float TargetGripper;
        public int LastCommandId;
        public bool MotionInProgress;
        public bool LastCommandWasClipped;

        public RobotSnapshot(
            Vector3 position,
            Quaternion rotation,
            float gripper,
            Vector3 targetPosition,
            Quaternion targetRotation,
            float targetGripper,
            int lastCommandId,
            bool motionInProgress,
            bool lastCommandWasClipped)
        {
            Position = position;
            Rotation = rotation;
            Gripper = gripper;
            TargetPosition = targetPosition;
            TargetRotation = targetRotation;
            TargetGripper = targetGripper;
            LastCommandId = lastCommandId;
            MotionInProgress = motionInProgress;
            LastCommandWasClipped = lastCommandWasClipped;
        }
    }
}
