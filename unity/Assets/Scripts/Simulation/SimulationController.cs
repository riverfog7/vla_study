using System;
using UnityEngine;
using UnityEngine.Serialization;
using VlaStudy.UnityHarness.Robot;

namespace VlaStudy.UnityHarness.Simulation
{
    public class SimulationController : MonoBehaviour
    {
        [FormerlySerializedAs("defaultStepDt")]
        [SerializeField] private float fallbackPhysicsDt = 0.02f;
        [SerializeField] private ControlTimingConfig controlTimingConfig;

        public float SimTime { get; private set; }
        public int StepCount { get; private set; }
        public float PhysicsDt => controlTimingConfig != null ? controlTimingConfig.PhysicsDt : fallbackPhysicsDt;
        public float PolicyPeriodSeconds => controlTimingConfig != null ? controlTimingConfig.PolicyPeriodSeconds : PhysicsDt;
        public int StepsPerAction => controlTimingConfig != null ? controlTimingConfig.StepsPerAction : 1;

        private IRobotAdapter _robotAdapter;

        private void Reset()
        {
            controlTimingConfig = GetComponent<ControlTimingConfig>();
        }

        private void OnValidate()
        {
            controlTimingConfig ??= GetComponent<ControlTimingConfig>();
            fallbackPhysicsDt = Mathf.Max(0.0001f, fallbackPhysicsDt);
        }

        private void Awake()
        {
            controlTimingConfig ??= GetComponent<ControlTimingConfig>();
            ApplySimulationMode(PhysicsDt);
            ResetSimulationClock();
        }

        public void Configure(IRobotAdapter robotAdapter)
        {
            _robotAdapter = robotAdapter;
            ApplySimulationMode(PhysicsDt);
        }

        public StepResult StepSimulation(int steps, float dt)
        {
            var resolvedSteps = Mathf.Max(1, steps);
            var resolvedDt = ResolveStepDt(dt);
            ApplySimulationMode(resolvedDt);

            for (var index = 0; index < resolvedSteps; index++)
            {
                _robotAdapter?.AdvanceMotion(resolvedDt);
                Physics.Simulate(resolvedDt);
                SimTime += resolvedDt;
                StepCount += 1;
            }

            return new StepResult(SimTime, StepCount);
        }

        public StepResult StepControlInterval(int controlIntervals = 1)
        {
            var resolvedIntervals = Mathf.Max(1, controlIntervals);
            return StepSimulation(resolvedIntervals * StepsPerAction, PhysicsDt);
        }

        public void ResetSimulationClock()
        {
            SimTime = 0f;
            StepCount = 0;
        }

        private float ResolveStepDt(float requestedDt)
        {
            if (requestedDt <= 0f)
            {
                return PhysicsDt;
            }

            if (Mathf.Abs(requestedDt - PhysicsDt) > 0.00001f)
            {
                throw new ArgumentException($"Requested dt {requestedDt:0.#####} does not match configured physics_dt {PhysicsDt:0.#####}. Update ControlTimingConfig instead.");
            }

            return PhysicsDt;
        }

        private static void ApplySimulationMode(float dt)
        {
            Physics.simulationMode = SimulationMode.Script;
            Time.fixedDeltaTime = dt;
        }
    }

    public struct StepResult
    {
        public float SimTime { get; }
        public int StepCount { get; }

        public StepResult(float simTime, int stepCount)
        {
            SimTime = simTime;
            StepCount = stepCount;
        }
    }
}
