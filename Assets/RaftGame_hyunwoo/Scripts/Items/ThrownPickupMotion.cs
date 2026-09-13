using UnityEngine;

namespace RaftSharkDive
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ThrownPickupMotion : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float waterSettleTime = 0.4f;
        [SerializeField, Min(0.05f)] private float surfaceDepth = 0.22f;

        private Rigidbody body;
        private bool enteredWater;
        private float settleAt;

        public bool SettledInWater { get; private set; }
        public bool SettledOnRaft { get; private set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            if (GameManager.Instance != null && transform.position.y <= GameManager.Instance.WaterSurfaceY)
                BeginWaterSettle();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (enteredWater || other.GetComponentInParent<WaterVolume>() == null) return;
            BeginWaterSettle();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (SettledOnRaft || collision.collider == null || body == null) return;
            RaftAutoMover raft = collision.collider.GetComponentInParent<RaftAutoMover>();
            if (raft == null) return;
            enteredWater = false;
            SettledInWater = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            transform.SetParent(raft.transform, true);
            SettledOnRaft = true;
        }

        private void FixedUpdate()
        {
            if (!enteredWater || SettledInWater || Time.time < settleAt || body == null) return;
            Vector3 position = body.position;
            if (GameManager.Instance != null) position.y = GameManager.Instance.WaterSurfaceY - surfaceDepth;
            body.position = position;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            SettledInWater = true;
        }

        private void BeginWaterSettle()
        {
            if (body == null) body = GetComponent<Rigidbody>();
            enteredWater = true;
            settleAt = Time.time + waterSettleTime;
            body.useGravity = false;
            body.linearDamping = 8f;
            body.angularDamping = 8f;
            body.linearVelocity *= 0.12f;
        }
    }
}
