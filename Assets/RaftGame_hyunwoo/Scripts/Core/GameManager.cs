using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RaftSharkDive
{
    [DefaultExecutionOrder(-100)]
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Stage 0")]
        [SerializeField, Min(1f)] private float islandLootDuration = 60f;
        [SerializeField] private Transform islandRoot;
        [SerializeField, Min(0.1f)] private float islandSinkDistance = 5f;
        [SerializeField] private AnimationCurve islandSinkCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.5f, 0.2f),
            new Keyframe(0.833f, 0.65f),
            new Keyframe(1f, 1f));
        [SerializeField, Min(0.1f)] private float postSubmergeSinkSpeed = 8f;
        [SerializeField, Min(5f)] private float islandDisableDepth = 28f;
        [SerializeField, Min(1)] private int stage0WoodRequired = 5;
        [SerializeField, Min(1)] private int stage0RopeRequired = 2;
        [SerializeField] private IslandSafeZone islandSafeZone;
        [Header("World")]
        [SerializeField] private Transform raftSpawn;
        [SerializeField] private Transform waterSurface;
        [Header("Ending")]
        [SerializeField, Min(0.5f)] private float drainDuration = 4f;
        [SerializeField, Min(1f)] private float drainDistance = 18f;
        [SerializeField, Min(20f)] private float fortressAheadDistance = 100f;
        [SerializeField, Min(2f)] private float fortressDepthBelowSurface = 11f;

        private PlayerController player;
        private PlayerWaterDetector playerWaterDetector;
        private float lootTimeRemaining;
        private bool endingStarted;
        private bool islandFullySubmerged;
        private bool raftBuilt;
        private bool sharkThreatEnabled;
        private bool islandDescentComplete;
        private bool islandCheckpointSaved;
        private Vector3 islandStartPosition;
        private GameObject fortressRoot;

        public GameStage Stage { get; private set; } = GameStage.IslandLoot;
        public float LootTimeRemaining => lootTimeRemaining;
        public float WaterSurfaceY
        {
            get
            {
                if (waterSurface == null) return 0f;
                Renderer surfaceRenderer = waterSurface.GetComponent<Renderer>();
                return surfaceRenderer != null ? surfaceRenderer.bounds.max.y : waterSurface.position.y;
            }
        }
        public Transform RaftSpawn => raftSpawn;
        public int Stage0WoodRequired => stage0WoodRequired;
        public int Stage0RopeRequired => stage0RopeRequired;
        public bool IslandFullySubmerged => islandFullySubmerged;
        public bool RaftBuilt => raftBuilt;
        public bool SharkThreatEnabled => sharkThreatEnabled;
        public bool IslandDescentComplete => islandDescentComplete;
        public bool FortressVisible => fortressRoot != null && fortressRoot.activeInHierarchy;
        public bool EndingStarted => endingStarted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            lootTimeRemaining = islandLootDuration;
            if (islandRoot != null) islandStartPosition = islandRoot.position;
            fortressRoot = FindSceneObject("UnderwaterFortress");
            if (fortressRoot != null) fortressRoot.SetActive(false);
            EnsureIslandPalmGameplay();
            EnsurePostIslandRaftSystems();
        }

        private void Start()
        {
            player = FindFirstObjectByType<PlayerController>();
            if (player != null && player.GetComponent<RescueRopeSystem>() == null)
                player.gameObject.AddComponent<RescueRopeSystem>();
            if (player != null && player.GetComponent<HeldItemController>() == null)
                player.gameObject.AddComponent<HeldItemController>();
            playerWaterDetector = player != null ? player.GetComponent<PlayerWaterDetector>() : null;
            if (playerWaterDetector != null)
            {
                playerWaterDetector.WaterStateChanged += HandlePlayerWaterStateChanged;
                HandlePlayerWaterStateChanged(playerWaterDetector.IsInWater);
            }
            GameUI.Instance?.ShowNotice("Collect Wood x5 and Rope x2 before the island sinks.", 4f);
        }

        private void OnDestroy()
        {
            if (playerWaterDetector != null)
                playerWaterDetector.WaterStateChanged -= HandlePlayerWaterStateChanged;
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (islandFullySubmerged)
            {
                UpdatePostSubmergeDescent();
                return;
            }
            lootTimeRemaining = Mathf.Max(0f, lootTimeRemaining - Time.deltaTime);
            float progress = 1f - lootTimeRemaining / islandLootDuration;
            if (islandRoot != null)
                islandRoot.position = islandStartPosition + Vector3.down * (islandSinkDistance * islandSinkCurve.Evaluate(progress));
            if (lootTimeRemaining <= 0f) BeginRaftSurvival();
        }

        public void BeginRaftSurvival()
        {
            if (islandFullySubmerged) return;
            islandFullySubmerged = true;
            if (Stage == GameStage.IslandLoot) Stage = GameStage.RaftSurvival;
            if (islandRoot != null) islandRoot.position = islandStartPosition + Vector3.down * islandSinkDistance;
            islandSafeZone?.DisablePermanently();
            GameUI.Instance?.ShowNotice("The island is submerged and sinking into the deep.", 5f);
        }

        private void UpdatePostSubmergeDescent()
        {
            if (islandDescentComplete || islandRoot == null || !islandRoot.gameObject.activeSelf) return;
            islandRoot.position += Vector3.down * (postSubmergeSinkSpeed * Time.deltaTime);
            float targetY = islandStartPosition.y - islandSinkDistance - islandDisableDepth;
            if (islandRoot.position.y > targetY) return;
            islandDescentComplete = true;
            sharkThreatEnabled = true;
            islandRoot.gameObject.SetActive(false);
            if (!islandCheckpointSaved)
            {
                islandCheckpointSaved = true;
                SaveSystem.Instance?.SaveNow(true);
            }
        }

        public void NotifyRaftBuilt()
        {
            if (raftBuilt) return;
            raftBuilt = true;
            GameUI.Instance?.ShowNotice("Raft built. Leave the island when ready.", 4f);
        }

        public void PlayerLeftIsland()
        {
            if (Stage == GameStage.IslandLoot) Stage = GameStage.RaftSurvival;
        }

        private void HandlePlayerWaterStateChanged(bool inWater)
        {
            if (islandDescentComplete)
            {
                sharkThreatEnabled = true;
                return;
            }
            if (sharkThreatEnabled == inWater) return;
            sharkThreatEnabled = inWater;
            if (inWater)
            {
                if (Stage == GameStage.IslandLoot) Stage = GameStage.RaftSurvival;
                GameUI.Instance?.ShowNotice("Water contact! Shark pursuit is active!", 3f);
            }
            else
            {
                GameUI.Instance?.ShowNotice("Out of the water. Shark returned to patrol.", 2.5f);
            }
        }

        public void OnOxygenTankCrafted()
        {
            if (RaftMultiplayerHooks.TryDiscoverFortress != null &&
                RaftMultiplayerHooks.TryDiscoverFortress(this))
            {
                return;
            }

            OnOxygenTankCraftedLocal();
        }

        public void OnOxygenTankCraftedLocal()
        {
            if (Stage < GameStage.DeepDive) Stage = GameStage.DeepDive;
            PositionFortressAheadOfRaft();
            SetFortressVisible(true);
            GameUI.Instance?.ShowNotice("Oxygen Tank crafted and equipped. Fortress detected ahead.", 5f);
        }

        public void ApplyFortressNetworkState(bool visible, Vector3 position, Quaternion rotation)
        {
            if (visible && Stage < GameStage.DeepDive) Stage = GameStage.DeepDive;
            if (fortressRoot == null) fortressRoot = FindSceneObject("UnderwaterFortress");
            if (fortressRoot == null) return;
            if (visible)
            {
                FortressLayoutRuntime.Ensure(fortressRoot);
                fortressRoot.transform.SetPositionAndRotation(position, rotation);
            }
            SetFortressVisible(visible);
        }

        public bool TryGetFortressTransform(out Vector3 position, out Quaternion rotation)
        {
            if (fortressRoot == null) fortressRoot = FindSceneObject("UnderwaterFortress");
            if (fortressRoot == null)
            {
                position = Vector3.zero;
                rotation = Quaternion.identity;
                return false;
            }
            position = fortressRoot.transform.position;
            rotation = fortressRoot.transform.rotation;
            return true;
        }

        public void EnterFortress()
        {
            if (Stage < GameStage.Fortress) Stage = GameStage.Fortress;
            GameUI.Instance?.ShowNotice("Find the orange ocean plug inside. Aim at it and press E to pull it.", 6f);
        }

        public void RestoreProgress(GameStage savedStage, bool savedRaftBuilt, bool islandGone, float savedLootTimeRemaining)
        {
            Stage = savedStage;
            lootTimeRemaining = Mathf.Clamp(savedLootTimeRemaining, 0f, islandLootDuration);
            raftBuilt = savedRaftBuilt;
            islandFullySubmerged = islandGone;
            islandDescentComplete = islandGone;
            sharkThreatEnabled = islandGone;
            islandCheckpointSaved = islandGone;
            if (savedStage >= GameStage.DeepDive) PositionFortressAheadOfRaft();
            SetFortressVisible(savedStage >= GameStage.DeepDive);
            if (islandRoot != null)
            {
                islandRoot.gameObject.SetActive(!islandGone);
                if (!islandGone)
                {
                    float progress = 1f - lootTimeRemaining / islandLootDuration;
                    islandRoot.position = islandStartPosition + Vector3.down *
                        (islandSinkDistance * islandSinkCurve.Evaluate(progress));
                }
            }
            RaftUpgradeStation station = FindFirstObjectByType<RaftUpgradeStation>(FindObjectsInactive.Include);
            if (station != null) station.gameObject.SetActive(savedRaftBuilt);
            RaftBuildPoint buildPoint = FindFirstObjectByType<RaftBuildPoint>(FindObjectsInactive.Include);
            buildPoint?.ApplyBuiltState(savedRaftBuilt);
            if (islandGone) islandSafeZone?.DisablePermanently();
        }

        public void Respawn(PlayerController target)
        {
            if (target == null) return;
            if (raftBuilt && raftSpawn != null)
                target.Teleport(raftSpawn.position, raftSpawn.rotation);
            else if (islandRoot != null && !islandFullySubmerged)
                target.Teleport(islandRoot.position + Vector3.up * 1.2f, Quaternion.identity);
            else
                return;
            target.GetComponent<PlayerHealth>()?.RestoreFull();
            target.GetComponent<PlayerOxygen>()?.RestoreFull();
            GameUI.Instance?.ShowNotice(raftBuilt ? "Respawned on the raft." : "Respawned on the island.", 3f);
        }

        public void CompleteEnding()
        {
            CompleteEndingLocal();
        }

        public void CompleteEndingLocal()
        {
            if (endingStarted) return;
            endingStarted = true;
            Stage = GameStage.Ending;
            StartCoroutine(DrainRoutine());
        }

        private IEnumerator DrainRoutine()
        {
            if (player != null) player.SetInputEnabled(false);
            Vector3 start = waterSurface != null ? waterSurface.position : Vector3.zero;
            Vector3 end = start + Vector3.down * drainDistance;
            float elapsed = 0f;
            while (elapsed < drainDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / drainDuration);
                if (waterSurface != null) waterSurface.position = Vector3.Lerp(start, end, t);
                yield return null;
            }
            GameUI.Instance?.ShowEnding();
        }

        public void Restart()
        {
            if (RaftMultiplayerHooks.TryRestartGame != null &&
                RaftMultiplayerHooks.TryRestartGame(this))
            {
                return;
            }
            RestartLocal();
        }

        public void RestartLocal() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        private void EnsurePostIslandRaftSystems()
        {
            RaftUpgradeStation raft = FindFirstObjectByType<RaftUpgradeStation>(FindObjectsInactive.Include);
            if (raft == null) return;
            if (raft.GetComponent<RaftAutoMover>() == null) raft.gameObject.AddComponent<RaftAutoMover>();
            EndlessOceanItemSpawner spawner = GetComponent<EndlessOceanItemSpawner>();
            if (spawner == null) spawner = gameObject.AddComponent<EndlessOceanItemSpawner>();
            spawner.Configure(raft.transform);
        }

        private void EnsureIslandPalmGameplay()
        {
            if (islandRoot == null) return;
            foreach (Transform child in islandRoot)
            {
                bool isPalmTree = child.name.StartsWith("palm tree_");
                bool isPalm = !isPalmTree && child.name.StartsWith("palm_");
                if (!isPalmTree && !isPalm) continue;

                EnsureVegetationCollider(child.gameObject, isPalmTree);
                if (!isPalm) continue;
                PickupItem pickup = child.GetComponent<PickupItem>();
                if (pickup == null) pickup = child.gameObject.AddComponent<PickupItem>();
                pickup.Configure(ItemId.Palm, 1);
                pickup.ConfigureUnderwater();
            }
        }

        private static void EnsureVegetationCollider(GameObject vegetation, bool isTree)
        {
            CapsuleCollider capsule = vegetation.GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                capsule.isTrigger = false;
                return;
            }

            Renderer[] renderers = vegetation.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            Vector3 scale = vegetation.transform.lossyScale;
            Vector3 localSize = new Vector3(
                bounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                bounds.size.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                bounds.size.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));

            capsule = vegetation.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.isTrigger = false;
            capsule.center = vegetation.transform.InverseTransformPoint(bounds.center);
            capsule.radius = isTree
                ? Mathf.Clamp(Mathf.Min(localSize.x, localSize.z) * 0.4f, 0.35f, 0.75f)
                : Mathf.Clamp(Mathf.Max(localSize.x, localSize.z) * 0.28f, 0.16f, 0.38f);
            capsule.height = Mathf.Max(capsule.radius * 2f, localSize.y * (isTree ? 0.9f : 0.82f));
        }

        private static GameObject FindSceneObject(string objectName)
        {
            foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                if (candidate.name == objectName && candidate.gameObject.scene.IsValid()) return candidate.gameObject;
            return null;
        }

        private void SetFortressVisible(bool visible)
        {
            if (fortressRoot == null) fortressRoot = FindSceneObject("UnderwaterFortress");
            if (fortressRoot != null)
            {
                FortressLayoutRuntime.Ensure(fortressRoot);
                fortressRoot.SetActive(visible);
            }
        }

        private void PositionFortressAheadOfRaft()
        {
            if (fortressRoot == null) fortressRoot = FindSceneObject("UnderwaterFortress");
            if (fortressRoot == null) return;

            FortressLayoutRuntime.Ensure(fortressRoot);

            RaftUpgradeStation raft = FindFirstObjectByType<RaftUpgradeStation>(FindObjectsInactive.Include);
            Transform reference = raft != null ? raft.transform : player != null ? player.transform : null;
            if (reference == null) return;

            Vector3 forward = Vector3.ProjectOnPlane(reference.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            fortressRoot.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            Vector3 desiredCenter = reference.position + forward * fortressAheadDistance;
            desiredCenter.y = WaterSurfaceY - fortressDepthBelowSurface;

            Renderer[] renderers = fortressRoot.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = default;
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer candidate = renderers[i];
                if (!candidate.enabled || !FortressLayoutRuntime.IsActiveBelowRoot(candidate.transform,
                        fortressRoot.transform)) continue;
                if (!hasBounds)
                {
                    bounds = candidate.bounds;
                    hasBounds = true;
                }
                else bounds.Encapsulate(candidate.bounds);
            }
            if (!hasBounds)
            {
                fortressRoot.transform.position = desiredCenter;
                return;
            }
            fortressRoot.transform.position += desiredCenter - bounds.center;
        }
    }

    internal sealed class FortressLayoutRuntime : MonoBehaviour
    {
        private const string GeometryName = "MvpFortressGeometry";
        private bool built;

        public static void Ensure(GameObject fortressRoot)
        {
            if (fortressRoot == null) return;
            FortressLayoutRuntime layout = fortressRoot.GetComponent<FortressLayoutRuntime>();
            if (layout == null) layout = fortressRoot.AddComponent<FortressLayoutRuntime>();
            layout.BuildIfNeeded();
        }

        public static bool IsActiveBelowRoot(Transform candidate, Transform root)
        {
            for (Transform current = candidate; current != null && current != root; current = current.parent)
                if (!current.gameObject.activeSelf) return false;
            return true;
        }

        private void BuildIfNeeded()
        {
            if (built || transform.Find(GeometryName) != null)
            {
                built = true;
                return;
            }

            FortressDoor door = GetComponentInChildren<FortressDoor>(true);
            EndingDrain drain = GetComponentInChildren<EndingDrain>(true);
            Material structureMaterial = FindMaterial("Wall") ?? FindMaterial("Floor");
            Material accentMaterial = FindMaterial("Beacon") ?? structureMaterial;

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if ((door != null && child == door.transform) || (drain != null && child == drain.transform)) continue;
                child.gameObject.SetActive(false);
            }

            Transform geometry = new GameObject(GeometryName).transform;
            geometry.SetParent(transform, false);

            CreateCube(geometry, "Floor", new Vector3(0f, -2.75f, 0f), new Vector3(14f, 0.75f, 24f), structureMaterial, true);
            CreateCube(geometry, "Roof", new Vector3(0f, 2.75f, 0f), new Vector3(14f, 0.55f, 24f), structureMaterial, true);
            CreateCube(geometry, "LeftWall", new Vector3(-6.65f, 0f, 0f), new Vector3(0.7f, 5.5f, 24f), structureMaterial, true);
            CreateCube(geometry, "RightWall", new Vector3(6.65f, 0f, 0f), new Vector3(0.7f, 5.5f, 24f), structureMaterial, true);
            CreateCube(geometry, "BackWall", new Vector3(0f, 0f, 11.65f), new Vector3(14f, 5.5f, 0.7f), structureMaterial, true);
            CreateCube(geometry, "FrontWallLeft", new Vector3(-4.65f, 0f, -11.65f), new Vector3(4.7f, 5.5f, 0.7f), structureMaterial, true);
            CreateCube(geometry, "FrontWallRight", new Vector3(4.65f, 0f, -11.65f), new Vector3(4.7f, 5.5f, 0.7f), structureMaterial, true);
            CreateCube(geometry, "DoorLintel", new Vector3(0f, 2.25f, -11.65f), new Vector3(4.6f, 1f, 0.7f), structureMaterial, true);

            CreateCylinder(geometry, "LeftTower", new Vector3(-5.1f, 0.25f, 8.8f), new Vector3(1.25f, 3.1f, 1.25f), structureMaterial, true);
            CreateCylinder(geometry, "RightTower", new Vector3(5.1f, 0.25f, 8.8f), new Vector3(1.25f, 3.1f, 1.25f), structureMaterial, true);
            CreateCylinder(geometry, "Beacon", new Vector3(0f, 5f, 3f), new Vector3(0.35f, 2.2f, 0.35f), accentMaterial, false);
            CreateCube(geometry, "FrontMarker", new Vector3(0f, 1.35f, -12.02f), new Vector3(3.8f, 0.18f, 0.12f), accentMaterial, false);

            if (door != null)
            {
                door.transform.SetParent(transform, false);
                door.transform.localPosition = new Vector3(0f, -0.2f, -12.02f);
                door.transform.localRotation = Quaternion.identity;
                door.transform.localScale = Vector3.one;
                door.gameObject.SetActive(true);
            }
            if (drain != null)
            {
                drain.transform.SetParent(transform, false);
                drain.transform.localPosition = new Vector3(0f, -2.25f, 4f);
                drain.transform.localRotation = Quaternion.identity;
                drain.transform.localScale = Vector3.one;
                drain.gameObject.SetActive(true);
            }

            built = true;
        }

        private Material FindMaterial(string namePart)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i].name.IndexOf(namePart, System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                    renderers[i].sharedMaterial != null)
                    return renderers[i].sharedMaterial;
            return renderers.Length > 0 ? renderers[0].sharedMaterial : null;
        }

        private static void CreateCube(Transform parent, string partName, Vector3 position, Vector3 scale,
            Material material, bool collider)
        {
            CreatePrimitive(parent, partName, PrimitiveType.Cube, position, scale, material, collider);
        }

        private static void CreateCylinder(Transform parent, string partName, Vector3 position, Vector3 scale,
            Material material, bool collider)
        {
            CreatePrimitive(parent, partName, PrimitiveType.Cylinder, position, scale, material, collider);
        }

        private static void CreatePrimitive(Transform parent, string partName, PrimitiveType type,
            Vector3 position, Vector3 scale, Material material, bool keepCollider)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = scale;
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null && material != null) renderer.sharedMaterial = material;
            if (!keepCollider)
            {
                Collider primitiveCollider = part.GetComponent<Collider>();
                if (primitiveCollider != null) Destroy(primitiveCollider);
            }
        }
    }
}
