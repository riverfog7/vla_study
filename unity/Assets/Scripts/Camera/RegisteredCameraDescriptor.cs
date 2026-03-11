using UnityEngine;
using VlaStudy.UnityHarness.Data;

namespace VlaStudy.UnityHarness.Camera
{
    public class RegisteredCameraDescriptor
    {
        public string CameraName { get; set; }
        public UnityEngine.Camera Camera { get; set; }
        public bool IsRuntime { get; set; }
        public string MountTargetName { get; set; } = string.Empty;
        public Vector3 LocalPositionOffset { get; set; } = Vector3.zero;
        public Vector3 LocalRotationEuler { get; set; } = Vector3.zero;
        public string TemplateCameraName { get; set; } = string.Empty;

        public bool IsMounted => !string.IsNullOrWhiteSpace(MountTargetName);
        public string Source => IsRuntime ? "runtime" : "authored";

        public CameraInfo ToCameraInfo()
        {
            return new CameraInfo
            {
                name = CameraName,
                source = Source,
                enabled = Camera != null && Camera.enabled,
                mounted = IsMounted,
                mount_target = MountTargetName,
                world_pose = Camera != null ? new PoseData(Camera.transform.position, Camera.transform.rotation) : new PoseData(Vector3.zero, Quaternion.identity),
                local_position = new Vector3Data(LocalPositionOffset),
                local_rotation_euler = new Vector3Data(LocalRotationEuler),
                field_of_view = Camera != null ? Camera.fieldOfView : 0f,
                template_camera = TemplateCameraName,
            };
        }
    }
}
