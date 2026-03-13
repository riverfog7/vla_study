using System;
using System.Collections.Generic;
using UnityEngine;
using VlaStudy.UnityHarness.Data;

namespace VlaStudy.UnityHarness.Robot
{
    [DisallowMultipleComponent]
    public class ArticulatedRobotAdapter : MonoBehaviour, IRobotAdapter
    {
        [Serializable]
        private class RevoluteJointBinding
        {
            public string jointName;
            public ArticulationBody articulationBody;
            public float driveSign = 1f;
            public float zeroOffsetDegrees;
            public float maxSpeedDegreesPerSecond = 180f;

            public bool IsConfigured => articulationBody != null;
        }

        [Serializable]
        private class PrismaticJointBinding
        {
            public string jointName;
            public ArticulationBody articulationBody;
            public float driveSign = 1f;
            public float zeroOffsetMeters;
            public float maxSpeedMetersPerSecond = 0.1f;

            public bool IsConfigured => articulationBody != null;
        }

        private const float ShoulderHeightMeters = 0.11065f;
        private const float ShoulderToElbowX = 0.04975f;
        private const float ShoulderToElbowZ = 0.25f;
        private const float ElbowToWristMeters = 0.25f;
        private const float WristToEndEffectorMeters = 0.158575f;
        private static readonly float ShoulderLinkLengthMeters = Mathf.Sqrt((ShoulderToElbowX * ShoulderToElbowX) + (ShoulderToElbowZ * ShoulderToElbowZ));
        private static readonly float ShoulderLinkBiasRadians = Mathf.Atan2(ShoulderToElbowZ, ShoulderToElbowX);

        [Header("Robot References")]
        [SerializeField] private ArticulationBody articulationRoot;
        [SerializeField] private Transform kinematicsBaseFrame;
        [SerializeField] private Transform endEffectorControlPoint;
        [SerializeField] private Transform wristCameraMount;

        [Header("Arm Joints")]
        [SerializeField] private RevoluteJointBinding waistJoint = new RevoluteJointBinding { jointName = "waist" };
        [SerializeField] private RevoluteJointBinding shoulderJoint = new RevoluteJointBinding { jointName = "shoulder" };
        [SerializeField] private RevoluteJointBinding elbowJoint = new RevoluteJointBinding { jointName = "elbow" };
        [SerializeField] private RevoluteJointBinding forearmRollJoint = new RevoluteJointBinding { jointName = "forearm_roll" };
        [SerializeField] private RevoluteJointBinding wristAngleJoint = new RevoluteJointBinding { jointName = "wrist_angle" };
        [SerializeField] private RevoluteJointBinding wristRotateJoint = new RevoluteJointBinding { jointName = "wrist_rotate" };

        [Header("Gripper")]
        [SerializeField] private PrismaticJointBinding leftFingerJoint = new PrismaticJointBinding { jointName = "left_finger", driveSign = 1f, maxSpeedMetersPerSecond = 0.1f };
        [SerializeField] private PrismaticJointBinding rightFingerJoint = new PrismaticJointBinding { jointName = "right_finger", driveSign = 1f, maxSpeedMetersPerSecond = 0.1f };
        [SerializeField] private float gripperOpenMeters = 0.015f;
        [SerializeField] private float gripperClosedMeters = 0.037f;

        [Header("Control")]
        [SerializeField] private Vector3 minPosition = new Vector3(-0.75f, 0.55f, -0.75f);
        [SerializeField] private Vector3 maxPosition = new Vector3(0.75f, 1.3f, 0.75f);
        [SerializeField] private float fixedToolPitchDegrees;
        [SerializeField] private float poseToleranceMeters = 0.01f;
        [SerializeField] private float jointToleranceDegrees = 1f;
        [SerializeField] private float gripperToleranceMeters = 0.001f;

        private readonly List<RevoluteJointBinding> _armJoints = new List<RevoluteJointBinding>();
        private readonly List<PrismaticJointBinding> _gripperJoints = new List<PrismaticJointBinding>();

        private Vector3 _homeBasePosition;
        private Quaternion _homeBaseRotation;
        private float[] _homeArmTargetsDegrees = Array.Empty<float>();
        private float[] _homeGripperTargetsMeters = Array.Empty<float>();
        private float[] _targetArmTargetsDegrees = Array.Empty<float>();
        private float[] _targetGripperTargetsMeters = Array.Empty<float>();
        private Vector3 _targetPosition;
        private Quaternion _targetRotation = Quaternion.identity;
        private float _targetGripper;
        private float _currentGripper;
        private int _lastAcceptedCommandId;
        private bool _lastCommandWasClipped;
        private bool _hasActiveTarget;
        private int _commandCounter;
        private string _robotMode = "unconfigured";

