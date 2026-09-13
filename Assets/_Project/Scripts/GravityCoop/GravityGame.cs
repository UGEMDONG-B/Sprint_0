using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sprint0.GravityCoop
{
    public enum GravityDirection { Down, Up, Left, Right }

    public sealed class GravityGame : NetworkBehaviour
    {
        public static GravityGame Instance { get; private set; }
        [Min(0.1f)] public float gravityStrength = 18f;
        [Min(0)] public float gravityCooldown = 0.5f;
        [Min(0)] public float respawnDelay = 0.8f;
        [Min(0.1f)] public float interactionDistance = 2.2f;
        [Min(0.1f)] public float cameraRotationSpeed = 5f;
        public float runnerCameraDistance = 7f;
        public GravityRunner runner;
        public GravityPuzzle[] puzzles;
        public NetworkVariable<GravityDirection> Direction = new(GravityDirection.Down);
        public NetworkVariable<int> Puzzle = new(0);
        public NetworkVariable<bool> Ready = new();
        public NetworkVariable<bool> Dead = new();
        public NetworkVariable<bool> Finished = new();
        public NetworkVariable<int> HeldBox = new(-1);
        public NetworkVariable<int> ResetCount = new();
        public NetworkVariable<double> NextGravityTime = new();
        public NetworkVariable<double> SectionStarted = new();
        double respawnAt;
        float inputAt;
        Vector3 cameraUp = Vector3.up;
        public Vector3 Down => DirectionVector(Direction.Value);
        public bool Playing => IsSpawned && Ready.Value && !Dead.Value && !Finished.Value;
        public bool IsOperator => IsSpawned && NetworkManager.LocalClientId == Unity.Netcode.NetworkManager.ServerClientId;
        public GravityPuzzle Current => puzzles[Mathf.Clamp(Puzzle.Value, 0, puzzles.Length - 1)];

        public static Vector3 DirectionVector(GravityDirection direction) => direction switch
        {
            GravityDirection.Up => Vector3.up,
            GravityDirection.Left => Vector3.left,
            GravityDirection.Right => Vector3.right,
            _ => Vector3.down
        };

        void Awake() { Instance = this; }
        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        public bool IsHeld(GravityBody box) => HeldBox.Value >= 0 && HeldBox.Value < Current.boxes.Length && Current.boxes[HeldBox.Value] == box;

        void Update()
        {
            if (!IsSpawned) return;
            if (IsServer)
            {
                bool pair = NetworkManager.ConnectedClientsIds.Count == 2;
                if (pair != Ready.Value)
                {
                    Ready.Value = pair;
                    ResetSection();
                }
                if (Dead.Value && NetworkManager.ServerTime.Time >= respawnAt) ResetSection();
            }
            var keyboard = Keyboard.current;
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-gravitySmoke") >= 0) return;
            if (keyboard == null) return;
            if (keyboard.rKey.wasPressedThisFrame) ResetRpc();
            if (!Playing) return;
            if (IsOperator)
            {
                if (keyboard.downArrowKey.wasPressedThisFrame) ChangeGravityRpc(GravityDirection.Down);
                if (keyboard.upArrowKey.wasPressedThisFrame) ChangeGravityRpc(GravityDirection.Up);
                if (keyboard.leftArrowKey.wasPressedThisFrame) ChangeGravityRpc(GravityDirection.Left);
                if (keyboard.rightArrowKey.wasPressedThisFrame) ChangeGravityRpc(GravityDirection.Right);
            }
            else
            {
                Vector2 move = new((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                    (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
                bool jump = keyboard.spaceKey.wasPressedThisFrame;
                if (Time.unscaledTime >= inputAt || jump)
                {
                    MoveRpc(move, jump);
                    inputAt = Time.unscaledTime + 1f / 30;
                }
                if (keyboard.eKey.wasPressedThisFrame) InteractRpc();
            }
        }

        bool IsRunner(ulong sender) => sender != Unity.Netcode.NetworkManager.ServerClientId && NetworkManager.ConnectedClients.ContainsKey(sender);

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ChangeGravityRpc(GravityDirection direction, RpcParams rpc = default)
        {
            if (!Playing || rpc.Receive.SenderClientId != Unity.Netcode.NetworkManager.ServerClientId || (int)direction < 0 || (int)direction > 3
                || NetworkManager.ServerTime.Time < NextGravityTime.Value || Direction.Value == direction) return;
            Direction.Value = direction;
            NextGravityTime.Value = NetworkManager.ServerTime.Time + gravityCooldown;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void MoveRpc(Vector2 input, bool jump, RpcParams rpc = default)
        {
            if (Playing && IsRunner(rpc.Receive.SenderClientId) && float.IsFinite(input.x) && float.IsFinite(input.y)) runner.SetInput(input, jump);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ResetRpc(RpcParams rpc = default)
        {
            if (NetworkManager.ConnectedClients.ContainsKey(rpc.Receive.SenderClientId)) ResetSection();
        }

        bool Reachable(Vector3 point, Transform target)
        {
            if (Vector3.Distance(runner.Body.position, point) > interactionDistance) return false;
            foreach (var hit in Physics.RaycastAll(runner.Body.position, point - runner.Body.position,
                Vector3.Distance(runner.Body.position, point), ~0, QueryTriggerInteraction.Ignore))
                if (hit.rigidbody != runner.Body && hit.transform != target) return false;
            return true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void InteractRpc(RpcParams rpc = default)
        {
            if (!Playing || !IsRunner(rpc.Receive.SenderClientId)) return;
            if (HeldBox.Value >= 0) { Drop(); return; }
            if (Current.lever != null && Reachable(Current.lever.position, Current.lever))
            {
                Current.TryToggleRoute();
                return;
            }
            int closest = -1;
            float distance = interactionDistance;
            for (int i = 0; i < Current.boxes.Length; i++)
            {
                var box = Current.boxes[i];
                float candidate = Vector3.Distance(runner.Body.position, box.Body.position);
                if (box.carryable && candidate < distance && Reachable(box.Body.position, box.transform)) { closest = i; distance = candidate; }
            }
            if (closest >= 0)
            {
                HeldBox.Value = closest;
                var box = Current.boxes[closest];
                box.Body.isKinematic = true;
                Physics.IgnoreCollision(runner.GetComponent<Collider>(), box.GetComponent<Collider>(), true);
            }
        }

        void Drop()
        {
            if (HeldBox.Value < 0) return;
            var box = Current.boxes[HeldBox.Value];
            Physics.IgnoreCollision(runner.GetComponent<Collider>(), box.GetComponent<Collider>(), false);
            box.Body.isKinematic = false;
            box.Body.linearVelocity = runner.Body.linearVelocity;
            HeldBox.Value = -1;
        }

        void FixedUpdate()
        {
            if (!IsServer || !Playing) return;
            var local = runner.Body.position - Current.transform.position;
            if (Mathf.Abs(local.x) > 16 || local.y < -4 || local.y > 20 || Mathf.Abs(local.z) > 7) { Die(); return; }
            foreach (var box in Current.boxes)
                if (Vector3.Distance(box.Body.position, Current.transform.position + Vector3.up * 8) > 26) { Die(); return; }
            if (HeldBox.Value < 0) return;
            var held = Current.boxes[HeldBox.Value];
            var target = runner.Body.position + runner.Facing * 1.25f;
            // Sweep the entire path: a held box must never teleport through a wall or door.
            var delta = target - held.Body.position;
            foreach (var hit in Physics.BoxCastAll(held.Body.position, Vector3.one * 0.48f, delta.normalized,
                held.Body.rotation, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (hit.rigidbody != held.Body && hit.rigidbody != runner.Body) { Drop(); return; }
            foreach (var col in Physics.OverlapBox(target, Vector3.one * 0.48f, held.Body.rotation, ~0, QueryTriggerInteraction.Ignore))
                if (col.attachedRigidbody != held.Body && col.attachedRigidbody != runner.Body) { Drop(); return; }
            held.Body.MovePosition(target);
        }

        public void Die()
        {
            if (!IsServer || !Playing) return;
            Drop();
            Dead.Value = true;
            respawnAt = NetworkManager.ServerTime.Time + respawnDelay;
        }

        public void ResetSection()
        {
            if (!IsServer) return;
            Drop();
            Direction.Value = GravityDirection.Down;
            NextGravityTime.Value = 0;
            SectionStarted.Value = NetworkManager.ServerTime.Time;
            Dead.Value = false;
            Finished.Value = false;
            ResetCount.Value++;
            Current.Restore();
            runner.Restore(Current.spawn.position);
            Physics.SyncTransforms();
        }

        public void CompletePuzzle()
        {
            if (!IsServer || !Playing) return;
            Drop();
            if (Puzzle.Value == puzzles.Length - 1) { Finished.Value = true; return; }
            Puzzle.Value++;
            ResetSection();
        }

        void LateUpdate()
        {
            if (!IsSpawned || Camera.main == null) return;
            var camera = Camera.main;
            camera.rect = new Rect(0, 0, 1, 0.70f);
            if (IsOperator)
            {
                camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(10.5f, 14f / camera.aspect);
                camera.transform.SetPositionAndRotation(Current.transform.position + new Vector3(0, 8, -35), Quaternion.identity);
            }
            else
            {
                camera.orthographic = false;
                // Quaternion interpolation also handles the 180-degree up/down transition.
                var desired = Quaternion.LookRotation(Vector3.forward, -Down);
                camera.transform.rotation = Quaternion.Slerp(camera.transform.rotation, desired, 1 - Mathf.Exp(-cameraRotationSpeed * Time.deltaTime));
                cameraUp = camera.transform.up;
                camera.transform.position = runner.transform.position - Vector3.forward * runnerCameraDistance + cameraUp * 1.2f;
            }
        }
    }
}

