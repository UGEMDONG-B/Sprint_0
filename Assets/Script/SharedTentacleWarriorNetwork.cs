using System;
using System.Collections.Generic;
using System.Linq;
using Sprint0.Multiplayer;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Sprint0.Prototype
{
    public enum GameResultState : byte
    {
        Playing,
        GameOver,
        GameClear
    }

    public struct TentaclePlayerState : INetworkSerializable, IEquatable<TentaclePlayerState>
    {
        public ulong ClientId;
        public Vector3 MoveDirection;
        public Vector3 AimPoint;

        public bool IsOccupied => ClientId != ulong.MaxValue;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref MoveDirection);
            serializer.SerializeValue(ref AimPoint);
        }

        public bool Equals(TentaclePlayerState other)
        {
            return ClientId == other.ClientId
                && MoveDirection == other.MoveDirection
                && AimPoint == other.AimPoint;
        }
    }

    [DefaultExecutionOrder(-90)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NetworkTransform))]
    [RequireComponent(typeof(ThirdPersonCharacterMotor))]
    public sealed class SharedTentacleWarriorNetwork : NetworkBehaviour
    {
        public const int TentacleCount = 4;

        readonly NetworkList<TentaclePlayerState> playerStates = new(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        readonly NetworkVariable<GameResultState> networkResult = new(
            GameResultState.Playing,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        ThirdPersonCharacterMotor characterMotor;
        Rigidbody body;
        TentacleProceduralController[] tentacles;
        TentacleAttackController[] attacks;
        int localSlot = -1;

        public int LocalSlot => localSlot;
        public GameResultState Result => IsSpawned ? networkResult.Value : GameResultState.Playing;
        public int ActiveTentacleCount
        {
            get
            {
                var count = 0;
                for (var index = 0; index < playerStates.Count; index++)
                {
                    if (playerStates[index].IsOccupied)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool IsSlotOccupied(int slot)
        {
            return slot >= 0 && slot < playerStates.Count && playerStates[slot].IsOccupied;
        }

        public bool IsSlotMoving(int slot)
        {
            return IsSlotOccupied(slot)
                && playerStates[slot].MoveDirection.sqrMagnitude > 0.0001f;
        }

        public bool IsClientAssignedToSlot(ulong clientId, int slot)
        {
            return slot >= 0 && slot < playerStates.Count
                && playerStates[slot].IsOccupied
                && playerStates[slot].ClientId == clientId;
        }

        void Awake()
        {
            characterMotor = GetComponent<ThirdPersonCharacterMotor>();
            body = GetComponent<Rigidbody>();
            tentacles = GetComponentsInChildren<TentacleProceduralController>(true)
                .OrderBy(item => item.name, StringComparer.Ordinal)
                .Take(TentacleCount)
                .ToArray();
            attacks = new TentacleAttackController[tentacles.Length];

            characterMotor.SetExternalControl(true);
            for (var index = 0; index < tentacles.Length; index++)
            {
                tentacles[index].SetSlotActive(false);
                tentacles[index].SetLocalControl(false);
                attacks[index] = tentacles[index].GetComponent<TentacleAttackController>();
                if (attacks[index] != null)
                {
                    attacks[index].SetLocalInputEnabled(false);
                }
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            networkResult.OnValueChanged += OnResultChanged;

            if (tentacles.Length != TentacleCount)
            {
                Debug.LogError($"[Step8] Shared warrior requires exactly {TentacleCount} Tentacle_Controller objects, but found {tentacles.Length}.", this);
            }

            if (IsServer && playerStates.Count == 0)
            {
                for (var index = 0; index < TentacleCount; index++)
                {
                    playerStates.Add(CreateEmptyState(index));
                }
            }

            if (networkResult.Value != GameResultState.Playing)
            {
                GameResultUI.Show(networkResult.Value);
            }

            var waveManager = FindFirstObjectByType<MonsterWaveManager>();
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

        public void DeclareGameOver()
        {
            if (IsSpawned)
            {
                if (IsServer && networkResult.Value == GameResultState.Playing)
                {
                    networkResult.Value = GameResultState.GameOver;
                }
            }
            else
            {
                GameResultUI.Show(GameResultState.GameOver);
            }
        }

        public void DeclareGameClear()
        {
            if (IsSpawned)
            {
                if (IsServer && networkResult.Value == GameResultState.Playing)
                {
                    networkResult.Value = GameResultState.GameClear;
                }
            }
            else
            {
                GameResultUI.Show(GameResultState.GameClear);
            }
        }

        public override void OnNetworkDespawn()
        {
            networkResult.OnValueChanged -= OnResultChanged;
            base.OnNetworkDespawn();
        }

        void OnResultChanged(GameResultState previous, GameResultState current)
        {
            if (current != GameResultState.Playing)
            {
                GameResultUI.Show(current);
            }
        }

        public int RegisterPlayer(ulong clientId)
        {
            if (!IsServer)
            {
                return -1;
            }

            for (var index = 0; index < playerStates.Count; index++)
            {
                if (playerStates[index].ClientId == clientId)
                {
                    return index;
                }
            }

            var availableSlots = new List<int>();
            for (var index = 0; index < playerStates.Count; index++)
            {
                if (!playerStates[index].IsOccupied)
                {
                    availableSlots.Add(index);
                }
            }

            if (availableSlots.Count > 0)
            {
                var slot = availableSlots[UnityEngine.Random.Range(0, availableSlots.Count)];
                var state = CreateEmptyState(slot);
                state.ClientId = clientId;
                playerStates[slot] = state;
                return slot;
            }

            return -1;
        }

        public void ReleasePlayer(ulong clientId)
        {
            if (!IsServer)
            {
                return;
            }

            for (var index = 0; index < playerStates.Count; index++)
            {
                if (playerStates[index].ClientId == clientId)
                {
                    playerStates[index] = CreateEmptyState(index);
                    return;
                }
            }
        }

        public void SetPlayerInput(int slot, ulong clientId, Vector3 moveDirection, Vector3 aimPoint)
        {
            if (!IsServer || slot < 0 || slot >= playerStates.Count
                || playerStates[slot].ClientId != clientId)
            {
                return;
            }

            moveDirection.y = 0f;
            var state = playerStates[slot];
            state.MoveDirection = Vector3.ClampMagnitude(moveDirection, 1f);
            state.AimPoint = aimPoint;
            playerStates[slot] = state;
        }

        public void ConfigureLocalSlot(int slot)
        {
            localSlot = Mathf.Clamp(slot, -1, TentacleCount - 1);
        }

        public Transform GetTentacleAnchor(int slot)
        {
            return slot >= 0 && slot < tentacles.Length ? tentacles[slot].transform : null;
        }

        public TentacleProceduralController GetTentacle(int slot)
        {
            return slot >= 0 && slot < tentacles.Length ? tentacles[slot] : null;
        }

        public int GetTentacleSlot(TentacleProceduralController tentacle)
        {
            return Array.IndexOf(tentacles, tentacle);
        }

        void Update()
        {
            var combinedDirection = GetCombinedDirection();
            characterMotor.SetExternalMovementState(combinedDirection);

            var canControl = MultiplayerGameController.Instance != null
                && MultiplayerGameController.Instance.CanControlPlayer;

            for (var index = 0; index < tentacles.Length; index++)
            {
                var active = IsSlotOccupied(index);
                var local = active && index == localSlot;
                tentacles[index].SetSlotActive(active);
                tentacles[index].SetLocalControl(local);

                if (active && !local)
                {
                    tentacles[index].SetExternalTarget(playerStates[index].AimPoint);
                }

                if (attacks[index] != null)
                {
                    attacks[index].SetLocalInputEnabled(local && canControl);
                }
            }
        }

        void FixedUpdate()
        {
            if (IsServer && IsSpawned)
            {
                characterMotor.ApplyExternalMovement(GetCombinedDirection());
            }
        }

        Vector3 GetCombinedDirection()
        {
            var combined = Vector3.zero;
            for (var index = 0; index < playerStates.Count; index++)
            {
                if (playerStates[index].IsOccupied)
                {
                    combined += playerStates[index].MoveDirection;
                }
            }

            return combined;
        }

        TentaclePlayerState CreateEmptyState(int slot)
        {
            var anchor = GetTentacleAnchor(slot);
            var origin = anchor != null ? anchor.position : transform.position;
            return new TentaclePlayerState
            {
                ClientId = ulong.MaxValue,
                MoveDirection = Vector3.zero,
                AimPoint = origin + transform.forward * 8f
            };
        }
    }
}
