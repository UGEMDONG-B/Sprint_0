using Unity.Netcode;
using UnityEngine;

namespace Sprint0.GravityCoop
{
    public sealed class GravityClamp : NetworkBehaviour
    {
        public GravityPuzzle puzzle;
        public BoxCollider socket;
        public BoxCollider handle;
        public TextMesh status;
        public NetworkVariable<int> LockedBox = new(-1);
        public NetworkVariable<bool> Rejected = new();

        public bool TryToggle()
        {
            var game = GravityGame.Instance;
            if (!IsServer || !game.Playing || game.Current != puzzle) return false;
            if (LockedBox.Value >= 0)
            {
                puzzle.boxes[LockedBox.Value].SetAnchored(false);
                LockedBox.Value = -1;
                Rejected.Value = false;
                return true;
            }
            for (int i = 0; i < puzzle.boxes.Length; i++)
            {
                var box = puzzle.boxes[i];
                if (game.IsHeld(box) || box.Anchored.Value || box.Body.linearVelocity.sqrMagnitude > 0.36f) continue;
                var bounds = box.GetComponent<Collider>().bounds;
                if (!socket.bounds.Contains(bounds.min) || !socket.bounds.Contains(bounds.max)) continue;
                box.SetAnchored(true);
                LockedBox.Value = i;
                Rejected.Value = false;
                puzzle.SaveClampCheckpoint(this, i);
                return true;
            }
            Rejected.Value = true;
            return false;
        }

        public void Restore()
        {
            LockedBox.Value = -1;
            Rejected.Value = false;
        }

        void Update()
        {
            if (status == null) return;
            status.text = LockedBox.Value >= 0 ? "P / LOCKED\nE / RELEASE"
                : Rejected.Value ? "P / PLACE + STOP\nE / LOCK" : "P / FREE\nE / LOCK";
            status.color = LockedBox.Value >= 0 ? Color.green : Rejected.Value ? Color.red : Color.white;
        }
    }
}
