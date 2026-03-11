using UnityEngine;
using VlaStudy.UnityHarness.Data;

namespace VlaStudy.UnityHarness.Camera
{
    [DisallowMultipleComponent]
    public class RuntimeCameraService : MonoBehaviour
    {
        private CameraRegistry _cameraRegistry;
        private CameraMountService _cameraMountService;

        public void Configure(CameraRegistry cameraRegistry, CameraMountService cameraMountService)
        {
            _cameraRegistry = cameraRegistry;
            _cameraMountService = cameraMountService;
        }

        public UpsertCameraResponse UpsertCamera(UpsertCameraRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.name))
            {
                throw new System.ArgumentException("Camera upsert request requires a non-empty name.");
            }

            if (_cameraRegistry == null || _cameraMountService == null)
            {
                throw new System.InvalidOperationException("Runtime camera service is not configured.");
            }

            if (_cameraRegistry.TryGetDescriptor(request.name, out var existingDescriptor))
            {
                if (!existingDescriptor.IsRuntime)
                {
                    throw new System.ArgumentException($"Camera '{request.name}' is scene-authored and cannot be overwritten by a runtime camera.");
                }

                ApplyRequest(existingDescriptor, request, allowTemplateCopy: false);
                return new UpsertCameraResponse
                {
                    ok = true,
                    camera = existingDescriptor.ToCameraInfo(),
                };
            }

            var createdDescriptor = CreateRuntimeDescriptor(request);
            _cameraRegistry.RegisterRuntime(createdDescriptor);
            return new UpsertCameraResponse
            {
                ok = true,
                camera = createdDescriptor.ToCameraInfo(),
            };
        }

        public DeleteCameraResponse DeleteRuntimeCamera(string cameraName)
        {
            if (string.IsNullOrWhiteSpace(cameraName))
            {
                throw new System.ArgumentException("Camera name is required.");
            }

            if (_cameraRegistry == null)
            {
                throw new System.InvalidOperationException("Runtime camera service is not configured.");
            }

            if (!_cameraRegistry.TryGetDescriptor(cameraName, out var descriptor))
            {
                throw new System.ArgumentException($"Camera '{cameraName}' is not registered.");
            }

            if (!descriptor.IsRuntime)
            {
                throw new System.ArgumentException($"Camera '{cameraName}' is scene-authored and cannot be deleted at runtime.");
            }

            _cameraRegistry.RemoveRuntime(cameraName);
            if (descriptor.Camera != null)
            {
                Destroy(descriptor.Camera.gameObject);
            }

            return new DeleteCameraResponse
            {
                ok = true,
                name = cameraName,
            };
        }

        private RegisteredCameraDescriptor CreateRuntimeDescriptor(UpsertCameraRequest request)
        {
            var templateCameraName = string.IsNullOrWhiteSpace(request.template_camera) ? "main" : request.template_camera;
            if (!_cameraRegistry.TryGetDescriptor(templateCameraName, out var templateDescriptor) || templateDescriptor.Camera == null)
            {
                throw new System.ArgumentException($"Template camera '{templateCameraName}' is not registered.");
            }

            var cameraObject = new GameObject(request.name);
            cameraObject.transform.SetParent(_cameraMountService.RuntimeCameraRoot, false);
            var runtimeCamera = cameraObject.AddComponent<UnityEngine.Camera>();
            runtimeCamera.CopyFrom(templateDescriptor.Camera);
            runtimeCamera.targetTexture = null;
            cameraObject.transform.position = templateDescriptor.Camera.transform.position;
            cameraObject.transform.rotation = templateDescriptor.Camera.transform.rotation;

            var descriptor = new RegisteredCameraDescriptor
            {
                CameraName = request.name,
                Camera = runtimeCamera,
                IsRuntime = true,
                TemplateCameraName = templateCameraName,
            };

            ApplyRequest(descriptor, request, allowTemplateCopy: true);
            return descriptor;
        }

        private void ApplyRequest(RegisteredCameraDescriptor descriptor, UpsertCameraRequest request, bool allowTemplateCopy)
        {
            if (descriptor.Camera == null)
            {
                throw new System.InvalidOperationException($"Camera '{descriptor.CameraName}' has no backing Camera component.");
            }

            if (allowTemplateCopy)
            {
                descriptor.TemplateCameraName = string.IsNullOrWhiteSpace(request.template_camera) ? descriptor.TemplateCameraName : request.template_camera;
            }

            descriptor.Camera.enabled = request.enabled;
            _cameraMountService.ApplyRuntimePose(
                descriptor,
                request.mount_target,
                request.world_position != null ? request.world_position.ToUnityVector3() : null,
                request.world_rotation_euler != null ? request.world_rotation_euler.ToUnityVector3() : null,
                request.local_position != null ? request.local_position.ToUnityVector3() : null,
                request.local_rotation_euler != null ? request.local_rotation_euler.ToUnityVector3() : null);
        }
    }
}
