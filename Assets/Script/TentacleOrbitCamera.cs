using UnityEngine;
using UnityEngine.InputSystem;

namespace Sprint0.Prototype
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class TentacleOrbitCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector3 targetOffset = Vector3.zero;
        [SerializeField, Min(0.1f)] float distance = 5.5f;
        [SerializeField, Min(0.1f)] float minimumDistance = 2.2f;
        [SerializeField, Min(0.1f)] float maximumDistance = 10f;
        [SerializeField, Min(0f)] float orbitSensitivity = 0.16f;
        [SerializeField, Min(0f)] float zoomSensitivity = 0.28f;
        [SerializeField, Range(1f, 3f)] float zoomRangeExpansion = 1.75f;
        [SerializeField] float minimumPitch = -25f;
        [SerializeField] float maximumPitch = 70f;
        [SerializeField, Min(0f)] float positionSharpness = 18f;
        [SerializeField, Min(0.001f)] float collisionRadius = 0.25f;
        [SerializeField] LayerMask collisionMask = ~0;

        float yaw;
        float pitch = 22f;

        public void SetTarget(Transform value)
        {
            target = value;
            Snap();
        }

        void Awake()
        {
            var euler = transform.eulerAngles;
            yaw = euler.y;
            pitch = NormalizePitch(euler.x);
            distance = Mathf.Clamp(distance, minimumDistance, maximumDistance);
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            if (mouse.rightButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (mouse.rightButton.wasReleasedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (mouse.rightButton.isPressed)
            {
                var delta = mouse.delta.ReadValue() * orbitSensitivity;
                yaw += delta.x;
                pitch = Mathf.Clamp(pitch - delta.y, minimumPitch, maximumPitch);
            }

            var scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                // Windows mice commonly report 120 per notch, while some devices report 1.
                // Normalize both forms and zoom proportionally so it stays useful at any distance.
                var scrollSteps = Mathf.Abs(scroll) >= 10f ? scroll / 120f : scroll;
                var effectiveSensitivity = Mathf.Max(zoomSensitivity, 0.22f);
                var minimumZoomDistance = minimumDistance / zoomRangeExpansion;
                var maximumZoomDistance = maximumDistance * zoomRangeExpansion;
                distance = Mathf.Clamp(
                    distance * Mathf.Exp(-scrollSteps * effectiveSensitivity),
                    minimumZoomDistance,
                    maximumZoomDistance);
            }
        }

        void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            var pivot = target.position + targetOffset;
            var orbit = Quaternion.Euler(pitch, yaw, 0f);
            var desiredPosition = pivot - orbit * Vector3.forward * distance;
            desiredPosition = ResolveCameraCollision(pivot, desiredPosition);

            var blend = 1f - Mathf.Exp(-positionSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, blend);
            var lookDirection = pivot - transform.position;
            if (lookDirection.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            }
        }

        Vector3 ResolveCameraCollision(Vector3 pivot, Vector3 desiredPosition)
        {
            var offset = desiredPosition - pivot;
            var castDistance = offset.magnitude;
            if (castDistance < 0.001f)
            {
                return desiredPosition;
            }

            var hits = Physics.SphereCastAll(
                pivot,
                collisionRadius,
                offset / castDistance,
                castDistance,
                collisionMask,
                QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            foreach (var hit in hits)
            {
                if (!hit.transform.IsChildOf(target.root))
                {
                    return hit.point + hit.normal * collisionRadius;
                }
            }

            return desiredPosition;
        }

        void Snap()
        {
            if (target == null)
            {
                return;
            }

            var pivot = target.position + targetOffset;
            var orbit = Quaternion.Euler(pitch, yaw, 0f);
            var desiredPosition = ResolveCameraCollision(
                pivot,
                pivot - orbit * Vector3.forward * distance);
            transform.SetPositionAndRotation(
                desiredPosition,
                Quaternion.LookRotation(pivot - desiredPosition, Vector3.up));
        }

        void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        static float NormalizePitch(float value)
        {
            return value > 180f ? value - 360f : value;
        }
    }
}
