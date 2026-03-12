using UnityEngine;
using VlaStudy.UnityHarness.Data;

namespace VlaStudy.UnityHarness.Robot
{
    public class ProxyPoseAdapter : MonoBehaviour, IRobotAdapter
    {
        [SerializeField] private Vector3 minPosition = new Vector3(-0.75f, 0.55f, -0.75f);
        [SerializeField] private Vector3 maxPosition = new Vector3(0.75f, 1.3f, 0.75f);
        [SerializeField] private float maxLinearSpeedMetersPerSecond = 0.25f;
        [SerializeField] private float maxAngularSpeedDegreesPerSecond = 180f;
        [SerializeField] private float maxGripperSpeedPerSecond = 2f;
        [SerializeField] private float positionToleranceMeters = 0.001f;
        [SerializeField] private float rotationToleranceDegrees = 0.5f;
        [SerializeField] private float gripperTolerance = 0.01f;

        private Transform _proxyTransform;
        private Vector3 _homePosition;
        private Quaternion _homeRotation;
        private float _currentGripper;
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private float _targetGripper;
        private ProxyGripperVisualController _gripperVisualController;
        private Rigidbody _proxyRigidbody;
        private int _lastAcceptedCommandId;
        private bool _lastCommandWasClipped;
        private bool _hasActiveTarget;
        private int _commandCounter;

        public string RobotMode => _hasActiveTarget ? "moving" : "ready";
        public float CurrentGripper => _currentGripper;

        public void Configure(Transform proxyTransform, Vector3 homePosition, Quaternion homeRotation)
        {
            _proxyTransform = proxyTransform;
            _homePosition = homePosition;
            _homeRotation = homeRotation;
            _gripperVisualController = _proxyTransform != null ? _proxyTransform.GetComponent<ProxyGripperVisualController>() : null;
            _proxyRigidbody = _proxyTransform != null ? _proxyTransform.GetComponent<Rigidbody>() : null;
            SetCurrentPoseImmediate(homePosition, homeRotation, 0f);
        }

        public RobotSnapshot GetSnapshot()
        {
            if (_proxyTransform == null)
            {
                return new RobotSnapshot(
                    Vector3.zero,
                    Quaternion.identity,
                    _currentGripper,
                    _targetPosition,
                    _targetRotation,
                    _targetGripper,
                    _lastAcceptedCommandId,
                    _hasActiveTarget,
                    _lastCommandWasClipped);
            }

            return new RobotSnapshot(
                _proxyTransform.position,
                _proxyTransform.rotation,
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
            if (_proxyTransform == null)
            {
                throw new System.InvalidOperationException("Proxy transform is not configured.");
            }

            if (command == null || !command.IsValid())
            {
                throw new System.ArgumentException("Pose command is invalid.");
            }

            if (!string.Equals(command.frame, "world", System.StringComparison.OrdinalIgnoreCase))
            {
                throw new System.ArgumentException("Only the world frame is supported in v1.");
            }

            var unclippedPosition = command.position.ToUnityVector3();
            var clippedPosition = new Vector3(
                Mathf.Clamp(unclippedPosition.x, minPosition.x, maxPosition.x),
                Mathf.Clamp(unclippedPosition.y, minPosition.y, maxPosition.y),
                Mathf.Clamp(unclippedPosition.z, minPosition.z, maxPosition.z));

            _lastCommandWasClipped = clippedPosition != unclippedPosition;
            if (_lastCommandWasClipped)
            {
                Debug.LogWarning($"Clipped proxy pose command from {unclippedPosition} to {clippedPosition}.");
            }

            if (command.blocking)
            {
                Debug.LogWarning("PoseCommand.blocking is not implemented yet; treating move_to_pose as non-blocking.");
            }

            _targetPosition = clippedPosition;
            _targetRotation = command.rotation.ToUnityQuaternion();
            _targetGripper = Mathf.Clamp01(command.gripper);
            _hasActiveTarget = !HasReachedTarget();
            _commandCounter += 1;
            _lastAcceptedCommandId = _commandCounter;
            return _lastAcceptedCommandId;
        }

        public void AdvanceMotion(float dt)
        {
            if (_proxyTransform == null || !_hasActiveTarget)
            {
                return;
            }

            var resolvedDt = Mathf.Max(0f, dt);
            if (resolvedDt <= 0f)
            {
                return;
            }

            var linearStep = maxLinearSpeedMetersPerSecond * resolvedDt;
            var angularStep = maxAngularSpeedDegreesPerSecond * resolvedDt;
            var gripperStep = maxGripperSpeedPerSecond * resolvedDt;
            var nextPosition = Vector3.MoveTowards(_proxyTransform.position, _targetPosition, linearStep);
            var nextRotation = Quaternion.RotateTowards(_proxyTransform.rotation, _targetRotation, angularStep);

            if (_proxyRigidbody != null && _proxyRigidbody.isKinematic)
            {
                _proxyRigidbody.MovePosition(nextPosition);
                _proxyRigidbody.MoveRotation(nextRotation);
            }
            else
            {
                _proxyTransform.position = nextPosition;
                _proxyTransform.rotation = nextRotation;
            }

            _currentGripper = Mathf.MoveTowards(_currentGripper, _targetGripper, gripperStep);
            _gripperVisualController?.SetGripper(_currentGripper);

            if (!HasReachedTarget())
            {
                return;
            }

            SetCurrentPoseImmediate(_targetPosition, _targetRotation, _targetGripper);
        }

        public void ResetRobot()
        {
            if (_proxyTransform == null)
            {
                return;
            }

            SetCurrentPoseImmediate(_homePosition, _homeRotation, 0f);
        }

        private void SetCurrentPoseImmediate(Vector3 position, Quaternion rotation, float gripper)
        {
            if (_proxyTransform == null)
            {
                return;
            }

            if (_proxyRigidbody != null)
            {
                _proxyRigidbody.position = position;
                _proxyRigidbody.rotation = rotation;
                if (!_proxyRigidbody.isKinematic)
                {
                    _proxyRigidbody.linearVelocity = Vector3.zero;
                    _proxyRigidbody.angularVelocity = Vector3.zero;
                }
            }
            else
            {
                _proxyTransform.position = position;
                _proxyTransform.rotation = rotation;
            }

            _currentGripper = Mathf.Clamp01(gripper);
            _gripperVisualController?.SetGripper(_currentGripper);
            _targetPosition = position;
            _targetRotation = rotation;
            _targetGripper = _currentGripper;
            _hasActiveTarget = false;
        }

        public void ClearTransientState()
        {
            _lastAcceptedCommandId = 0;
            _lastCommandWasClipped = false;
            _hasActiveTarget = false;
            _targetPosition = _proxyTransform != null ? _proxyTransform.position : _homePosition;
            _targetRotation = _proxyTransform != null ? _proxyTransform.rotation : _homeRotation;
            _targetGripper = _currentGripper;
        }

        private bool HasReachedTarget()
        {
            if (_proxyTransform == null)
            {
                return true;
            }

            var positionAtTarget = Vector3.Distance(_proxyTransform.position, _targetPosition) <= positionToleranceMeters;
            var rotationAtTarget = Quaternion.Angle(_proxyTransform.rotation, _targetRotation) <= rotationToleranceDegrees;
            var gripperAtTarget = Mathf.Abs(_currentGripper - _targetGripper) <= gripperTolerance;
            return positionAtTarget && rotationAtTarget && gripperAtTarget;
        }
    }
}
