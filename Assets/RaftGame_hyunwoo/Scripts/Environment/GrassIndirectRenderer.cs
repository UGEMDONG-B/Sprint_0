using UnityEngine;
using UnityEngine.Rendering;

namespace RaftSharkDive
{
    public sealed class GrassIndirectRenderer : MonoBehaviour
    {
        [SerializeField] private Mesh grassMesh;
        [SerializeField] private Material grassMaterial;
        [SerializeField] private Matrix4x4[] localMatrices;
        [SerializeField] private Bounds localBounds = new Bounds(Vector3.zero, new Vector3(140f, 40f, 140f));
        [SerializeField, Min(0f)] private float playerHideDistance = 1.25f;

        private ComputeBuffer matrixBuffer;
        private ComputeBuffer argumentsBuffer;
        private MaterialPropertyBlock properties;
        private PlayerController player;

        public int InstanceCount => localMatrices?.Length ?? 0;
        public Mesh GrassMesh => grassMesh;
        public Material GrassMaterial => grassMaterial;
        public bool BuffersReady => matrixBuffer != null && argumentsBuffer != null;

        public void Configure(Mesh mesh, Material material, Matrix4x4[] matrices, Bounds bounds)
        {
            grassMesh = mesh;
            grassMaterial = material;
            localMatrices = matrices;
            localBounds = bounds;
        }

        private void OnEnable()
        {
            player = FindFirstObjectByType<PlayerController>();
            BuildBuffers();
        }

        private void OnDisable() => ReleaseBuffers();
        private void OnDestroy() => ReleaseBuffers();

        private void BuildBuffers()
        {
            ReleaseBuffers();
            if (grassMesh == null || grassMaterial == null || localMatrices == null || localMatrices.Length == 0) return;
            matrixBuffer = new ComputeBuffer(localMatrices.Length, sizeof(float) * 16);
            matrixBuffer.SetData(localMatrices);
            uint[] arguments =
            {
                grassMesh.GetIndexCount(0), (uint)localMatrices.Length, grassMesh.GetIndexStart(0),
                (uint)grassMesh.GetBaseVertex(0), 0
            };
            argumentsBuffer = new ComputeBuffer(1, arguments.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            argumentsBuffer.SetData(arguments);
            properties = new MaterialPropertyBlock();
            properties.SetBuffer("_InstanceMatrices", matrixBuffer);
        }

        private void LateUpdate()
        {
            if (!BuffersReady || grassMesh == null || grassMaterial == null) return;
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            properties.SetMatrix("_RootMatrix", transform.localToWorldMatrix);
            properties.SetVector("_PlayerPosition", player != null ? player.transform.position : new Vector3(99999f, 99999f, 99999f));
            properties.SetFloat("_HideDistance", playerHideDistance);
            Vector3 worldCenter = transform.TransformPoint(localBounds.center);
            Vector3 worldSize = Vector3.Scale(localBounds.size, transform.lossyScale);
            Graphics.DrawMeshInstancedIndirect(grassMesh, 0, grassMaterial,
                new Bounds(worldCenter, worldSize), argumentsBuffer, 0, properties,
                ShadowCastingMode.Off, true, gameObject.layer);
        }

        private void ReleaseBuffers()
        {
            matrixBuffer?.Release();
            argumentsBuffer?.Release();
            matrixBuffer = null;
            argumentsBuffer = null;
        }
    }
}
