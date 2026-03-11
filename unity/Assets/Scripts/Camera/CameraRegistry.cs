using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VlaStudy.UnityHarness.Data;

namespace VlaStudy.UnityHarness.Camera
{
    public class CameraRegistry : MonoBehaviour
    {
        private readonly Dictionary<string, RegisteredCameraDescriptor> _cameras = new Dictionary<string, RegisteredCameraDescriptor>(System.StringComparer.OrdinalIgnoreCase);

        public void Clear()
        {
            _cameras.Clear();
        }

        public void RegisterAuthored(RegisteredCameraDescriptor descriptor)
        {
            RegisterDescriptor(descriptor);
        }

        public void RegisterRuntime(RegisteredCameraDescriptor descriptor)
        {
            RegisterDescriptor(descriptor);
        }

        public bool TryGetDescriptor(string cameraName, out RegisteredCameraDescriptor descriptor)
        {
            return _cameras.TryGetValue(cameraName, out descriptor);
        }

        public bool TryGetCamera(string cameraName, out UnityEngine.Camera camera)
        {
            if (_cameras.TryGetValue(cameraName, out var descriptor) && descriptor.Camera != null)
            {
                camera = descriptor.Camera;
                return true;
            }

            camera = null;
            return false;
        }

        public CameraListResponse BuildCameraListResponse()
        {
            return new CameraListResponse
            {
                cameras = _cameras.Values
                    .OrderBy(descriptor => descriptor.CameraName)
                    .Select(descriptor => descriptor.ToCameraInfo())
                    .ToArray(),
            };
        }

        public void RemoveRuntime(string cameraName)
        {
            if (_cameras.TryGetValue(cameraName, out var descriptor) && descriptor.IsRuntime)
            {
                _cameras.Remove(cameraName);
            }
        }

        private void RegisterDescriptor(RegisteredCameraDescriptor descriptor)
        {
            if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.CameraName) || descriptor.Camera == null)
            {
                throw new System.ArgumentException("Camera descriptor must include a name and Camera component.");
            }

            _cameras[descriptor.CameraName] = descriptor;
        }
    }
}
