using UnityEngine;
using VlaStudy.UnityHarness.Robot;

namespace VlaStudy.UnityHarness.Simulation
{
    public class TaskResetService : MonoBehaviour
    {
        private SimulationController _simulationController;
        private IRobotAdapter _robotAdapter;
        private Transform _targetTransform;
        private Vector3 _targetInitialPosition;
        private Quaternion _targetInitialRotation;
        private RigidbodyResetState[] _rigidbodyResetStates = System.Array.Empty<RigidbodyResetState>();

        public void Configure(SimulationController simulationController, IRobotAdapter robotAdapter, Transform targetTransform)
        {
            _simulationController = simulationController;
            _robotAdapter = robotAdapter;
            _targetTransform = targetTransform;

            if (_targetTransform != null)
            {
                _targetInitialPosition = _targetTransform.position;
                _targetInitialRotation = _targetTransform.rotation;
            }

            CaptureInitialRigidbodyState();
        }

        public void ResetScene()
        {
            _simulationController?.ResetSimulationClock();
            _robotAdapter?.ResetRobot();

            if (_targetTransform != null)
            {
                _targetTransform.position = _targetInitialPosition;
                _targetTransform.rotation = _targetInitialRotation;
            }

            RestoreRigidbodies();

            if (_robotAdapter is ProxyPoseAdapter proxyPoseAdapter)
            {
                proxyPoseAdapter.ClearTransientState();
            }
        }

        private void CaptureInitialRigidbodyState()
        {
            var rigidbodies = FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
            _rigidbodyResetStates = new RigidbodyResetState[rigidbodies.Length];

            for (var index = 0; index < rigidbodies.Length; index++)
            {
                _rigidbodyResetStates[index] = new RigidbodyResetState(rigidbodies[index]);
            }
        }

        private void RestoreRigidbodies()
        {
            foreach (var rigidbodyResetState in _rigidbodyResetStates)
            {
                rigidbodyResetState.Restore();
            }
        }

        private readonly struct RigidbodyResetState
        {
            private readonly Rigidbody _rigidbody;
            private readonly Vector3 _position;
            private readonly Quaternion _rotation;
            private readonly Vector3 _linearVelocity;
            private readonly Vector3 _angularVelocity;
            private readonly bool _isKinematic;

            public RigidbodyResetState(Rigidbody rigidbody)
            {
                _rigidbody = rigidbody;
                _position = rigidbody.position;
                _rotation = rigidbody.rotation;
                _linearVelocity = rigidbody.linearVelocity;
                _angularVelocity = rigidbody.angularVelocity;
                _isKinematic = rigidbody.isKinematic;
            }

            public void Restore()
            {
                if (_rigidbody == null)
                {
                    return;
                }

                _rigidbody.isKinematic = true;
                _rigidbody.position = _position;
                _rigidbody.rotation = _rotation;
                _rigidbody.linearVelocity = _linearVelocity;
                _rigidbody.angularVelocity = _angularVelocity;
                _rigidbody.isKinematic = _isKinematic;
                _rigidbody.Sleep();
            }
        }
    }
}
