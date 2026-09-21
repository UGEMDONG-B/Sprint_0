using Sprint0.GravityCoop;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sprint0.Editor
{
    public static partial class GravityPrototypeBuilder
    {
        static void BuildExpansion(GravityPuzzle puzzle, Color wall, out Vector3 exitPosition, out Vector3 exitSize)
        {
            var parent = puzzle.transform;
            int index = puzzle.index;
            exitPosition = new Vector3(11.5f, 6, 1.8f);
            exitSize = new Vector3(1.2f, 2.4f, 1.4f);
            Label(parent, (index + 1) + " / " + new[] { "HANDOFF", "ROTATE", "ANCHOR", "DELIVERY TEAM" }[index - 8], new Vector3(-6, 12, -2.8f));
            if (index == 8)
            {
                puzzle.spawn.localPosition = new Vector3(-9.5f, 0.7f, -1.6f);
                puzzle.boxes = new[] { Box(parent, index, new Vector3(-8, 0.6f, -1.6f)) };
                Partition(parent, true, wall);
                puzzle.plates = new[] { Delivery(parent, 15, 1.8f) };
                PaintArea(parent, "A / RECEIVE", new Vector3(-6, 0.02f, -1.6f), new Vector3(3.2f, 0.025f, 2.2f));
                PaintArea(parent, "A / REAR", new Vector3(-6, 0.02f, 1.6f), new Vector3(3.2f, 0.025f, 2.2f));
                Label(parent, "B / RECEIVE", new Vector3(2, 14, -2.5f));
            }
            else if (index == 9)
            {
                puzzle.spawn.localPosition = new Vector3(-9, 0.7f, 1.8f);
                puzzle.boxes = new[] { Box(parent, index, new Vector3(-6, 0.6f, -1.5f)) };
                puzzle.rotors = new[] {
                    Rotor(puzzle, "R1", new Vector3(-6, 6, -1.5f), 0, new Vector3(-9, 15.1f, 1.8f)),
                    Rotor(puzzle, "R2", new Vector3(4, 6, -1.5f), 180, new Vector3(1, 15.1f, 1.8f))
                };
                // Broad intermediate shelf catches failed transfers. The rear lane is a service path.
                SplitShelf(parent, "Intake", 4, -6, 2.4f, wall);
                SplitShelf(parent, "Delivery", 9, 4, 2.4f, wall);
                puzzle.plates = new[] { Delivery(parent, 15, -1.5f) };
                Label(parent, "FRONT / CRATES", new Vector3(-2, 2, -2.5f));
                Label(parent, "REAR / HANDLES", new Vector3(-2, 14, 1.8f));
            }
            else
            {
                puzzle.spawn.localPosition = new Vector3(-10.2f, 0.7f, -1.6f);
                puzzle.boxes = new[] { Box(parent, index, new Vector3(-9, 0.6f, -1.6f)), Box(parent, index, new Vector3(-2, 0.6f, -1.6f)) };
                float px = -8.2f;
                var p = Volume(parent, "P anchor plate", new Vector3(px, 15.65f, -1.6f), new Vector3(2.8f, 0.7f, 2.4f), Color.yellow);
                puzzle.clamps = new[] { Clamp(puzzle, new Vector3(px, 15, -1.6f), new Vector3(-10.3f, 15.1f, -1.6f)) };
                PaintArea(parent, "P / LOAD", new Vector3(px, 0.02f, -1.6f), new Vector3(2.8f, 0.025f, 2.4f));
                if (index == 10)
                {
                    puzzle.plates = new[] { p, Delivery(parent, 15, 1.7f) };
                    PaintArea(parent, "Q / LOAD", new Vector3(2, 0.02f, 1.7f), new Vector3(4, 0.025f, 2.2f));
                }
                else
                {
                    exitPosition.z = -1.8f;
                    Partition(parent, false, wall);
                    puzzle.transferGate = Block(parent, "P opens handoff A", new Vector3(-6, 1.5f, 0), new Vector3(4, 3, 0.3f), new Color(0.25f, 0.4f, 0.8f)).GetComponent<BoxCollider>();
                    Hint(puzzle.transferGate, "P 연결 인계문", "P 압력판이 눌리면 열립니다. P 상자를 고정하면 중력을 바꿔도 통로를 유지할 수 있습니다.");
                    puzzle.rotors = new[] { Rotor(puzzle, "R1", new Vector3(4, 10, 1.5f), 0, new Vector3(-1, 15.1f, -1.6f)) };
                    puzzle.plates = new[] { p, Delivery(parent, 11.3f, 1.7f) };
                    puzzle.clampCheckpoint = true;
                    puzzle.checkpointLabel = Label(parent, "LOCK P / CHECKPOINT", new Vector3(-7, 11, -2.7f));
                    PaintArea(parent, "R1 / LOAD", new Vector3(4, 0.02f, 1.6f), new Vector3(3.2f, 0.025f, 2.2f));
                }
            }
        }

        static BoxCollider Delivery(Transform parent, float y, float z)
        {
            var plate = Volume(parent, "Q delivery plate", new Vector3(11.65f, y, z), new Vector3(0.7f, 2, 2.3f), Color.yellow);
            Label(parent, "Q / WEIGHT", new Vector3(10.6f, y - 0.4f, z - 0.5f));
            return plate;
        }

        static void PaintArea(Transform parent, string name, Vector3 position, Vector3 size)
        {
            var area = Block(parent, name, position, size, new Color(0.15f, 0.65f, 0.95f));
            Object.DestroyImmediate(area.GetComponent<Collider>());
            Label(parent, name, position + new Vector3(0, position.z > 0 ? 3.3f : 2.3f, -0.3f));
        }

        static void SplitShelf(Transform parent, string id, float y, float opening, float halfWidth, Color wall)
        {
            float left = opening - halfWidth + 12;
            float right = 12 - opening - halfWidth;
            Block(parent, id + " shelf left", new Vector3(-12 + left / 2, y, -1.5f), new Vector3(left, 0.3f, 3), wall);
            Block(parent, id + " shelf right", new Vector3(opening + halfWidth + right / 2, y, -1.5f), new Vector3(right, 0.3f, 3), wall);
        }

        static void Partition(Transform parent, bool upperWindow, Color wall)
        {
            // Transparent collision panes, visibly outlined; both roles can see the other lane.
            float[] xs = { -12, -8, -4, 0, 4, 12 };
            float[] ys = { 0, 3, 13, 16 };
            for (int x = 0; x < xs.Length - 1; x++)
                for (int y = 0; y < ys.Length - 1; y++)
                {
                    if (x == 1 && y == 0 || upperWindow && x == 3 && y == 2) continue;
                    var size = new Vector3(xs[x + 1] - xs[x], ys[y + 1] - ys[y], 0.25f);
                    var position = new Vector3((xs[x] + xs[x + 1]) / 2, (ys[y] + ys[y + 1]) / 2, 0);
                    var pane = Block(parent, "Clear lane divider", position, size, wall);
                    pane.GetComponent<Renderer>().sharedMaterial = PartitionGlass();
                    Hint(pane.GetComponent<Collider>(), "앞뒤 격벽", "테두리 안의 투명 벽입니다. A 또는 B 창구에서만 상자를 앞뒤로 옮길 수 있습니다.");
                    Outline(parent, "Divider frame", position, size.x, size.y, new Color(0.15f, 0.65f, 0.95f));
                }
            Outline(parent, "A window", new Vector3(-6, 1.5f, -0.18f), 4, 3, Color.green);
            Label(parent, "A / PASS", new Vector3(-6, 4.5f, -0.5f));
            if (upperWindow) Outline(parent, "B window", new Vector3(2, 14.5f, -0.18f), 4, 3, Color.green);
            if (upperWindow) Label(parent, "B / PASS", new Vector3(2, 12.4f, -0.5f));
            Label(parent, "FRONT", new Vector3(7, 1.2f, -2.5f));
            Label(parent, "REAR", new Vector3(7, 1.2f, 2.5f));
        }

        static Material PartitionGlass()
        {
            const string path = Materials + "/PartitionGlass.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", new Color(0.1f, 0.55f, 0.8f, 0.08f));
            material.SetFloat("_Surface", 1);
            material.SetFloat("_Blend", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void Outline(Transform parent, string name, Vector3 position, float width, float height, Color color)
        {
            foreach (float sign in new[] { -1f, 1f })
            {
                var horizontal = Block(parent, name, position + Vector3.up * height * sign / 2, new Vector3(width, 0.05f, 0.05f), color);
                var vertical = Block(parent, name, position + Vector3.right * width * sign / 2, new Vector3(0.05f, height, 0.05f), color);
                Object.DestroyImmediate(horizontal.GetComponent<Collider>());
                Object.DestroyImmediate(vertical.GetComponent<Collider>());
            }
        }

        static GravityRotor Rotor(GravityPuzzle puzzle, string id, Vector3 position, float angle, Vector3 handlePosition)
        {
            var root = new GameObject(id + " rotary elbow");
            root.transform.SetParent(puzzle.transform, false);
            root.transform.localPosition = position;
            root.AddComponent<NetworkObject>();
            var rotor = root.AddComponent<GravityRotor>();
            rotor.puzzle = puzzle;
            rotor.id = id;
            rotor.initialAngle = angle;
            rotor.elbow = new GameObject("Physical elbow").transform;
            rotor.elbow.SetParent(root.transform, false);
            var color = new Color(0.8f, 0.4f, 0.95f);
            rotor.walls = new[] {
                Block(rotor.elbow, "Elbow side", new Vector3(-2, 0, 0), new Vector3(0.3f, 4.3f, 3), color).GetComponent<BoxCollider>(),
                Block(rotor.elbow, "Elbow base", new Vector3(0, -2, 0), new Vector3(4.3f, 0.3f, 3), color).GetComponent<BoxCollider>()
            };
            rotor.elbow.localRotation = Quaternion.Euler(0, 0, angle);
            var interior = new GameObject("Occupied region");
            interior.transform.SetParent(root.transform, false);
            rotor.interior = interior.AddComponent<BoxCollider>();
            rotor.interior.isTrigger = true;
            rotor.interior.size = new Vector3(3.6f, 3.6f, 3);
            rotor.handle = Volume(puzzle.transform, id + " handle", handlePosition, new Vector3(0.65f, 0.65f, 0.65f), color);
            rotor.status = Label(puzzle.transform, id + " / A\nE / TURN", handlePosition + new Vector3(0, -1, -0.5f));
            Label(puzzle.transform, id, position + new Vector3(0, 0, -1.7f));
            Hint(rotor.handle, id + " 회전 손잡이", "E로 통로를 90도 돌립니다. 통로 안에 사람이나 상자가 있거나 새 벽이 물체와 겹치면 돌리지 않습니다.");
            return rotor;
        }

        static GravityClamp Clamp(GravityPuzzle puzzle, Vector3 position, Vector3 handlePosition)
        {
            var root = new GameObject("P clamp");
            root.transform.SetParent(puzzle.transform, false);
            root.AddComponent<NetworkObject>();
            var clamp = root.AddComponent<GravityClamp>();
            clamp.puzzle = puzzle;
            var socket = new GameObject("Clamp region");
            socket.transform.SetParent(root.transform, false);
            socket.transform.localPosition = position;
            clamp.socket = socket.AddComponent<BoxCollider>();
            clamp.socket.isTrigger = true;
            clamp.socket.size = new Vector3(3, 2.1f, 2.7f);
            Outline(root.transform, "Clamp outline", position + Vector3.back * 1.4f, 3, 2, Color.yellow);
            clamp.handle = Volume(puzzle.transform, "P clamp handle", handlePosition, Vector3.one * 0.65f, new Color(1, 0.55f, 0.12f));
            clamp.status = Label(puzzle.transform, "P / FREE\nE / LOCK", handlePosition + new Vector3(0, -1.1f, -0.5f));
            Hint(clamp.handle, "P 상자 고정 장치", "상자를 P 안에 완전히 놓고 멈춘 뒤 E로 고정합니다. 다시 E를 누르면 해제합니다. 운반 중인 상자는 고정할 수 없습니다.");
            return clamp;
        }
    }
}
