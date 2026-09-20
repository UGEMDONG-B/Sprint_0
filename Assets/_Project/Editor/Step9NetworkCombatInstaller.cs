using System;
using System.Linq;
using Sprint0.Prototype;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Sprint0.Editor
{
    public static class Step9NetworkCombatInstaller
    {
        const string ScenePath = "Assets/Scenes/ForestScene.unity";
        const string MonsterPath = "Assets/Art/Prefab/몬스터.prefab";
        const string BossPath = "Assets/Art/Prefab/보스.prefab";

        static readonly string[] ProjectilePaths =
        {
            "Assets/Art/Prefab/Attack_Standard.prefab",
            "Assets/Art/Prefab/Attack_Fire.prefab",
            "Assets/Art/Prefab/Attack_Spear.prefab",
            "Assets/Art/Prefab/Attack_Break.prefab"
        };

        [MenuItem("Tools/Sprint 0/Install Step 9 Network Combat")]
        public static void Install()
        {
            var networkPrefabs = ProjectilePaths
                .Select(ConfigureNetworkPrefab)
                .Prepend(ConfigureNetworkPrefab(BossPath))
                .Prepend(ConfigureNetworkPrefab(MonsterPath))
                .ToArray();

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var networkManager = Object.FindFirstObjectByType<NetworkManager>();
            if (networkManager == null)
            {
                throw new InvalidOperationException("Step 9 requires the Step 8 NetworkManager.");
            }

            foreach (var prefab in networkPrefabs)
            {
                if (!networkManager.NetworkConfig.Prefabs.Contains(prefab))
                {
                    networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
                }
            }

            EditorUtility.SetDirty(networkManager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Step9] Server-authoritative enemies, projectiles, health, progression and evolution configured.");
        }

        static GameObject ConfigureNetworkPrefab(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<NetworkObject>() == null)
                {
                    root.AddComponent<NetworkObject>();
                }

                var networkTransform = root.GetComponent<NetworkTransform>();
                if (networkTransform == null)
                {
                    networkTransform = root.AddComponent<NetworkTransform>();
                }
                networkTransform.Interpolate = true;

                var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (saved == null)
                {
                    throw new InvalidOperationException($"Failed to configure network prefab: {path}");
                }

                return saved;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
