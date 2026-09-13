using Unity.Netcode;
using UnityEngine;

namespace Sprint0.GravityCoop
{
    public sealed class GravityPuzzle : NetworkBehaviour
    {
        public int index;
        public string title;
        [TextArea] public string objective;
        public Transform spawn;
        public GravityBody[] boxes;
        public BoxCollider[] plates;
        public BoxCollider exit;
        public Collider door;
        public Transform lever;
        [Header("Route selector: OFF opens A, ON opens B")]
        public BoxCollider routeA;
        public BoxCollider routeB;
        public bool requireLeverForExit;
        public BoxCollider[] hazards;
        [Min(0)] public float laserPeriod = 5f;
        [Min(0)] public float laserOnTime = 2.6f;
        public bool timedHazards;
        public bool boxesTriggerHazards;
        public NetworkVariable<int> Pressed = new();
        public NetworkVariable<bool> LeverOn = new();
        public NetworkVariable<bool> DoorOpen = new();
        public NetworkVariable<bool> LaserOn = new();
        public NetworkVariable<bool> RouteBlocked = new();
        public bool ConditionsMet => Pressed.Value == (1 << plates.Length) - 1 && (!requireLeverForExit || LeverOn.Value);

        public bool TryToggleRoute()
        {
            if (!IsServer) return false;
            var closing = LeverOn.Value ? routeB : routeA;
            RouteBlocked.Value = closing != null && Occupied(closing);
            if (RouteBlocked.Value) return false;
            LeverOn.Value = !LeverOn.Value;
            ApplyRoutes();
            return true;
        }

        bool Occupied(BoxCollider volume)
        {
            // Query the volume even while its collider is disabled. Ignore adjoining static walls.
            foreach (var col in Physics.OverlapBox(volume.transform.TransformPoint(volume.center),
                Vector3.Scale(volume.size, volume.transform.lossyScale) * 0.5f - Vector3.one * 0.02f,
                volume.transform.rotation, ~0, QueryTriggerInteraction.Ignore))
                if (col.attachedRigidbody != null) return true;
            return false;
        }

        void ApplyRoutes()
        {
            SetGate(routeA, !LeverOn.Value);
            SetGate(routeB, LeverOn.Value);
        }

        static void SetGate(Collider gate, bool open)
        {
            if (gate == null) return;
            gate.enabled = !open;
            gate.GetComponent<Renderer>().enabled = !open;
        }

        public void Restore()
        {
            foreach (var box in boxes) box.Restore();
            Pressed.Value = 0;
            LeverOn.Value = false;
            DoorOpen.Value = false;
            RouteBlocked.Value = false;
            ApplyRoutes();
        }

        public static bool ContainsBody(BoxCollider volume, Rigidbody body)
        {
            foreach (var col in Physics.OverlapBox(volume.transform.TransformPoint(volume.center),
                Vector3.Scale(volume.size, volume.transform.lossyScale) * 0.5f, volume.transform.rotation, ~0, QueryTriggerInteraction.Ignore))
                if (col.attachedRigidbody == body) return true;
            return false;
        }

        void FixedUpdate()
        {
            var game = GravityGame.Instance;
            if (!IsServer || game == null || !game.Playing || game.Puzzle.Value != index) return;
            if (RouteBlocked.Value)
            {
                var closing = LeverOn.Value ? routeB : routeA;
                RouteBlocked.Value = closing != null && Occupied(closing);
            }
            int pressed = 0;
            for (int i = 0; i < plates.Length; i++)
            {
                bool occupied = ContainsBody(plates[i], game.runner.Body);
                foreach (var box in boxes)
                    if (!game.IsHeld(box) && ContainsBody(plates[i], box.Body)) occupied = true;
                if (occupied) pressed |= 1 << i;
            }
            Pressed.Value = pressed;
            DoorOpen.Value = ConditionsMet;
            LaserOn.Value = hazards.Length > 0 && (!timedHazards || (game.NetworkManager.ServerTime.Time - game.SectionStarted.Value) % Mathf.Max(0.1f, laserPeriod) < laserOnTime);
            if (LaserOn.Value)
                foreach (var hazard in hazards)
                {
                    if (ContainsBody(hazard, game.runner.Body)) { game.Die(); return; }
                    if (boxesTriggerHazards)
                        foreach (var box in boxes)
                            if (ContainsBody(hazard, box.Body)) { game.Die(); return; }
                }
            if (ConditionsMet && ContainsBody(exit, game.runner.Body)) game.CompletePuzzle();
        }

        void Update()
        {
            ApplyRoutes();
            if (door != null)
            {
                door.enabled = !DoorOpen.Value;
                door.GetComponent<Renderer>().enabled = !DoorOpen.Value;
            }
            for (int i = 0; i < plates.Length; i++)
                Paint(plates[i].GetComponent<Renderer>(), (Pressed.Value & (1 << i)) != 0 ? Color.green : Color.yellow);
            if (lever != null) Paint(lever.GetComponent<Renderer>(), LeverOn.Value ? Color.green : Color.magenta);
            foreach (var hazard in hazards) hazard.GetComponent<Renderer>().enabled = LaserOn.Value;
        }

        static void Paint(Renderer renderer, Color color)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
        }
    }
}
