using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sprint0.Prototype.Editor
{
    [InitializeOnLoad]
    public static class Step5SceneInstaller
    {
        const string ScenePath = "Assets/Scenes/ForestScene.unity";
        const string MonsterPrefabPath = "Assets/Art/Prefab/몬스터.prefab";
        const string PlayerName = "Step1_TentacleWarrior";
        const string SessionKey = "Sprint0.Step5SceneInstaller.Completed.v1";

        static Step5SceneInstaller()
        {
            EditorApplication.delayCall += AutoInstall;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Sprint 0/Step 5/스탯 및 접촉 피해 구성 적용")]
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
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
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
            ConfigureMonsterPrefab();

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
                Debug.LogError("[Step 5] Player root was not found.");
                Finish(scene, openedTemporarily, previousActiveScene);
                return;
            }

            if (player.GetComponent<PlayerStats>() == null)
            {
                player.AddComponent<PlayerStats>();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            SessionState.SetBool(SessionKey, true);
            Debug.Log("[Step 5] Player/monster stats, contact damage, invincibility, and death lock configured.");
            Finish(scene, openedTemporarily, previousActiveScene);
        }

        static void ConfigureMonsterPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(MonsterPrefabPath);
            var collider = root.GetComponentInChildren<Collider>(true);
            if (collider != null)
            {
                collider.isTrigger = true;
            }
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
            if (root.GetComponent<MonsterHealth>() == null)
            {
                root.AddComponent<MonsterHealth>();
            }
            if (root.GetComponent<MonsterChaseController>() == null)
            {
                root.AddComponent<MonsterChaseController>();
            }
            if (root.GetComponent<MonsterContactDamage>() == null)
            {
                root.AddComponent<MonsterContactDamage>();
            }
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
