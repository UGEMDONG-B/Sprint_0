using Sprint0.Prototype;
using Unity.Netcode;
using UnityEngine;

namespace Sprint0.Multiplayer
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(OwnerNetworkTransform))]
    public sealed class TentacleNetworkPlayer : NetworkBehaviour
    {
        static readonly Vector3[] SpawnOffsets =
        {
            new(-2.5f, 0f, -2.5f),
            new(2.5f, 0f, -2.5f),
            new(-2.5f, 0f, 2.5f),
            new(2.5f, 0f, 2.5f)
        };

        [SerializeField] Vector3 spawnCenter;
        [SerializeField] ThirdPersonCharacterMotor characterMotor;
        [SerializeField] TentacleAttackController attackController;
        [SerializeField] TentacleProceduralController tentacleController;

        TentacleOrbitCamera orbitCamera;
        MonsterWaveManager waveManager;

        public void ConfigureSpawnCenter(Vector3 value)
        {
            spawnCenter = value;
        }

        void Awake()
        {
            characterMotor ??= GetComponent<ThirdPersonCharacterMotor>();
            attackController ??= GetComponentInChildren<TentacleAttackController>(true);
            tentacleController ??= GetComponentInChildren<TentacleProceduralController>(true);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            var localOwner = IsOwner;
            SetLocalGameplayEnabled(false);

            if (!localOwner)
            {
                return;
            }

            var index = (int)(OwnerClientId % (ulong)SpawnOffsets.Length);
            transform.position = spawnCenter + SpawnOffsets[index];

            orbitCamera = Camera.main != null ? Camera.main.GetComponent<TentacleOrbitCamera>() : null;
            orbitCamera?.SetTarget(transform);

            waveManager = FindFirstObjectByType<MonsterWaveManager>();
            if (waveManager != null)
            {
                if (IsServer)
                {
                    waveManager.SetPlayerTarget(transform);
                }
                else
                {
                    waveManager.enabled = false;
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            SetLocalGameplayEnabled(false);
            base.OnNetworkDespawn();
        }

        void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            var controller = MultiplayerGameController.Instance;
            var canControl = IsOwner && controller != null && controller.CanControlPlayer;
            SetLocalGameplayEnabled(canControl);

            if (IsOwner && orbitCamera == null && Camera.main != null)
            {
                orbitCamera = Camera.main.GetComponent<TentacleOrbitCamera>();
                orbitCamera?.SetTarget(transform);
            }
        }

        void SetLocalGameplayEnabled(bool value)
        {
            characterMotor?.SetControlsEnabled(value);

            if (attackController != null)
            {
                attackController.enabled = value;
            }

            if (tentacleController != null)
            {
                tentacleController.enabled = value;
            }
        }
    }
}
