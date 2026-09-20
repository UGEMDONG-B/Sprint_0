using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sprint0.Prototype.Editor
{
    [InitializeOnLoad]
    public static class Step6SceneInstaller
    {
        const string ScenePath = "Assets/Scenes/ForestScene.unity";
        const string PlayerName = "Step1_TentacleWarrior";
        const string MonsterPrefabPath = "Assets/Art/Prefab/몬스터.prefab";
        const string StandardPath = "Assets/Art/Prefab/Attack_Standard.prefab";
        const string FirePath = "Assets/Art/Prefab/Attack_Fire.prefab";
        const string PiercePath = "Assets/Art/Prefab/Attack_Spear.prefab";
        const string StrikePath = "Assets/Art/Prefab/Attack_Break.prefab";
        const string FireVisualPath = "Assets/Art/Prefab/Tentacle_Fire_Rig.prefab";
        const string PierceVisualPath = "Assets/Art/Prefab/Tentacle_Spear_Rig.prefab";
        const string StrikeVisualPath = "Assets/Art/Prefab/Tentacle_Break_Rig.prefab";
        const string SessionKey = "Sprint0.Step6SceneInstaller.Completed.v1";

        static Step6SceneInstaller()
        {
            EditorApplication.delayCall += AutoInstall;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Sprint 0/Step 6/성장 및 진화 시스템 구성 적용")]
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
            var standard = AssetDatabase.LoadAssetAtPath<GameObject>(StandardPath);
            var fire = AssetDatabase.LoadAssetAtPath<GameObject>(FirePath);
            var pierce = AssetDatabase.LoadAssetAtPath<GameObject>(PiercePath);
            var strike = AssetDatabase.LoadAssetAtPath<GameObject>(StrikePath);
            var fireVisual = AssetDatabase.LoadAssetAtPath<GameObject>(FireVisualPath);
            var pierceVisual = AssetDatabase.LoadAssetAtPath<GameObject>(PierceVisualPath);
            var strikeVisual = AssetDatabase.LoadAssetAtPath<GameObject>(StrikeVisualPath);
            if (standard == null || fire == null || pierce == null || strike == null
                || fireVisual == null || pierceVisual == null || strikeVisual == null)
            {
                Debug.LogError("[Step 6] One or more projectile or evolved tentacle prefabs are missing.");
                return;
            }

            ConfigureProjectilePrefab(StandardPath);
            ConfigureProjectilePrefab(FirePath);
            ConfigureProjectilePrefab(PiercePath);
            ConfigureProjectilePrefab(StrikePath);
            ConfigureMonsterPrefab();

            var previousActiveScene = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var openedTemporarily = !scene.isLoaded;
            if (openedTemporarily)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            var player = scene.GetRootGameObjects().FirstOrDefault(root => root.name == PlayerName);
            var tentacle = player != null
                ? player.GetComponentInChildren<TentacleProceduralController>(true)
                : null;
            var motor = player != null ? player.GetComponent<ThirdPersonCharacterMotor>() : null;
            if (player == null || tentacle == null || motor == null)
            {
                Debug.LogError("[Step 6] Player, tentacle, or movement controller is missing.");
                Finish(scene, openedTemporarily, previousActiveScene);
                return;
            }

            var progression = tentacle.GetComponent<TentacleProgression>();
            if (progression == null)
            {
                progression = tentacle.gameObject.AddComponent<TentacleProgression>();
            }

            var data = new SerializedObject(progression);
            data.FindProperty("tentacle").objectReferenceValue = tentacle;
            data.FindProperty("characterMotor").objectReferenceValue = motor;
            data.FindProperty("standardProjectilePrefab").objectReferenceValue = standard;
            data.FindProperty("fireProjectilePrefab").objectReferenceValue = fire;
            data.FindProperty("pierceProjectilePrefab").objectReferenceValue = pierce;
            data.FindProperty("strikeProjectilePrefab").objectReferenceValue = strike;
            data.FindProperty("fireVisualPrefab").objectReferenceValue = fireVisual;
            data.FindProperty("pierceVisualPrefab").objectReferenceValue = pierceVisual;
            data.FindProperty("strikeVisualPrefab").objectReferenceValue = strikeVisual;
            data.FindProperty("experiencePerLevel").intValue = 3;
            data.ApplyModifiedPropertiesWithoutUndo();

            var attack = tentacle.GetComponent<TentacleAttackController>();
            if (attack != null)
            {
                var attackData = new SerializedObject(attack);
                attackData.FindProperty("progression").objectReferenceValue = progression;
                attackData.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            SessionState.SetBool(SessionKey, true);
            Debug.Log("[Step 6] Per-tentacle growth, level choices, and three evolutions configured.");
            Finish(scene, openedTemporarily, previousActiveScene);
        }

        static void ConfigureProjectilePrefab(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            var sphere = root.GetComponent<SphereCollider>();
            if (sphere == null)
            {
                sphere = root.AddComponent<SphereCollider>();
            }
            sphere.isTrigger = true;
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                collider.isTrigger = true;
            }

            var body = root.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = root.AddComponent<Rigidbody>();
            }
            body.useGravity = false;
            body.isKinematic = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            if (root.GetComponent<TentacleProjectile>() == null)
            {
                root.AddComponent<TentacleProjectile>();
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static void ConfigureMonsterPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(MonsterPrefabPath);
            var body = root.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = root.AddComponent<Rigidbody>();
            }
            body.isKinematic = false;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.constraints = RigidbodyConstraints.FreezePositionY
                | RigidbodyConstraints.FreezeRotationX
                | RigidbodyConstraints.FreezeRotationZ;
            PrefabUtility.SaveAsPrefabAsset(root, MonsterPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
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
