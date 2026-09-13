using UnityEngine;

namespace RaftSharkDive
{
    [RequireComponent(typeof(PlayerController), typeof(CharacterController))]
    public sealed class PlayerRigAnimator : MonoBehaviour
    {
        public enum MotionState { Idle, Walk, Swim }

        [SerializeField] private Animator animator;
        [SerializeField] private Transform leftUpperLeg;
        [SerializeField] private Transform leftLowerLeg;
        [SerializeField] private Transform leftFoot;
        [SerializeField] private Transform rightUpperLeg;
        [SerializeField] private Transform rightLowerLeg;
        [SerializeField] private Transform rightFoot;
        [SerializeField, Min(0.1f)] private float footProbeDistance = 1f;

        private PlayerController movement;
        private CharacterController characterController;
        private static readonly int MotionStateId = Animator.StringToHash("MotionState");

        public MotionState State { get; private set; }
        public bool LeftFootGrounded { get; private set; }
        public bool RightFootGrounded { get; private set; }
        public Animator RigAnimator => animator;

        public void Configure(Animator targetAnimator, Transform leftUpper, Transform leftLower, Transform leftEnd,
            Transform rightUpper, Transform rightLower, Transform rightEnd)
        {
            animator = targetAnimator;
            leftUpperLeg = leftUpper;
            leftLowerLeg = leftLower;
            leftFoot = leftEnd;
            rightUpperLeg = rightUpper;
            rightLowerLeg = rightLower;
            rightFoot = rightEnd;
        }

        private void Awake()
        {
            movement = GetComponent<PlayerController>();
            characterController = GetComponent<CharacterController>();
        }

        private void Update()
        {
            float planarSpeed = new Vector2(characterController.velocity.x, characterController.velocity.z).magnitude;
            State = movement.IsSwimming ? MotionState.Swim : planarSpeed > 0.15f ? MotionState.Walk : MotionState.Idle;
            if (animator != null) animator.SetInteger(MotionStateId, (int)State);
        }

        private void LateUpdate()
        {
            LeftFootGrounded = false;
            RightFootGrounded = false;
            if (State == MotionState.Swim || !characterController.isGrounded) return;
            LeftFootGrounded = SolveFoot(leftUpperLeg, leftLowerLeg, leftFoot);
            RightFootGrounded = SolveFoot(rightUpperLeg, rightLowerLeg, rightFoot);
        }

        private bool SolveFoot(Transform upper, Transform lower, Transform foot)
        {
            if (upper == null || lower == null || foot == null) return false;
            Vector3 origin = foot.position + Vector3.up * 0.45f;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, footProbeDistance,
                    ~0, QueryTriggerInteraction.Ignore)) return false;
            Vector3 target = hit.point + hit.normal * 0.035f;
            Vector3 upperToFoot = foot.position - upper.position;
            Vector3 upperToTarget = target - upper.position;
            if (upperToFoot.sqrMagnitude > 0.0001f && upperToTarget.sqrMagnitude > 0.0001f)
                upper.rotation = Quaternion.FromToRotation(upperToFoot, upperToTarget) * upper.rotation;
            Vector3 lowerToFoot = foot.position - lower.position;
            Vector3 lowerToTarget = target - lower.position;
            if (lowerToFoot.sqrMagnitude > 0.0001f && lowerToTarget.sqrMagnitude > 0.0001f)
                lower.rotation = Quaternion.FromToRotation(lowerToFoot, lowerToTarget) * lower.rotation;
            foot.position = target;
            foot.rotation = Quaternion.FromToRotation(foot.up, hit.normal) * foot.rotation;
            return true;
        }
    }
}
