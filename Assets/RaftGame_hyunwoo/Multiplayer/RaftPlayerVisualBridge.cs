using RaftSharkDive;
using Sprint0.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RaftGame.Hyunwoo.Multiplayer
{
    /// <summary>
    /// Raft-only adapter that reuses the shared NetworkPlayer instances for transform replication.
    /// It changes runtime instances in Main only; no shared prefab or NetworkManager asset is edited.
    /// </summary>
    [DefaultExecutionOrder(9000)]
    public sealed class RaftPlayerVisualBridge : MonoBehaviour
    {
        PlayerController localPlayer;
        Transform playerRigTemplate;
        bool separatedLocalSpawn;

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnUnitySceneLoaded;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnUnitySceneLoaded;
        }

        void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != RaftMultiplayerBootstrap.MainScenePath) return;
            localPlayer = null;
            playerRigTemplate = null;
            separatedLocalSpawn = false;
        }

        void Update()
        {
            if (SceneManager.GetActiveScene().path != RaftMultiplayerBootstrap.MainScenePath)
            {
                return;
            }

            FindLocalPlayerRig();
            if (localPlayer == null || playerRigTemplate == null)
            {
                return;
            }

            var networkPlayers = FindObjectsByType<ThirdPersonNetworkPlayer>(FindObjectsSortMode.None);
            foreach (var networkPlayer in networkPlayers)
            {
                if (!networkPlayer.IsSpawned)
                {
                    continue;
                }

                var visual = networkPlayer.GetComponent<RaftNetworkPlayerVisual>();
                if (visual == null)
                {
                    visual = networkPlayer.gameObject.AddComponent<RaftNetworkPlayerVisual>();
                }

                visual.Initialize(localPlayer, playerRigTemplate);
            }

            SeparateLocalSpawn(networkPlayers);
        }

        void FindLocalPlayerRig()
        {
            if (localPlayer != null && playerRigTemplate != null)
            {
                return;
            }

            localPlayer = FindFirstObjectByType<PlayerController>();
            if (localPlayer == null)
            {
                return;
            }

            playerRigTemplate = localPlayer.transform.Find("PlayerRig");
            if (playerRigTemplate == null)
            {
                Debug.LogError("[RaftMultiplayer] PlayerRig was not found under the Raft Player prefab.");
            }
        }

        void SeparateLocalSpawn(ThirdPersonNetworkPlayer[] networkPlayers)
        {
            if (separatedLocalSpawn)
            {
                return;
            }

            foreach (var networkPlayer in networkPlayers)
            {
                if (!networkPlayer.IsSpawned || !networkPlayer.IsOwner)
                {
                    continue;
                }

                separatedLocalSpawn = true;
                var spawnIndex = (int)(networkPlayer.OwnerClientId % 8);
                if (spawnIndex == 0)
                {
                    return;
                }

                // Client 1 starts directly in front of the host and faces back toward it, so a
                // two-player test can see the other player immediately in both Game views.
                var angle = (spawnIndex - 1) * 45f;
                var offsetDirection = Quaternion.AngleAxis(angle, Vector3.up) * localPlayer.transform.forward;
                var offset = offsetDirection * 2.2f;
                var faceCenter = Quaternion.LookRotation(-offsetDirection, Vector3.up);
                localPlayer.Teleport(localPlayer.transform.position + offset, faceCenter);
                Debug.Log($"[RaftMultiplayer] Separated local Raft spawn for client {networkPlayer.OwnerClientId}.");
                return;
            }
        }
    }

    /// <summary>
    /// Lives only on a runtime clone of the shared NetworkPlayer while Raft Main is active.
    /// The owner follows the local Raft Player; OwnerNetworkTransform distributes that pose.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed class RaftNetworkPlayerVisual : MonoBehaviour
    {
        static readonly int MotionStateId = Animator.StringToHash("MotionState");

        ThirdPersonNetworkPlayer networkPlayer;
        PlayerController localPlayer;
        Animator animator;
        GameObject meshObject;
        Vector3 previousPosition;
        bool initialized;

        public void Initialize(PlayerController raftPlayer, Transform rigTemplate)
        {
            if (initialized)
            {
                if (localPlayer == raftPlayer && meshObject != null) return;
                if (meshObject != null) Destroy(meshObject);
                animator = null;
                initialized = false;
            }

            networkPlayer = GetComponent<ThirdPersonNetworkPlayer>();
            if (networkPlayer == null || !networkPlayer.IsSpawned || rigTemplate == null)
            {
                return;
            }

            localPlayer = raftPlayer;

            // Hide the shared prototype capsule on this runtime instance only.
            var capsuleVisual = transform.Find("CapsuleVisual");
            if (capsuleVisual != null)
            {
                foreach (var renderer in capsuleVisual.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.enabled = false;
                }
            }

            meshObject = Instantiate(rigTemplate.gameObject, transform, false);
            meshObject.name = "RaftPlayerMesh";
            meshObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            meshObject.transform.localScale = Vector3.one;
            SetLayerRecursively(meshObject.transform, 0);
            animator = meshObject.GetComponent<Animator>();

            // First-person owners already have their original body. Only the remote clone is rendered.
            foreach (var renderer in meshObject.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = !networkPlayer.IsOwner;
            }

            if (networkPlayer.IsOwner)
            {
                var sharedController = GetComponent<CharacterController>();
                if (sharedController != null)
                {
                    sharedController.enabled = false;
                }

                transform.SetPositionAndRotation(localPlayer.transform.position, localPlayer.transform.rotation);
            }

            previousPosition = transform.position;
            initialized = true;
            Debug.Log($"[RaftMultiplayer] Player mesh attached for client {networkPlayer.OwnerClientId} " +
                      $"(local owner: {networkPlayer.IsOwner}).");
        }

        static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root)
            {
                SetLayerRecursively(child, layer);
            }
        }

        void LateUpdate()
        {
            if (!initialized || networkPlayer == null || !networkPlayer.IsSpawned)
            {
                return;
            }

            if (networkPlayer.IsOwner && localPlayer != null)
            {
                transform.SetPositionAndRotation(localPlayer.transform.position, localPlayer.transform.rotation);
            }

            if (animator != null)
            {
                var planarDelta = transform.position - previousPosition;
                planarDelta.y = 0f;
                animator.SetInteger(MotionStateId, planarDelta.sqrMagnitude > 0.000001f ? 1 : 0);
            }

            previousPosition = transform.position;
        }
    }
}
