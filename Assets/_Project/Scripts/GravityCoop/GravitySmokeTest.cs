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
                new GameObject("Development integration test").AddComponent<GravitySmokeTest>();
        }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            float until = Time.realtimeSinceStartup + 45;
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
                float deadline = Time.realtimeSinceStartup + 180;
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

            // All four surfaces: falling, landing, tangent motion, and jumping through client input RPCs.
            foreach (GravityDirection direction in Enum.GetValues(typeof(GravityDirection)))
            {
                game.ResetSection();
                game.runner.Restore(new Vector3(0, 7, 0));
                game.ChangeGravityRpc(direction);
                yield return Wait(2.5f);
                Check(game.Direction.Value == direction, "Direction " + direction);
                Check(game.runner.Grounded, "Landing " + direction);
                Vector3 start = game.runner.Body.position;
                yield return Drive(new Vector2(0, 1), 0.35f);
                Check(game.runner.Body.position.z > start.z + 0.25f, "Movement " + direction);
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
                box.Body.position = game.Current.transform.position + new Vector3(0, 7, 0);
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

            // P1: use ceiling then walk to exit.
            game.Puzzle.Value = 0;
            game.ResetSection();
            game.ChangeGravityRpc(GravityDirection.Up);
            yield return Wait(2);
            yield return MoveX(10);
            yield return EnterExit(0);
            Check(game.Puzzle.Value == 1, "Puzzle 1 completed");
            if (failed) yield break;

            // P2: carry crate underneath its ceiling plate, then change gravity.
            yield return Wait(0.5f);
            game.InteractRpc();
            Check(game.HeldBox.Value == -1, "Operator cannot pick up crates");
            Input(Vector2.zero, false, true);
            yield return Wait(0.25f);
            Check(game.HeldBox.Value == 0, "Pickup via client RPC");
            yield return MoveX(4.75f);
            Input(Vector2.zero, false, true);
            yield return Wait(0.4f);
            Check(game.HeldBox.Value == -1, "Drop via client RPC");
            game.ChangeGravityRpc(GravityDirection.Up);
            yield return Wait(2);
            Check(game.Current.Pressed.Value == 1, "Ceiling pressure plate");
            yield return Drive(new Vector2(0, 1), 0.65f); // Walk around the crate instead of pushing it off the plate.
            yield return MoveX(10);
            yield return EnterExit(1);
            Check(game.Puzzle.Value == 2, "Puzzle 2 completed");
            if (failed) yield break;

            // P3: straight up is blocked by the shelf. Right/up/left/up routes around it.
            game.ChangeGravityRpc(GravityDirection.Up);
            yield return Wait(1.8f);
            Check(game.Current.boxes[0].Body.position.y < 8, "Wrong order blocked by shelf");
            game.ResetSection();
            game.ChangeGravityRpc(GravityDirection.Right);
            yield return Wait(2.5f);
            game.ChangeGravityRpc(GravityDirection.Up);
            yield return Wait(2.5f);
            game.ChangeGravityRpc(GravityDirection.Left);
            yield return Wait(2.5f);
            game.ChangeGravityRpc(GravityDirection.Up);
            yield return Wait(1.2f);
            Check(game.Current.Pressed.Value == 1, "Sequence plate");
            yield return MoveX(8);
            yield return EnterExit(2);
            Check(game.Puzzle.Value == 3, "Puzzle 3 completed");
            if (failed) yield break;

            // P4: wait for the pulse to turn off before traversing the laser plane.
            yield return SafeUp();
            yield return MoveX(10);
            yield return EnterExit(3);
            Check(game.Puzzle.Value == 4, "Puzzle 4 completed");
            if (failed) yield break;

            // P5: runner positions the second crate, operator watches the global laser phase.
            yield return MoveX(4.7f);
            Input(Vector2.zero, false, true);
            yield return Wait(0.3f);
            Check(game.HeldBox.Value == 1, "Second crate pickup");
            yield return MoveX(6.75f);
            Input(Vector2.zero, false, true);
            yield return Wait(0.4f);
            yield return SafeUp();
            Check(game.Current.Pressed.Value == 3, "Both crates occupy plates");
            yield return MoveX(4);
            Input(Vector2.zero, false, true);
            yield return Wait(0.4f);
            Check(game.Current.LeverOn.Value, "Runner-only lever");
            yield return MoveX(0);
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