        public string RobotMode => _robotMode;
        public float CurrentGripper => _currentGripper;
        public bool IsConfiguredForControl { get; private set; }

        private void Awake()
        {
            RebuildJointCaches();
        }

        private void OnValidate()
        {
            RebuildJointCaches();
            poseToleranceMeters = Mathf.Max(0.0001f, poseToleranceMeters);
            jointToleranceDegrees = Mathf.Max(0.01f, jointToleranceDegrees);
            gripperToleranceMeters = Mathf.Max(0.00001f, gripperToleranceMeters);
        }

        public void Configure(Transform baseFrame, Transform endEffector, Transform cameraMount)
        {
            kinematicsBaseFrame = baseFrame != null ? baseFrame : kinematicsBaseFrame;
            endEffectorControlPoint = endEffector != null ? endEffector : endEffectorControlPoint;
            wristCameraMount = cameraMount != null ? cameraMount : wristCameraMount;
            RebuildJointCaches();

            IsConfiguredForControl = articulationRoot != null &&
                                     kinematicsBaseFrame != null &&
                                     endEffectorControlPoint != null &&
                                     waistJoint.IsConfigured &&
                                     shoulderJoint.IsConfigured &&
                                     elbowJoint.IsConfigured &&
                                     wristAngleJoint.IsConfigured;

            if (!IsConfiguredForControl)
            {
                _robotMode = "unconfigured";
                return;
            }

            _homeBasePosition = articulationRoot.transform.position;
            _homeBaseRotation = articulationRoot.transform.rotation;
            _homeArmTargetsDegrees = CaptureArmTargetsDegrees();
            _homeGripperTargetsMeters = CaptureGripperTargetsMeters();
            _targetArmTargetsDegrees = (float[])_homeArmTargetsDegrees.Clone();
            _targetGripperTargetsMeters = (float[])_homeGripperTargetsMeters.Clone();
            _targetPosition = endEffectorControlPoint.position;
            _targetRotation = endEffectorControlPoint.rotation;
            _currentGripper = ComputeNormalizedGripper();
            _targetGripper = _currentGripper;
            _robotMode = "ready";
        }

        public RobotSnapshot GetSnapshot()
        {
            var position = endEffectorControlPoint != null ? endEffectorControlPoint.position : Vector3.zero;
            var rotation = endEffectorControlPoint != null ? endEffectorControlPoint.rotation : Quaternion.identity;
            return new RobotSnapshot(
                position,
                rotation,
                _currentGripper,
                _targetPosition,
                _targetRotation,
                _targetGripper,
                _lastAcceptedCommandId,
                _hasActiveTarget,
                _lastCommandWasClipped);
        }

