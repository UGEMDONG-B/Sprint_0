using UnityEngine;
using UnityEngine.Rendering;

namespace Sprint0.GravityCoop
{
    // Each peer reads the same world-space names through its own camera.
    // Run after GravityGame.LateUpdate has positioned that camera.
    [DefaultExecutionOrder(100)]
    public sealed class GravityLandmark : MonoBehaviour
    {
        void LateUpdate() => FaceCamera(Camera.main);
        void OnEnable() => RenderPipelineManager.beginCameraRendering += BeforeCameraRender;
        void OnDisable() => RenderPipelineManager.beginCameraRendering -= BeforeCameraRender;
        void BeforeCameraRender(ScriptableRenderContext context, Camera camera)
        {
            if (GravityGame.Instance != null && GravityGame.Instance.IsSolo) FaceCamera(camera);
        }

        public void FaceCamera(Camera camera)
        {
            if (camera != null) transform.rotation = camera.transform.rotation;
        }
    }
}
