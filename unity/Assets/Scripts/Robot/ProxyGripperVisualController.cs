using UnityEngine;

namespace VlaStudy.UnityHarness.Robot
{
    [DisallowMultipleComponent]
    public class ProxyGripperVisualController : MonoBehaviour
    {
        private const string LeftJawName = "JawLeft";
        private const string RightJawName = "JawRight";

        [SerializeField] private Transform leftJaw;
        [SerializeField] private Transform rightJaw;
        [SerializeField] private float jawTravelDistance = 0.028f;

        private Vector3 _leftJawOpenLocalPosition;
        private Vector3 _rightJawOpenLocalPosition;
        private bool _positionsInitialized;

        private void Awake()
        {
            AutoAssignJaws();
            CacheOpenPositions();
            SetGripper(0f);
        }

        private void OnValidate()
        {
            AutoAssignJaws();
            CacheOpenPositions();
        }

        public void SetGripper(float normalizedGripper)
        {
            AutoAssignJaws();
            if (!_positionsInitialized)
            {
                CacheOpenPositions();
            }

            if (!_positionsInitialized)
            {
                return;
            }

            var clamped = Mathf.Clamp01(normalizedGripper);
            var inwardOffset = jawTravelDistance * clamped;

            if (leftJaw != null)
            {
                leftJaw.localPosition = _leftJawOpenLocalPosition + new Vector3(inwardOffset, 0f, 0f);
            }

            if (rightJaw != null)
            {
                rightJaw.localPosition = _rightJawOpenLocalPosition + new Vector3(-inwardOffset, 0f, 0f);
            }
        }

        private void AutoAssignJaws()
        {
            leftJaw ??= transform.Find(LeftJawName);
            rightJaw ??= transform.Find(RightJawName);
        }

        private void CacheOpenPositions()
        {
            if (leftJaw == null || rightJaw == null)
            {
                _positionsInitialized = false;
                return;
            }

            _leftJawOpenLocalPosition = leftJaw.localPosition;
            _rightJawOpenLocalPosition = rightJaw.localPosition;
            _positionsInitialized = true;
        }
    }
}
