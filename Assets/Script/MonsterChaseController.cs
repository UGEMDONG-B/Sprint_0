using UnityEngine;

namespace Sprint0.Prototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class MonsterChaseController : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField, Min(0f)] float moveSpeed = 2.25f;
        [SerializeField, Min(0f)] float stopDistance = 1.1f;
        [SerializeField, Min(0f)] float rotationSharpness = 10f;

        Rigidbody body;
        float knockedBackUntil;
        float speedMultiplier = 1f;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = false;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.constraints = RigidbodyConstraints.FreezePositionY
                | RigidbodyConstraints.FreezeRotationX
                | RigidbodyConstraints.FreezeRotationZ;
        }

        public void Initialize(Transform chaseTarget)
        {
            target = chaseTarget;
        }

        public void BeginKnockback(float duration)
        {
            knockedBackUntil = Mathf.Max(knockedBackUntil, Time.time + Mathf.Max(0f, duration));
        }

        public void SetSpeedMultiplier(float value)
        {
            speedMultiplier = Mathf.Max(0f, value);
        }

        void FixedUpdate()
        {
            if (Time.time < knockedBackUntil)
            {
                return;
            }

            if (target == null)
            {
                var player = FindFirstObjectByType<ThirdPersonCharacterMotor>();
                target = player != null ? player.transform : null;
                if (target == null)
                {
                    return;
                }
            }

            var toTarget = target.position - transform.position;
            toTarget.y = 0f;
            var distance = toTarget.magnitude;
            if (distance <= stopDistance || distance <= 0.0001f)
            {
                body.linearVelocity = Vector3.zero;
                return;
            }

            var direction = toTarget / distance;
            body.linearVelocity = direction * (moveSpeed * speedMultiplier);

            var targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            var blend = 1f - Mathf.Exp(-rotationSharpness * Time.fixedDeltaTime);
            body.MoveRotation(Quaternion.Slerp(body.rotation, targetRotation, blend));
        }
    }
}
