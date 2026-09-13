using Unity.Netcode;
using UnityEngine;

namespace Sprint0.GravityCoop
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class GravityRunner : NetworkBehaviour
    {
        [Min(0)] public float moveSpeed = 5f;
        [Min(0)] public float jumpSpeed = 7f;
        [Range(0, 1)] public float airControl = 0.45f;
        [Min(0)] public float acceleration = 30f;
        [Min(0)] public float rotationSpeed = 10f;
        public Transform visual;
        public Rigidbody Body { get; private set; }
        public Vector3 Facing { get; private set; } = Vector3.right;
        public bool Grounded { get; private set; }
        Vector2 input;
        bool jump;
        float lastInput;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.useGravity = false;
            Body.isKinematic = true;
        }

        public void SetInput(Vector2 move, bool jumpPressed)
        {
            input = Vector2.ClampMagnitude(move, 1);
            jump |= jumpPressed;
            lastInput = Time.unscaledTime;
        }

        public void Restore(Vector3 position)
        {
            input = Vector2.zero;
            jump = false;
            Body.isKinematic = false;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.position = position;
            Body.rotation = Quaternion.identity;
            Facing = Vector3.right;
            GetComponent<Unity.Netcode.Components.NetworkTransform>().Teleport(position, Quaternion.identity, Vector3.one);
        }

        void FixedUpdate()
        {
            var game = GravityGame.Instance;
            if (!IsServer || game == null) return;
            Body.isKinematic = !game.Playing;
            if (!game.Playing) { input = Vector2.zero; jump = false; return; }
            if (Time.unscaledTime - lastInput > 0.3f) input = Vector2.zero;
            var up = -game.Down;
            var right = Vector3.Cross(up, Vector3.forward);
            Grounded = false;
            foreach (var hit in Physics.SphereCastAll(Body.position, 0.42f, game.Down, 0.22f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.rigidbody != Body && Vector3.Dot(hit.normal, up) > 0.65f) Grounded = true;
            var direction = right * input.x + Vector3.forward * input.y;
            if (direction.sqrMagnitude > 0.01f) Facing = direction.normalized;
            var lateral = Vector3.ProjectOnPlane(Body.linearVelocity, up);
            var correction = Vector3.ClampMagnitude(direction * moveSpeed - lateral,
                acceleration * (Grounded ? 1 : airControl) * Time.fixedDeltaTime);
            Body.linearVelocity += correction;
            if (jump && Grounded) Body.linearVelocity = Vector3.ProjectOnPlane(Body.linearVelocity, up) + up * jumpSpeed;
            jump = false;
            Body.AddForce(game.Down * game.gravityStrength, ForceMode.Acceleration);
        }

        void LateUpdate()
        {
            var game = GravityGame.Instance;
            if (game == null || visual == null) return;
            // The spherical collision hull cannot snag when the visual changes its up axis.
            var target = Quaternion.LookRotation(Vector3.forward, -game.Down);
            visual.rotation = Quaternion.Slerp(visual.rotation, target, 1 - Mathf.Exp(-rotationSpeed * Time.deltaTime));
        }
    }
}
