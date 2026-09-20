using UnityEngine;
using Unity.Netcode;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Sprint0.Prototype
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ThirdPersonCharacterMotor : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] Camera movementCamera;

        [Header("Movement")]
        [SerializeField, Min(0f)] float moveSpeed = 3f;
        [SerializeField, Min(0f)] float rotationSharpness = 12f;

        Rigidbody body;
        Vector2 moveInput;
        Vector3 worldMoveDirection;
        float attackLockedUntil;
        bool controlsEnabled = true;
        bool externalControl;

        public bool IsMoving => externalControl
            ? worldMoveDirection.sqrMagnitude > 0.0001f
            : controlsEnabled && moveInput.sqrMagnitude > 0.0001f;
        public bool IsAttacking => Time.time < attackLockedUntil;
        public Vector3 MoveDirection => worldMoveDirection;
        public float MoveSpeed => moveSpeed;

        public void AddMoveSpeed(float amount)
        {
            moveSpeed = Mathf.Max(0f, moveSpeed + amount);
        }

        public void SetControlsEnabled(bool value)
        {
            controlsEnabled = value;
            if (!controlsEnabled)
            {
                moveInput = Vector2.zero;
                worldMoveDirection = Vector3.zero;
            }
        }

        public void SetExternalControl(bool value)
        {
            externalControl = value;
            controlsEnabled = !value;
            moveInput = Vector2.zero;
            worldMoveDirection = Vector3.zero;
        }

        public void SetExternalMovementState(Vector3 combinedDirection)
        {
            if (!externalControl)
            {
                return;
            }

            combinedDirection.y = 0f;
            worldMoveDirection = combinedDirection;
        }

        public void ApplyExternalMovement(Vector3 combinedDirection)
        {
            if (!externalControl || body == null)
            {
                return;
            }

            SetExternalMovementState(combinedDirection);
            if (worldMoveDirection.sqrMagnitude <= 0.0001f || IsAttacking)
            {
                return;
            }

            // Do not normalize here. Each tentacle contributes one full movement
            // vector, so matching directions add speed and opposing vectors cancel.
            body.MovePosition(body.position + worldMoveDirection * (moveSpeed * Time.fixedDeltaTime));

            var targetRotation = Quaternion.LookRotation(worldMoveDirection.normalized, Vector3.up);
            var rotationBlend = 1f - Mathf.Exp(-rotationSharpness * Time.fixedDeltaTime);
            body.MoveRotation(Quaternion.Slerp(body.rotation, targetRotation, rotationBlend));
        }

        public void LockMovementForAttack(float duration)
        {
            attackLockedUntil = Mathf.Max(attackLockedUntil, Time.time + Mathf.Max(0f, duration));
            moveInput = Vector2.zero;
            worldMoveDirection = Vector3.zero;
        }

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            ConfigureBody();

            if (movementCamera == null)
            {
                movementCamera = Camera.main;
            }
        }

        void Update()
        {
            if (externalControl)
            {
                return;
            }

            if (IsSpawned && !IsOwner)
            {
                moveInput = Vector2.zero;
                worldMoveDirection = Vector3.zero;
                return;
            }

            moveInput = !controlsEnabled || IsAttacking ? Vector2.zero : ReadNormalizedMoveInput();

            if (movementCamera == null)
            {
                movementCamera = Camera.main;
            }
        }

        void FixedUpdate()
        {
            if (externalControl)
            {
                return;
            }

            if (IsSpawned && !IsOwner)
            {
                worldMoveDirection = Vector3.zero;
                return;
            }

            if (movementCamera == null || !controlsEnabled || IsAttacking)
            {
                worldMoveDirection = Vector3.zero;
                return;
            }

            var cameraForward = movementCamera.transform.forward;
            var cameraRight = movementCamera.transform.right;
            cameraForward.y = 0f;
            cameraRight.y = 0f;
            cameraForward.Normalize();
            cameraRight.Normalize();

            worldMoveDirection = cameraForward * moveInput.y + cameraRight * moveInput.x;
            if (worldMoveDirection.sqrMagnitude > 1f)
            {
                worldMoveDirection.Normalize();
            }

            if (worldMoveDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            body.MovePosition(body.position + worldMoveDirection * (moveSpeed * Time.fixedDeltaTime));

            var targetRotation = Quaternion.LookRotation(worldMoveDirection, Vector3.up);
            var rotationBlend = 1f - Mathf.Exp(-rotationSharpness * Time.fixedDeltaTime);
            body.MoveRotation(Quaternion.Slerp(body.rotation, targetRotation, rotationBlend));
        }

        void ConfigureBody()
        {
            // The Step 1 body uses a non-convex MeshCollider. Keeping this Rigidbody
            // kinematic preserves that valid collider setup while still using the
            // physics loop and MovePosition/MoveRotation for deterministic movement.
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        static Vector2 ReadNormalizedMoveInput()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            var input = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical"));
#elif ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return Vector2.zero;
            }

            var input = new Vector2(
                (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                    - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f)
                    - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f));
#else
            var input = Vector2.zero;
#endif
            return Vector2.ClampMagnitude(input, 1f);
        }
    }
}
