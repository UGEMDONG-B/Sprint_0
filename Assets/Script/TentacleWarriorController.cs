using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;

namespace Sprint0.Prototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class TentacleWarriorController : MonoBehaviour
    {
        [Header("Shared Movement")]
        [SerializeField, Min(0f)] float speedPerPlayer = 2.75f;
        [SerializeField, Min(0f)] float rotationSharpness = 14f;
        [SerializeField] float gravity = -25f;

        [Header("Third Person Camera")]
        [SerializeField] Camera gameplayCamera;
        [SerializeField, Min(0f)] float cameraHeight = 1.4f;
        [SerializeField, Min(0.1f)] float cameraDistance = 5.5f;
        [SerializeField, Min(0.1f)] float minCameraDistance = 2.2f;
        [SerializeField, Min(0.1f)] float maxCameraDistance = 10f;
        [SerializeField, Min(0f)] float orbitSensitivity = 0.16f;
        [SerializeField, Min(0f)] float zoomSensitivity = 0.012f;
        [SerializeField] float minPitch = 12f;
        [SerializeField] float maxPitch = 72f;
        [SerializeField, Min(0f)] float cameraPositionSharpness = 18f;
        [SerializeField, Min(0f)] float cameraCollisionRadius = 0.25f;

        [Header("Tentacle Aim")]
        [SerializeField, Min(0.1f)] float tentacleAimDistance = 2.5f;
        [SerializeField] float tentacleAimHeight = 0.45f;
        [SerializeField, Min(0f)] float targetSpacing = 0.35f;
        [SerializeField, Min(0f)] float aimSharpness = 22f;
        [SerializeField, Min(0f)] float wriggleAmount = 0.09f;
        [SerializeField, Min(0f)] float wriggleSpeed = 4.5f;
        [SerializeField] LayerMask aimLayers = ~0;

        [Header("Prototype HUD")]
        [SerializeField] bool showInputDebug = true;

        readonly List<Transform> tentacleTargets = new();

        CharacterController characterController;
        float verticalVelocity;
        float cameraYaw;
        float cameraPitch = 34f;
        bool rotatingCamera;

        public Vector2 WasdInput { get; private set; }
        public Vector2 ArrowInput { get; private set; }
        public Vector2 CombinedInput { get; private set; }

        void Awake()
        {
            characterController = GetComponent<CharacterController>();
            ResolveCamera();
            CollectTentacleTargets();

            if (gameplayCamera != null)
            {
                cameraYaw = gameplayCamera.transform.eulerAngles.y;
            }
        }

        void Start()
        {
            SnapCamera();
        }

        void Update()
        {
            ReadMovementInput();
            MoveSharedBody();
            ReadCameraInput();
        }

        void LateUpdate()
        {
            ResolveCamera();
            UpdateCamera();
            UpdateTentacleTargets();
        }

        void OnDisable()
        {
            SetCursorCaptured(false);
        }

        void ReadMovementInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                WasdInput = Vector2.zero;
                ArrowInput = Vector2.zero;
                CombinedInput = Vector2.zero;
                return;
            }

            WasdInput = ReadDigitalVector(
                keyboard.aKey.isPressed,
                keyboard.dKey.isPressed,
                keyboard.sKey.isPressed,
                keyboard.wKey.isPressed);

            ArrowInput = ReadDigitalVector(
                keyboard.leftArrowKey.isPressed,
                keyboard.rightArrowKey.isPressed,
                keyboard.downArrowKey.isPressed,
                keyboard.upArrowKey.isPressed);

            // Each player's digital vector contributes independently. Equal directions
            // accelerate up to 2x; opposing directions cancel each other out.
            CombinedInput = WasdInput + ArrowInput;
        }

        static Vector2 ReadDigitalVector(bool left, bool right, bool down, bool up)
        {
            var value = new Vector2(
                (right ? 1f : 0f) - (left ? 1f : 0f),
                (up ? 1f : 0f) - (down ? 1f : 0f));

            return value.sqrMagnitude > 1f ? value.normalized : value;
        }

        void MoveSharedBody()
        {
            if (characterController == null)
            {
                return;
            }

            var cameraTransform = gameplayCamera != null ? gameplayCamera.transform : transform;
            var forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            var right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            var planarVelocity = (right * CombinedInput.x + forward * CombinedInput.y) * speedPerPlayer;

            if (planarVelocity.sqrMagnitude > 0.001f)
            {
                var desiredRotation = Quaternion.LookRotation(planarVelocity.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    desiredRotation,
                    1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));
            }

            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }

            verticalVelocity += gravity * Time.deltaTime;
            characterController.Move((planarVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
        }

        void ReadCameraInput()
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            if (mouse.rightButton.wasPressedThisFrame)
            {
                rotatingCamera = true;
                SetCursorCaptured(true);
            }
            else if (mouse.rightButton.wasReleasedThisFrame)
            {
                rotatingCamera = false;
                SetCursorCaptured(false);
            }

            if (rotatingCamera && mouse.rightButton.isPressed)
            {
                var delta = mouse.delta.ReadValue() * orbitSensitivity;
                cameraYaw += delta.x;
                cameraPitch = Mathf.Clamp(cameraPitch - delta.y, minPitch, maxPitch);
            }

            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                cameraDistance = Mathf.Clamp(
                    cameraDistance - scroll * zoomSensitivity,
                    minCameraDistance,
                    maxCameraDistance);
            }
        }

        void UpdateCamera()
        {
            if (gameplayCamera == null)
            {
                return;
            }

            var pivot = transform.position + Vector3.up * cameraHeight;
            var orbit = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
            var desiredPosition = pivot - orbit * Vector3.forward * cameraDistance;

            var toCamera = desiredPosition - pivot;
            var safeDistance = toCamera.magnitude;
            if (safeDistance > 0.001f && Physics.SphereCast(
                    pivot,
                    cameraCollisionRadius,
                    toCamera / safeDistance,
                    out var hit,
                    safeDistance,
                    aimLayers,
                    QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(transform))
            {
                desiredPosition = hit.point + hit.normal * cameraCollisionRadius;
            }

            var blend = 1f - Mathf.Exp(-cameraPositionSharpness * Time.deltaTime);
            gameplayCamera.transform.position = Vector3.Lerp(gameplayCamera.transform.position, desiredPosition, blend);
            gameplayCamera.transform.rotation = Quaternion.LookRotation(
                pivot - gameplayCamera.transform.position,
                Vector3.up);
        }

        void SnapCamera()
        {
            if (gameplayCamera == null)
            {
                return;
            }

            var pivot = transform.position + Vector3.up * cameraHeight;
            var orbit = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
            var position = pivot - orbit * Vector3.forward * cameraDistance;
            gameplayCamera.transform.SetPositionAndRotation(
                position,
                Quaternion.LookRotation(pivot - position, Vector3.up));
        }

        void UpdateTentacleTargets()
        {
            if (gameplayCamera == null || tentacleTargets.Count == 0)
            {
                return;
            }

            var mouse = Mouse.current;
            var screenPoint = rotatingCamera || mouse == null
                ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
                : mouse.position.ReadValue();

            var ray = gameplayCamera.ScreenPointToRay(screenPoint);
            var rawAimPoint = FindAimPoint(ray);
            var flatDirection = Vector3.ProjectOnPlane(rawAimPoint - transform.position, Vector3.up);

            if (flatDirection.sqrMagnitude < 0.0001f)
            {
                flatDirection = Vector3.ProjectOnPlane(gameplayCamera.transform.forward, Vector3.up);
            }

            flatDirection.Normalize();
            var center = transform.position + flatDirection * tentacleAimDistance + Vector3.up * tentacleAimHeight;
            var side = Vector3.Cross(Vector3.up, flatDirection).normalized;
            var blend = 1f - Mathf.Exp(-aimSharpness * Time.deltaTime);

            for (var index = 0; index < tentacleTargets.Count; index++)
            {
                var target = tentacleTargets[index];
                if (target == null)
                {
                    continue;
                }

                var centeredIndex = index - (tentacleTargets.Count - 1) * 0.5f;
                var phase = Time.time * wriggleSpeed + index * 1.7f;
                var wriggle = side * Mathf.Sin(phase) * wriggleAmount
                    + Vector3.up * Mathf.Cos(phase * 0.83f) * wriggleAmount;
                var desiredPosition = center + side * centeredIndex * targetSpacing + wriggle;
                target.position = Vector3.Lerp(target.position, desiredPosition, blend);
            }
        }

        Vector3 FindAimPoint(Ray ray)
        {
            var hits = Physics.RaycastAll(ray, 200f, aimLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                if (!hit.transform.IsChildOf(transform))
                {
                    return hit.point;
                }
            }

            var groundPlane = new Plane(Vector3.up, transform.position);
            return groundPlane.Raycast(ray, out var distance)
                ? ray.GetPoint(distance)
                : transform.position + Vector3.ProjectOnPlane(gameplayCamera.transform.forward, Vector3.up).normalized * tentacleAimDistance;
        }

        void ResolveCamera()
        {
            if (gameplayCamera == null)
            {
                gameplayCamera = Camera.main;
            }
        }

        void CollectTentacleTargets()
        {
            tentacleTargets.Clear();
            var constraints = GetComponentsInChildren<ChainIKConstraint>(true);

            foreach (var constraint in constraints)
            {
                var target = constraint.data.target;
                if (target != null && !tentacleTargets.Contains(target))
                {
                    tentacleTargets.Add(target);
                }
            }

            if (tentacleTargets.Count == 0)
            {
                Debug.LogWarning("[TentacleWarrior] No ChainIKConstraint targets were found under the player.", this);
            }
        }

        static void SetCursorCaptured(bool captured)
        {
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }

        void OnGUI()
        {
            if (!showInputDebug)
            {
                return;
            }

            const int width = 390;
            GUILayout.BeginArea(new Rect(16, 16, width, 128), GUI.skin.box);
            GUILayout.Label("촉수 용사 프로토타입");
            GUILayout.Label($"P1 WASD: {WasdInput}   P2 방향키: {ArrowInput}");
            GUILayout.Label($"합산 이동: {CombinedInput}   기여 인원: {(WasdInput == Vector2.zero ? 0 : 1) + (ArrowInput == Vector2.zero ? 0 : 1)}");
            GUILayout.Label("우클릭 드래그: 카메라 회전 / 휠: 확대·축소 / 마우스: 촉수 조준");
            GUILayout.EndArea();
        }
    }
}