        public int ApplyPoseCommand(PoseCommand command)
        {
            if (!IsConfiguredForControl)
            {
                throw new InvalidOperationException("Articulated robot adapter is not configured.");
            }

            if (command == null || !command.IsValid())
            {
                throw new ArgumentException("Pose command is invalid.");
            }

            if (!string.Equals(command.frame, "world", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Only the world frame is supported in v1.");
            }

            var unclippedPosition = command.position.ToUnityVector3();
            var clippedPosition = new Vector3(
                Mathf.Clamp(unclippedPosition.x, minPosition.x, maxPosition.x),
                Mathf.Clamp(unclippedPosition.y, minPosition.y, maxPosition.y),
                Mathf.Clamp(unclippedPosition.z, minPosition.z, maxPosition.z));

            _lastCommandWasClipped = clippedPosition != unclippedPosition;
            if (!TrySolvePositionOnlyIk(clippedPosition, out var solvedTargetsDegrees))
            {
                _robotMode = "ik_failed";
                throw new ArgumentException($"Target position {clippedPosition} is unreachable for the articulated robot.");
            }

            _targetArmTargetsDegrees = solvedTargetsDegrees;
            _targetGripperTargetsMeters = ResolveGripperTargets(command.gripper);
            _targetPosition = clippedPosition;
            _targetRotation = command.rotation.ToUnityQuaternion();
            _targetGripper = Mathf.Clamp01(command.gripper);
            _commandCounter += 1;
            _lastAcceptedCommandId = _commandCounter;
            _hasActiveTarget = true;
            _robotMode = "moving";
            return _lastAcceptedCommandId;
        }

        public void AdvanceMotion(float dt)
        {
            if (!IsConfiguredForControl || !_hasActiveTarget)
            {
                _currentGripper = ComputeNormalizedGripper();
                return;
            }

            var resolvedDt = Mathf.Max(0f, dt);
            if (resolvedDt <= 0f)
            {
                return;
            }

            for (var index = 0; index < _armJoints.Count; index++)
            {
                StepRevoluteJoint(_armJoints[index], _targetArmTargetsDegrees[index], resolvedDt);
            }

            for (var index = 0; index < _gripperJoints.Count; index++)
            {
                StepPrismaticJoint(_gripperJoints[index], _targetGripperTargetsMeters[index], resolvedDt);
            }

            _currentGripper = ComputeNormalizedGripper();
            _hasActiveTarget = !HasReachedTarget();
            _robotMode = _hasActiveTarget ? "moving" : "ready";
        }

        public void ResetRobot()
        {
            if (!IsConfiguredForControl)
            {
                return;
            }

            articulationRoot.TeleportRoot(_homeBasePosition, _homeBaseRotation);

            ApplyArmTargetsImmediate(_homeArmTargetsDegrees);
            ApplyGripperTargetsImmediate(_homeGripperTargetsMeters);
            _currentGripper = ComputeNormalizedGripper();
            _targetPosition = endEffectorControlPoint.position;
            _targetRotation = endEffectorControlPoint.rotation;
            _targetGripper = _currentGripper;
            _robotMode = "ready";
        }

        public void ClearTransientState()
        {
            _lastAcceptedCommandId = 0;
            _lastCommandWasClipped = false;
            _hasActiveTarget = false;
            _robotMode = IsConfiguredForControl ? "ready" : "unconfigured";
            _targetArmTargetsDegrees = _homeArmTargetsDegrees.Length > 0 ? (float[])_homeArmTargetsDegrees.Clone() : Array.Empty<float>();
            _targetGripperTargetsMeters = _homeGripperTargetsMeters.Length > 0 ? (float[])_homeGripperTargetsMeters.Clone() : Array.Empty<float>();
            _targetPosition = endEffectorControlPoint != null ? endEffectorControlPoint.position : Vector3.zero;
            _targetRotation = endEffectorControlPoint != null ? endEffectorControlPoint.rotation : Quaternion.identity;
            _currentGripper = ComputeNormalizedGripper();
            _targetGripper = _currentGripper;
        }

        private void RebuildJointCaches()
        {
            _armJoints.Clear();
            _armJoints.Add(waistJoint);
            _armJoints.Add(shoulderJoint);
            _armJoints.Add(elbowJoint);
            _armJoints.Add(forearmRollJoint);
            _armJoints.Add(wristAngleJoint);
            _armJoints.Add(wristRotateJoint);

            _gripperJoints.Clear();
            if (leftFingerJoint != null && leftFingerJoint.IsConfigured)
            {
                _gripperJoints.Add(leftFingerJoint);
            }

            if (rightFingerJoint != null && rightFingerJoint.IsConfigured)
            {
                _gripperJoints.Add(rightFingerJoint);
            }
        }


        private bool TrySolvePositionOnlyIk(Vector3 worldTargetPosition, out float[] solvedTargetsDegrees)
        {
            solvedTargetsDegrees = Array.Empty<float>();

            if (kinematicsBaseFrame == null)
            {
                return false;
            }

            var targetInBase = kinematicsBaseFrame.InverseTransformPoint(worldTargetPosition);
            var radialDistance = Mathf.Sqrt((targetInBase.x * targetInBase.x) + (targetInBase.y * targetInBase.y));
            var planarHeight = targetInBase.z - ShoulderHeightMeters;
            var waistRadians = Mathf.Atan2(targetInBase.y, targetInBase.x);

            if (radialDistance < 0.0001f)
            {
                waistRadians = GetCurrentMathRadians(waistJoint);
            }

            var fixedPitchRadians = fixedToolPitchDegrees * Mathf.Deg2Rad;
            var wristX = radialDistance - (WristToEndEffectorMeters * Mathf.Cos(fixedPitchRadians));
            var wristZ = planarHeight - (WristToEndEffectorMeters * Mathf.Sin(fixedPitchRadians));
            var numerator = (wristX * wristX) + (wristZ * wristZ) - (ShoulderLinkLengthMeters * ShoulderLinkLengthMeters) - (ElbowToWristMeters * ElbowToWristMeters);
            var denominator = 2f * ShoulderLinkLengthMeters * ElbowToWristMeters;
            if (Mathf.Abs(denominator) <= Mathf.Epsilon)
            {
                return false;
            }

            var rawD = numerator / denominator;
            if (rawD < -1f || rawD > 1f)
            {
                return false;
            }

            var d = Mathf.Clamp(rawD, -1f, 1f);
            var elbowCandidates = new[]
            {
                -Mathf.Acos(d),
                Mathf.Acos(d),
            };

            var bestCost = float.PositiveInfinity;
            float[] bestSolution = null;
            foreach (var theta2 in elbowCandidates)
            {
                var theta1 = Mathf.Atan2(wristZ, wristX) - Mathf.Atan2(ElbowToWristMeters * Mathf.Sin(theta2), ShoulderLinkLengthMeters + (ElbowToWristMeters * Mathf.Cos(theta2)));
                var shoulderRadians = theta1 - ShoulderLinkBiasRadians;
                var elbowRadians = theta2 + ShoulderLinkBiasRadians;
                var wristAngleRadians = fixedPitchRadians - (theta1 + theta2);

                var candidateRadians = new[]
                {
                    waistRadians,
                    shoulderRadians,
                    elbowRadians,
                    GetCurrentMathRadians(forearmRollJoint),
                    wristAngleRadians,
                    GetCurrentMathRadians(wristRotateJoint),
                };

                var candidateDegrees = ToDriveTargetsDegrees(candidateRadians);
                if (!AreDriveTargetsWithinLimits(candidateDegrees))
                {
                    continue;
                }

                var cost = ComputeSolutionCost(candidateRadians);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestSolution = candidateDegrees;
                }
            }

            if (bestSolution == null)
            {
                return false;
            }

            solvedTargetsDegrees = bestSolution;
            return true;
        }

