using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RaftSharkDive
{
    [RequireComponent(typeof(CharacterController), typeof(PlayerWaterDetector))]
    public sealed class PlayerController : MonoBehaviour
    {
        private static readonly List<PlayerController> activePlayers = new List<PlayerController>();

        [Header("References")]
        [SerializeField] private Transform cameraPivot;
        [Header("Walking")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 4.5f;
        [SerializeField, Min(0.1f)] private float sprintSpeed = 7f;
        [SerializeField, Min(0.1f)] private float jumpHeight = 1.4f;
        [SerializeField] private float gravity = -22f;
        [Header("Swimming")]
        [SerializeField, Min(0.1f)] private float swimSpeed = 4f;
        [SerializeField, Min(0.1f)] private float verticalSwimSpeed = 3.5f;
        [Header("View")]
        [SerializeField, Range(0.01f, 1f)] private float lookSensitivity = 0.12f;
        [SerializeField, Range(30f, 89f)] private float maxLookAngle = 85f;

        private CharacterController controller;
        private PlayerWaterDetector waterDetector;
        private float verticalVelocity;
        private float pitch;
        private bool inputEnabled = true;

        public bool IsSwimming => waterDetector != null && waterDetector.IsInWater;
        public static IReadOnlyList<PlayerController> ActivePlayers => activePlayers;
        public Transform CameraPivot => cameraPivot;
        public float SprintSpeed => sprintSpeed;
        public float SwimSpeed => swimSpeed;
        public PlayerWaterDetector WaterDetector => waterDetector;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            waterDetector = GetComponent<PlayerWaterDetector>();
            if (cameraPivot == null && Camera.main != null) cameraPivot = Camera.main.transform;
        }

        private void OnEnable()
        {
            if (!activePlayers.Contains(this)) activePlayers.Add(this);
        }

        private void OnDisable()
        {
            activePlayers.Remove(this);
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (!inputEnabled || Keyboard.current == null) return;
            UpdateLook();
            UpdateMovement();
        }

        private void UpdateLook()
        {
            if (Mouse.current == null || Cursor.lockState != CursorLockMode.Locked || cameraPivot == null) return;
            Vector2 delta = Mouse.current.delta.ReadValue() * lookSensitivity;
            transform.Rotate(Vector3.up, delta.x, Space.World);
            pitch = Mathf.Clamp(pitch - delta.y, -maxLookAngle, maxLookAngle);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void UpdateMovement()
        {
            Keyboard kb = Keyboard.current;
            float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            float z = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            Vector3 planar = (transform.right * x + transform.forward * z).normalized;
            if (IsSwimming)
            {
                float vertical = (kb.spaceKey.isPressed ? 1f : 0f) - (kb.leftCtrlKey.isPressed ? 1f : 0f);
                Vector3 motion = planar * swimSpeed + Vector3.up * vertical * verticalSwimSpeed;
                controller.Move(motion * Time.deltaTime);
                verticalVelocity = 0f;
            }
            else
            {
                float speed = kb.leftShiftKey.isPressed ? sprintSpeed : walkSpeed;
                if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
                if (controller.isGrounded && kb.spaceKey.wasPressedThisFrame)
                    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                verticalVelocity += gravity * Time.deltaTime;
                controller.Move((planar * speed + Vector3.up * verticalVelocity) * Time.deltaTime);
            }
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            verticalVelocity = 0f;
            controller.enabled = true;
        }

        public void MoveWithPlatform(Vector3 displacement)
        {
            if (!isActiveAndEnabled || controller == null || !controller.enabled) return;
            controller.Move(displacement);
        }

        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            if (!enabled) verticalVelocity = 0f;
        }
    }
}
