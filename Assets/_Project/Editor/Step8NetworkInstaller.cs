using System.Linq;
using Sprint0.Multiplayer;
using Sprint0.Prototype;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Sprint0.Editor
{
    public static class Step8NetworkInstaller
    {
        const string ScenePath = "Assets/Scenes/ForestScene.unity";
        const string PlayerName = "Step1_TentacleWarrior";
        const string WarriorPrefabPath = "Assets/_Project/Prefabs/TentacleWarriorNetwork.prefab";
        const string SeatPrefabPath = "Assets/_Project/Prefabs/TentaclePlayerSeat.prefab";

        [MenuItem("Tools/Sprint 0/Install Step 8 Networking")]
        public static void Install()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var warriorPrefab = BuildSharedWarriorPrefab(scene);
            var seatPrefab = BuildSeatPrefab(warriorPrefab);
            BuildNetworkScene(seatPrefab, warriorPrefab);
            AddSceneToBuildSettings();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Step8] Networking installed: one shared warrior, four player-owned tentacles and summed movement.");
        }

        static GameObject BuildSharedWarriorPrefab(Scene scene)
        {
            var playerRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == PlayerName);
            if (playerRoot == null)
            {
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(WarriorPrefabPath);
                if (existing == null)
                {
                    throw new System.InvalidOperationException($"Could not find {PlayerName} or {WarriorPrefabPath}.");
                }

                return existing;
            }

            var tentacles = playerRoot.GetComponentsInChildren<TentacleProceduralController>(true)
                .Where(item => item.name.StartsWith("Tentacle_Controller"))
                .OrderBy(item => item.name)
                .ToArray();
            if (tentacles.Length != SharedTentacleWarriorNetwork.TentacleCount)
            {
                throw new System.InvalidOperationException(
                    $"The shared warrior needs exactly four Tentacle_Controller objects. Found {tentacles.Length}.");
            }

            if (playerRoot.GetComponent<NetworkObject>() == null)
            {
                playerRoot.AddComponent<NetworkObject>();
            }

            var oldPlayer = playerRoot.GetComponent<TentacleNetworkPlayer>();
            if (oldPlayer != null)
            {
                Object.DestroyImmediate(oldPlayer);
            }

            foreach (var transform in playerRoot.GetComponents<NetworkTransform>())
            {
                Object.DestroyImmediate(transform);
            }

            var networkTransform = playerRoot.AddComponent<NetworkTransform>();
            networkTransform.Interpolate = true;
            if (playerRoot.GetComponent<SharedTentacleWarriorNetwork>() == null)
            {
                playerRoot.AddComponent<SharedTentacleWarriorNetwork>();
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(playerRoot, WarriorPrefabPath);
            if (prefab == null)
            {
                throw new System.InvalidOperationException("Failed to save the shared tentacle warrior prefab.");
            }

            // The server creates exactly one shared body after the first player joins.
            Object.DestroyImmediate(playerRoot);
            return prefab;
        }

        static GameObject BuildSeatPrefab(GameObject warriorPrefab)
        {
            var root = new GameObject("TentaclePlayerSeat");
            root.AddComponent<NetworkObject>();
            var seat = root.AddComponent<TentaclePlayerSeat>();
            seat.Configure(warriorPrefab);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, SeatPrefabPath);
            Object.DestroyImmediate(root);
            if (prefab == null)
            {
                throw new System.InvalidOperationException("Failed to save the tentacle player seat prefab.");
            }

            return prefab;
        }

        static void BuildNetworkScene(GameObject seatPrefab, GameObject warriorPrefab)
        {
            var networkManager = Object.FindFirstObjectByType<NetworkManager>();
            if (networkManager == null)
            {
                networkManager = SampleSceneBuilder.BuildNetworkManager(seatPrefab);
            }
            else
            {
                networkManager.NetworkConfig.PlayerPrefab = seatPrefab;
                networkManager.NetworkConfig.EnableSceneManagement = false;
                networkManager.NetworkConfig.TickRate = 30;
            }

            if (!networkManager.NetworkConfig.Prefabs.Contains(seatPrefab))
            {
                networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = seatPrefab });
            }

            if (!networkManager.NetworkConfig.Prefabs.Contains(warriorPrefab))
            {
                networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = warriorPrefab });
            }

            if (GameObject.Find("Interface") == null)
            {
                SampleSceneBuilder.BuildInterface(networkManager);
            }

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                SampleSceneBuilder.BuildEventSystem();
            }

            var camera = Camera.main;
            if (camera != null)
            {
                camera.GetComponent<TentacleOrbitCamera>()?.SetTarget(null);
            }
        }

        static void AddSceneToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(scene => scene.path != ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
