using System.Collections.Generic;
using UnityEngine;

namespace VlaStudy.UnityHarness.Camera
{
    public class CameraRegistry : MonoBehaviour
    {
        private readonly Dictionary<string, UnityEngine.Camera> _cameras = new Dictionary<string, UnityEngine.Camera>();

        public void Register(string cameraName, UnityEngine.Camera camera)
        {
            if (string.IsNullOrWhiteSpace(cameraName) || camera == null)
            {
                return;
            }

            _cameras[cameraName] = camera;
        }

        public bool TryGetCamera(string cameraName, out UnityEngine.Camera camera)
        {
            return _cameras.TryGetValue(cameraName, out camera);
        }
    }
}
