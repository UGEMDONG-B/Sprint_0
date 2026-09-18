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
        [Min(0)] public float carryClearance = 0.3f;
        [Min(0.1f)] public float cameraRotationSpeed = 5f;
        [Header("Runner first-person camera")]
        [Range(0, 0.3f)] public float eyeHeight = 0.25f;
        [Min(0.01f)] public float mouseSensitivity = 0.12f;
        public Vector2 pitchLimits = new(-85, 85);
        public float observerElevation = 3f;
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
        Quaternion gravityCameraFrame = Quaternion.identity;
        float cameraYaw;
        float cameraPitch;
        Renderer[] runnerRenderers;
        float runnerLookYaw;
        bool hasRunnerLook;

        public override void OnNetworkSpawn()
        {
            runnerRenderers = runner.visual.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in runnerRenderers) renderer.forceRenderingOff = !IsOperator;
        }

        public override void OnNetworkDespawn()
        {
            if (runnerRenderers != null)
                foreach (var renderer in runnerRenderers)
                    if (renderer != null) renderer.forceRenderingOff = false;
            ReleaseCursor();
        }
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
            if (!IsOperator) ReleaseCursor();
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        public bool IsHeld(GravityBody box) => HeldBox.Value >= 0 && HeldBox.Value < Current.boxes.Length && Current.boxes[HeldBox.Value] == box;

        void Update()
        {
            if (!IsSpawned) return;
            if (IsServer)
            {
                var flow = Sprint0.Multiplayer.MultiplayerGameController.Instance;
                bool pair = NetworkManager.ConnectedClientsIds.Count == 2 && (flow == null || flow.HasGameStarted);
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
            var menu = Sprint0.Multiplayer.MultiplayerGameController.Instance;
            if (menu != null && !menu.CanControlPlayer)
            {
                if (!IsOperator) { ReleaseCursor(); if (Playing) MoveRpc(Vector2.zero, false); }
                return;
            }
            if (!IsOperator) UpdateCameraInput(keyboard);
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
                if (Cursor.lockState != CursorLockMode.Locked) { move = Vector2.zero; jump = false; }
                // Keep the server's validated two-axis input; only the local input basis changes.
                move = CameraRelativeInput(move, cameraYaw);
                if (Time.unscaledTime >= inputAt || jump)
                {
                    LookRpc(cameraYaw);
                    MoveRpc(move, jump);
                    inputAt = Time.unscaledTime + 1f / 30;
                }
                if (Cursor.lockState == CursorLockMode.Locked && keyboard.eKey.wasPressedThisFrame)
                {
                    LookRpc(cameraYaw);
                    InteractRpc();
                }
            }
        }

        public static Vector2 CameraRelativeInput(Vector2 input, float yaw)
        {
            float angle = yaw * Mathf.Deg2Rad;
            return new Vector2(input.x * Mathf.Cos(angle) + input.y * Mathf.Sin(angle),
                -input.x * Mathf.Sin(angle) + input.y * Mathf.Cos(angle));
        }

        static void ReleaseCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void OnApplicationFocus(bool focused) { if (!focused && !IsOperator) ReleaseCursor(); }

        void UpdateCameraInput(Keyboard keyboard)
        {
            if (!Playing || keyboard.escapeKey.wasPressedThisFrame) { ReleaseCursor(); return; }
            var mouse = Mouse.current;
            if (mouse == null) return;
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                // Keep HUD buttons clickable. Click inside the world view to resume looking.
                Vector2 point = mouse.position.ReadValue();
                if (mouse.leftButton.wasPressedThisFrame && Camera.main != null && Camera.main.pixelRect.Contains(point))
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                return;
            }
            var delta = mouse.delta.ReadValue();
            cameraYaw = Mathf.Repeat(cameraYaw + delta.x * mouseSensitivity, 360);
            cameraPitch = Mathf.Clamp(cameraPitch - delta.y * mouseSensitivity, pitchLimits.x, pitchLimits.y);
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
        public void LookRpc(float yaw, RpcParams rpc = default)
        {
            if (!Playing || !IsRunner(rpc.Receive.SenderClientId) || !float.IsFinite(yaw)) return;
            runnerLookYaw = Mathf.Repeat(yaw, 360);
            hasRunnerLook = true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ResetRpc(RpcParams rpc = default)
        {
            if (NetworkManager.ConnectedClients.ContainsKey(rpc.Receive.SenderClientId)) ResetSection();
        }

        bool Reachable(Collider target, out float distance)
        {
            var point = target.ClosestPoint(runner.Body.position);
            distance = Vector3.Distance(runner.Body.position, point);
            if (distance > interactionDistance) return false;
            foreach (var hit in Physics.RaycastAll(runner.Body.position, point - runner.Body.position,
                distance, ~0, QueryTriggerInteraction.Ignore))
                if (hit.rigidbody != runner.Body && hit.collider != target) return false;
            return true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void InteractRpc(RpcParams rpc = default)
        {
            if (!Playing || !IsRunner(rpc.Receive.SenderClientId)) return;
            if (HeldBox.Value >= 0) { Drop(); return; }
            int closest = -1;
            float distance = interactionDistance;
            bool useLever = Current.lever != null && Reachable(Current.lever.GetComponent<Collider>(), out distance);
            if (!useLever) distance = interactionDistance;
            for (int i = 0; i < Current.boxes.Length; i++)
            {
                var box = Current.boxes[i];
                if (box.carryable && Reachable(box.GetComponent<Collider>(), out float candidate) && candidate <= distance)
                {
                    closest = i;
                    distance = candidate;
                }
            }
            if (closest >= 0)
            {
                HeldBox.Value = closest;
                var box = Current.boxes[closest];
                runner.FaceObject(box.Body.position, -Down);
                box.Body.isKinematic = true;
                Physics.IgnoreCollision(runner.GetComponent<Collider>(), box.GetComponent<Collider>(), true);
            }
            else if (useLever) Current.TryToggleRoute();
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
            // Strafing and backing up must not swing the held crate around the player.
            var facing = hasRunnerLook
                ? Quaternion.LookRotation(Vector3.forward, -Down) * Quaternion.Euler(0, runnerLookYaw, 0) * Vector3.forward
                : runner.Facing;
            var target = runner.Body.position + facing * 1.25f - Down * carryClearance;
            // Release only when the player actually leaves a blocked crate behind, not on contact.
            if (Vector3.Distance(runner.Body.position, held.Body.position) > interactionDistance + 1f)
            {
                Drop();
                return;
            }
            // Sweep the entire path: a held box must never teleport through a wall or door.
            var delta = target - held.Body.position;
            float travel = delta.magnitude;
            foreach (var hit in Physics.BoxCastAll(held.Body.position, Vector3.one * 0.48f, delta.normalized,
                held.Body.rotation, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (hit.rigidbody != held.Body && hit.rigidbody != runner.Body)
                {
                    travel = Mathf.Min(travel, Mathf.Max(0, hit.distance - 0.03f));
                }
            target = held.Body.position + delta.normalized * travel;
            foreach (var col in Physics.OverlapBox(target, Vector3.one * 0.48f, held.Body.rotation, ~0, QueryTriggerInteraction.Ignore))
                if (col.attachedRigidbody != held.Body && col.attachedRigidbody != runner.Body)
                {
                    return;
                }
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
            hasRunnerLook = false;
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
            camera.rect = new Rect(0, 0, 1, 1);
            if (IsOperator)
            {
                camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(10.5f, 14f / camera.aspect);
                camera.transform.SetPositionAndRotation(Current.transform.position + new Vector3(0, 8 + observerElevation, -35),
                    Quaternion.LookRotation(new Vector3(0, -observerElevation, 35), Vector3.up));
            }
            else
            {
                camera.orthographic = false;
                camera.nearClipPlane = 0.05f;
                // Quaternion interpolation also handles the 180-degree up/down transition.
                var desired = Quaternion.LookRotation(Vector3.forward, -Down);
                gravityCameraFrame = Quaternion.Slerp(gravityCameraFrame, desired, 1 - Mathf.Exp(-cameraRotationSpeed * Time.deltaTime));
                var rotation = gravityCameraFrame * Quaternion.Euler(cameraPitch, cameraYaw, 0);
                // Keep the eye inside the spherical collision hull, including during gravity turns.
                // Use the gravity frame rather than look pitch so looking down does not move the eye.
                var eye = runner.transform.position + gravityCameraFrame * Vector3.up * Mathf.Clamp(eyeHeight, 0, 0.3f);
                camera.transform.SetPositionAndRotation(eye, rotation);
            }
        }
    }
}
