using Unity.Netcode;
using UnityEngine;

namespace Sprint0.GravityCoop
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class GravityBody : NetworkBehaviour
    {
        public bool affectedByGravity = true;
        public int puzzleIndex;
        public bool carryable = true;
        public Rigidbody Body { get; private set; }
        Vector3 initialPosition;
        Quaternion initialRotation;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.useGravity = false;
            Body.isKinematic = true;
            initialPosition = transform.position;
            initialRotation = transform.rotation;
        }

        public void Restore()
        {
            Body.isKinematic = false;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.position = initialPosition;
            Body.rotation = initialRotation;
            GetComponent<Unity.Netcode.Components.NetworkTransform>().Teleport(initialPosition, initialRotation, transform.localScale);
        }

        void FixedUpdate()
        {
            if (!IsServer || GravityGame.Instance == null) return;
            var game = GravityGame.Instance;
            bool active = game.Playing && game.Puzzle.Value == puzzleIndex && !game.IsHeld(this);
            Body.isKinematic = !active || !affectedByGravity;
            if (active && affectedByGravity) Body.AddForce(game.Down * game.gravityStrength, ForceMode.Acceleration);
        }
    }
}
