using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sprint0.Prototype.Editor
{
    [InitializeOnLoad]
    public static class Step2SceneInstaller
    {
        const string ScenePath = "Assets/Scenes/ForestScene.unity";
        const string PlayerName = "Step1_TentacleWarrior";
        const string SessionKey = "Sprint0.Step2SceneInstaller.Completed.v1";

        static Step2SceneInstaller()
        {
            EditorApplication.delayCall += AutoInstall;
        }

        [MenuItem("Sprint 0/Step 2/이동 시스템 구성 적용")]
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
                Debug.LogError("[Step 2] Step 1 player root was not found. No scene values were changed.");
                Finish(scene, openedTemporarily, previousActiveScene);
                return;
            }

            var motor = player.GetComponent<ThirdPersonCharacterMotor>();
            if (motor == null)
            {
                motor = player.AddComponent<ThirdPersonCharacterMotor>();
            }

            var mainCamera = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .FirstOrDefault(camera => camera.CompareTag("MainCamera"));
            var motorData = new SerializedObject(motor);
            motorData.FindProperty("movementCamera").objectReferenceValue = mainCamera;
            motorData.ApplyModifiedPropertiesWithoutUndo();

            var body = player.GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            SessionState.SetBool(SessionKey, true);
            Debug.Log("[Step 2] Camera-relative movement and smooth rotation configured without changing transforms.");

            Finish(scene, openedTemporarily, previousActiveScene);
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