        private float[] ToDriveTargetsDegrees(float[] mathRadians)
        {
            var targets = new float[_armJoints.Count];
            for (var index = 0; index < _armJoints.Count; index++)
            {
                var joint = _armJoints[index];
                var mathDegrees = mathRadians[index] * Mathf.Rad2Deg;
                targets[index] = joint.zeroOffsetDegrees + (joint.driveSign * mathDegrees);
            }

            return targets;
        }

        private bool AreDriveTargetsWithinLimits(float[] driveTargetsDegrees)
        {
            for (var index = 0; index < _armJoints.Count; index++)
            {
                var joint = _armJoints[index];
                if (!joint.IsConfigured)
                {
                    continue;
                }

                var drive = joint.articulationBody.xDrive;
                if (drive.lowerLimit <= drive.upperLimit)
                {
                    if (driveTargetsDegrees[index] < drive.lowerLimit || driveTargetsDegrees[index] > drive.upperLimit)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private float ComputeSolutionCost(float[] candidateRadians)
        {
            var currentRadians = new[]
            {
                GetCurrentMathRadians(waistJoint),
                GetCurrentMathRadians(shoulderJoint),
                GetCurrentMathRadians(elbowJoint),
                GetCurrentMathRadians(forearmRollJoint),
                GetCurrentMathRadians(wristAngleJoint),
                GetCurrentMathRadians(wristRotateJoint),
            };

            var cost = 0f;
            for (var index = 0; index < currentRadians.Length; index++)
            {
                var deltaDegrees = Mathf.DeltaAngle(currentRadians[index] * Mathf.Rad2Deg, candidateRadians[index] * Mathf.Rad2Deg);
                cost += deltaDegrees * deltaDegrees;
            }

            return cost;
        }

        private float GetCurrentMathRadians(RevoluteJointBinding joint)
        {
            if (joint == null || !joint.IsConfigured)
            {
                return 0f;
            }

            var currentDriveDegrees = ReadDriveTarget(joint.articulationBody);
            var mathDegrees = (currentDriveDegrees - joint.zeroOffsetDegrees) * joint.driveSign;
            return mathDegrees * Mathf.Deg2Rad;
        }

        private float[] ResolveGripperTargets(float normalizedGripper)
        {
            var resolvedTargets = new float[_gripperJoints.Count];
            var clamped = Mathf.Clamp01(normalizedGripper);
            var desiredMagnitude = Mathf.Lerp(gripperOpenMeters, gripperClosedMeters, clamped);
            for (var index = 0; index < _gripperJoints.Count; index++)
            {
                var joint = _gripperJoints[index];
                resolvedTargets[index] = joint.zeroOffsetMeters + (joint.driveSign * desiredMagnitude);
            }

            return resolvedTargets;
        }

        private void StepRevoluteJoint(RevoluteJointBinding joint, float targetDegrees, float dt)
        {
            if (joint == null || !joint.IsConfigured)
            {
                return;
            }

            var drive = joint.articulationBody.xDrive;
            var current = drive.target;
            var delta = Mathf.DeltaAngle(current, targetDegrees);
            var maxStep = Mathf.Max(0.01f, joint.maxSpeedDegreesPerSecond) * dt;
            var next = Mathf.Abs(delta) <= maxStep ? targetDegrees : current + Mathf.Sign(delta) * maxStep;
            drive.target = Mathf.Clamp(next, drive.lowerLimit, drive.upperLimit);
            joint.articulationBody.xDrive = drive;
            joint.articulationBody.WakeUp();
        }

        private void StepPrismaticJoint(PrismaticJointBinding joint, float targetMeters, float dt)
        {
            if (joint == null || !joint.IsConfigured)
            {
                return;
            }

            var drive = joint.articulationBody.xDrive;
            var current = drive.target;
            var maxStep = Mathf.Max(0.0001f, joint.maxSpeedMetersPerSecond) * dt;
            var next = Mathf.MoveTowards(current, targetMeters, maxStep);
            drive.target = Mathf.Clamp(next, drive.lowerLimit, drive.upperLimit);
            joint.articulationBody.xDrive = drive;
            joint.articulationBody.WakeUp();
        }

        private bool HasReachedTarget()
        {
            if (endEffectorControlPoint == null)
            {
                return true;
            }

            if (Vector3.Distance(endEffectorControlPoint.position, _targetPosition) > poseToleranceMeters)
            {
                return false;
            }

            for (var index = 0; index < _armJoints.Count; index++)
            {
                if (!_armJoints[index].IsConfigured)
                {
                    continue;
                }

                if (Mathf.Abs(Mathf.DeltaAngle(ReadDriveTarget(_armJoints[index].articulationBody), _targetArmTargetsDegrees[index])) > jointToleranceDegrees)
                {
                    return false;
                }
            }

            for (var index = 0; index < _gripperJoints.Count; index++)
            {
                if (Mathf.Abs(ReadDriveTarget(_gripperJoints[index].articulationBody) - _targetGripperTargetsMeters[index]) > gripperToleranceMeters)
                {
                    return false;
                }
            }

            return true;
        }

        private float ComputeNormalizedGripper()
        {
            if (_gripperJoints.Count == 0)
            {
                return _currentGripper;
            }

            var signedMagnitude = 0f;
            for (var index = 0; index < _gripperJoints.Count; index++)
            {
                var joint = _gripperJoints[index];
                var jointValue = ReadDriveTarget(joint.articulationBody);
                signedMagnitude += Mathf.Abs((jointValue - joint.zeroOffsetMeters) * joint.driveSign);
            }

            var averageMagnitude = signedMagnitude / _gripperJoints.Count;
            return Mathf.InverseLerp(gripperOpenMeters, gripperClosedMeters, averageMagnitude);
        }

        private float[] CaptureArmTargetsDegrees()
        {
            var result = new float[_armJoints.Count];
            for (var index = 0; index < _armJoints.Count; index++)
            {
                result[index] = _armJoints[index].IsConfigured ? ReadDriveTarget(_armJoints[index].articulationBody) : 0f;
            }

            return result;
        }

        private float[] CaptureGripperTargetsMeters()
        {
            var result = new float[_gripperJoints.Count];
            for (var index = 0; index < _gripperJoints.Count; index++)
            {
                result[index] = ReadDriveTarget(_gripperJoints[index].articulationBody);
            }

            return result;
        }

        private void ApplyArmTargetsImmediate(float[] driveTargetsDegrees)
        {
            for (var index = 0; index < _armJoints.Count && index < driveTargetsDegrees.Length; index++)
            {
                var joint = _armJoints[index];
                if (!joint.IsConfigured)
                {
                    continue;
                }

                var drive = joint.articulationBody.xDrive;
                drive.target = Mathf.Clamp(driveTargetsDegrees[index], drive.lowerLimit, drive.upperLimit);
                joint.articulationBody.xDrive = drive;
                joint.articulationBody.Sleep();
            }
        }

        private void ApplyGripperTargetsImmediate(float[] driveTargetsMeters)
        {
            for (var index = 0; index < _gripperJoints.Count && index < driveTargetsMeters.Length; index++)
            {
                var joint = _gripperJoints[index];
                var drive = joint.articulationBody.xDrive;
                drive.target = Mathf.Clamp(driveTargetsMeters[index], drive.lowerLimit, drive.upperLimit);
                joint.articulationBody.xDrive = drive;
                joint.articulationBody.Sleep();
            }
        }

        private static float ReadDriveTarget(ArticulationBody articulationBody)
        {
            return articulationBody != null ? articulationBody.xDrive.target : 0f;
        }

    }
}
