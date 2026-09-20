using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sprint0.Prototype.Editor
{
    [InitializeOnLoad]
    public static class Step4SceneInstaller
    {
        const string ScenePath = "Assets/Scenes/ForestScene.unity";
        const string MonsterPrefabPath = "Assets/Art/Prefab/몬스터.prefab";
        const string PlayerName = "Step1_TentacleWarrior";
        const string ManagerName = "Step4_WaveManager";
        const string SessionKey = "Sprint0.Step4SceneInstaller.Completed.v1";

        static Step4SceneInstaller()
        {
            EditorApplication.delayCall += AutoInstall;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Sprint 0/Step 4/몬스터 웨이브 구성 적용")]
        public static void InstallFromMenu()
        {
            SessionState.EraseBool(SessionKey);
            Install();
        }

        static void AutoInstall()
        {
            if (PrototypeSceneInstallGuard.IsStep8Installed)
            {
                SessionState.SetBool(SessionKey, true);
                return;
            }

            if (SessionState.GetBool(SessionKey, false))
            {
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.isCompiling
                || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall -= AutoInstall;
                EditorApplication.delayCall += AutoInstall;
                return;
            }

            Install();
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.delayCall -= AutoInstall;
                EditorApplication.delayCall += AutoInstall;
            }
        }

        static void Install()
        {
            var monsterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterPrefabPath);
            if (monsterPrefab == null)
            {
                Debug.LogError("[Step 4] Monster prefab was not found.");
                return;
            }

            var monsterLayer = EnsureLayer("Monster");
            ConfigureMonsterPrefab(monsterLayer);

            var previousActiveScene = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var openedTemporarily = !scene.isLoaded;
            if (openedTemporarily)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            var player = scene.GetRootGameObjects().FirstOrDefault(root => root.name == PlayerName);
            if (player == null)
            {
                Debug.LogError("[Step 4] Player root was not found.");
                Finish(scene, openedTemporarily, previousActiveScene);
                return;
            }

            var managerObject = scene.GetRootGameObjects().FirstOrDefault(root => root.name == ManagerName);
            if (managerObject == null)
            {
                managerObject = new GameObject(ManagerName);
                SceneManager.MoveGameObjectToScene(managerObject, scene);
            }

            var manager = managerObject.GetComponent<MonsterWaveManager>();
            if (manager == null)
            {
                manager = managerObject.AddComponent<MonsterWaveManager>();
            }

            var points = EnsureSpawnPoints(managerObject.transform, player.transform.position);
            var data = new SerializedObject(manager);
            data.FindProperty("monsterPrefab").objectReferenceValue = monsterPrefab;
            data.FindProperty("playerTarget").objectReferenceValue = player.transform;
            var pointsProperty = data.FindProperty("spawnPoints");
            pointsProperty.arraySize = points.Length;
            for (var i = 0; i < points.Length; i++)
            {
                pointsProperty.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            }
            data.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            SessionState.SetBool(SessionKey, true);
            Debug.Log("[Step 4] Four spawn points, wave manager, and monster tracking configured.");
            Finish(scene, openedTemporarily, previousActiveScene);
        }

        static Transform[] EnsureSpawnPoints(Transform parent, Vector3 center)
        {
            const float radius = 18f;
            var names = new[] { "Spawn_North", "Spawn_South", "Spawn_East", "Spawn_West" };
            var offsets = new[]
            {
                Vector3.forward * radius,
                Vector3.back * radius,
                Vector3.right * radius,
                Vector3.left * radius
            };
            var points = new Transform[names.Length];

            for (var i = 0; i < names.Length; i++)
            {
                var point = parent.Find(names[i]);
                if (point == null)
                {
                    point = new GameObject(names[i]).transform;
                    point.SetParent(parent, false);
                    var position = center + offsets[i];
                    var terrain = Terrain.activeTerrain;
                    if (terrain != null)
                    {
                        position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
                    }
                    point.position = position;
                    point.rotation = Quaternion.LookRotation((center - position).normalized, Vector3.up);
                }
                points[i] = point;
            }

            return points;
        }

        static void ConfigureMonsterPrefab(int monsterLayer)
        {
            var root = PrefabUtility.LoadPrefabContents(MonsterPrefabPath);
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = monsterLayer;
            }
            if (root.GetComponent<MonsterHealth>() == null)
            {
                root.AddComponent<MonsterHealth>();
            }
            if (root.GetComponent<MonsterChaseController>() == null)
            {
                root.AddComponent<MonsterChaseController>();
            }
            PrefabUtility.SaveAsPrefabAsset(root, MonsterPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static int EnsureLayer(string layerName)
        {
            var existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0)
            {
                return existing;
            }

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (var index = 8; index < 32; index++)
            {
                var layer = layers.GetArrayElementAtIndex(index);
                if (!string.IsNullOrEmpty(layer.stringValue))
                {
                    continue;
                }
                layer.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                return index;
            }
            return -1;
        }

        static void Finish(Scene scene, bool openedTemporarily, Scene previousActiveScene)
        {
            if (!openedTemporarily)
            {
                return;
            }
            EditorSceneManager.CloseScene(scene, true);
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousActiveScene);
            }
        }
    }
}
