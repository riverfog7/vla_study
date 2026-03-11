using UnityEngine;

namespace VlaStudy.UnityHarness.Bootstrap
{
    [DisallowMultipleComponent]
    public class HarnessSceneReferences : MonoBehaviour
    {
        [SerializeField] private Transform workspaceTable;
        [SerializeField] private Transform targetObject;
        [SerializeField] private Transform proxyEndEffector;
        [SerializeField] private UnityEngine.Camera mainCamera;

        public Transform WorkspaceTable => workspaceTable;
        public Transform TargetObject => targetObject;
        public Transform ProxyEndEffector => proxyEndEffector;
        public UnityEngine.Camera MainCamera => mainCamera;

        public bool IsConfigured()
        {
            return workspaceTable != null && targetObject != null && proxyEndEffector != null && mainCamera != null;
        }

        public void Configure(Transform workspace, Transform target, Transform proxy, UnityEngine.Camera cameraComponent)
        {
            workspaceTable = workspace;
            targetObject = target;
            proxyEndEffector = proxy;
            mainCamera = cameraComponent;
        }
    }
}
