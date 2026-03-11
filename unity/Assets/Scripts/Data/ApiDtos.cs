using System;

namespace VlaStudy.UnityHarness.Data
{
    [Serializable]
    public class HealthResponse
    {
        public bool ok;
        public string app;
        public string version;
    }

    [Serializable]
    public class StateResponse
    {
        public float sim_time;
        public int step_count;
        public float physics_dt;
        public float policy_period_seconds;
        public int steps_per_action;
        public string robot_mode;
        public PoseData current_pose;
        public float gripper;
        public PoseData target_pose;
        public float target_gripper;
        public int last_command_id;
        public bool motion_in_progress;
        public bool last_command_was_clipped;
    }

    [Serializable]
    public class StepRequest
    {
        public int steps = 1;
        public float dt = 0.02f;
    }

    [Serializable]
    public class StepResponse
    {
        public bool ok;
        public float sim_time;
        public int step_count;
    }

    [Serializable]
    public class MoveToPoseResponse
    {
        public bool accepted;
        public int command_id;
    }

    [Serializable]
    public class ResetResponse
    {
        public bool ok;
    }

    [Serializable]
    public class ErrorResponse
    {
        public string error;
        public string details;
    }
}
