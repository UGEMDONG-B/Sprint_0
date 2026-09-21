using Unity.Netcode;
using UnityEngine;

namespace Sprint0.GravityCoop
{
    // Two discrete physical elbow orientations. Never turn around an occupant.
    public sealed class GravityRotor : NetworkBehaviour
    {
        public GravityPuzzle puzzle;
        public Transform elbow;
        public BoxCollider interior;
        public BoxCollider[] walls;
        public BoxCollider handle;
        public TextMesh status;
        public string id;
        public float initialAngle;
        public float turnAngle = -90;
        public NetworkVariable<bool> Turned = new();
        public NetworkVariable<bool> Blocked = new();

        public bool TryTurn()
        {
            if (!IsServer || !GravityGame.Instance.Playing || GravityGame.Instance.Current != puzzle) return false;
            if (HasBody(interior)) { Blocked.Value = true; return false; }
            var old = elbow.localRotation;
            elbow.localRotation = Quaternion.Euler(0, 0, initialAngle + (Turned.Value ? 0 : turnAngle));
            Physics.SyncTransforms();
            bool blocked = false;
            foreach (var wall in walls) blocked |= HasBody(wall);
            if (blocked)
            {
                elbow.localRotation = old;
                Physics.SyncTransforms();
                Blocked.Value = true;
                return false;
            }
            Turned.Value = !Turned.Value;
            Blocked.Value = false;
            return true;
        }

        static bool HasBody(BoxCollider volume)
        {
            foreach (var col in Physics.OverlapBox(volume.transform.TransformPoint(volume.center),
                Vector3.Scale(volume.size, volume.transform.lossyScale) * 0.5f - Vector3.one * 0.02f,
                volume.transform.rotation, ~0, QueryTriggerInteraction.Ignore))
                if (col.attachedRigidbody != null) return true;
            return false;
        }

        public void Restore()
        {
            Turned.Value = false;
            Blocked.Value = false;
            Apply();
        }

        public override void OnNetworkSpawn() => Apply();
        void Update() => Apply();
        void Apply()
        {
            if (elbow == null) return;
            elbow.localRotation = Quaternion.Euler(0, 0, initialAngle + (Turned.Value ? turnAngle : 0));
            if (status != null)
            {
                status.text = id + (Blocked.Value ? " / BLOCKED" : Turned.Value ? " / B" : " / A") + (Blocked.Value ? "\nE / RETRY" : "\nE / TURN");
                status.color = Blocked.Value ? Color.red : Turned.Value ? Color.green : Color.white;
            }
        }
    }
}
