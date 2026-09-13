using System.IO;
using System.Linq;
using Sprint0.GravityCoop;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sprint0.Editor
{
    public static class GravityPrototypeBuilder
    {
        public const string ScenePath = "Assets/Scenes/GravityPrototype.unity";
        const string Materials = "Assets/_Project/Materials/Gravity";

        [MenuItem("Tools/Sprint 0/Build Gravity Prototype")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory(Materials);
            AssetDatabase.Refresh();
            var managerObject = new GameObject("Gravity Network");
            var transport = managerObject.AddComponent<UnityTransport>();
            var manager = managerObject.AddComponent<NetworkManager>();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.EnableSceneManagement = true;
            manager.NetworkConfig.ConnectionApproval = true;
            manager.NetworkConfig.TickRate = 30;
            manager.NetworkConfig.PlayerPrefab = null;
            new GameObject("Connection HUD").AddComponent<GravityConnection>().manager = manager;

            var gameObject = new GameObject("Gravity Game — tune gameplay here");
            gameObject.AddComponent<NetworkObject>();
            var game = gameObject.AddComponent<GravityGame>();
            game.puzzles = new GravityPuzzle[5];
            for (int i = 0; i < 5; i++) game.puzzles[i] = Room(i);

            var actor = new GameObject("Runner — server simulated");
            actor.transform.position = game.puzzles[0].spawn.position;
            var sphere = actor.AddComponent<SphereCollider>();
            sphere.radius = 0.5f;
            sphere.sharedMaterial = Friction("RunnerFriction", 0);
            var rigidbody = actor.AddComponent<Rigidbody>();
            rigidbody.useGravity = false;
            rigidbody.mass = 2;
            rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            actor.AddComponent<NetworkObject>();
            actor.AddComponent<NetworkTransform>();
            game.runner = actor.AddComponent<GravityRunner>();
            var visual = Primitive(actor.transform, "RunnerVisual", PrimitiveType.Capsule, Vector3.zero, new Vector3(0.8f, 0.5f, 0.8f), new Color(0.1f, 0.9f, 0.8f));
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            game.runner.visual = visual.transform;
            var marker = Primitive(visual.transform, "Head", PrimitiveType.Sphere, new Vector3(0, 0.7f, 0), Vector3.one * 0.35f, Color.white);
            Object.DestroyImmediate(marker.GetComponent<Collider>());

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0, 8, -35);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.04f, 0.065f);
            camera.farClipPlane = 65;
            camera.fieldOfView = 60;
            cameraObject.AddComponent<AudioListener>();
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.7f;
            light.transform.rotation = Quaternion.Euler(35, -30, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.55f, 0.65f);

            Validate(scene, game);

            EditorSceneManager.SaveScene(scene, ScenePath);
            // Preserve the sample as a secondary scene; standalone starts at the prototype.
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                .Concat(EditorBuildSettings.scenes.Where(s => s.path != ScenePath)).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[Gravity] Prototype scene generated.");
        }

        static void Validate(Scene scene, GravityGame game)
        {
            int components = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) throw new System.Exception("Missing script in prototype scene");
                    components++;
                    var serialized = new SerializedObject(component);
                    var property = serialized.GetIterator();
                    while (property.NextVisible(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null
                            && property.objectReferenceInstanceIDValue != 0)
                            throw new System.Exception("Missing reference: " + component.name + "." + property.propertyPath);
                }
            if (game.runner == null || game.runner.visual == null || game.puzzles.Length != 5) throw new System.Exception("Invalid prototype configuration");
            foreach (var puzzle in game.puzzles)
                if (puzzle.spawn == null || puzzle.exit == null || puzzle.boxes.Any(b => b == null)
                    || puzzle.plates.Any(p => p == null) || puzzle.hazards.Any(h => h == null)) throw new System.Exception("Invalid puzzle references");
            Debug.Log($"[Gravity] Validated {components} components; no missing scripts/references; five configured puzzles.");
        }

        static GravityPuzzle Room(int index)
        {
            var root = new GameObject($"Puzzle {index + 1}");
            root.transform.position = new Vector3(index * 40, 0, 0);
            root.AddComponent<NetworkObject>();
            var puzzle = root.AddComponent<GravityPuzzle>();
            puzzle.index = index;
            puzzle.title = new[] { "벽으로 건너가기", "상자 주차", "두 갈래 배송", "공중 환승", "두 상자와 탈출로" }[index];
            puzzle.objective = new[] {
                "받침의 앞뒤를 확인하고 벽 너머로 건너가세요. 상대에게 출발 위치를 알려주세요.",
                "P 홈에 상자를 보관하세요. 다음 중력에서도 압력판을 유지할 수 있을까요?",
                "A로 넣고 B로 배송하세요. 레버와 상자의 통과 시점을 함께 판단하세요.",
                "점프 신호에 맞춰 중력을 바꾸고 넓은 발판으로 환승하세요. 바닥에서 재도전할 수 있습니다.",
                "P와 Q에 상자를 보관하고 탈출하세요. 분기 전환 전에 두 상자의 낙하 경로를 확인하세요."
            }[index];
            var parent = root.transform;
            var wall = new Color(0.15f, 0.21f, 0.29f);
            Block(parent, "Floor", new Vector3(0, -0.5f, 0), new Vector3(25, 1, 7), wall);
            Block(parent, "Ceiling", new Vector3(0, 16.5f, 0), new Vector3(25, 1, 7), wall);
            Block(parent, "Left wall", new Vector3(-12.5f, 8, 0), new Vector3(1, 16, 7), wall);
            Block(parent, "Right wall", new Vector3(12.5f, 8, 0), new Vector3(1, 16, 7), wall);
            Block(parent, "Back wall", new Vector3(0, 8, 3.5f), new Vector3(24, 16, 1), new Color(0.08f, 0.12f, 0.18f));
            var front = Block(parent, "Invisible front boundary", new Vector3(0, 8, -3.5f), new Vector3(24, 16, 1), wall);
            front.GetComponent<Renderer>().enabled = false;
            puzzle.spawn = new GameObject("Checkpoint").transform;
            puzzle.spawn.SetParent(parent, false);
            puzzle.spawn.localPosition = new Vector3(-7, 0.7f, -1.5f);
            puzzle.plates = System.Array.Empty<BoxCollider>();
            puzzle.boxes = System.Array.Empty<GravityBody>();
            puzzle.hazards = System.Array.Empty<BoxCollider>();
            Vector3 exitPosition = new(10, 14.8f, 1.8f);
            Vector3 exitSize = new(2, 2, 1.4f);
            if (index == 0)
            {
                Block(parent, "Front shelf — choose rear launch lane", new Vector3(-4.5f, 8, -1.4f), new Vector3(15, 0.5f, 3.2f), wall);
                Block(parent, "Rear baffle — change lane at the ceiling", new Vector3(6, 13, 1.6f), new Vector3(0.5f, 6, 2.8f), wall);
                Label(parent, "A / FRONT", new Vector3(-7, 7.3f, -2.8f));
                Label(parent, "B / REAR", new Vector3(-1, 9, 0.5f));
            }
            if (index == 1)
            {
                puzzle.boxes = new[] { Box(parent, index, new Vector3(-7, 0.6f, 0)) };
                puzzle.plates = new[] { ParkingHome(parent, 6.1f, "P", wall) };
                exitPosition = new Vector3(11.5f, 8, 1.8f);
                exitSize = new Vector3(1.2f, 2.4f, 1.4f);
            }
            if (index == 2 || index == 4)
            {
                float intake = index == 2 ? -4 : 2;
                float divider = index == 2 ? 0 : 6;
                float left = index == 2 ? -12 : 0;
                float leftWidth = intake - 1.2f - left;
                Block(parent, "Intake shelf left", new Vector3(left + leftWidth / 2, 10, 0), new Vector3(leftWidth, 0.4f, 6), wall);
                float rightWidth = 12 - (intake + 1.2f);
                Block(parent, "Intake shelf right", new Vector3(intake + 1.2f + rightWidth / 2, 10, 0), new Vector3(rightWidth, 0.4f, 6), wall);
                if (index == 4)
                    Block(parent, "Branch outer wall", new Vector3(0, 13, 0), new Vector3(0.4f, 6, 6), wall);
                puzzle.routeA = Block(parent, "Route A — inlet", new Vector3(intake, 10, 0), new Vector3(2.4f, 0.4f, 6), new Color(0.15f, 0.65f, 0.95f)).GetComponent<BoxCollider>();
                puzzle.routeB = Block(parent, "Route B — delivery", new Vector3(divider, 13, 0), new Vector3(0.4f, 6, 6), new Color(0.8f, 0.4f, 0.95f)).GetComponent<BoxCollider>();
                puzzle.lever = Volume(parent, "Route selector", new Vector3(divider - 1.5f, 9.1f, -1.5f), new Vector3(0.5f, 0.7f, 0.5f), Color.magenta).transform;
                Label(parent, "A / IN", new Vector3(intake, 10.8f, -2.95f));
                Label(parent, "B / OUT", new Vector3(divider + 0.8f, 12.8f, -2.95f));
                Label(parent, "E / A-B", new Vector3(divider - 1.5f, 8.5f, -2.5f));
                var delivery = Volume(parent, "Q delivery plate", new Vector3(11.65f, 15, 0), new Vector3(0.7f, 2, 5.8f), Color.yellow);
                Label(parent, "Q", new Vector3(10.8f, 14.5f, -2.8f));
                if (index == 2)
                {
                    puzzle.boxes = new[] { Box(parent, index, new Vector3(-7, 0.6f, 0)) };
                    puzzle.plates = new[] { delivery };
                }
                else
                {
                    puzzle.boxes = new[] { Box(parent, index, new Vector3(-9, 0.6f, 0)), Box(parent, index, new Vector3(-1, 0.6f, 0)) };
                    puzzle.plates = new[] { ParkingHome(parent, -8.2f, "P", wall), delivery };
                    // The lip makes preparing a launch position useful before the rightward transfer.
                    Block(parent, "Exit approach lip", new Vector3(11.25f, 7.9f, 0), new Vector3(1.5f, 0.4f, 6), wall);
                    Label(parent, "LAUNCH", new Vector3(8, 9, -2.8f));
                }
                exitPosition = new Vector3(11.5f, index == 2 ? 7 : 5.8f, 1.8f);
                exitSize = new Vector3(1.2f, 2, 1.4f);
            }
            if (index == 3)
            {
                puzzle.spawn.localPosition = new Vector3(-8, 4.85f, -1.5f);
                Block(parent, "Launch platform", new Vector3(-8, 4, -1), new Vector3(6, 0.6f, 4), wall);
                Block(parent, "Launch lip", new Vector3(-5.1f, 4.7f, -1), new Vector3(0.4f, 0.8f, 4), wall);
                Block(parent, "Broad transfer wall", new Vector3(1, 7, 0), new Vector3(0.6f, 4, 6), wall);
                Block(parent, "Ceiling landing platform", new Vector3(2, 12, 0), new Vector3(8, 0.6f, 6), wall);
                Label(parent, "1 / JUMP", new Vector3(-7, 5.7f, -2.8f));
                Label(parent, "2 / REST", new Vector3(0.2f, 7.5f, -2.8f));
                Label(parent, "3 / LAND", new Vector3(4.5f, 10.7f, -2.8f));
                exitPosition = new Vector3(4.5f, 11.1f, 1.8f);
                exitSize = new Vector3(2, 1.2f, 1.4f);
                // The broad floor recovers missed transfers; only the far corner is dangerous.
                puzzle.hazards = new[] { Volume(parent, "Corner hazard", new Vector3(10, 0.15f, 0), new Vector3(3, 0.3f, 6), new Color(1, 0.15f, 0.12f)) };
            }
            puzzle.exit = Volume(parent, "Exit", exitPosition, exitSize, new Color(0.1f, 0.7f, 0.55f));
            Label(parent, "EXIT", exitPosition + new Vector3(-1.3f, 0, -1.8f));
            if (puzzle.plates.Length > 0)
                puzzle.door = Block(parent, "Conditional exit gate", exitPosition, exitSize, new Color(0.25f, 0.4f, 0.8f)).GetComponent<Collider>();
            return puzzle;
        }

        static BoxCollider ParkingHome(Transform parent, float x, string label, Color wall)
        {
            Block(parent, label + " retaining lip", new Vector3(x + 0.9f, 14.75f, -1), new Vector3(0.5f, 2.5f, 3), wall);
            var plate = Volume(parent, label + " home plate", new Vector3(x, 15.65f, -1), new Vector3(1.9f, 0.7f, 3), Color.yellow);
            Label(parent, label, new Vector3(x, 14.6f, -2.7f));
            return plate;
        }

        static void Label(Transform parent, string text, Vector3 position)
        {
            var obj = new GameObject(text);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            var label = obj.AddComponent<TextMesh>();
            label.text = text;
            label.fontSize = 36;
            label.characterSize = 0.075f;
            label.anchor = TextAnchor.MiddleCenter;
            label.color = Color.white;
        }

        static GravityBody Box(Transform parent, int puzzle, Vector3 position)
        {
            var box = Block(parent, "Gravity crate", position, Vector3.one, new Color(1, 0.55f, 0.12f));
            box.GetComponent<Collider>().sharedMaterial = Friction("CrateFriction", 0.6f);
            var body = box.AddComponent<Rigidbody>();
            body.mass = 1;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            box.AddComponent<NetworkObject>();
            box.AddComponent<NetworkTransform>();
            var gravity = box.AddComponent<GravityBody>();
            gravity.puzzleIndex = puzzle;
            return gravity;
        }

        static PhysicsMaterial Friction(string name, float friction)
        {
            var path = Materials + "/" + name + ".physicMaterial";
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (material != null) return material;
            material = new PhysicsMaterial(name) { dynamicFriction = friction, staticFriction = friction, bounciness = 0, frictionCombine = PhysicsMaterialCombine.Minimum };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static BoxCollider Volume(Transform parent, string name, Vector3 position, Vector3 size, Color color)
        {
            var result = Block(parent, name, position, size, color).GetComponent<BoxCollider>();
            result.isTrigger = true;
            return result;
        }

        static GameObject Block(Transform parent, string name, Vector3 position, Vector3 size, Color color) => Primitive(parent, name, PrimitiveType.Cube, position, size, color);

        static GameObject Primitive(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 size, Color color)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = size;
            string path = Materials + "/Color" + ColorUtility.ToHtmlStringRGB(color) + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
                AssetDatabase.CreateAsset(material, path);
            }
            obj.GetComponent<Renderer>().sharedMaterial = material;
            return obj;
        }

        public static void BuildPlayer()
        {
            Build();
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = "Builds/GravityPrototype/GravityPrototype.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new System.Exception("Gravity build failed");
        }

        // Render the saved scene offscreen: hidden batch-mode player windows have black backbuffers.
        public static void CapturePreview()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var game = Object.FindFirstObjectByType<GravityGame>();
            Validate(scene, game);
            var camera = Camera.main;
            camera.orthographic = true;
            camera.orthographicSize = 10.5f;
            camera.transform.position = game.puzzles[4].transform.position + new Vector3(0, 8, -35);
            camera.transform.rotation = Quaternion.identity;
            Capture(camera, "Logs/gravity-overview-preview.png");
            camera.orthographic = false;
            camera.transform.position = game.runner.transform.position + new Vector3(0, 1.2f, -7);
            Capture(camera, "Logs/gravity-runner-preview.png");
        }

        static void Capture(Camera camera, string path)
        {
            var target = new RenderTexture(1280, 900, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var texture = new Texture2D(1280, 900, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1280, 900), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(target);
        }
    }
}
