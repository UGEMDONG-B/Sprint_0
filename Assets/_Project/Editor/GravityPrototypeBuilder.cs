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
    public static partial class GravityPrototypeBuilder
    {
        public const string ScenePath = "Assets/Scenes/GravityPrototype.unity";
        const string Materials = "Assets/_Project/Materials/Gravity";
        const int PuzzleCount = 12;

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
            ImportSharedInterface(scene, manager);

            var gameObject = new GameObject("Gravity Game — tune gameplay here");
            gameObject.AddComponent<NetworkObject>();
            var game = gameObject.AddComponent<GravityGame>();
            game.puzzles = new GravityPuzzle[PuzzleCount];
            for (int i = 0; i < PuzzleCount; i++) game.puzzles[i] = Room(i);

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
                    if (component is TextMesh text && text.font == null)
                        throw new System.Exception("Missing label font: " + component.name);
                    var serialized = new SerializedObject(component);
                    var property = serialized.GetIterator();
                    while (property.NextVisible(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null
                            && property.objectReferenceInstanceIDValue != 0)
                            throw new System.Exception("Missing reference: " + component.name + "." + property.propertyPath);
                }
            if (game.runner == null || game.runner.visual == null || game.puzzles.Length != PuzzleCount) throw new System.Exception("Invalid prototype configuration");
            foreach (var puzzle in game.puzzles)
            {
                if (puzzle.spawn == null || puzzle.exit == null || puzzle.boxes.Any(b => b == null)
                    || puzzle.plates.Any(p => p == null) || puzzle.hazards.Any(h => h == null)) throw new System.Exception("Invalid puzzle references");
                if (puzzle.rotors.Any(r => r == null || r.handle == null || r.elbow == null || r.interior == null || r.walls.Length != 2 || r.walls.Any(w => w == null))
                    || puzzle.clamps.Any(c => c == null || c.handle == null || c.socket == null)) throw new System.Exception("Invalid expansion references");
            }
            Debug.Log($"[Gravity] Validated {components} components; no missing scripts/references; {game.puzzles.Length} configured puzzles.");
        }

        static void ImportSharedInterface(Scene destination, NetworkManager manager)
        {
            // Move the loaded UI roots together so all serialized cross-root button references survive.
            // SampleScene remains the source of truth; never save the additive source scene.
            var source = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Additive);
            var controller = source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Sprint0.Multiplayer.MultiplayerGameController>(true)).Single();
            var canvas = source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Canvas>(true)).Single();
            var events = source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true)).Single();
            foreach (var root in new[] { controller.transform.root.gameObject, canvas.transform.root.gameObject, events.transform.root.gameObject }.Distinct())
                SceneManager.MoveGameObjectToScene(root, destination);
            EditorSceneManager.CloseScene(source, true);
            SceneManager.SetActiveScene(destination);
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("maxPlayers").intValue = 2;
            serialized.FindProperty("minimumPlayersToStart").intValue = 2;
            serialized.FindProperty("sessionType").stringValue = "Sprint0.GravityCoop";
            serialized.FindProperty("lockCursorForGameplay").boolValue = false;
            serialized.FindProperty("reloadSceneAfterSession").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            // Replace only the sample's gameplay presentation; preserve all shared menu screens.
            foreach (Transform child in controller.GameHudScreen.transform) child.gameObject.SetActive(false);
        }

        static GravityPuzzle Room(int index)
        {
            var root = new GameObject($"Puzzle {index + 1}");
            root.transform.position = new Vector3(index * 40, 0, 0);
            root.AddComponent<NetworkObject>();
            var puzzle = root.AddComponent<GravityPuzzle>();
            puzzle.index = index;
            puzzle.title = new[] { "벽으로 건너가기", "상자 주차", "두 갈래 배송", "공중 환승", "두 상자와 탈출로", "받고 다시 보내기", "둘 다 준비됐어?", "보관하고 길 열기", "앞에서 받고 뒤로 보내기", "돌려서 이어주기", "하나를 남겨두고", "배송팀" }[index];
            puzzle.objective = new[] {
                "받침의 앞뒤를 확인하고 벽 너머로 건너가세요. 상대에게 출발 위치를 알려주세요.",
                "P 홈에 상자를 보관하세요. 다음 중력에서도 압력판을 유지할 수 있을까요?",
                "하늘색 A 구역 안에 상자를 놓고 준비 신호를 주세요. A로 올린 뒤 B를 열어 Q로 배송하세요.",
                "점프 신호에 맞춰 중력을 바꾸고 넓은 발판으로 환승하세요. 바닥에서 재도전할 수 있습니다.",
                "P와 Q에 상자를 보관하고 탈출하세요. 분기 전환 전에 두 상자의 낙하 경로를 확인하세요.",
                "상자를 A로 올린 뒤 천장의 스위치로 이동하세요. 내부 플레이어의 다음 행동을 기다렸다가 B로 배송하세요.",
                "P와 Q의 발사 위치를 함께 준비하세요. 두 상자가 안착하면 뒤쪽 탈출 경로로 이동한 뒤 오른쪽 중력을 요청하세요.",
                "P를 보관한 채 Q를 A로 올리세요. A를 지나 뒤쪽 천장 스위치를 열고, 탈출 경로로 빠진 뒤 B로 보내세요.",
                "A 아래 창구와 B 위 창구 중 인계할 곳을 정하세요. 상자를 뒤쪽으로 넘기고 Q에 배송하세요.",
                "내부 플레이어가 R1과 R2 통로를 회전시키고 조작자가 상자를 보냅니다. 상자가 통로 안에 있으면 돌릴 수 없습니다.",
                "P에 도착한 상자를 E로 고정한 뒤 Q를 배송하세요. 중력을 바꾸기 전에 잠금 표시를 확인하세요.",
                "P를 고정해 인계 창구를 열고, 두 번째 상자를 뒤로 넘겨 R1로 배송하세요. P 고정 후 R로 중간 재시도, Shift+R로 처음부터 시작합니다."
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
            if (index >= 8) BuildExpansion(puzzle, wall, out exitPosition, out exitSize);
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
            if (index == 2 || index == 4 || index == 5 || index == 7)
            {
                bool singleDelivery = index == 2 || index == 5;
                float intake = singleDelivery ? -4 : 2;
                float divider = singleDelivery ? 0 : 6;
                float left = singleDelivery ? -12 : 0;
                float intakeHalfWidth = index == 2 ? 2.4f : 1.2f;
                float leftWidth = intake - intakeHalfWidth - left;
                Block(parent, "Intake shelf left", new Vector3(left + leftWidth / 2, 10, 0), new Vector3(leftWidth, 0.4f, 6), wall);
                float rightWidth = 12 - (intake + intakeHalfWidth);
                Block(parent, "Intake shelf right", new Vector3(intake + intakeHalfWidth + rightWidth / 2, 10, 0), new Vector3(rightWidth, 0.4f, 6), wall);
                if (index == 5 || index == 7)
                {
                    // After joining the crate upstairs, the runner needs a return path
                    // that stays open when A closes. Crate delivery remains in the front lane.
                    var returnShelf = parent.Find("Intake shelf right");
                    returnShelf.localPosition += Vector3.back;
                    returnShelf.localScale = new Vector3(rightWidth, 0.4f, 4);
                    Label(parent, "REAR / RETURN", new Vector3(8, 9, 1.8f));
                }
                if (!singleDelivery)
                    Block(parent, "Branch outer wall", new Vector3(0, 13, 0), new Vector3(0.4f, 6, 6), wall);
                puzzle.routeA = Block(parent, "Route A — inlet", new Vector3(intake, 10, 0), new Vector3(intakeHalfWidth * 2, 0.4f, 6), new Color(0.15f, 0.65f, 0.95f)).GetComponent<BoxCollider>();
                puzzle.routeB = Block(parent, "Route B — delivery", new Vector3(divider, 13, 0), new Vector3(0.4f, 6, 6), new Color(0.8f, 0.4f, 0.95f)).GetComponent<BoxCollider>();
                puzzle.routeA.enabled = false;
                puzzle.routeA.GetComponent<Renderer>().enabled = false;
                puzzle.lever = Volume(parent, "Route selector", new Vector3(intake - 2.2f, 9.1f, -1.5f), new Vector3(0.5f, 0.7f, 0.5f), Color.magenta).transform;
                if (index == 2)
                {
                    // Keep the switch under solid shelf beside the wider opening.
                    puzzle.lever.localPosition = new Vector3(intake - intakeHalfWidth - 1, 9.1f, -1.5f);
                    // Paint only: no collider, snapping, trigger, or extra puzzle condition.
                    // A one-unit crate fully inside this area has generous aperture clearance.
                    var pad = Block(parent, "A loading area — visual only", new Vector3(intake, 0.012f, 0),
                        new Vector3(3.2f, 0.02f, 4), new Color(0.15f, 0.65f, 0.95f));
                    Object.DestroyImmediate(pad.GetComponent<Collider>());
                    Label(parent, "A / LOAD", new Vector3(intake, 1.3f, -2.5f)).color = new Color(0.15f, 0.65f, 0.95f);
                    Label(parent, "FRONT", new Vector3(2, 0.8f, -2.6f));
                    Label(parent, "REAR", new Vector3(2, 0.8f, 2.6f));
                }
                var inletLabel = Label(parent, "A / IN", new Vector3(intake, 10.8f, -2.95f));
                var outletLabel = Label(parent, "B / OUT", new Vector3(divider + 0.8f, 12.8f, -2.95f));
                if (index == 2)
                {
                    inletLabel.color = new Color(0.15f, 0.65f, 0.95f);
                    outletLabel.color = new Color(0.8f, 0.4f, 0.95f);
                }
                if (index == 5 || index == 7)
                {
                    // A safe stopping point separates loading from the runner's next action.
                    // The operator must wait for the runner to reach the existing selector.
                    var switchPosition = index == 5 ? new Vector3(-8, 15.1f, -1.5f) : new Vector3(4.5f, 15.1f, 1.8f);
                    puzzle.lever.localPosition = switchPosition;
                    Label(parent, "WAIT / SWITCH", switchPosition + new Vector3(0, -1.3f, -0.6f));
                }
                Label(parent, "E / A-B", puzzle.lever.localPosition + new Vector3(0, -0.6f, -1));
                var delivery = Volume(parent, "Q delivery plate", new Vector3(11.65f, 15, 0), new Vector3(0.7f, 2, 5.8f), Color.yellow);
                Label(parent, "Q / WEIGHT", new Vector3(10.8f, 14.5f, -2.8f));
                if (singleDelivery)
                {
                    puzzle.boxes = new[] { Box(parent, index, new Vector3(-7, 0.6f, 0)) };
                    puzzle.plates = new[] { delivery };
                }
                else
                {
                    puzzle.boxes = new[] { Box(parent, index, new Vector3(-9, 0.6f, 0)), Box(parent, index, new Vector3(-1, 0.6f, 0)) };
                    puzzle.plates = new[] { ParkingHome(parent, -8.2f, "P", wall), delivery };
                    if (index == 4)
                    {
                        // The lip makes preparing a launch position useful before the rightward transfer.
                        Block(parent, "Exit approach lip", new Vector3(11.25f, 7.9f, 0), new Vector3(1.5f, 0.4f, 6), wall);
                        Label(parent, "LAUNCH", new Vector3(8, 9, -2.8f));
                    }
                }
                exitPosition = new Vector3(11.5f, singleDelivery ? 7 : 5.8f, 1.8f);
                exitSize = new Vector3(1.2f, 2, 1.4f);
            }
            if (index == 6)
            {
                puzzle.boxes = new[] { Box(parent, index, new Vector3(-9, 0.6f, 0)), Box(parent, index, new Vector3(-1, 0.6f, 0)) };
                puzzle.plates = new[] { ParkingHome(parent, -6, "P", wall), ParkingHome(parent, 4, "Q", wall) };
                // Front pockets retain both crates; the rear lane lets the runner pass the lips.
                Block(parent, "Front exit baffle — prepare rear lane", new Vector3(8, 13, -1), new Vector3(0.5f, 6, 3), wall);
                Label(parent, "P / LOAD", new Vector3(-6, 1.5f, -2.7f));
                Label(parent, "Q / LOAD", new Vector3(4, 1.5f, -2.7f));
                Label(parent, "REAR / READY", new Vector3(8, 11, 1.8f));
                exitPosition = new Vector3(11.5f, 8, 1.8f);
                exitSize = new Vector3(1.2f, 2.4f, 1.4f);
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
                puzzle.door = Block(parent, "Conditional exit gate", exitPosition, exitSize + Vector3.one * 0.12f, new Color(0.25f, 0.4f, 0.8f)).GetComponent<Collider>();
            foreach (var crate in puzzle.boxes)
            {
                Hint(crate.GetComponent<Collider>(), "운반 상자", "중력을 따라 떨어집니다. 내부 플레이어가 가까이에서 E로 집거나 내려놓을 수 있습니다.");
                Label(crate.transform, "E / CARRY", new Vector3(0, 0.8f, -0.6f));
            }
            foreach (var surface in parent.GetComponentsInChildren<BoxCollider>())
                if (!surface.isTrigger && surface.name != "Invisible front boundary" && surface.GetComponent<GravityHint>() == null && surface != puzzle.door && surface != puzzle.routeA && surface != puzzle.routeB)
                    Hint(surface, "고정 벽 / 발판", "중력 방향에 따라 벽도 바닥이 됩니다. 앞뒤로 비어 있는 공간과 착지할 면을 살펴보세요.");
            string plateNames = "";
            for (int i = 0; i < puzzle.plates.Length; i++)
            {
                var plate = puzzle.plates[i];
                string id = plate.name.StartsWith("P") ? "P" : "Q";
                Hint(plate, id + " 압력판", "사람이나 내려놓은 상자가 닿아 있는 동안 작동합니다. 떨어지면 꺼지고 출구가 닫힙니다.").plateIndex = i;
                plateNames += (i == 0 ? "" : " + ") + id;
            }
            Hint(puzzle.exit, "출구" + (plateNames.Length > 0 ? " · " + plateNames : ""),
                plateNames.Length > 0 ? "같은 이름의 압력판과 연결되어 있습니다. 문이 열린 동안 내부 플레이어가 들어가면 완료됩니다." : "내부 플레이어가 이 영역에 들어가면 완료됩니다.");
            if (plateNames.Length > 0) Label(parent, plateNames + " / HOLD", exitPosition + new Vector3(-2, -0.7f, -1.8f));
            if (puzzle.lever != null)
            {
                Hint(puzzle.lever.GetComponent<Collider>(), "A/B 전환 레버", "내부 플레이어가 가까이에서 E를 누르면 한 통로가 열리고 다른 통로가 닫힙니다.");
                Hint(puzzle.routeA, "A 통로 · 하늘색", "레버로 여닫습니다. 문이 사라지면 통과할 수 있습니다. 문 안에 사람이나 상자가 있으면 닫히지 않습니다.");
                Hint(puzzle.routeB, "B 통로 · 보라색", "A와 반대로 열리고 닫힙니다. 레버를 조작하는 사람과 통과 시점을 맞춰보세요.");
            }
            foreach (var hazard in puzzle.hazards)
                Hint(hazard, "위험 구역 · 빨강", "닿으면 현재 퍼즐이 처음 상태로 돌아갑니다.");
            if (index == 2 || index >= 8)
                foreach (var label in parent.GetComponentsInChildren<TextMesh>())
                    label.gameObject.AddComponent<GravityLandmark>();
            return puzzle;
        }

        static GravityHint Hint(Collider target, string heading, string explanation)
        {
            var hint = target.gameObject.AddComponent<GravityHint>();
            hint.target = target;
            hint.heading = heading;
            hint.explanation = explanation;
            return hint;
        }

        static BoxCollider ParkingHome(Transform parent, float x, string label, Color wall)
        {
            var lip = Block(parent, label + " retaining lip", new Vector3(x + 0.9f, 14.75f, -1), new Vector3(0.5f, 2.5f, 3), wall);
            Hint(lip.GetComponent<Collider>(), "고정 받침 턱", "중력이 바뀌어도 움직이지 않습니다. 상자가 미끄러지는 것을 받쳐줄 수 있습니다.");
            var plate = Volume(parent, label + " home plate", new Vector3(x, 15.65f, -1), new Vector3(1.9f, 0.7f, 3), Color.yellow);
            Label(parent, label + " / WEIGHT", new Vector3(x, 14.6f, -2.7f));
            return plate;
        }

        static TextMesh Label(Transform parent, string text, Vector3 position)
        {
            var obj = new GameObject(text);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            var label = obj.AddComponent<TextMesh>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            obj.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
            label.text = text;
            label.fontSize = 36;
            label.characterSize = 0.1f;
            label.anchor = TextAnchor.MiddleCenter;
            label.color = Color.white;
            return label;
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
            for (int i = 0; i < game.puzzles.Length; i++)
            {
                camera.transform.position = game.puzzles[i].transform.position + new Vector3(0, 8 + game.observerElevation, -35);
                camera.transform.rotation = Quaternion.LookRotation(new Vector3(0, -game.observerElevation, 35), Vector3.up);
                Capture(camera, $"Logs/gravity-redesign-puzzle-{i + 1}.png");
            }
            camera.orthographic = false;
            camera.transform.position = game.runner.transform.position + new Vector3(0, 1.2f, -7);
            camera.transform.rotation = Quaternion.identity;
            Capture(camera, "Logs/gravity-runner-preview.png");
        }

        public static void CaptureCommunicationPreview()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var game = Object.FindFirstObjectByType<GravityGame>();
            Validate(scene, game);
            var origin = game.puzzles[2].transform.position;
            var camera = Camera.main;
            camera.orthographic = true;
            camera.orthographicSize = 10.5f;
            camera.transform.SetPositionAndRotation(origin + new Vector3(0, 8 + game.observerElevation, -35),
                Quaternion.LookRotation(new Vector3(0, -game.observerElevation, 35), Vector3.up));
            Capture(camera, "Logs/gravity-communication-operator.png");
            camera.orthographic = false;
            camera.nearClipPlane = 0.05f;
            camera.transform.position = origin + new Vector3(-8.5f, 3.5f, -2.5f);
            camera.transform.rotation = Quaternion.LookRotation(origin + new Vector3(-3, 0.8f, 0) - camera.transform.position);
            Capture(camera, "Logs/gravity-communication-runner.png");
            camera.transform.position = origin + new Vector3(1, 3.5f, 2.5f);
            camera.transform.rotation = Quaternion.LookRotation(origin + new Vector3(-4, 0.8f, 0) - camera.transform.position);
            Capture(camera, "Logs/gravity-communication-runner-rear.png");
        }

        public static void CaptureSharedInterface()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var controller = Object.FindFirstObjectByType<Sprint0.Multiplayer.MultiplayerGameController>();
            var data = new SerializedObject(controller);
            var screens = new[] { "mainScreen", "roomBrowserScreen", "lobbyScreen", "gameHudScreen", "settingsScreen", "serverClosedScreen" };
            var font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 24);
            var canvas = Object.FindFirstObjectByType<Canvas>();
            foreach (var text in canvas.GetComponentsInChildren<UnityEngine.UI.Text>(true)) text.font = font;
            var camera = Camera.main;
            camera.rect = new Rect(0, 0, 1, 1);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;

            ((UnityEngine.UI.Text)data.FindProperty("statusText").objectReferenceValue).transform.parent.gameObject.SetActive(false);
            foreach (var selected in new[] { "mainScreen", "roomBrowserScreen", "lobbyScreen", "settingsScreen" })
            {
                foreach (var name in screens) ((GameObject)data.FindProperty(name).objectReferenceValue).SetActive(name == selected);
                ((UnityEngine.UI.Text)data.FindProperty("lobbyPlayerCountText").objectReferenceValue).text = "현재 플레이어  1 / 2\n플레이어를 기다리는 중";
                Canvas.ForceUpdateCanvases();
                Capture(camera, "Logs/gravity-shared-" + selected + ".png");
            }
            Validate(scene, Object.FindFirstObjectByType<GravityGame>());
            Object.DestroyImmediate(font);
        }

        public static void BuildSharedInterfaceValidation()
        {
            BuildPlayer();
            CaptureSharedInterface();
        }

        static void Capture(Camera camera, string path)
        {
            foreach (var landmark in Object.FindObjectsByType<GravityLandmark>(FindObjectsSortMode.None))
                landmark.FaceCamera(camera);
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
