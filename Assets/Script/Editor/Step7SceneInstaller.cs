using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sprint0.Prototype.Editor
{
    [InitializeOnLoad]
    public static class Step7SceneInstaller
    {
        const string ScenePath = "Assets/Scenes/ForestScene.unity";
        const string BossPrefabPath = "Assets/Art/Prefab/보스.prefab";
        const string WaveManagerName = "Step4_WaveManager";
        const string SessionKey = "Sprint0.Step7SceneInstaller.Completed.v1";

        static Step7SceneInstaller()
        {
            EditorApplication.delayCall += AutoInstall;
        }

        [MenuItem("Sprint 0/Step 7/보스전 구성 적용")]
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
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall -= AutoInstall;
                EditorApplication.delayCall += AutoInstall;
                return;
            }
            Install();
        }

        static void Install()
        {
            var bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
            if (bossPrefab == null)
            {
                Debug.LogError("[Step 7] Boss prefab was not found.");
                return;
            }

            ConfigureBossPrefab();
            bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);

            var previousActiveScene = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var openedTemporarily = !scene.isLoaded;
            if (openedTemporarily)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            var managerObject = scene.GetRootGameObjects().FirstOrDefault(root => root.name == WaveManagerName);
            var manager = managerObject != null ? managerObject.GetComponent<MonsterWaveManager>() : null;
            if (manager == null)
            {
                Debug.LogError("[Step 7] Wave manager was not found.");
                Finish(scene, openedTemporarily, previousActiveScene);
                return;
            }

            var data = new SerializedObject(manager);
            data.FindProperty("bossPrefab").objectReferenceValue = bossPrefab;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            SessionState.SetBool(SessionKey, true);
            Debug.Log("[Step 7] Boss wave, enrage, knockback immunity, and boss health UI configured.");
            Finish(scene, openedTemporarily, previousActiveScene);
        }

        static void ConfigureBossPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(BossPrefabPath);
            var monsterLayer = LayerMask.NameToLayer("Monster");
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = monsterLayer;
            }

            var collider = root.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = root.AddComponent<BoxCollider>();
            }
            var mesh = root.GetComponent<MeshFilter>();
            if (mesh != null && mesh.sharedMesh != null)
            {
                collider.center = mesh.sharedMesh.bounds.center;
                collider.size = mesh.sharedMesh.bounds.size;
            }
            collider.isTrigger = true;

            var body = root.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = root.AddComponent<Rigidbody>();
            }
            body.isKinematic = false;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            var health = root.GetComponent<MonsterHealth>();
            if (health == null)
            {
                health = root.AddComponent<MonsterHealth>();
            }
            var healthData = new SerializedObject(health);
            healthData.FindProperty("hp").intValue = 100;
            healthData.FindProperty("experienceReward").intValue = 0;
            healthData.FindProperty("isBoss").boolValue = true;
            healthData.ApplyModifiedPropertiesWithoutUndo();

            var chase = root.GetComponent<MonsterChaseController>();
            if (chase == null)
            {
                chase = root.AddComponent<MonsterChaseController>();
            }
            var chaseData = new SerializedObject(chase);
            chaseData.FindProperty("moveSpeed").floatValue = 1.5f;
            chaseData.FindProperty("stopDistance").floatValue = 2.5f;
            chaseData.ApplyModifiedPropertiesWithoutUndo();

            var contact = root.GetComponent<MonsterContactDamage>();
            if (contact == null)
            {
                contact = root.AddComponent<MonsterContactDamage>();
            }
            var contactData = new SerializedObject(contact);
            contactData.FindProperty("attackDamage").intValue = 2;
            contactData.ApplyModifiedPropertiesWithoutUndo();

            if (root.GetComponent<BossController>() == null)
            {
                root.AddComponent<BossController>();
            }

            PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
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
