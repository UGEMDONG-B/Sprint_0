using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sprint0.Multiplayer
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class ThirdPersonNetworkPlayer : NetworkBehaviour
    {
        [Header("Movement")]
        [SerializeField] float moveSpeed = 5.5f;
        [SerializeField] float sprintSpeed = 8f;
        [SerializeField] float rotationSharpness = 14f;
        [SerializeField] float gravity = -22f;

        [Header("Camera")]
        [SerializeField] float cameraDistance = 5.5f;
        [SerializeField] float cameraHeight = 1.7f;
        [SerializeField] float mouseSensitivity = 0.12f;
        [SerializeField] float minPitch = -25f;
        [SerializeField] float maxPitch = 65f;

        CharacterController characterController;
        Camera localCamera;
        Renderer capsuleRenderer;
        float verticalVelocity;
        float yaw;
        float pitch = 18f;

        void Awake()
        {
            characterController = GetComponent<CharacterController>();
            capsuleRenderer = GetComponentInChildren<Renderer>();
        }

        public override void OnNetworkSpawn()
        {
            ApplyPlayerColor();
            characterController.enabled = IsOwner;

            if (!IsOwner)
            {
                return;
            }

            var spawnIndex = (int)(OwnerClientId % 8);
            var angle = spawnIndex * Mathf.PI * 0.25f;
            transform.position = new Vector3(Mathf.Cos(angle), 0.05f, Mathf.Sin(angle)) * 3f;

            localCamera = Camera.main;
            if (localCamera != null)
            {
                yaw = transform.eulerAngles.y;
                SnapCamera();
            }
        }

        void Update()
        {
            if (!IsSpawned || !IsOwner || characterController == null || !characterController.enabled)
            {
                return;
            }

            if (MultiplayerGameController.Instance == null || !MultiplayerGameController.Instance.CanControlPlayer)
            {
                return;
            }

            ReadLookInput();
            ReadMovementInput();
        }

        void LateUpdate()
        {
            if (!IsSpawned || !IsOwner || localCamera == null)
            {
                return;
            }

            UpdateCamera();
        }

        void ReadLookInput()
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            var look = mouse.delta.ReadValue() * mouseSensitivity;
            yaw += look.x;
            pitch = Mathf.Clamp(pitch - look.y, minPitch, maxPitch);
        }

        void ReadMovementInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            var input = Vector2.zero;
            if (keyboard.wKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed) input.x -= 1f;
            input = Vector2.ClampMagnitude(input, 1f);

            var cameraYaw = Quaternion.Euler(0f, yaw, 0f);
            var direction = cameraYaw * new Vector3(input.x, 0f, input.y);
            var speed = keyboard.leftShiftKey.isPressed ? sprintSpeed : moveSpeed;

            if (direction.sqrMagnitude > 0.001f)
            {
                var targetRotation = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));
            }

            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            verticalVelocity += gravity * Time.deltaTime;
            var velocity = direction * speed + Vector3.up * verticalVelocity;
            characterController.Move(velocity * Time.deltaTime);
        }

        void UpdateCamera()
        {
            var target = transform.position + Vector3.up * cameraHeight;
            var orbit = Quaternion.Euler(pitch, yaw, 0f);
            var desiredPosition = target - orbit * Vector3.forward * cameraDistance;

            if (Physics.Linecast(target, desiredPosition, out var hit, ~0, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(transform))
            {
                desiredPosition = hit.point + hit.normal * 0.2f;
            }

            localCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(localCamera.transform.position, desiredPosition, 1f - Mathf.Exp(-18f * Time.deltaTime)),
                Quaternion.LookRotation(target - desiredPosition, Vector3.up));
        }

        void SnapCamera()
        {
            var target = transform.position + Vector3.up * cameraHeight;
            var orbit = Quaternion.Euler(pitch, yaw, 0f);
            var cameraPosition = target - orbit * Vector3.forward * cameraDistance;
            localCamera.transform.SetPositionAndRotation(
                cameraPosition,
                Quaternion.LookRotation(target - cameraPosition, Vector3.up));
        }

        void ApplyPlayerColor()
        {
            if (capsuleRenderer == null)
            {
                return;
            }

            var colors = new[]
            {
                new Color(0.12f, 0.78f, 0.72f),
                new Color(1f, 0.40f, 0.32f),
                new Color(0.98f, 0.72f, 0.20f),
                new Color(0.45f, 0.48f, 1f),
                new Color(0.80f, 0.34f, 0.86f),
                new Color(0.35f, 0.78f, 0.34f),
            };

            capsuleRenderer.material.color = colors[OwnerClientId % (ulong)colors.Length];
        }
    }
}
