using UnityEngine;
using VlaStudy.UnityHarness.Bootstrap;

namespace VlaStudy.UnityHarness.Camera
{
    [DisallowMultipleComponent]
    public class CameraMountService : MonoBehaviour
    {
        private const string RuntimeCameraRootName = "RuntimeCameras";

        private HarnessSceneReferences _sceneReferences;
        private Transform _runtimeCameraRoot;

        public Transform RuntimeCameraRoot => _runtimeCameraRoot;

        public void Configure(HarnessSceneReferences sceneReferences)
        {
            _sceneReferences = sceneReferences;
            _runtimeCameraRoot = EnsureRuntimeCameraRoot();
        }

        public string NormalizeMountTargetName(string rawMountTarget)
        {
            if (string.IsNullOrWhiteSpace(rawMountTarget))
            {
                return string.Empty;
            }

            var normalized = rawMountTarget.Trim().ToLowerInvariant().Replace("-", "_").Replace(" ", "_");
            return normalized switch
            {
                "proxy_end_effector" or "proxyendeffector" => "proxy_end_effector",
                "proxy_camera_mount" or "proxycameramount" or "gripper" or "gripper_mount" or "wrist" => "proxy_camera_mount",
                _ => throw new System.ArgumentException($"Unsupported camera mount target '{rawMountTarget}'."),
            };
        }

        public bool TryResolveMountTarget(string rawMountTarget, out Transform mountTarget)
        {
            mountTarget = null;
            if (_sceneReferences == null)
            {
                return false;
            }

            var normalized = NormalizeMountTargetName(rawMountTarget);
            switch (normalized)
            {
                case "":
                    return false;
                case "proxy_end_effector":
                    mountTarget = _sceneReferences.ProxyEndEffector;
                    return mountTarget != null;
                case "proxy_camera_mount":
                    mountTarget = _sceneReferences.ProxyCameraMount;
                    return mountTarget != null;
                default:
                    return false;
            }
        }

        public void ApplyAuthoredCameraMount(RegisteredCameraDescriptor descriptor)
        {
            if (descriptor?.Camera == null)
            {
                return;
            }

            if (!descriptor.IsMounted)
            {
                return;
            }

            if (!TryResolveMountTarget(descriptor.MountTargetName, out var mountTarget))
            {
                throw new System.InvalidOperationException($"Could not resolve authored mount target '{descriptor.MountTargetName}' for camera '{descriptor.CameraName}'.");
            }

            MountCamera(descriptor.Camera.transform, mountTarget, descriptor.LocalPositionOffset, descriptor.LocalRotationEuler);
        }

        public void ApplyRuntimePose(
            RegisteredCameraDescriptor descriptor,
            string rawMountTarget,
            Vector3? worldPosition,
            Vector3? worldRotationEuler,
            Vector3? localPosition,
            Vector3? localRotationEuler)
        {
            if (descriptor?.Camera == null)
            {
                throw new System.ArgumentException("Camera descriptor is not configured.");
            }

            var normalizedMountTarget = NormalizeMountTargetName(rawMountTarget);
            if (string.IsNullOrWhiteSpace(normalizedMountTarget))
            {
                descriptor.MountTargetName = string.Empty;
                descriptor.LocalPositionOffset = Vector3.zero;
                descriptor.LocalRotationEuler = Vector3.zero;

                descriptor.Camera.transform.SetParent(RuntimeCameraRoot, true);
                if (worldPosition.HasValue)
                {
                    descriptor.Camera.transform.position = worldPosition.Value;
                }

                if (worldRotationEuler.HasValue)
                {
                    descriptor.Camera.transform.rotation = Quaternion.Euler(worldRotationEuler.Value);
                }

                return;
            }

            if (!TryResolveMountTarget(normalizedMountTarget, out var mountTarget))
            {
                throw new System.ArgumentException($"Unsupported runtime camera mount target '{rawMountTarget}'.");
            }

            descriptor.MountTargetName = normalizedMountTarget;
            descriptor.LocalPositionOffset = localPosition ?? descriptor.LocalPositionOffset;
            descriptor.LocalRotationEuler = localRotationEuler ?? descriptor.LocalRotationEuler;
            MountCamera(descriptor.Camera.transform, mountTarget, descriptor.LocalPositionOffset, descriptor.LocalRotationEuler);
        }

        private void MountCamera(Transform cameraTransform, Transform mountTarget, Vector3 localPosition, Vector3 localRotationEuler)
        {
            cameraTransform.SetParent(mountTarget, false);
            cameraTransform.localPosition = localPosition;
            cameraTransform.localRotation = Quaternion.Euler(localRotationEuler);
        }

        private Transform EnsureRuntimeCameraRoot()
        {
            var existing = transform.Find(RuntimeCameraRootName);
            if (existing != null)
            {
                return existing;
            }

            var runtimeRoot = new GameObject(RuntimeCameraRootName);
            runtimeRoot.transform.SetParent(transform, false);
            return runtimeRoot.transform;
        }
    }
}
