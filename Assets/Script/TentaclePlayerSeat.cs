using Sprint0.Multiplayer;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sprint0.Prototype
{
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class TentaclePlayerSeat : NetworkBehaviour
    {
        [SerializeField] GameObject sharedWarriorPrefab;
        [SerializeField, Min(1f)] float inputSendRate = 20f;

        readonly NetworkVariable<int> slotIndex = new(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        SharedTentacleWarriorNetwork sharedWarrior;
        TentacleOrbitCamera orbitCamera;
        Vector3 pendingMoveDirection;
        int configuredSlot = -1;
        float nextInputSendTime;

        public int SlotIndex => slotIndex.Value;

        public void Configure(GameObject warriorPrefab)
        {
            sharedWarriorPrefab = warriorPrefab;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer)
            {
                EnsureSharedWarrior();
                slotIndex.Value = sharedWarrior != null
                    ? sharedWarrior.RegisterPlayer(OwnerClientId)
                    : -1;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && sharedWarrior != null)
            {
                sharedWarrior.ReleasePlayer(OwnerClientId);
            }

            if (IsOwner && sharedWarrior != null && sharedWarrior.LocalSlot == slotIndex.Value)
            {
                sharedWarrior.ConfigureLocalSlot(-1);
            }

            base.OnNetworkDespawn();
        }

        void Update()
        {
            if (!IsOwner || !IsSpawned)
            {
                return;
            }

            ResolveSharedWarrior();
            ConfigureLocalView();

            var canControl = MultiplayerGameController.Instance != null
                && MultiplayerGameController.Instance.CanControlPlayer;
            pendingMoveDirection = canControl ? ReadCameraRelativeMovement() : Vector3.zero;
        }

        void LateUpdate()
        {
            if (!IsOwner || sharedWarrior == null || slotIndex.Value < 0
                || Time.unscaledTime < nextInputSendTime)
            {
                return;
            }

            var tentacle = sharedWarrior.GetTentacle(slotIndex.Value);
            if (tentacle == null)
            {
                return;
            }

            nextInputSendTime = Time.unscaledTime + 1f / inputSendRate;
            SubmitInputServerRpc(pendingMoveDirection, tentacle.CurrentTarget);
        }

        [ServerRpc]
        void SubmitInputServerRpc(Vector3 moveDirection, Vector3 aimPoint)
        {
            ResolveSharedWarrior();
            sharedWarrior?.SetPlayerInput(slotIndex.Value, OwnerClientId, moveDirection, aimPoint);
        }

        void EnsureSharedWarrior()
        {
            ResolveSharedWarrior();
            if (sharedWarrior != null || sharedWarriorPrefab == null)
            {
                return;
            }

            var instance = Instantiate(
                sharedWarriorPrefab,
                sharedWarriorPrefab.transform.position,
                sharedWarriorPrefab.transform.rotation);
            sharedWarrior = instance.GetComponent<SharedTentacleWarriorNetwork>();
            instance.GetComponent<NetworkObject>().Spawn(true);
        }

        void ResolveSharedWarrior()
        {
            if (sharedWarrior == null)
            {
                sharedWarrior = FindFirstObjectByType<SharedTentacleWarriorNetwork>();
            }
        }

        void ConfigureLocalView()
        {
            var slot = slotIndex.Value;
            if (sharedWarrior == null || slot < 0 || slot == configuredSlot)
            {
                return;
            }

            configuredSlot = slot;
            sharedWarrior.ConfigureLocalSlot(slot);

            var progression = sharedWarrior.GetTentacle(slot)?.GetComponent<TentacleProgression>();
            TentacleLevelUpUI.EnsureInstance().SetDisplayedProgression(progression);

            orbitCamera = Camera.main != null ? Camera.main.GetComponent<TentacleOrbitCamera>() : null;
            orbitCamera?.SetTarget(sharedWarrior.GetTentacleAnchor(slot));
        }

        static Vector3 ReadCameraRelativeMovement()
        {
            var keyboard = Keyboard.current;
            var camera = Camera.main;
            if (keyboard == null || camera == null)
            {
                return Vector3.zero;
            }

            var input = new Vector2(
                (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                    - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f)
                    - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f));
            input = Vector2.ClampMagnitude(input, 1f);

            var forward = camera.transform.forward;
            var right = camera.transform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
            return forward * input.y + right * input.x;
        }
    }
}
