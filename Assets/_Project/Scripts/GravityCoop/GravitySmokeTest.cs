#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Sprint0.GravityCoop
{
    // Development-only, opt-in two-process integration test. No test controls in normal play.
    public sealed class GravitySmokeTest : MonoBehaviour
    {
        GravityGame game;
        NetworkManager manager;
        bool failed;
        int checks;
        const string Channel = "GravitySmokeInput";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-gravitySmoke") >= 0)
            {
                // Only opt-in development tests bypass the shared online menus.
                var flow = Sprint0.Multiplayer.MultiplayerGameController.Instance;
                if (flow != null) flow.enabled = false;
                foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.gameObject.SetActive(false);
                new GameObject("Development integration test").AddComponent<GravitySmokeTest>();
            }
        }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            manager = NetworkManager.Singleton;
            manager.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>().SetConnectionData("127.0.0.1", 7777, "0.0.0.0");
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-gravityHost") >= 0) manager.StartHost();
            else manager.StartClient();
            float until = Time.realtimeSinceStartup + 45;
            if (manager.IsServer)
            {
                while (manager.ConnectedClientsIds.Count < 2 && Time.realtimeSinceStartup < until) yield return null;
                yield return new WaitForSeconds(0.3f);
                game = GravityGame.Instance;
                Check(!game.Ready.Value, "Two peers stay in lobby before host start");
                var position = game.runner.transform.position;
                yield return new WaitForSeconds(0.3f);
                Check(Vector3.Distance(position, game.runner.transform.position) < 0.01f, "Lobby does not simulate gameplay");
                // Simulate the shared controller's session-start notification, without contacting UGS in physics tests.
                typeof(Sprint0.Multiplayer.MultiplayerGameController).GetField("isGameStarted", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(Sprint0.Multiplayer.MultiplayerGameController.Instance, true);
            }
            while ((GravityGame.Instance == null || !GravityGame.Instance.Playing) && Time.realtimeSinceStartup < until) yield return null;
            game = GravityGame.Instance;
            manager = NetworkManager.Singleton;
            if (game == null || !game.Playing) { Fail("Two peers did not become ready"); yield break; }
            manager.CustomMessagingManager.RegisterNamedMessageHandler(Channel, Receive);
            if (!manager.IsServer)
            {
                Debug.Log("[GravityTest] CLIENT connected; authoritative input relay ready");
                game.ChangeGravityRpc(GravityDirection.Right); // Must be rejected by the server's role check.
                yield return new WaitForSeconds(1);
                Capture("runner");
                float deadline = Time.realtimeSinceStartup + 360;
                while (game != null && !game.Finished.Value && manager.IsConnectedClient && Time.realtimeSinceStartup < deadline) yield return null;
                if (game == null || !game.Finished.Value) { Fail("Client disconnected or timed out before completion"); yield break; }
                Check(game.Puzzle.Value == 4 && game.Current.DoorOpen.Value, "CLIENT final state synchronized");
                Debug.Log("[GravityTest] CLIENT PASS");
                yield return new WaitForSeconds(4);
                Application.Quit(failed ? 1 : 0);
                yield break;
            }
            yield return new WaitForSeconds(1);
            Capture("operator");
            Check(game.Direction.Value == GravityDirection.Down, "Runner cannot change gravity");
            foreach (var root in game.gameObject.scene.GetRootGameObjects())
                foreach (var component in root.GetComponentsInChildren<Component>(true)) Check(component != null, "No missing component");
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-gravityFinal") >= 0)
            {
                game.Puzzle.Value = 4;
                game.ResetSection();
                goto FinalPuzzle;
            }

            // All four surfaces: falling, landing, tangent motion, and jumping through client input RPCs.
            foreach (GravityDirection direction in Enum.GetValues(typeof(GravityDirection)))
            {
                game.ResetSection();
                game.runner.Restore(new Vector3(8, 6, 0));
                game.ChangeGravityRpc(direction);
                yield return Wait(2.5f);
                Check(game.Direction.Value == direction, "Direction " + direction);
                Check(game.runner.Grounded, "Landing " + direction);
                Vector3 start = game.runner.Body.position;
                yield return Drive(new Vector2(0, 1), 0.35f);
                Check(game.runner.Body.position.z > start.z + 0.25f, "Movement " + direction);
                start = game.runner.Body.position;
                yield return Drive(GravityGame.CameraRelativeInput(Vector2.up, 90), 0.35f);
                Check(Vector3.Dot(game.runner.Body.position - start, Vector3.Cross(-game.Down, Vector3.forward)) > 0.25f,
                    "Camera yaw 90 forward movement " + direction);
                start = game.runner.Body.position;
                yield return Drive(GravityGame.CameraRelativeInput(Vector2.up, 180), 0.35f);
                Check(game.runner.Body.position.z < start.z - 0.25f, "Camera yaw 180 forward movement " + direction);
                start = game.runner.Body.position;
                Input(Vector2.zero, true);
                yield return Wait(0.2f);
                Check(Vector3.Dot(game.runner.Body.position - start, -game.Down) > 0.3f, "Jump " + direction);
            }
            game.ResetSection();
            game.ChangeGravityRpc(GravityDirection.Right);
            game.ChangeGravityRpc(GravityDirection.Up);
            Check(game.Direction.Value == GravityDirection.Right, "Gravity cooldown");

            game.Puzzle.Value = 1;
            foreach (GravityDirection direction in Enum.GetValues(typeof(GravityDirection)))
            {
                game.ResetSection();
                var box = game.Current.boxes[0];
                box.Body.position = game.Current.transform.position + new Vector3(8, 6, 0);
                game.Direction.Value = direction;
                Vector3 start = box.Body.position;
                yield return Wait(0.35f);
                Check(Vector3.Dot(box.Body.position - start, game.Down) > 0.5f, "Crate gravity " + direction);
            }
            game.ResetSection();
            Check(game.Current.Pressed.Value == 0 && !game.Current.LeverOn.Value && game.HeldBox.Value == -1 && game.Direction.Value == GravityDirection.Down, "Reset complete");
            game.Die();
            Check(game.Dead.Value, "Death state");
            yield return Wait(1.1f);
            Check(!game.Dead.Value, "Respawn");

            // A closing selector must not materialize through a crate.
            game.Puzzle.Value = 2;
            game.ResetSection();
            game.runner.Restore(game.Current.lever.position + Vector3.left * 0.7f);
            game.Current.boxes[0].Body.position = game.Current.routeA.transform.position;
            Physics.SyncTransforms();
            Input(Vector2.zero, false, true);
            yield return Wait(0.1f);
            Check(!game.Current.LeverOn.Value && game.Current.RouteBlocked.Value, "Occupied route rejects closing");
            game.ResetSection();
            game.Current.LeverOn.Value = true;
            game.Current.boxes[0].Body.position = game.Current.transform.position + new Vector3(-4, 8, 0);
            game.Direction.Value = GravityDirection.Up;
            yield return Wait(1.2f);
            Check(game.Current.boxes[0].Body.position.y < 10 && game.Current.Pressed.Value == 0, "Starting with B blocks inlet A");
            game.ResetSection();
            game.Current.boxes[0].Body.position = game.Current.transform.position + new Vector3(-4, 15.3f, 0);
            game.Direction.Value = GravityDirection.Right;
            yield return Wait(1.3f);
            Check(game.Current.boxes[0].Body.position.x - game.Current.transform.position.x < 0 && game.Current.Pressed.Value == 0, "Unchanged A blocks delivery B");
            game.ResetSection();
            Check(!game.Current.LeverOn.Value && !game.Current.RouteBlocked.Value && !game.Current.routeA.enabled && game.Current.routeB.enabled, "Reset restores both routes");

            game.Puzzle.Value = 0;
            game.ResetSection();
            game.ChangeGravityRpc(GravityDirection.Up);
            yield return Wait(1.6f);
            Check(game.runner.Body.position.y < 8, "Wrong launch lane stops at shelf");
            game.ResetSection();
            yield return MoveZ(1.8f);
            game.ChangeGravityRpc(GravityDirection.Up);
            yield return Wait(2);
            yield return MoveX(4.5f);
            yield return MoveZ(-1.5f);
            yield return MoveX(10);
            yield return EnterExit(0);
            Check(game.Puzzle.Value == 1, "Puzzle 1 completed");
            if (failed) yield break;

            // P2: place a crate inside the retaining lip, then take the independent front/back route.
            yield return Wait(0.5f);
            game.InteractRpc();
            Check(game.HeldBox.Value == -1, "Operator cannot pick up crates");
            Input(Vector2.zero, false, true);
            yield return Wait(0.25f);
            Check(game.HeldBox.Value == 0, "Pickup via client RPC");
            yield return MoveX(4.85f);
            Input(Vector2.zero, false, true);
            yield return Wait(0.4f);
            game.ChangeGravityRpc(GravityDirection.Up);
            yield return Wait(2);
            Check(game.Current.Pressed.Value == 1, "Home P occupied");
            yield return MoveZ(1.8f);
            var parkingCollider = game.Current.boxes[0].GetComponent<Collider>();
            var originalFriction = parkingCollider.sharedMaterial;
            var frictionless = new PhysicsMaterial("Test zero friction") { staticFriction = 0, dynamicFriction = 0, frictionCombine = PhysicsMaterialCombine.Minimum };
            parkingCollider.sharedMaterial = frictionless;
            game.ChangeGravityRpc(GravityDirection.Right);
            yield return Wait(1.5f);
            Check(game.Current.Pressed.Value == 1 && !game.Current.boxes[0].Body.isKinematic, "Parking survives gravity change with zero friction");
            parkingCollider.sharedMaterial = originalFriction;
            Destroy(frictionless);
            yield return MoveY(8);
            yield return EnterExit(1);
            Check(game.Puzzle.Value == 2, "Puzzle 2 completed");
            if (failed) yield break;

            // P3: load via A, wait until the inlet clears, switch B through a real client interaction.
            yield return Wait(0.5f);
            Input(Vector2.zero, false, true);
            yield return Wait(0.25f);
            yield return MoveX(-5.25f);
            Input(Vector2.zero, false, true);
            yield return Wait(0.4f);
            game.ChangeGravityRpc(GravityDirection.Up);
            yield return Wait(2);
            Check(game.Current.boxes[0].Body.position.y > 14, "Crate loaded through A");
            Check(!game.Current.LeverOn.Value && game.Current.Pressed.Value == 0, "Closed B prevents delivery");
            Input(Vector2.zero, false, true);
            yield return Wait(0.3f);
            Check(game.Current.LeverOn.Value && game.Current.routeA.enabled && !game.Current.routeB.enabled, "Selector swaps physical routes");
            game.ChangeGravityRpc(GravityDirection.Right);
            yield return Wait(1.8f);
            Check(game.Current.Pressed.Value == 1, "B delivers crate to Q");
            yield return MoveY(7);
            yield return EnterExit(2);
            Check(game.Puzzle.Value == 3, "Puzzle 3 completed");
            if (failed) yield break;

            // P4: jump off the launch lip, transfer to the wall, then jump into an upward landing.
            yield return Wait(0.4f);
            yield return MoveX(-5.95f);
            Input(Vector2.zero, true);
            yield return Wait(0.16f);
            game.ChangeGravityRpc(GravityDirection.Right);
            yield return MoveY(7);
            yield return Wait(1.5f);
            Check(game.runner.Grounded && game.runner.Body.position.x - game.Current.transform.position.x < 2, "First aerial transfer landed");
            Input(Vector2.zero, true);
            yield return Wait(0.16f);
            game.ChangeGravityRpc(GravityDirection.Up);
            yield return Wait(1.6f);
            Check(game.runner.Grounded && game.runner.Body.position.y > 10, "Second aerial transfer landed");
            yield return MoveX(4.5f);
            yield return EnterExit(3);
            Check(game.Puzzle.Value == 4, "Puzzle 4 completed");
            if (failed) yield break;

            FinalPuzzle:
            // P5: prepare both paths, preserve P while delivering Q, then coordinate the exit approach.
            yield return Wait(0.4f);
            yield return MoveX(-10.2f);
            Input(Vector2.zero, false, true);
            yield return Wait(0.3f);
            Check(game.HeldBox.Value == 0, "First crate prepared for parking");
            yield return MoveX(-9.45f);
            Check(game.HeldBox.Value == 0, "First crate stays held during placement");
            Input(Vector2.zero, false, true);
            yield return Wait(0.4f);
            Check(game.HeldBox.Value == -1 && Mathf.Abs(game.Current.boxes[0].Body.position.x - game.Current.transform.position.x + 8.2f) < 0.8f, "First crate released on P launch line");
            yield return MoveZ(1.8f);
            yield return MoveX(-2.0f);
            yield return MoveZ(-1.5f);
            Check(Mathf.Abs(game.Current.boxes[0].Body.position.x - game.Current.transform.position.x + 8.2f) < 0.8f, "Parked launch position survives runner bypass");
            Input(Vector2.zero, false, true);
            yield return Wait(0.3f);
            Check(game.HeldBox.Value == 1, "Second crate prepared for routing");
            yield return MoveX(0.75f);
            Input(Vector2.zero, false, true);
            yield return Wait(0.4f);
            game.ChangeGravityRpc(GravityDirection.Up);
            yield return Wait(2);
            Check((game.Current.Pressed.Value & 1) != 0, "P is parked before Q delivery");
            Check(game.Current.boxes[1].Body.position.y > 14, "Q crate loaded through A");
            Input(Vector2.zero, false, true);
            yield return Wait(0.3f);
            Check(game.Current.LeverOn.Value, "Runner chooses delivery route");
            yield return MoveX(8);
            Input(Vector2.zero, true);
            yield return Wait(0.16f);
            game.ChangeGravityRpc(GravityDirection.Right);
            yield return Drive(new Vector2(-1, 0), 0.55f);
            yield return Wait(1.5f);
            Check(game.Current.Pressed.Value == 3, "P survives while Q arrives");
            yield return MoveY(5.8f);
            yield return EnterExit(4);
            Check(game.Finished.Value, "Puzzle 5 completed");
            Debug.Log($"[GravityTest] HOST {(failed ? "FAIL" : "PASS")} {checks} checks");
            yield return new WaitForSeconds(2);
            Application.Quit(failed ? 1 : 0);
        }

        void Receive(ulong sender, FastBufferReader reader)
        {
            if (manager.IsServer || sender != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out Vector2 move);
            reader.ReadValueSafe(out bool jump);
            reader.ReadValueSafe(out bool interact);
            game.MoveRpc(move, jump);
            if (interact) game.InteractRpc();
        }

        void Input(Vector2 move, bool jump = false, bool interact = false)
        {
            using var writer = new FastBufferWriter(32, Allocator.Temp);
            writer.WriteValueSafe(move);
            writer.WriteValueSafe(jump);
            writer.WriteValueSafe(interact);
            foreach (ulong client in manager.ConnectedClientsIds)
                if (client != NetworkManager.ServerClientId) manager.CustomMessagingManager.SendNamedMessage(Channel, client, writer);
        }

        IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) { Input(Vector2.zero); yield return null; }
        }

        IEnumerator Drive(Vector2 input, float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) { Input(input); yield return null; }
            Input(Vector2.zero);
        }

        IEnumerator MoveX(float x)
        {
            int puzzle = game.Puzzle.Value;
            float end = Time.time + 12;
            while (Time.time < end && game.Puzzle.Value == puzzle && !game.Finished.Value)
            {
                float delta = x - (game.runner.Body.position.x - game.Current.transform.position.x);
                if (Mathf.Abs(delta) < 0.12f) break;
                float right = Vector3.Cross(-game.Down, Vector3.forward).x;
                Input(new Vector2(Mathf.Clamp(delta * 2, -1, 1) * right, 0));
                yield return null;
            }
            Input(Vector2.zero);
            yield return Wait(0.2f);
        }

        IEnumerator SafeUp()
        {
            while ((manager.ServerTime.Time - game.SectionStarted.Value) % game.Current.laserPeriod < game.Current.laserOnTime + 0.1f)
                yield return Wait(0.02f);
            game.ChangeGravityRpc(GravityDirection.Up);
            yield return Wait(2);
        }

        IEnumerator MoveZ(float target)
        {
            int puzzle = game.Puzzle.Value;
            float end = Time.time + 6;
            while (Time.time < end && game.Puzzle.Value == puzzle && !game.Finished.Value)
            {
                float delta = target - game.runner.Body.position.z;
                if (Mathf.Abs(delta) < 0.1f) break;
                Input(new Vector2(0, Mathf.Clamp(delta * 2, -1, 1)));
                yield return null;
            }
            yield return Wait(0.2f);
        }

        IEnumerator MoveY(float target)
        {
            int puzzle = game.Puzzle.Value;
            float end = Time.time + 8;
            while (Time.time < end && game.Puzzle.Value == puzzle && !game.Finished.Value)
            {
                float delta = target - game.runner.Body.position.y;
                if (Mathf.Abs(delta) < 0.1f) break;
                float right = Vector3.Cross(-game.Down, Vector3.forward).y;
                Input(new Vector2(Mathf.Clamp(delta * 2, -1, 1) * right, 0));
                yield return null;
            }
            yield return Wait(0.2f);
        }

        IEnumerator EnterExit(int puzzle)
        {
            float end = Time.time + 3;
            while (Time.time < end && game.Puzzle.Value == puzzle && !game.Finished.Value)
            {
                Input(new Vector2(0, 1));
                yield return null;
            }
            yield return Wait(0.2f);
        }

        void Check(bool condition, string message)
        {
            checks++;
            if (!condition) Fail(message);
            else if (message != "No missing component") Debug.Log("[GravityTest] PASS " + message);
        }

        void Fail(string message)
        {
            failed = true;
            Debug.LogError("[GravityTest] FAIL " + message + (game == null ? "" : $" | puzzle={game.Puzzle.Value} runner={game.runner.transform.position} pressed={game.Current.Pressed.Value} gravity={game.Direction.Value}"));
            if (game != null)
            {
                Debug.Log($"[GravityTest] State held={game.HeldBox.Value} lever={game.Current.LeverOn.Value} blocked={game.Current.RouteBlocked.Value}");
                foreach (var box in game.Current.boxes) Debug.Log($"[GravityTest] Crate position={box.Body.position} velocity={box.Body.linearVelocity}");
            }
            Application.Quit(1);
        }

        static void Capture(string role)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../../Logs/gravity-" + role + ".png"));
            ScreenCapture.CaptureScreenshot(path);
        }
    }
}
#endif
