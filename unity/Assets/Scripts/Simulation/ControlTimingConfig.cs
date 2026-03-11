using UnityEngine;

namespace VlaStudy.UnityHarness.Simulation
{
    [DisallowMultipleComponent]
    public class ControlTimingConfig : MonoBehaviour
    {
        [SerializeField] private float physicsDt = 0.02f;
        [SerializeField] private float policyPeriodSeconds = 0.2f;
        [SerializeField] private int defaultManualStepCount = 1;

        public float PhysicsDt => physicsDt;
        public float PolicyPeriodSeconds => policyPeriodSeconds;
        public float PolicyHz => 1f / policyPeriodSeconds;
        public int StepsPerAction => Mathf.Max(1, Mathf.RoundToInt(policyPeriodSeconds / physicsDt));
        public int DefaultManualStepCount => Mathf.Max(1, defaultManualStepCount);

        private void OnValidate()
        {
            physicsDt = Mathf.Max(0.0001f, physicsDt);
            policyPeriodSeconds = Mathf.Max(physicsDt, policyPeriodSeconds);
            defaultManualStepCount = Mathf.Max(1, defaultManualStepCount);
        }
    }
}
