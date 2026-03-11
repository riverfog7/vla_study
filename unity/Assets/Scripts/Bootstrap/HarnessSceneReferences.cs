using System.Collections.Generic;
using UnityEngine;
using VlaStudy.UnityHarness.Camera;

namespace VlaStudy.UnityHarness.Bootstrap
{
    [DisallowMultipleComponent]
    public class HarnessSceneReferences : MonoBehaviour
    {
        [SerializeField] private Transform workspaceTable;
        [SerializeField] private Transform targetObject;
        [SerializeField] private Transform proxyEndEffector;
        [SerializeField] private Transform proxyCameraMount;
        [SerializeField] private NamedCameraDefinition[] cameras = System.Array.Empty<NamedCameraDefinition>();

        public Transform WorkspaceTable => workspaceTable;
        public Transform TargetObject => targetObject;
        public Transform ProxyEndEffector => proxyEndEffector;
        public Transform ProxyCameraMount => proxyCameraMount;
        public IReadOnlyList<NamedCameraDefinition> Cameras => cameras;

        public bool IsConfigured()
        {
            return workspaceTable != null &&
                   targetObject != null &&
                   proxyEndEffector != null &&
                   proxyCameraMount != null &&
                   HasCameraNamed("main");
        }

        public void ConfigureBaseReferences(Transform workspace, Transform target, Transform proxy, Transform cameraMount)
        {
            workspaceTable = workspace;
            targetObject = target;
            proxyEndEffector = proxy;
            proxyCameraMount = cameraMount;
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
