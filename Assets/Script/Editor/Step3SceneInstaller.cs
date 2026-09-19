using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sprint0.Prototype.Editor
{
    [InitializeOnLoad]
    public static class Step3SceneInstaller
    {
        const string ScenePath = "Assets/Scenes/ForestScene.unity";
        const string AttackPrefabPath = "Assets/Art/Prefab/Attack_Standard.prefab";
        const string MonsterPrefabPath = "Assets/Art/Prefab/몬스터.prefab";
        const string PlayerName = "Step1_TentacleWarrior";
        const string SessionKey = "Sprint0.Step3SceneInstaller.Completed.v1";

        static Step3SceneInstaller()
        {
            EditorApplication.delayCall += AutoInstall;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Sprint 0/Step 3/공격 시스템 구성 적용")]
        public static void InstallFromMenu()
        {
            SessionState.EraseBool(SessionKey);
            Install();
        }

        static void AutoInstall()
        {
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
            var attackPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AttackPrefabPath);
            if (attackPrefab == null)
            {
                Debug.LogError("[Step 3] Attack_Standard prefab was not found.");
                return;
            }

            var monsterLayer = EnsureLayer("Monster");
            ConfigureProjectilePrefab();
            ConfigureMonsterPrefab(monsterLayer);

            var previousActiveScene = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var openedTemporarily = !scene.isLoaded;
            if (openedTemporarily)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            var player = scene.GetRootGameObjects().FirstOrDefault(root => root.name == PlayerName);
            var motor = player != null ? player.GetComponent<ThirdPersonCharacterMotor>() : null;
            var tentacle = player != null ? player.GetComponentInChildren<TentacleProceduralController>(true) : null;
            var camera = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .FirstOrDefault(item => item.CompareTag("MainCamera"));

            if (player == null || motor == null || tentacle == null || camera == null)
            {
                Debug.LogError("[Step 3] Step 1/2 player, tentacle, motor, or camera is missing.");
                Finish(scene, openedTemporarily, previousActiveScene);
                return;
            }

            var attack = tentacle.GetComponent<TentacleAttackController>();
            if (attack == null)
            {
                attack = tentacle.gameObject.AddComponent<TentacleAttackController>();
            }

            var data = new SerializedObject(attack);
            data.FindProperty("tentacle").objectReferenceValue = tentacle;
            data.FindProperty("characterMotor").objectReferenceValue = motor;
            data.FindProperty("aimingCamera").objectReferenceValue = camera;
            data.FindProperty("projectilePrefab").objectReferenceValue = attackPrefab;
            data.FindProperty("projectileSpeed").floatValue = 28f;
            data.FindProperty("aimDistance").floatValue = 300f;
            data.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            SessionState.SetBool(SessionKey, true);
            Debug.Log("[Step 3] Attack_Standard projectile and move/attack exclusion configured.");
            Finish(scene, openedTemporarily, previousActiveScene);
        }

        static void ConfigureProjectilePrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(AttackPrefabPath);
            var collider = root.GetComponent<SphereCollider>();
            if (collider == null)
            {
                collider = root.AddComponent<SphereCollider>();
            }
            collider.isTrigger = true;
            var body = root.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = root.AddComponent<Rigidbody>();
            }
            body.useGravity = false;
            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var projectile = root.GetComponent<TentacleProjectile>();
            if (projectile == null)
            {
                projectile = root.AddComponent<TentacleProjectile>();
            }

            var projectileData = new SerializedObject(projectile);
            projectileData.FindProperty("lifetime").floatValue = 12f;
            projectileData.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, AttackPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static void ConfigureMonsterPrefab(int monsterLayer)
        {
            if (monsterLayer < 0 || AssetDatabase.LoadAssetAtPath<GameObject>(MonsterPrefabPath) == null)
            {
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(MonsterPrefabPath);
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = monsterLayer;
            }

            if (root.GetComponent<MonsterHealth>() == null)
            {
                root.AddComponent<MonsterHealth>();
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

            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
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
