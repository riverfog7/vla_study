using UnityEngine;

namespace VlaStudy.UnityHarness.Camera
{
    public class CameraCaptureService : MonoBehaviour
    {
        private CameraRegistry _cameraRegistry;

        public void Configure(CameraRegistry cameraRegistry)
        {
            _cameraRegistry = cameraRegistry;
        }

        public byte[] CaptureJpeg(string cameraName, int width, int height, int quality)
        {
            if (_cameraRegistry == null)
            {
                throw new System.InvalidOperationException("Camera registry is not configured.");
            }

            if (!_cameraRegistry.TryGetCamera(cameraName, out var camera))
            {
                throw new System.ArgumentException($"Camera '{cameraName}' is not registered.");
            }

            var resolvedWidth = Mathf.Max(1, width);
            var resolvedHeight = Mathf.Max(1, height);
            var resolvedQuality = Mathf.Clamp(quality, 1, 100);
            var renderTexture = RenderTexture.GetTemporary(resolvedWidth, resolvedHeight, 24, RenderTextureFormat.ARGB32);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;

            try
            {
                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                camera.Render();

                var texture = new Texture2D(resolvedWidth, resolvedHeight, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0f, 0f, resolvedWidth, resolvedHeight), 0, 0);
                texture.Apply();

                var jpgBytes = ImageConversion.EncodeToJPG(texture, resolvedQuality);
                Destroy(texture);
                return jpgBytes;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }
    }
}
