using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sprint0.Prototype.Editor
{
    [InitializeOnLoad]
    public static class Step1SceneInstaller
    {
        const string ScenePath = "Assets/Scenes/ForestScene.unity";
        const string BodyPrefabPath = "Assets/Art/Prefab/시체 용사.prefab";
        const string TentaclePrefabPath = "Assets/Art/Prefab/Tentacle_Standard_Rig.prefab";
        const string PlayerName = "Step1_TentacleWarrior";
        const string SessionKey = "Sprint0.Step1SceneInstaller.Completed.v2";

        static Step1SceneInstaller()
        {
            EditorApplication.delayCall += AutoInstall;
        }

        [MenuItem("Sprint 0/Step 1/구성 다시 적용")]
        public static void InstallFromMenu()
        {
            SessionState.EraseBool(SessionKey);
            Install();
        }

        static void AutoInstall()
        {
            if (SessionState.GetBool(SessionKey, false)
                || EditorApplication.isPlayingOrWillChangePlaymode
                || EditorApplication.isCompiling
                || EditorApplication.isUpdating)
            {
                return;
            }

            Install();
        }

        static void Install()
        {
            var bodyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BodyPrefabPath);
            var tentaclePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TentaclePrefabPath);
            if (bodyPrefab == null || tentaclePrefab == null)
            {
                Debug.LogError("[Step 1] Required body or tentacle prefab is missing.");
                return;
            }

            var bodyLayer = EnsureLayer("body");
            if (bodyLayer < 0)
            {
                Debug.LogError("[Step 1] No free user layer was available for the body layer.");
                return;
            }

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
                player = new GameObject(PlayerName);
                SceneManager.MoveGameObjectToScene(player, scene);
                player.transform.position = new Vector3(0f, 1.25f, 0f);
            }

            var body = FindDirectChild(player.transform, "시체 용사");
            if (body == null)
            {
                body = (GameObject)PrefabUtility.InstantiatePrefab(bodyPrefab, scene);
                body.name = "시체 용사";
                body.transform.SetParent(player.transform, false);
            }

            SetLayerRecursively(body, bodyLayer);
            var meshFilter = body.GetComponentInChildren<MeshFilter>(true);
            if (meshFilter != null)
            {
                var meshCollider = meshFilter.GetComponent<MeshCollider>();
                if (meshCollider == null)
                {
                    meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
                }

                meshCollider.sharedMesh = meshFilter.sharedMesh;
                meshCollider.convex = false;
            }

            var controllerObject = FindDirectChild(player.transform, "Tentacle_Controller");
            if (controllerObject == null)
            {
                controllerObject = new GameObject("Tentacle_Controller");
                controllerObject.transform.SetParent(player.transform, false);
            }

            var bodyRenderer = body.GetComponentInChildren<Renderer>(true);
            if (bodyRenderer != null)
            {
                var bounds = bodyRenderer.bounds;
                controllerObject.transform.position = bounds.center
                    + player.transform.right * bounds.extents.x * 0.72f
                    + player.transform.up * bounds.extents.y * 0.28f;
            }

            var controller = controllerObject.GetComponent<TentacleProceduralController>();
            if (controller == null)
            {
                controller = controllerObject.AddComponent<TentacleProceduralController>();
            }

            var mainCamera = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .FirstOrDefault(camera => camera.CompareTag("MainCamera"));
            if (mainCamera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                cameraObject.tag = "MainCamera";
                mainCamera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            var controllerData = new SerializedObject(controller);
            controllerData.FindProperty("tentaclePrefab").objectReferenceValue = tentaclePrefab;
            controllerData.FindProperty("aimingCamera").objectReferenceValue = mainCamera;
            controllerData.FindProperty("bodyMask").intValue = 1 << bodyLayer;
            controllerData.ApplyModifiedPropertiesWithoutUndo();

            var orbitCamera = mainCamera.GetComponent<TentacleOrbitCamera>();
            if (orbitCamera == null)
            {
                orbitCamera = mainCamera.gameObject.AddComponent<TentacleOrbitCamera>();
            }

            var cameraData = new SerializedObject(orbitCamera);
            cameraData.FindProperty("target").objectReferenceValue = controllerObject.transform;
            cameraData.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            SessionState.SetBool(SessionKey, true);
            Debug.Log("[Step 1] ForestScene configured: body, tentacle controller, procedural chain, orbit and zoom camera.");

            if (openedTemporarily)
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActiveScene);
                }
            }
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

        static GameObject FindDirectChild(Transform parent, string objectName)
        {
            for (var index = 0; index < parent.childCount; index++)
            {
                var child = parent.GetChild(index);
                if (child.name == objectName)
                {
                    return child.gameObject;
                }
            }

            return null;
        }

        static void SetLayerRecursively(GameObject root, int layer)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = layer;
            }
        }
    }
}
