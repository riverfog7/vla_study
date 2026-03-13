using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using VlaStudy.UnityHarness.Camera;

namespace VlaStudy.UnityHarness.Bootstrap
{
    [DisallowMultipleComponent]
    public class HarnessSceneReferences : MonoBehaviour
    {
        [SerializeField] private Transform workspaceTable;
        [SerializeField] private Transform targetObject;
        [FormerlySerializedAs("proxyEndEffector")]
        [SerializeField] private Transform robotEndEffector;
        [FormerlySerializedAs("proxyCameraMount")]
        [SerializeField] private Transform robotCameraMount;
        [SerializeField] private Transform robotBaseFrame;
        [SerializeField] private NamedCameraDefinition[] cameras = System.Array.Empty<NamedCameraDefinition>();

        public Transform WorkspaceTable => workspaceTable;
        public Transform TargetObject => targetObject;
        public Transform RobotEndEffector => robotEndEffector;
        public Transform RobotCameraMount => robotCameraMount;
        public Transform RobotBaseFrame => robotBaseFrame;
        public Transform ProxyEndEffector => robotEndEffector;
        public Transform ProxyCameraMount => robotCameraMount;
        public IReadOnlyList<NamedCameraDefinition> Cameras => cameras;

        public bool IsConfigured()
        {
            return workspaceTable != null &&
                   targetObject != null &&
                   robotEndEffector != null &&
                   robotCameraMount != null &&
                   HasCameraNamed("main");
        }

        public void ConfigureBaseReferences(Transform workspace, Transform target, Transform endEffector, Transform cameraMount)
        {
            workspaceTable = workspace;
            targetObject = target;
            robotEndEffector = endEffector;
            robotCameraMount = cameraMount;
        }

        public void ConfigureRobotReferences(Transform baseFrame, Transform endEffector, Transform cameraMount)
        {
            robotBaseFrame = baseFrame;
            robotEndEffector = endEffector;
            robotCameraMount = cameraMount;
        }

        public bool EnsureCameraDefinition(string cameraName, UnityEngine.Camera cameraComponent, bool enabled, string mountTarget = "")
        {
            if (string.IsNullOrWhiteSpace(cameraName) || cameraComponent == null)
            {
                return false;
            }

            cameras ??= System.Array.Empty<NamedCameraDefinition>();
            for (var index = 0; index < cameras.Length; index++)
            {
                var existing = cameras[index];
                if (existing == null || !string.Equals(existing.cameraName, cameraName, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var changed = existing.camera != cameraComponent || existing.enabled != enabled || existing.mountTarget != mountTarget;
                existing.camera = cameraComponent;
                existing.enabled = enabled;
                existing.mountTarget = mountTarget;
                return changed;
            }

            var updated = new NamedCameraDefinition[cameras.Length + 1];
            cameras.CopyTo(updated, 0);
            updated[cameras.Length] = new NamedCameraDefinition
            {
                cameraName = cameraName,
                camera = cameraComponent,
                enabled = enabled,
                mountTarget = mountTarget,
                localPositionOffset = Vector3.zero,
                localRotationEuler = Vector3.zero,
            };
            cameras = updated;
            return true;
        }

        public bool HasCameraNamed(string cameraName)
        {
            if (string.IsNullOrWhiteSpace(cameraName) || cameras == null)
            {
                return false;
            }

            foreach (var cameraDefinition in cameras)
            {
                if (cameraDefinition != null && cameraDefinition.IsValid() && string.Equals(cameraDefinition.cameraName, cameraName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
