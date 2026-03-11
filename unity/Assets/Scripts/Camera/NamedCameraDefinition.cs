using System;
using UnityEngine;

namespace VlaStudy.UnityHarness.Camera
{
    [Serializable]
    public class NamedCameraDefinition
    {
        public string cameraName = "main";
        public UnityEngine.Camera camera;
        public bool enabled = true;
        public string mountTarget = string.Empty;
        public Vector3 localPositionOffset = Vector3.zero;
        public Vector3 localRotationEuler = Vector3.zero;

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(cameraName) && camera != null;
        }
    }
}
