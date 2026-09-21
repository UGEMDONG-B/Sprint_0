#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using Sprint0.Multiplayer;
using Unity.Netcode;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Sprint0.GravityCoop
{
    // Opt-in UI lifecycle test; also captures the real menu and split-screen rendering.
    public sealed class GravitySoloModeTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-gravitySoloModeTest") >= 0
                && FindFirstObjectByType<GravitySoloModeTest>() == null)
            {
                var test = new GameObject("Solo mode lifecycle test");
                DontDestroyOnLoad(test);
                test.AddComponent<GravitySoloModeTest>();
            }
        }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                yield return new WaitForSeconds(1);
                Check(UnityServices.State == ServicesInitializationState.Uninitialized, "Menus do not connect online");
                Check(!NetworkManager.Singleton.IsListening, "Mode selection is disconnected");
                if (attempt == 0) Capture("menu");
                yield return new WaitForSeconds(0.2f);
                ButtonWithLabel("혼자 테스트 (좌우 분할)").onClick.Invoke();
                yield return new WaitForSeconds(1);
                var game = GravityGame.Instance;
                Check(game.Playing && game.IsSolo, "Solo button starts gameplay");
                Check(NetworkManager.Singleton.NetworkConfig.NetworkTransport is OfflineTransport, "No network transport in solo");
                Check(FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 1, "Only one audio listener");
                Check(game.ObserverCamera != null && Camera.main.rect.x == 0.5f, "Both cameras available");
                if (attempt == 0) Capture("split");
                yield return new WaitForSeconds(0.2f);
                MultiplayerGameController.Instance.OpenSettings();
                yield return null;
                Check(!MultiplayerGameController.Instance.CanControlPlayer, "Settings block player input");
                if (attempt == 0) Capture("settings");
                yield return new WaitForSeconds(0.2f);
                ButtonWithLabel("테스트 종료 · 모드 선택").onClick.Invoke();
                yield return new WaitForSeconds(1);
                Check(!MultiplayerGameController.Instance.IsSoloMode, "Exit returns to mode selection");
                Check(NetworkManager.Singleton.NetworkConfig.NetworkTransport is Unity.Netcode.Transports.UTP.UnityTransport,
                    "Multiplayer transport restored");
                Check(FindObjectsByType<Camera>(FindObjectsSortMode.None).Length == 1, "Extra camera cleaned up");
                Check(ButtonWithLabel("멀티 · 방 생성").interactable && ButtonWithLabel("멀티 · 방 참가").interactable,
                    "Multiplayer buttons available after solo");
            }
            Debug.Log("[SoloModeTest] PASS: two solo enter/exit cycles, UI and offline lifecycle");
            Application.Quit(0);
        }

        static Button ButtonWithLabel(string label) => FindObjectsByType<Button>(FindObjectsSortMode.None)
            .Single(button => button.GetComponentInChildren<Text>()?.text == label);

        static void Check(bool passed, string message)
        {
            if (passed) { Debug.Log("[SoloModeTest] PASS " + message); return; }
            Debug.LogError("[SoloModeTest] FAIL " + message);
            Application.Quit(1);
            throw new Exception(message);
        }

        static void Capture(string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            // Hidden windows can skip backbuffer rendering; explicitly render to textures for visual QA.
            var canvas = FindFirstObjectByType<Canvas>();
            var mode = canvas.renderMode;
            var worldCamera = canvas.worldCamera;
            float planeDistance = canvas.planeDistance;
            var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            try
            {
                if (name == "split")
                {
                    RenderView(GravityGame.Instance.ObserverCamera, texture, 0, 800);
                    RenderView(Camera.main, texture, 800, 800);
                }
                else
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = Camera.main;
                    canvas.planeDistance = 1;
                    RenderView(Camera.main, texture, 0, 1600);
                }
                texture.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.GetFullPath("Logs/gravity-solo-" + name + ".png"), texture.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = mode;
                canvas.worldCamera = worldCamera;
                canvas.planeDistance = planeDistance;
                Destroy(texture);
            }
        }

        static void RenderView(Camera camera, Texture2D texture, int x, int width)
        {
            var target = new RenderTexture(width, 900, 24);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var rect = camera.rect;
            float aspect = camera.aspect;
            try
            {
                camera.targetTexture = target;
                camera.rect = new Rect(0, 0, 1, 1);
                camera.aspect = width / 900f;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, 900), x, 0);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.rect = rect;
                camera.aspect = aspect;
                RenderTexture.active = previousActive;
                Destroy(target);
            }
        }
    }
}
#endif
