#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using RaftSharkDive;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class RaftSharkDiveMvpBuilder
{
    private const string GeneratedRoot = "Assets/Generated";
    private const string PrefabRoot = "Assets/Prefabs";
    private static readonly Vector2[] Stage0WoodLocalPositions =
    {
        new Vector2(-30f, -25f), new Vector2(30f, -22f), new Vector2(-28f, 12f),
        new Vector2(26f, 17f), new Vector2(8f, 36f)
    };
    private static readonly Vector2[] Stage0RopeLocalPositions =
    {
        new Vector2(-15f, 30f), new Vector2(15f, -5f)
    };

    [MenuItem("Tools/Raft Shark Dive/Build Complete MVP")]
    public static void BuildCompleteMvp()
    {
        EnsureFolders();

        Material sand = CreateMaterial("Sand", new Color(0.72f, 0.57f, 0.31f));
        Material grassMaterial = CreateGrassIndirectMaterial();
        Material wood = CreateMaterial("Wood", new Color(0.30f, 0.16f, 0.07f));
        Material pickupOutline = CreateOutlineMaterial();
        Material pickupWoodBase = CreatePickupBaseMaterial("PickupWoodBase", new Color(0.34f, 0.17f, 0.065f), false);
        Material pickupRopeBase = CreatePickupBaseMaterial("PickupRopeBase", new Color(0.56f, 0.38f, 0.16f), true);
        Material water = CreateMaterial("Ocean", new Color(0.03f, 0.34f, 0.52f, 0.72f), true);
        Material stone = CreateMaterial("Stone", new Color(0.18f, 0.23f, 0.26f));
        Material metal = CreateMaterial("Metal", new Color(0.34f, 0.42f, 0.44f));
        Material accent = CreateMaterial("EndingAccent", new Color(0.85f, 0.35f, 0.08f));

        GameObject playerPrefab = BuildPlayerPrefab();
        GameObject raftPrefab = BuildRaftPrefab(pickupWoodBase);
        GameObject raftBuildPointPrefab = BuildRaftBuildPointPrefab(wood, accent);
        Dictionary<ItemId, GameObject> pickupPrefabs = BuildPickupPrefabs(pickupOutline, pickupWoodBase, pickupRopeBase);
        BuildOxygenTankPrefab();
        BuildCratePrefab();
        GameObject sharkPrefab = BuildSharkPrefab();
        GameObject doorPrefab = BuildDoorPrefab(metal);
        GameObject wallPrefab = BuildEnvironmentPrefab("FortressWall", "Assets/model/wall.fbx", new Vector3(3f, 3f, 0.5f), stone);
        GameObject floorPrefab = BuildEnvironmentPrefab("FortressFloor", "Assets/model/plane.fbx", new Vector3(4f, 0.25f, 4f), stone);
        GameObject drainPrefab = BuildDrainPrefab(metal, accent);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "Main";

        BuildLighting();
        GameObject ocean = CreatePrimitive("OceanSurface", PrimitiveType.Cube, new Vector3(0f, -0.1f, 180f), new Vector3(1500f, 0.2f, 1900f), water, false);
        BoxCollider waterTrigger = ocean.AddComponent<BoxCollider>();
        waterTrigger.isTrigger = true;
        waterTrigger.size = new Vector3(1f, 2000f, 1f);
        waterTrigger.center = new Vector3(0f, -999.5f, 0f);
        ocean.AddComponent<WaterVolume>();
        CreatePrimitive("SeaFloor", PrimitiveType.Cube, new Vector3(0f, -16f, 180f), new Vector3(1500f, 1f, 1900f), stone, true);

        Transform island = new GameObject("IslandRoot").transform;
        island.position = new Vector3(0f, 1.5f, -90f);
        GameObject islandPlaneRoot = new GameObject("PlaneGround");
        islandPlaneRoot.transform.SetParent(island);
        islandPlaneRoot.transform.localPosition = Vector3.zero;
        GameObject islandPlane = AddModelVisual(islandPlaneRoot.transform, "Assets/model/plane.fbx", 128f, false, Quaternion.identity);
        ApplyMaterialRecursively(islandPlane, sand);
        AddMeshColliders(islandPlane);
        Physics.SyncTransforms();
        AddIslandDecor(island, grassMaterial);
        NavMeshSurface navMeshSurface = island.gameObject.AddComponent<NavMeshSurface>();
        navMeshSurface.collectObjects = CollectObjects.Children;
        navMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

        Bounds islandPlaneBounds = CalculateBounds(islandPlane);
        float islandMidHeight = (islandPlaneBounds.min.y + islandPlaneBounds.max.y) * 0.5f;
        float startingWaterSurfaceY = Mathf.Min(-1.25f, islandMidHeight - 0.1f);
        ocean.transform.position = new Vector3(ocean.transform.position.x,
            startingWaterSurfaceY - ocean.transform.lossyScale.y * 0.5f, ocean.transform.position.z);

        GameObject raft = (GameObject)PrefabUtility.InstantiatePrefab(raftPrefab);
        raft.name = "Raft";
        raft.transform.position = new Vector3(0f, startingWaterSurfaceY + 0.35f, -18f);
        Transform raftSpawn = new GameObject("RaftSpawn").transform;
        raftSpawn.SetParent(raft.transform);
        raftSpawn.localPosition = new Vector3(0f, 1.2f, 0f);
        raft.SetActive(false);

        GameObject buildPointObject = (GameObject)PrefabUtility.InstantiatePrefab(raftBuildPointPrefab);
        buildPointObject.name = "RaftBuildPoint";
        buildPointObject.transform.position = new Vector3(0f, startingWaterSurfaceY + 0.3f, -25f);
        RaftBuildPoint buildPoint = buildPointObject.GetComponent<RaftBuildPoint>();
        buildPoint.Configure(raft);

        GameObject safeZoneObject = new GameObject("IslandSafeZone");
        safeZoneObject.transform.position = new Vector3(0f, 2f, -90f);
        SphereCollider safeZoneCollider = safeZoneObject.AddComponent<SphereCollider>();
        safeZoneCollider.radius = 66f;
        safeZoneCollider.isTrigger = true;
        IslandSafeZone safeZone = safeZoneObject.AddComponent<IslandSafeZone>();

        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
        player.name = "Player";
        MeshCollider[] playerSpawnSurfaces = island.GetComponentsInChildren<MeshCollider>(true);
        Vector3 playerSpawnProbe = island.TransformPoint(new Vector3(0f, 50f, 0f));
        if (!TryGetIslandSurfacePoint(island, playerSpawnSurfaces, playerSpawnProbe, out Vector3 playerSurface,
                out Vector3 playerSurfaceNormal) || Vector3.Dot(playerSurfaceNormal, Vector3.up) < 0.82f)
            throw new System.InvalidOperationException("A walkable island surface was not found for the player spawn.");
        player.transform.SetPositionAndRotation(playerSurface + Vector3.up * 0.04f, Quaternion.identity);

        SpawnIslandLoot(pickupPrefabs, island, safeZoneCollider, startingWaterSurfaceY);
        SnapFloatingStage0ObjectsToSurface(island);
        navMeshSurface.BuildNavMesh();
        SpawnUnderwaterWoodField(pickupPrefabs[ItemId.Wood], 15);
        SpawnUnderwaterLoot(pickupPrefabs);

        BuildFortress(doorPrefab, wallPrefab, floorPrefab, drainPrefab);
        GameObject shark = (GameObject)PrefabUtility.InstantiatePrefab(sharkPrefab);
        shark.name = "Shark";
        shark.transform.position = new Vector3(7f, -6f, 65f);
        SerializedObject sharkSo = new SerializedObject(shark.GetComponent<SharkAI>());
        sharkSo.FindProperty("patrolRadius").floatValue = 55f;
        sharkSo.ApplyModifiedPropertiesWithoutUndo();

        GameObject systems = new GameObject("Systems");
        GameManager manager = systems.AddComponent<GameManager>();
        systems.AddComponent<GameUI>();
        systems.AddComponent<SaveSystem>();
        SerializedObject managerSo = new SerializedObject(manager);
        managerSo.FindProperty("islandRoot").objectReferenceValue = island;
        managerSo.FindProperty("islandSafeZone").objectReferenceValue = safeZone;
        managerSo.FindProperty("raftSpawn").objectReferenceValue = raftSpawn;
        managerSo.FindProperty("waterSurface").objectReferenceValue = ocean.transform;
        managerSo.ApplyModifiedPropertiesWithoutUndo();

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.38f, 0.55f, 0.65f);
        RenderSettings.ambientEquatorColor = new Color(0.13f, 0.28f, 0.33f);
        RenderSettings.ambientGroundColor = new Color(0.05f, 0.10f, 0.12f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.06f, 0.24f, 0.30f);
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 12f;
        RenderSettings.fogEndDistance = 240f;

        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Main.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Main.unity", true) };
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = player;
        Debug.Log("[Raft Shark Dive] MVP scene and prefabs built successfully.");
    }

    [MenuItem("Tools/Raft Shark Dive/Validate MVP")]
    public static void ValidateMvp()
    {
        const string scenePath = "Assets/Scenes/Main.unity";
        if (EditorUtility.scriptCompilationFailed)
            throw new System.InvalidOperationException("Unity reports script compilation errors.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            throw new System.InvalidOperationException("Main scene is missing.");

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        int missingScripts = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
                if (component == null) missingScripts++;
        }
        if (missingScripts > 0)
            throw new System.InvalidOperationException($"Main scene contains {missingScripts} missing script reference(s).");

        string[] requiredObjects = { "Player", "Raft", "RaftBuildPoint", "IslandSafeZone", "IslandRoot", "OceanSurface", "Shark", "UnderwaterFortress", "UnderwaterWoodField", "Systems" };
        foreach (string objectName in requiredObjects)
            if (FindSceneObject(objectName) == null)
                throw new System.InvalidOperationException($"Required object is missing: {objectName}");

        string[] requiredPrefabs =
        {
            PrefabRoot + "/Player/Player.prefab",
            PrefabRoot + "/Environment/Raft.prefab",
            PrefabRoot + "/Environment/RaftBuildPoint.prefab",
            PrefabRoot + "/Items/OxygenTank.prefab",
            PrefabRoot + "/Enemy/Shark.prefab",
            PrefabRoot + "/Fortress/FortressDoor.prefab",
            PrefabRoot + "/Fortress/EndingDrain.prefab"
        };
        foreach (string prefabPath in requiredPrefabs)
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                throw new System.InvalidOperationException($"Required prefab is missing: {prefabPath}");

        GameManager manager = Object.FindFirstObjectByType<GameManager>();
        SerializedObject managerSo = new SerializedObject(manager);
        if (managerSo.FindProperty("islandRoot").objectReferenceValue == null ||
            managerSo.FindProperty("islandSafeZone").objectReferenceValue == null ||
            managerSo.FindProperty("raftSpawn").objectReferenceValue == null ||
            managerSo.FindProperty("waterSurface").objectReferenceValue == null)
            throw new System.InvalidOperationException("GameManager world references are not assigned.");
        if (managerSo.FindProperty("postSubmergeSinkSpeed").floatValue <= 0f ||
            managerSo.FindProperty("islandDisableDepth").floatValue < 5f)
            throw new System.InvalidOperationException("The post-60-second island descent is not configured.");

        GameObject islandRoot = FindSceneObject("IslandRoot");
        SphereCollider safeZone = FindSceneObject("IslandSafeZone").GetComponent<SphereCollider>();
        if (safeZone == null || !safeZone.isTrigger)
            throw new System.InvalidOperationException("IslandSafeZone must use a trigger collider.");
        int woodCount = 0;
        int ropeCount = 0;
        foreach (PickupItem pickup in islandRoot.GetComponentsInChildren<PickupItem>(true))
        {
            if (pickup.Item == ItemId.Wood) woodCount++;
            else if (pickup.Item == ItemId.Rope) ropeCount++;
            else if (pickup.Item != ItemId.Palm)
                throw new System.InvalidOperationException($"Stage 0 contains forbidden resource: {pickup.Item}");
        }
        if (woodCount != 5 || ropeCount != 2)
            throw new System.InvalidOperationException($"Stage 0 resource count mismatch. Wood={woodCount}, Rope={ropeCount}");
        ValidateStage0LayoutAndOutline(islandRoot, safeZone);
        GameObject raft = FindSceneObject("Raft");
        if (raft.transform.IsChildOf(islandRoot.transform))
            throw new System.InvalidOperationException("Raft must remain outside IslandRoot.");
        if (raft.activeSelf)
            throw new System.InvalidOperationException("Raft must be inactive when Stage 0 starts.");
        ValidateRaftVisualAndColliders(raft);

        Transform planeGround = islandRoot.transform.Find("PlaneGround");
        if (planeGround == null || planeGround.GetComponentsInChildren<MeshCollider>(true).Length < 1)
            throw new System.InvalidOperationException("The island plane must use its own MeshCollider.");
        foreach (BoxCollider boxCollider in islandRoot.GetComponentsInChildren<BoxCollider>(true))
            if (!boxCollider.isTrigger)
                throw new System.InvalidOperationException($"The starting island contains a forbidden solid BoxCollider: {boxCollider.name}");

        GrassIndirectRenderer grass = islandRoot.GetComponentInChildren<GrassIndirectRenderer>(true);
        if (grass == null || grass.InstanceCount < 4400)
            throw new System.InvalidOperationException($"The starting island needs at least 4400 indirect grass instances. Found {grass?.InstanceCount ?? 0}.");
        if (islandRoot.GetComponentsInChildren<GrassProximityHider>(true).Length > 0)
            throw new System.InvalidOperationException("Individual grass GameObjects must not remain in the scene.");

        RaftBuildPoint buildPoint = FindSceneObject("RaftBuildPoint").GetComponent<RaftBuildPoint>();
        if (buildPoint == null || buildPoint.RaftToActivate != raft)
            throw new System.InvalidOperationException("RaftBuildPoint is not connected to the inactive raft.");
        GameObject ocean = FindSceneObject("OceanSurface");
        if (ocean.transform.lossyScale.x < 1500f || ocean.transform.lossyScale.z < 1900f)
            throw new System.InvalidOperationException("Ocean scale does not satisfy the Stage 0 revision.");
        Bounds planeBounds = CalculateBounds(planeGround.gameObject);
        float waterSurfaceY = ocean.transform.position.y + ocean.transform.lossyScale.y * 0.5f;
        float planeMidHeight = (planeBounds.min.y + planeBounds.max.y) * 0.5f;
        if (waterSurfaceY > planeMidHeight || waterSurfaceY > -1.2f)
            throw new System.InvalidOperationException($"Starting water is too high. Surface={waterSurfaceY:0.00}, island midpoint={planeMidHeight:0.00}.");

        ValidateStage0Vegetation(islandRoot, grass.InstanceCount);
        GameObject player = FindSceneObject("Player");
        ValidatePlayerOrientation(player, islandRoot);
        ValidateGrassOptimization(grass);
        ValidateWaterDrivenShark(player, ocean, islandRoot);
        ValidateUnderwaterWood(islandRoot);
        ValidateNavMeshPaths(islandRoot, player);

        Debug.Log($"[Raft Shark Dive] integrated validation passed: Stage 0 resources, NavMesh, palm colliders, DrawMeshInstancedIndirect grass x{grass.InstanceCount}, rigged Animator clips with two-foot IK, reliable seabed Wood interaction, raft expansion/repair, water FX/audio, checkpoint save/load, water-triggered shark, island descent, and one reft raft visual.");
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Scripts");
        EnsureFolder("Assets/Editor");
        EnsureFolder(GeneratedRoot);
        EnsureFolder(GeneratedRoot + "/Materials");
        EnsureFolder(GeneratedRoot + "/Textures");
        EnsureFolder(GeneratedRoot + "/Animations");
        EnsureFolder(PrefabRoot);
        EnsureFolder(PrefabRoot + "/Player");
        EnsureFolder(PrefabRoot + "/Environment");
        EnsureFolder(PrefabRoot + "/Items");
        EnsureFolder(PrefabRoot + "/Enemy");
        EnsureFolder(PrefabRoot + "/Fortress");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        string name = path.Substring(slash + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static Material CreateMaterial(string name, Color color, bool transparent = false)
    {
        string path = $"{GeneratedRoot}/Materials/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
        material.DisableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        if (transparent)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateGrassIndirectMaterial()
    {
        const string path = GeneratedRoot + "/Materials/GrassIndirect.mat";
        Shader shader = Shader.Find("RaftSharkDive/Grass Indirect");
        if (shader == null) throw new System.InvalidOperationException("Grass Indirect shader was not found.");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = "GrassIndirect" };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetColor("_BaseColor", new Color(0.16f, 0.38f, 0.12f, 1f));
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateOutlineMaterial()
    {
        const string path = GeneratedRoot + "/Materials/PickupOutline.mat";
        Shader shader = Shader.Find("RaftSharkDive/Pickup Outline");
        if (shader == null) throw new System.InvalidOperationException("Pickup outline shader was not found.");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = "PickupOutline" };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetColor("_OutlineColor", new Color(1f, 0.92f, 0.55f, 1f));
        material.DisableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreatePickupBaseMaterial(string name, Color baseColor, bool ropePattern)
    {
        Material material = CreateMaterial(name, Color.white);
        string texturePath = $"{GeneratedRoot}/Textures/{name}Texture.asset";
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture == null)
        {
            texture = new Texture2D(32, 32, TextureFormat.RGBA32, true) { name = name + "Texture" };
            AssetDatabase.CreateAsset(texture, texturePath);
        }

        Color[] pixels = new Color[32 * 32];
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float detail = ropePattern
                    ? 0.78f + (((x + y * 2) / 3) % 2) * 0.22f
                    : 0.72f + 0.18f * Mathf.Sin(y * 0.72f + Mathf.Sin(x * 0.35f) * 1.8f) +
                      0.08f * Mathf.Sin(x * 1.7f + y * 0.21f);
                Color pixel = baseColor * Mathf.Clamp(detail, 0.5f, 1.15f);
                pixel.a = 1f;
                pixels[y * 32 + x] = pixel;
            }
        }
        texture.SetPixels(pixels);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
        texture.Apply(true, false);
        EditorUtility.SetDirty(texture);

        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject BuildPlayerPrefab()
    {
        GameObject root = new GameObject("Player");
        CharacterController controller = root.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.35f;
        controller.center = new Vector3(0f, 0.9f, 0f);
        controller.stepOffset = 0.35f;
        PlayerController movement = root.AddComponent<PlayerController>();
        root.AddComponent<PlayerHealth>();
        root.AddComponent<Inventory>();
        root.AddComponent<PlayerOxygen>();
        CraftingSystem crafting = root.AddComponent<CraftingSystem>();
        root.AddComponent<RescueRopeSystem>();
        root.AddComponent<HeldItemController>();
        root.AddComponent<PlayerInteractor>();
        root.AddComponent<WaterEntryEffects>();

        GameObject pivot = new GameObject("CameraPivot");
        pivot.transform.SetParent(root.transform);
        pivot.transform.localPosition = new Vector3(0f, 1.58f, 0f);
        Camera camera = pivot.AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.nearClipPlane = 0.05f;
        camera.cullingMask &= ~(1 << LayerMask.NameToLayer("Ignore Raycast"));
        pivot.AddComponent<AudioListener>();
        SerializedObject movementSo = new SerializedObject(movement);
        movementSo.FindProperty("cameraPivot").objectReferenceValue = pivot.transform;
        movementSo.ApplyModifiedPropertiesWithoutUndo();

        PlayerRigParts rig = BuildPlayerRig(root.transform);
        Animator animator = rig.root.gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = CreatePlayerAnimatorController();
        animator.applyRootMotion = false;
        PlayerRigAnimator rigAnimator = root.AddComponent<PlayerRigAnimator>();
        rigAnimator.Configure(animator, rig.leftUpperLeg, rig.leftLowerLeg, rig.leftFoot,
            rig.rightUpperLeg, rig.rightLowerLeg, rig.rightFoot);

        GameObject tankVisual = new GameObject("EquippedOxygenTank");
        tankVisual.transform.SetParent(root.transform);
        tankVisual.transform.localPosition = new Vector3(0.32f, 0.95f, -0.28f);
        GameObject tankModel = AddModelVisual(tankVisual.transform, "Assets/model/reservoir.fbx", 0.9f, false, Quaternion.identity);
        if (tankModel != null) SetLayerRecursively(tankModel, LayerMask.NameToLayer("Ignore Raycast"));
        tankVisual.SetActive(false);
        SerializedObject craftingSo = new SerializedObject(crafting);
        craftingSo.FindProperty("equippedTankVisual").objectReferenceValue = tankVisual;
        craftingSo.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = SavePrefab(root, PrefabRoot + "/Player/Player.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    private sealed class PlayerRigParts
    {
        public Transform root;
        public Transform leftUpperLeg;
        public Transform leftLowerLeg;
        public Transform leftFoot;
        public Transform rightUpperLeg;
        public Transform rightLowerLeg;
        public Transform rightFoot;
    }

    private static PlayerRigParts BuildPlayerRig(Transform playerRoot)
    {
        Material body = CreateMaterial("PlayerBody", new Color(0.16f, 0.34f, 0.58f));
        Material skin = CreateMaterial("PlayerSkin", new Color(0.72f, 0.53f, 0.38f));
        Material accent = CreateMaterial("PlayerAccent", new Color(0.09f, 0.12f, 0.16f));
        Transform rigRoot = CreateBone(playerRoot, "PlayerRig", Vector3.zero);
        Transform hips = CreateBone(rigRoot, "Hips", new Vector3(0f, 0.9f, 0f));
        CreatePrimitiveChild(hips, "Pelvis", PrimitiveType.Cube, new Vector3(0f, 0.06f, 0f),
            new Vector3(0.45f, 0.36f, 0.26f), body, false);
        Transform spine = CreateBone(hips, "Spine", new Vector3(0f, 0.28f, 0f));
        CreatePrimitiveChild(spine, "Torso", PrimitiveType.Cube, new Vector3(0f, 0.25f, 0f),
            new Vector3(0.52f, 0.58f, 0.28f), body, false);
        Transform head = CreateBone(spine, "Head", new Vector3(0f, 0.67f, 0f));
        CreatePrimitiveChild(head, "HeadMesh", PrimitiveType.Sphere, Vector3.zero,
            new Vector3(0.3f, 0.34f, 0.3f), skin, false);

        Transform leftUpper = CreateBone(hips, "LeftUpperLeg", new Vector3(-0.16f, -0.05f, 0f));
        CreatePrimitiveChild(leftUpper, "LeftThigh", PrimitiveType.Capsule, new Vector3(0f, -0.2f, 0f),
            new Vector3(0.13f, 0.22f, 0.13f), body, false);
        Transform leftLower = CreateBone(leftUpper, "LeftLowerLeg", new Vector3(0f, -0.4f, 0f));
        CreatePrimitiveChild(leftLower, "LeftShin", PrimitiveType.Capsule, new Vector3(0f, -0.19f, 0f),
            new Vector3(0.115f, 0.21f, 0.115f), accent, false);
        Transform leftFoot = CreateBone(leftLower, "LeftFoot", new Vector3(0f, -0.39f, 0.08f));
        CreatePrimitiveChild(leftFoot, "LeftFootMesh", PrimitiveType.Cube, new Vector3(0f, 0.03f, 0.08f),
            new Vector3(0.2f, 0.12f, 0.35f), accent, false);

        Transform rightUpper = CreateBone(hips, "RightUpperLeg", new Vector3(0.16f, -0.05f, 0f));
        CreatePrimitiveChild(rightUpper, "RightThigh", PrimitiveType.Capsule, new Vector3(0f, -0.2f, 0f),
            new Vector3(0.13f, 0.22f, 0.13f), body, false);
        Transform rightLower = CreateBone(rightUpper, "RightLowerLeg", new Vector3(0f, -0.4f, 0f));
        CreatePrimitiveChild(rightLower, "RightShin", PrimitiveType.Capsule, new Vector3(0f, -0.19f, 0f),
            new Vector3(0.115f, 0.21f, 0.115f), accent, false);
        Transform rightFoot = CreateBone(rightLower, "RightFoot", new Vector3(0f, -0.39f, 0.08f));
        CreatePrimitiveChild(rightFoot, "RightFootMesh", PrimitiveType.Cube, new Vector3(0f, 0.03f, 0.08f),
            new Vector3(0.2f, 0.12f, 0.35f), accent, false);

        BuildArm(spine, "Left", -0.35f, body, skin);
        BuildArm(spine, "Right", 0.35f, body, skin);
        SetLayerRecursively(rigRoot.gameObject, LayerMask.NameToLayer("Ignore Raycast"));
        return new PlayerRigParts
        {
            root = rigRoot,
            leftUpperLeg = leftUpper,
            leftLowerLeg = leftLower,
            leftFoot = leftFoot,
            rightUpperLeg = rightUpper,
            rightLowerLeg = rightLower,
            rightFoot = rightFoot
        };
    }

    private static void BuildArm(Transform spine, string side, float x, Material sleeve, Material skin)
    {
        Transform upper = CreateBone(spine, side + "UpperArm", new Vector3(x, 0.47f, 0f));
        CreatePrimitiveChild(upper, side + "UpperArmMesh", PrimitiveType.Capsule, new Vector3(0f, -0.18f, 0f),
            new Vector3(0.1f, 0.2f, 0.1f), sleeve, false);
        Transform lower = CreateBone(upper, side + "LowerArm", new Vector3(0f, -0.36f, 0f));
        CreatePrimitiveChild(lower, side + "LowerArmMesh", PrimitiveType.Capsule, new Vector3(0f, -0.16f, 0f),
            new Vector3(0.085f, 0.18f, 0.085f), skin, false);
    }

    private static Transform CreateBone(Transform parent, string name, Vector3 localPosition)
    {
        GameObject bone = new GameObject(name);
        bone.transform.SetParent(parent, false);
        bone.transform.localPosition = localPosition;
        return bone.transform;
    }

    private static RuntimeAnimatorController CreatePlayerAnimatorController()
    {
        AnimationClip idle = CreatePlayerClip("PlayerIdle", 1.4f, clip =>
        {
            SetCurve(clip, "Hips", "m_LocalPosition.y", 1.4f, 0.9f, 0.92f, 0.9f);
            SetCurve(clip, "Hips/Spine", "localEulerAnglesRaw.z", 1.4f, -1.2f, 1.2f, -1.2f);
        });
        AnimationClip walk = CreatePlayerClip("PlayerWalk", 0.8f, clip =>
        {
            SetCurve(clip, "Hips", "m_LocalPosition.y", 0.8f, 0.9f, 0.95f, 0.9f);
            SetCurve(clip, "Hips/LeftUpperLeg", "localEulerAnglesRaw.x", 0.8f, -28f, 28f, -28f);
            SetCurve(clip, "Hips/RightUpperLeg", "localEulerAnglesRaw.x", 0.8f, 28f, -28f, 28f);
            SetCurve(clip, "Hips/Spine/LeftUpperArm", "localEulerAnglesRaw.x", 0.8f, 24f, -24f, 24f);
            SetCurve(clip, "Hips/Spine/RightUpperArm", "localEulerAnglesRaw.x", 0.8f, -24f, 24f, -24f);
        });
        AnimationClip swim = CreatePlayerClip("PlayerSwim", 1.1f, clip =>
        {
            SetCurve(clip, "Hips", "localEulerAnglesRaw.x", 1.1f, 58f, 64f, 58f);
            SetCurve(clip, "Hips/LeftUpperLeg", "localEulerAnglesRaw.x", 1.1f, -12f, 18f, -12f);
            SetCurve(clip, "Hips/RightUpperLeg", "localEulerAnglesRaw.x", 1.1f, 18f, -12f, 18f);
            SetCurve(clip, "Hips/Spine/LeftUpperArm", "localEulerAnglesRaw.z", 1.1f, -55f, -82f, -55f);
            SetCurve(clip, "Hips/Spine/RightUpperArm", "localEulerAnglesRaw.z", 1.1f, 55f, 82f, 55f);
        });

        const string controllerPath = GeneratedRoot + "/Animations/Player.controller";
        if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath) != null)
            AssetDatabase.DeleteAsset(controllerPath);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.AddParameter("MotionState", AnimatorControllerParameterType.Int);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idleState = machine.AddState("Idle"); idleState.motion = idle;
        AnimatorState walkState = machine.AddState("Walk"); walkState.motion = walk;
        AnimatorState swimState = machine.AddState("Swim"); swimState.motion = swim;
        machine.defaultState = idleState;
        AddMotionTransition(machine, idleState, 0);
        AddMotionTransition(machine, walkState, 1);
        AddMotionTransition(machine, swimState, 2);
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void AddMotionTransition(AnimatorStateMachine machine, AnimatorState destination, int state)
    {
        AnimatorStateTransition transition = machine.AddAnyStateTransition(destination);
        transition.hasExitTime = false;
        transition.duration = 0.12f;
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.Equals, state, "MotionState");
    }

    private static AnimationClip CreatePlayerClip(string name, float length, System.Action<AnimationClip> configure)
    {
        string path = $"{GeneratedRoot}/Animations/{name}.anim";
        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null) AssetDatabase.DeleteAsset(path);
        AnimationClip clip = new AnimationClip { name = name, frameRate = 30f };
        configure(clip);
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    private static void SetCurve(AnimationClip clip, string path, string property, float length,
        float start, float middle, float end)
    {
        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, start), new Keyframe(length * 0.5f, middle), new Keyframe(length, end));
        for (int i = 0; i < curve.length; i++)
        {
            curve.SmoothTangents(i, 0f);
        }
        clip.SetCurve(path, typeof(Transform), property, curve);
    }

    private static GameObject BuildRaftPrefab(Material extensionLogMaterial)
    {
        GameObject root = new GameObject("Raft");
        BoxCollider deckCollider = root.AddComponent<BoxCollider>();
        deckCollider.center = Vector3.zero;
        deckCollider.size = new Vector3(8f, 0.35f, 8f);
        GameObject visual = AddModelVisual(root.transform, "Assets/model/reft.fbx", 7.5f, false, Quaternion.identity);
        if (visual != null) visual.name = "RaftVisual_reft";
        RaftUpgradeStation upgradeStation = root.AddComponent<RaftUpgradeStation>();
        GameObject extensionLog = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/model/reft tree.fbx");
        upgradeStation.Configure(visual != null ? visual.transform : null, deckCollider,
            extensionLog, extensionLogMaterial);
        root.AddComponent<RaftAutoMover>();
        GameObject prefab = SavePrefab(root, PrefabRoot + "/Environment/Raft.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject BuildRaftBuildPointPrefab(Material wood, Material accent)
    {
        GameObject root = new GameObject("RaftBuildPoint");
        SphereCollider collider = root.AddComponent<SphereCollider>();
        collider.center = new Vector3(0f, 1f, 0f);
        collider.radius = 1.5f;
        collider.isTrigger = true;
        root.AddComponent<RaftBuildPoint>();

        CreatePrimitiveChild(root.transform, "BuildMarker", PrimitiveType.Cylinder,
            new Vector3(0f, 0.55f, 0f), new Vector3(1.2f, 0.08f, 1.2f), accent, false);
        for (int i = 0; i < 4; i++)
        {
            float angle = i * 90f * Mathf.Deg2Rad;
            Vector3 position = new Vector3(Mathf.Cos(angle) * 0.85f, 0.75f, Mathf.Sin(angle) * 0.85f);
            CreatePrimitiveChild(root.transform, $"WoodMarker_{i + 1}", PrimitiveType.Cylinder,
                position, new Vector3(0.12f, 0.75f, 0.12f), wood, false);
        }

        GameObject prefab = SavePrefab(root, PrefabRoot + "/Environment/RaftBuildPoint.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static Dictionary<ItemId, GameObject> BuildPickupPrefabs(Material pickupOutline,
        Material pickupWoodBase, Material pickupRopeBase)
    {
        Dictionary<ItemId, string> models = new Dictionary<ItemId, string>()
        {
            [ItemId.Rope] = "Assets/model/rope.fbx",
            [ItemId.Scrap] = "Assets/model/scrap.fbx",
            [ItemId.Rubber] = "Assets/model/rubber.fbx",
            [ItemId.Filter] = "Assets/model/filter.fbx"
        };
        Dictionary<ItemId, GameObject> result = new Dictionary<ItemId, GameObject>();
        foreach (KeyValuePair<ItemId, string> pair in models)
        {
            GameObject root = new GameObject($"Pickup_{pair.Key}");
            SphereCollider collider = root.AddComponent<SphereCollider>();
            collider.radius = 0.55f;
            collider.isTrigger = true;
            PickupItem pickup = root.AddComponent<PickupItem>();
            pickup.Configure(pair.Key, 1);
            if (pair.Key == ItemId.Rope)
            {
                GameObject ropeOriginal = AddPickupVisuals(root.transform, pair.Value, 0.85f, false,
                    Quaternion.identity, pickupOutline);
                ApplyMaterialRecursively(ropeOriginal, pickupRopeBase);
            }
            else
                AddModelVisual(root.transform, pair.Value, 0.85f, false, Quaternion.identity);
            result[pair.Key] = SavePrefab(root, $"{PrefabRoot}/Items/Pickup_{pair.Key}.prefab");
            Object.DestroyImmediate(root);
        }

        GameObject woodRoot = new GameObject("Pickup_Wood");
        BoxCollider woodCollider = woodRoot.AddComponent<BoxCollider>();
        woodCollider.size = new Vector3(1.7f, 0.7f, 0.9f);
        woodCollider.isTrigger = true;
        PickupItem woodPickup = woodRoot.AddComponent<PickupItem>();
        woodPickup.Configure(ItemId.Wood, 1);
        GameObject woodVisual = AddPickupVisuals(woodRoot.transform, "Assets/model/reft tree.fbx", 2.4f,
            true, Quaternion.identity, pickupOutline);
        ApplyMaterialRecursively(woodVisual, pickupWoodBase);
        Bounds woodBounds = CalculateBounds(woodVisual);
        woodCollider.center = woodRoot.transform.InverseTransformPoint(woodBounds.center);
        woodCollider.size = woodRoot.transform.InverseTransformVector(woodBounds.size + Vector3.one * 0.15f);
        result[ItemId.Wood] = SavePrefab(woodRoot, $"{PrefabRoot}/Items/Pickup_Wood.prefab");
        Object.DestroyImmediate(woodRoot);
        return result;
    }

    private static GameObject BuildOxygenTankPrefab()
    {
        GameObject root = new GameObject("OxygenTank");
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.size = new Vector3(0.8f, 1.4f, 0.8f);
        AddModelVisual(root.transform, "Assets/model/reservoir.fbx", 1.35f, true, Quaternion.identity);
        GameObject prefab = SavePrefab(root, PrefabRoot + "/Items/OxygenTank.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject BuildCratePrefab()
    {
        GameObject root = new GameObject("SupplyBox");
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.size = Vector3.one * 1.2f;
        collider.isTrigger = true;
        root.AddComponent<LootCrate>();
        AddModelVisual(root.transform, "Assets/model/box.fbx", 1.25f, true, Quaternion.identity);
        GameObject prefab = SavePrefab(root, PrefabRoot + "/Items/SupplyBox.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject BuildSharkPrefab()
    {
        GameObject root = new GameObject("Shark");
        CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
        collider.direction = 2;
        collider.height = 3.8f;
        collider.radius = 0.65f;
        collider.isTrigger = true;
        root.AddComponent<SharkAI>();
        AddModelVisual(root.transform, "Assets/model/shark.fbx", 4.2f, false, Quaternion.identity);
        GameObject prefab = SavePrefab(root, PrefabRoot + "/Enemy/Shark.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject BuildDoorPrefab(Material metal)
    {
        GameObject root = new GameObject("FortressDoor");
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.size = new Vector3(3.2f, 5f, 0.7f);
        root.AddComponent<FortressDoor>();
        CreatePrimitiveChild(root.transform, "DoorBacking", PrimitiveType.Cube, Vector3.zero, new Vector3(3.2f, 5f, 0.45f), metal, false);
        AddModelVisual(root.transform, "Assets/model/door.fbx", 4.8f, false, Quaternion.identity);
        GameObject prefab = SavePrefab(root, PrefabRoot + "/Fortress/FortressDoor.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject BuildEnvironmentPrefab(string name, string modelPath, Vector3 colliderSize, Material material)
    {
        GameObject root = new GameObject(name);
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.size = colliderSize;
        CreatePrimitiveChild(root.transform, "Structure", PrimitiveType.Cube, Vector3.zero, colliderSize, material, false);
        AddModelVisual(root.transform, modelPath, Mathf.Max(colliderSize.x, colliderSize.y, colliderSize.z), false, Quaternion.identity);
        GameObject prefab = SavePrefab(root, $"{PrefabRoot}/Fortress/{name}.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject BuildDrainPrefab(Material metal, Material accent)
    {
        GameObject root = new GameObject("EndingDrain");
        SphereCollider collider = root.AddComponent<SphereCollider>();
        collider.radius = 2.2f;
        collider.isTrigger = true;
        root.AddComponent<EndingDrain>();
        CreatePrimitiveChild(root.transform, "DrainRing", PrimitiveType.Cylinder, Vector3.zero, new Vector3(3.8f, 0.35f, 3.8f), metal, false);
        CreatePrimitiveChild(root.transform, "Plug", PrimitiveType.Cylinder, new Vector3(0, 0.35f, 0), new Vector3(2.5f, 0.55f, 2.5f), accent, false);
        GameObject handle = CreatePrimitiveChild(root.transform, "Handle", PrimitiveType.Cylinder, new Vector3(0, 1.5f, 0), new Vector3(0.35f, 2.2f, 0.35f), metal, false);
        handle.transform.localRotation = Quaternion.Euler(0, 0, 90);
        GameObject prefab = SavePrefab(root, PrefabRoot + "/Fortress/EndingDrain.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void AddIslandDecor(Transform island, Material grassMaterial)
    {
        MeshCollider[] islandSurfaceColliders = island.GetComponentsInChildren<MeshCollider>(true);
        Random.State previousState = Random.state;
        Random.InitState(260906);
        List<Vector2> treePositions = new List<Vector2>();
        int attempts = 0;
        while (treePositions.Count < 26 && attempts++ < 2000)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Mathf.Sqrt(Random.Range(0.08f, 1f)) * 52f;
            Vector2 point = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            if (Mathf.Abs(point.x) < 6f && point.y > -8f) continue;
            if (IsNearStage0Pickup(point, 5f)) continue;

            bool tooClose = false;
            foreach (Vector2 existing in treePositions)
            {
                if ((existing - point).sqrMagnitude >= 42.25f) continue;
                tooClose = true;
                break;
            }
            if (tooClose) continue;

            Vector3 probe = island.TransformPoint(new Vector3(point.x, 50f, point.y));
            if (!TryGetIslandSurfacePoint(island, islandSurfaceColliders, probe, out Vector3 surfacePoint, out Vector3 surfaceNormal) ||
                Vector3.Dot(surfaceNormal, Vector3.up) < 0.82f) continue;

            treePositions.Add(point);
            int treeNumber = treePositions.Count;
            float surfaceY = island.InverseTransformPoint(surfacePoint).y + 0.03f;
            float treeSize = Random.Range(12.5f, 16.5f);
            GameObject tree = AddDecorLocal(island, $"palm tree_{treeNumber:00}", "Assets/model/palm tree.fbx",
                new Vector3(point.x, surfaceY, point.y), treeSize,
                Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), false);
            AddPalmTreeCollider(tree, treeSize);

            int undergrowthCount = Random.Range(1, 4);
            for (int undergrowthIndex = 0; undergrowthIndex < undergrowthCount; undergrowthIndex++)
            {
                float undergrowthAngle = Random.Range(0f, Mathf.PI * 2f);
                float undergrowthDistance = Random.Range(1.35f, 2.35f);
                Vector2 undergrowthPoint = point + new Vector2(Mathf.Cos(undergrowthAngle), Mathf.Sin(undergrowthAngle)) * undergrowthDistance;
                Vector3 undergrowthProbe = island.TransformPoint(new Vector3(undergrowthPoint.x, 50f, undergrowthPoint.y));
                if (!TryGetIslandSurfacePoint(island, islandSurfaceColliders, undergrowthProbe, out Vector3 undergrowthSurface, out Vector3 undergrowthNormal) ||
                    Vector3.Dot(undergrowthNormal, Vector3.up) < 0.82f) continue;
                float undergrowthY = island.InverseTransformPoint(undergrowthSurface).y + 0.025f;
                GameObject palm = AddDecorLocal(island, $"palm_{treeNumber:00}_{undergrowthIndex + 1}", "Assets/model/palm .fbx",
                    new Vector3(undergrowthPoint.x, undergrowthY, undergrowthPoint.y), Random.Range(0.9f, 1.45f),
                    Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), false);
                AddPalmPlantGameplay(palm);
            }
        }
        if (treePositions.Count < 26)
            throw new System.InvalidOperationException($"Could only place {treePositions.Count} safe palm trees.");

        const float grassRadius = 57f;
        const float grassSpacing = 1.45f;
        List<Matrix4x4> grassMatrices = new List<Matrix4x4>();
        GameObject grassPrototype = new GameObject("GrassPrototype");
        grassPrototype.transform.SetParent(island, false);
        GameObject grassPrototypeVisual = AddModelVisual(grassPrototype.transform, "Assets/model/gress.fbx", 1f,
            true, Quaternion.identity);
        if (grassPrototypeVisual == null) throw new System.InvalidOperationException("gress.fbx could not be loaded.");
        grassPrototypeVisual.transform.localScale = Vector3.Scale(grassPrototypeVisual.transform.localScale,
            new Vector3(0.26f, 0.26f, 1f));
        MeshFilter grassFilter = grassPrototypeVisual.GetComponentInChildren<MeshFilter>(true);
        if (grassFilter == null || grassFilter.sharedMesh == null)
            throw new System.InvalidOperationException("gress.fbx has no mesh for indirect rendering.");
        Matrix4x4 prototypeMatrix = grassPrototype.transform.worldToLocalMatrix * grassFilter.transform.localToWorldMatrix;
        Mesh grassMesh = grassFilter.sharedMesh;
        int row = 0;
        for (float z = -grassRadius; z <= grassRadius; z += grassSpacing, row++)
        {
            float rowOffset = (row & 1) == 0 ? 0f : grassSpacing * 0.5f;
            for (float x = -grassRadius; x <= grassRadius; x += grassSpacing)
            {
                Vector2 point = new Vector2(
                    x + rowOffset + Random.Range(-0.5f, 0.5f),
                    z + Random.Range(-0.5f, 0.5f));
                if (point.sqrMagnitude > grassRadius * grassRadius) continue;

                float size = Random.Range(0.6f, 0.85f);
                Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                float surfaceY = GetIslandSurfaceLocalY(island, islandSurfaceColliders, point);
                Matrix4x4 placement = Matrix4x4.TRS(new Vector3(point.x, surfaceY, point.y), rotation,
                    Vector3.one * size);
                grassMatrices.Add(placement * prototypeMatrix);
            }
        }
        Object.DestroyImmediate(grassPrototype);
        GameObject grassRoot = new GameObject("GrassIndirectField");
        grassRoot.transform.SetParent(island, false);
        GrassIndirectRenderer indirectRenderer = grassRoot.AddComponent<GrassIndirectRenderer>();
        indirectRenderer.Configure(grassMesh, grassMaterial, grassMatrices.ToArray(),
            new Bounds(Vector3.zero, new Vector3(122f, 32f, 122f)));
        Random.state = previousState;
    }

    private static bool IsNearStage0Pickup(Vector2 point, float minimumDistance)
    {
        float minimumDistanceSquared = minimumDistance * minimumDistance;
        foreach (Vector2 pickupPoint in Stage0WoodLocalPositions)
            if ((pickupPoint - point).sqrMagnitude < minimumDistanceSquared) return true;
        foreach (Vector2 pickupPoint in Stage0RopeLocalPositions)
            if ((pickupPoint - point).sqrMagnitude < minimumDistanceSquared) return true;
        return false;
    }

    private static void AddPalmTreeCollider(GameObject tree, float treeSize)
    {
        CapsuleCollider collider = tree.AddComponent<CapsuleCollider>();
        collider.direction = 1;
        collider.radius = Mathf.Clamp(treeSize * 0.042f, 0.48f, 0.7f);
        collider.height = treeSize * 0.58f;
        collider.center = new Vector3(0f, collider.height * 0.5f, 0f);
    }

    private static void AddPalmPlantGameplay(GameObject palm)
    {
        Bounds bounds = CalculateBounds(palm);
        CapsuleCollider collider = palm.AddComponent<CapsuleCollider>();
        collider.direction = 1;
        collider.isTrigger = false;
        collider.center = palm.transform.InverseTransformPoint(bounds.center);
        Vector3 localSize = palm.transform.InverseTransformVector(bounds.size);
        localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
        collider.radius = Mathf.Clamp(Mathf.Max(localSize.x, localSize.z) * 0.28f, 0.16f, 0.38f);
        collider.height = Mathf.Max(collider.radius * 2f, localSize.y * 0.82f);
        PickupItem pickup = palm.AddComponent<PickupItem>();
        pickup.Configure(ItemId.Palm, 1);
        pickup.ConfigureUnderwater();
    }

    private static float GetIslandSurfaceLocalY(Transform island, MeshCollider[] surfaceColliders, Vector2 point)
    {
        Ray ray = new Ray(island.TransformPoint(new Vector3(point.x, 50f, point.y)), Vector3.down);
        float closestDistance = float.MaxValue;
        Vector3 closestPoint = Vector3.zero;
        bool found = false;
        foreach (MeshCollider surfaceCollider in surfaceColliders)
        {
            if (!surfaceCollider.Raycast(ray, out RaycastHit hit, 100f) || hit.distance >= closestDistance) continue;
            closestDistance = hit.distance;
            closestPoint = hit.point;
            found = true;
        }

        return found ? island.InverseTransformPoint(closestPoint).y + 0.03f : 0.03f;
    }

    private static void SpawnIslandLoot(Dictionary<ItemId, GameObject> pickups, Transform island,
        SphereCollider safeZone, float waterSurfaceY)
    {
        MeshCollider[] surfaceColliders = island.GetComponentsInChildren<MeshCollider>(true);
        foreach (Vector2 position in Stage0WoodLocalPositions)
            SpawnWalkableIslandPickup(pickups[ItemId.Wood], island, surfaceColliders, safeZone, waterSurfaceY, position);
        foreach (Vector2 position in Stage0RopeLocalPositions)
            SpawnWalkableIslandPickup(pickups[ItemId.Rope], island, surfaceColliders, safeZone, waterSurfaceY, position);
    }

    private static void SpawnWalkableIslandPickup(GameObject prefab, Transform island, MeshCollider[] surfaceColliders,
        SphereCollider safeZone, float waterSurfaceY, Vector2 localPosition)
    {
        Vector3 probe = island.TransformPoint(new Vector3(localPosition.x, 50f, localPosition.y));
        if (!TryGetIslandSurfacePoint(island, surfaceColliders, probe, out Vector3 surfacePoint, out Vector3 surfaceNormal))
            throw new System.InvalidOperationException($"No island plane below Stage 0 pickup position {localPosition}.");
        if (Vector3.Dot(surfaceNormal, Vector3.up) < 0.82f)
            throw new System.InvalidOperationException($"Stage 0 pickup position is too steep: {localPosition}.");
        float safeRadius = safeZone.radius * Mathf.Max(safeZone.transform.lossyScale.x, safeZone.transform.lossyScale.z);
        Vector2 safeOffset = new Vector2(surfacePoint.x - safeZone.transform.position.x, surfacePoint.z - safeZone.transform.position.z);
        if (safeOffset.magnitude > safeRadius - 10f || surfacePoint.y <= waterSurfaceY + 0.15f)
            throw new System.InvalidOperationException($"Stage 0 pickup position is outside the safe walkable island: {localPosition}.");

        GameObject pickup = SpawnPrefab(prefab, surfacePoint + Vector3.up * 2f);
        pickup.transform.SetParent(island, true);
        Bounds bounds = CalculateBounds(pickup);
        pickup.transform.position += Vector3.up * (surfacePoint.y - bounds.min.y);
    }

    private static void SnapFloatingStage0ObjectsToSurface(Transform island)
    {
        Physics.SyncTransforms();
        MeshCollider[] surfaceColliders = island.GetComponentsInChildren<MeshCollider>(true);
        int adjusted = 0;
        foreach (Transform child in island)
        {
            if (!IsStage0GroundTarget(child.gameObject)) continue;
            Bounds bounds = CalculateBounds(child.gameObject);
            if (!TryGetIslandSurfacePoint(island, surfaceColliders, bounds.center, out Vector3 surfacePoint)) continue;

            float gap = bounds.min.y - surfacePoint.y;
            if (gap <= 0.03f) continue;
            child.position += Vector3.down * gap;
            adjusted++;
        }
        Physics.SyncTransforms();
        Debug.Log($"[Raft Shark Dive] Grounded {adjusted} floating Stage 0 object(s); already-grounded objects were unchanged.");
    }

    private static bool IsStage0GroundTarget(GameObject target)
    {
        if (target.name.StartsWith("reft tree_") || target.name.StartsWith("palm tree_") || target.name.StartsWith("palm_"))
            return true;
        PickupItem pickup = target.GetComponent<PickupItem>();
        return pickup != null && (pickup.Item == ItemId.Wood || pickup.Item == ItemId.Rope);
    }

    private static bool TryGetIslandSurfacePoint(Transform island, MeshCollider[] surfaceColliders, Vector3 worldPosition, out Vector3 surfacePoint)
    {
        return TryGetIslandSurfacePoint(island, surfaceColliders, worldPosition, out surfacePoint, out _);
    }

    private static bool TryGetIslandSurfacePoint(Transform island, MeshCollider[] surfaceColliders, Vector3 worldPosition,
        out Vector3 surfacePoint, out Vector3 surfaceNormal)
    {
        Ray ray = new Ray(new Vector3(worldPosition.x, island.position.y + 50f, worldPosition.z), Vector3.down);
        float closestDistance = float.MaxValue;
        surfacePoint = Vector3.zero;
        surfaceNormal = Vector3.up;
        bool found = false;
        foreach (MeshCollider surfaceCollider in surfaceColliders)
        {
            if (!surfaceCollider.Raycast(ray, out RaycastHit hit, 100f) || hit.distance >= closestDistance) continue;
            closestDistance = hit.distance;
            surfacePoint = hit.point;
            surfaceNormal = hit.normal;
            found = true;
        }
        return found;
    }

    private static void ValidateStage0LayoutAndOutline(GameObject islandRoot, SphereCollider safeZone)
    {
        MeshCollider[] surfaceColliders = islandRoot.GetComponentsInChildren<MeshCollider>(true);
        GameObject ocean = FindSceneObject("OceanSurface");
        float waterSurfaceY = ocean.transform.position.y + ocean.transform.lossyScale.y * 0.5f;
        Material outlineMaterial = AssetDatabase.LoadAssetAtPath<Material>(GeneratedRoot + "/Materials/PickupOutline.mat");
        Material woodBase = AssetDatabase.LoadAssetAtPath<Material>(GeneratedRoot + "/Materials/PickupWoodBase.mat");
        Material ropeBase = AssetDatabase.LoadAssetAtPath<Material>(GeneratedRoot + "/Materials/PickupRopeBase.mat");
        if (outlineMaterial == null || outlineMaterial.shader == null || outlineMaterial.shader.name != "RaftSharkDive/Pickup Outline")
            throw new System.InvalidOperationException("Pickup outline material or shader is missing.");
        if (IsEmissive(outlineMaterial))
            throw new System.InvalidOperationException("Pickup outline must not use emission.");
        if (woodBase == null || ropeBase == null || IsEmissive(woodBase) || IsEmissive(ropeBase))
            throw new System.InvalidOperationException("Dedicated non-emissive Wood/Rope base materials are missing.");
        if (GetMainTexture(woodBase) == null || GetMainTexture(ropeBase) == null)
            throw new System.InvalidOperationException("Wood/Rope pickup materials must use dedicated recognition textures.");

        foreach (Transform child in islandRoot.transform)
        {
            if (!IsStage0GroundTarget(child.gameObject)) continue;
            Bounds bounds = CalculateBounds(child.gameObject);
            if (!TryGetIslandSurfacePoint(islandRoot.transform, surfaceColliders, bounds.center,
                    out Vector3 surfacePoint, out Vector3 surfaceNormal))
                throw new System.InvalidOperationException($"No island surface was found below {child.name}.");
            float gap = bounds.min.y - surfacePoint.y;
            if (gap > 0.06f)
                throw new System.InvalidOperationException($"Stage 0 object is floating: {child.name}, gap={gap:0.000}m.");
            if (Vector3.Dot(surfaceNormal, Vector3.up) < 0.82f)
                throw new System.InvalidOperationException($"Stage 0 object is on an inaccessible slope: {child.name}.");
        }

        foreach (PickupItem pickup in islandRoot.GetComponentsInChildren<PickupItem>(true))
        {
            if (pickup.Item != ItemId.Wood && pickup.Item != ItemId.Rope) continue;
            Bounds pickupBounds = CalculateBounds(pickup.gameObject);
            Vector2 safeOffset = new Vector2(pickupBounds.center.x - safeZone.transform.position.x,
                pickupBounds.center.z - safeZone.transform.position.z);
            float safeRadius = safeZone.radius * Mathf.Max(safeZone.transform.lossyScale.x, safeZone.transform.lossyScale.z);
            if (safeOffset.magnitude > safeRadius - 10f || pickupBounds.min.y <= waterSurfaceY + 0.15f)
                throw new System.InvalidOperationException($"Pickup is not safely inside the walkable island: {pickup.name}");

            Transform original = pickup.transform.Find("PickupOriginal");
            Transform outline = pickup.transform.Find("PickupOutline");
            if (original == null || outline == null)
                throw new System.InvalidOperationException($"Pickup must contain Original and Outline meshes: {pickup.name}");

            MeshFilter sourceMesh = original.GetComponentInChildren<MeshFilter>(true);
            string expectedModelPath = pickup.Item == ItemId.Wood ? "Assets/model/reft tree.fbx" : "Assets/model/rope.fbx";
            if (sourceMesh == null || AssetDatabase.GetAssetPath(sourceMesh.sharedMesh) != expectedModelPath)
                throw new System.InvalidOperationException($"Pickup uses the wrong source mesh: {pickup.name}");

            Renderer[] originalRenderers = original.GetComponentsInChildren<Renderer>(true);
            Renderer[] outlineRenderers = outline.GetComponentsInChildren<Renderer>(true);
            Material expectedBase = pickup.Item == ItemId.Wood ? woodBase : ropeBase;
            if (originalRenderers.Length == 0 || outlineRenderers.Length == 0)
                throw new System.InvalidOperationException($"Pickup renderers are missing: {pickup.name}");
            foreach (Renderer originalRenderer in originalRenderers)
                foreach (Material material in originalRenderer.sharedMaterials)
                    if (material != expectedBase || material == outlineMaterial || IsEmissive(material))
                        throw new System.InvalidOperationException($"Pickup original mesh must keep a non-emissive base material: {pickup.name}");
            foreach (Renderer outlineRenderer in outlineRenderers)
                foreach (Material material in outlineRenderer.sharedMaterials)
                    if (material != outlineMaterial)
                        throw new System.InvalidOperationException($"Pickup outline uses an unexpected material: {pickup.name}");
        }

        foreach (Renderer environmentRenderer in islandRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (environmentRenderer.GetComponentInParent<PickupItem>() != null) continue;
            foreach (Material material in environmentRenderer.sharedMaterials)
                if (material == outlineMaterial || IsEmissive(material))
                    throw new System.InvalidOperationException($"Environment object must not use pickup highlighting: {environmentRenderer.name}");
        }
    }

    private static void ValidateStage0Vegetation(GameObject islandRoot, int grassCount)
    {
        List<Transform> palmTrees = new List<Transform>();
        List<Transform> palms = new List<Transform>();
        foreach (Transform child in islandRoot.transform)
        {
            if (child.name.StartsWith("reft tree_"))
                throw new System.InvalidOperationException("reft tree must only be used as a Wood pickup.");
            if (child.name.StartsWith("palm tree_")) palmTrees.Add(child);
            else if (child.name.StartsWith("palm_")) palms.Add(child);
        }
        if (palmTrees.Count < 24)
            throw new System.InvalidOperationException($"The Stage 0 island needs a dense palm tree forest. Found {palmTrees.Count}.");
        if (palms.Count < palmTrees.Count || palms.Count > palmTrees.Count * 3)
            throw new System.InvalidOperationException($"Each palm tree needs 1-3 small root palms. Trees={palmTrees.Count}, palms={palms.Count}.");

        foreach (Transform palmTree in palmTrees)
        {
            CapsuleCollider capsule = palmTree.GetComponent<CapsuleCollider>();
            if (capsule == null || capsule.isTrigger || capsule.radius < 0.3f || capsule.height <= capsule.radius * 2f)
                throw new System.InvalidOperationException($"Palm tree needs a simple solid CapsuleCollider: {palmTree.name}");
        }

        foreach (Transform palm in palms)
        {
            CapsuleCollider capsule = palm.GetComponent<CapsuleCollider>();
            PickupItem pickup = palm.GetComponent<PickupItem>();
            if (capsule == null || capsule.isTrigger || pickup == null || pickup.Item != ItemId.Palm)
                throw new System.InvalidOperationException($"Palm needs a solid collider and edible pickup: {palm.name}");
            Bounds palmBounds = CalculateBounds(palm.gameObject);
            if (Mathf.Max(palmBounds.size.x, palmBounds.size.y, palmBounds.size.z) > 1.55f)
                throw new System.InvalidOperationException($"Root palm is too large: {palm.name}");
            float nearestTreeDistance = float.MaxValue;
            foreach (Transform palmTree in palmTrees)
            {
                Vector2 offset = new Vector2(palm.position.x - palmTree.position.x, palm.position.z - palmTree.position.z);
                nearestTreeDistance = Mathf.Min(nearestTreeDistance, offset.magnitude);
            }
            if (nearestTreeDistance > 2.7f)
                throw new System.InvalidOperationException($"Root palm is too far from every palm tree: {palm.name}");
        }

        if (grassCount < 4400)
            throw new System.InvalidOperationException($"The starting island needs very dense gress coverage. Found {grassCount}.");
    }

    private static void ValidatePlayerOrientation(GameObject player, GameObject islandRoot)
    {
        if (player.GetComponent<PlayerWaterDetector>() == null)
            throw new System.InvalidOperationException("PlayerWaterDetector is missing from Player.");
        PlayerRigAnimator rigAnimator = player.GetComponent<PlayerRigAnimator>();
        Transform rig = player.transform.Find("PlayerRig");
        if (rigAnimator == null || rigAnimator.RigAnimator == null || rigAnimator.RigAnimator.runtimeAnimatorController == null || rig == null)
            throw new System.InvalidOperationException("Rigged Player Animator, controller, or two-foot IK driver is missing.");
        if (rig.Find("Hips/LeftUpperLeg/LeftLowerLeg/LeftFoot") == null ||
            rig.Find("Hips/RightUpperLeg/RightLowerLeg/RightFoot") == null)
            throw new System.InvalidOperationException("Player two-leg bone hierarchy is incomplete.");
        AnimationClip[] clips = rigAnimator.RigAnimator.runtimeAnimatorController.animationClips;
        if (clips.Length < 3)
            throw new System.InvalidOperationException("Player Animator needs Idle, Walk, and Swim clips.");
        Bounds bounds = CalculateBounds(rig.gameObject);
        if (bounds.size.y <= Mathf.Max(bounds.size.x, bounds.size.z) * 1.15f)
            throw new System.InvalidOperationException($"Player rig is not upright. Bounds={bounds.size}");
        if (Mathf.Abs(bounds.min.y - player.transform.position.y) > 0.08f)
            throw new System.InvalidOperationException("Player rig feet are not aligned with PlayerRoot.");

        MeshCollider[] surfaceColliders = islandRoot.GetComponentsInChildren<MeshCollider>(true);
        if (!TryGetIslandSurfacePoint(islandRoot.transform, surfaceColliders, player.transform.position,
                out Vector3 surfacePoint, out Vector3 surfaceNormal) || Vector3.Dot(surfaceNormal, Vector3.up) < 0.82f)
            throw new System.InvalidOperationException("Player is not above a walkable island surface.");
        float groundGap = player.transform.position.y - surfacePoint.y;
        if (groundGap < 0f || groundGap > 0.08f)
            throw new System.InvalidOperationException($"Player feet are not on the island surface. Gap={groundGap:0.000}m.");
    }

    private static void ValidateGrassOptimization(GrassIndirectRenderer grass)
    {
        if (grass.GrassMesh == null || grass.GrassMaterial == null ||
            grass.GrassMaterial.shader == null || grass.GrassMaterial.shader.name != "RaftSharkDive/Grass Indirect")
            throw new System.InvalidOperationException("Grass must use the indirect mesh/material pipeline.");
        if (!grass.GrassMaterial.enableInstancing)
            throw new System.InvalidOperationException("Indirect grass material must enable instancing.");
    }

    private static void ValidateWaterDrivenShark(GameObject player, GameObject ocean, GameObject islandRoot)
    {
        WaterVolume waterVolume = ocean.GetComponent<WaterVolume>();
        BoxCollider trigger = ocean.GetComponent<BoxCollider>();
        if (waterVolume == null || trigger == null || !trigger.isTrigger)
            throw new System.InvalidOperationException("OceanSurface needs a trigger WaterVolume for contact detection.");
        if (player.GetComponent<PlayerWaterDetector>() == null || Object.FindFirstObjectByType<SharkAI>() == null)
            throw new System.InvalidOperationException("Water-driven Player/Shark components are missing.");
        if (player.GetComponent<WaterEntryEffects>() == null || Object.FindFirstObjectByType<SaveSystem>() == null)
            throw new System.InvalidOperationException("Water effects/audio or checkpoint save system is missing.");
        GameObject shark = FindSceneObject("Shark");
        if (shark.transform.IsChildOf(islandRoot.transform) || ocean.transform.IsChildOf(islandRoot.transform))
            throw new System.InvalidOperationException("Shark and ocean must remain independent from IslandRoot descent.");
    }

    private static void ValidateUnderwaterWood(GameObject islandRoot)
    {
        GameObject field = FindSceneObject("UnderwaterWoodField");
        if (field == null || field.transform.IsChildOf(islandRoot.transform))
            throw new System.InvalidOperationException("UnderwaterWoodField must exist outside IslandRoot.");
        UnderwaterWoodSpawner spawner = field.GetComponent<UnderwaterWoodSpawner>();
        if (spawner == null || spawner.SpawnCount < 10 || spawner.SpawnCount > 20 || spawner.WoodPickupPrefab == null)
            throw new System.InvalidOperationException("Underwater wood spawner must expose an adjustable 10-20 pickup count.");
        PickupItem[] pickups = field.GetComponentsInChildren<PickupItem>(true);
        if (pickups.Length != spawner.SpawnCount)
            throw new System.InvalidOperationException($"Underwater wood count mismatch. Expected={spawner.SpawnCount}, Found={pickups.Length}.");
        foreach (PickupItem pickup in pickups)
        {
            if (pickup.Item != ItemId.Wood || Mathf.Abs(Vector3.Dot(pickup.transform.up, Vector3.up)) > 0.35f)
                throw new System.InvalidOperationException($"Underwater wood must be a horizontal/tilted Wood pickup: {pickup.name}");
            Bounds bounds = CalculateBounds(pickup.gameObject);
            if (bounds.min.y < -15.55f || bounds.min.y > -15.25f || bounds.max.y >= -1.5f)
                throw new System.InvalidOperationException($"Underwater wood is not placed near the seabed: {pickup.name}, bounds={bounds}");
            Transform original = pickup.transform.Find("PickupOriginal");
            MeshFilter source = original != null ? original.GetComponentInChildren<MeshFilter>(true) : null;
            if (source == null || AssetDatabase.GetAssetPath(source.sharedMesh) != "Assets/model/reft tree.fbx")
                throw new System.InvalidOperationException($"Underwater wood must use the reft tree model: {pickup.name}");
            BoxCollider interaction = pickup.GetComponent<BoxCollider>();
            if (interaction == null || !interaction.isTrigger || pickup.Rotates ||
                interaction.bounds.size.magnitude < bounds.size.magnitude * 0.85f)
                throw new System.InvalidOperationException($"Underwater wood interaction collider does not fit its visible model: {pickup.name}");
        }
    }

    private static void ValidateRaftVisualAndColliders(GameObject raft)
    {
        if (raft.GetComponentsInChildren<MeshCollider>(true).Length > 0)
            throw new System.InvalidOperationException("Raft must not use MeshCollider.");
        if (raft.GetComponent<RaftUpgradeStation>() == null)
            throw new System.InvalidOperationException("Raft expansion/repair station is missing.");
        BoxCollider[] boxes = raft.GetComponentsInChildren<BoxCollider>(true);
        if (boxes.Length < 1)
            throw new System.InvalidOperationException("Raft needs invisible BoxCollider components.");
        foreach (BoxCollider box in boxes)
            if (box.GetComponent<Renderer>() != null)
                throw new System.InvalidOperationException($"Raft collider object must be invisible: {box.name}");

        Transform visualRoot = raft.transform.Find("RaftVisual_reft");
        Renderer[] renderers = raft.GetComponentsInChildren<Renderer>(true);
        if (visualRoot == null || renderers.Length == 0)
            throw new System.InvalidOperationException("Raft needs exactly one reft visual hierarchy.");
        foreach (Renderer renderer in renderers)
        {
            if (!renderer.transform.IsChildOf(visualRoot) && renderer.transform != visualRoot)
                throw new System.InvalidOperationException($"Raft contains a rendered object outside its reft visual: {renderer.name}");
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || AssetDatabase.GetAssetPath(filter.sharedMesh) != "Assets/model/reft.fbx")
                throw new System.InvalidOperationException($"Raft visible mesh must only come from reft.fbx: {renderer.name}");
        }
    }

    private static void ValidateNavMeshPaths(GameObject islandRoot, GameObject player)
    {
        NavMeshSurface surface = islandRoot.GetComponent<NavMeshSurface>();
        if (surface == null || surface.navMeshData == null)
            throw new System.InvalidOperationException("Island NavMeshSurface or baked data is missing.");
        surface.RemoveData();
        surface.AddData();
        if (!NavMesh.SamplePosition(player.transform.position, out NavMeshHit start, 3f, NavMesh.AllAreas))
            throw new System.InvalidOperationException("Player spawn is not on the baked NavMesh.");
        foreach (PickupItem pickup in islandRoot.GetComponentsInChildren<PickupItem>(true))
        {
            if (!NavMesh.SamplePosition(pickup.transform.position, out NavMeshHit end, 4f, NavMesh.AllAreas))
                throw new System.InvalidOperationException($"Pickup cannot be sampled on NavMesh: {pickup.name}");
            NavMeshPath path = new NavMeshPath();
            if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete)
                throw new System.InvalidOperationException($"Palm colliders block the route to pickup: {pickup.name}");
        }
    }

    private static Texture GetMainTexture(Material material)
    {
        if (material == null) return null;
        if (material.HasProperty("_BaseMap")) return material.GetTexture("_BaseMap");
        return material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
    }

    private static bool IsEmissive(Material material)
    {
        return material != null && material.IsKeywordEnabled("_EMISSION") &&
               material.HasProperty("_EmissionColor") && material.GetColor("_EmissionColor").maxColorComponent > 0.01f;
    }

    private static void SpawnUnderwaterWoodField(GameObject woodPickupPrefab, int count)
    {
        GameObject field = new GameObject("UnderwaterWoodField");
        field.transform.position = new Vector3(0f, 0f, 8f);
        UnderwaterWoodSpawner spawner = field.AddComponent<UnderwaterWoodSpawner>();
        spawner.Configure(woodPickupPrefab, count, -15.45f);
        for (int i = 0; i < count; i++)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(woodPickupPrefab);
            instance.name = $"UnderwaterWood_{i + 1:00}";
            spawner.PlaceInstance(instance, i);
        }
    }

    private static void SpawnUnderwaterLoot(Dictionary<ItemId, GameObject> pickups)
    {
        ItemId[] sequence = { ItemId.Scrap, ItemId.Rubber, ItemId.Filter, ItemId.Scrap, ItemId.Rope, ItemId.Rubber, ItemId.Scrap };
        Vector3[] positions =
        {
            new Vector3(-14f, -2f, 22f), new Vector3(16f, -3f, 31f), new Vector3(-22f, -4.5f, 42f), new Vector3(25f, -5f, 52f),
            new Vector3(-28f, -4f, 61f), new Vector3(18f, -5.5f, 72f), new Vector3(-10f, -5f, 84f)
        };
        for (int i = 0; i < sequence.Length; i++) SpawnPrefab(pickups[sequence[i]], positions[i]);
    }

    private static void BuildFortress(GameObject doorPrefab, GameObject wallPrefab, GameObject floorPrefab, GameObject drainPrefab)
    {
        Transform fortress = new GameObject("UnderwaterFortress").transform;
        const float zOffset = 60f;
        for (int z = 61; z <= 79; z += 4)
        {
            GameObject floor = SpawnPrefab(floorPrefab, new Vector3(0, -14.6f, z + zOffset));
            floor.transform.SetParent(fortress);
            floor.transform.localScale = new Vector3(3.2f, 1f, 1f);
        }

        for (int z = 61; z <= 81; z += 4)
        {
            SpawnWall(wallPrefab, fortress, new Vector3(-7f, -11.5f, z + zOffset), new Vector3(1f, 2f, 1.4f));
            SpawnWall(wallPrefab, fortress, new Vector3(7f, -11.5f, z + zOffset), new Vector3(1f, 2f, 1.4f));
        }
        SpawnWall(wallPrefab, fortress, new Vector3(-5f, -11.5f, 58f + zOffset), new Vector3(2.2f, 2f, 1f));
        SpawnWall(wallPrefab, fortress, new Vector3(5f, -11.5f, 58f + zOffset), new Vector3(2.2f, 2f, 1f));
        SpawnWall(wallPrefab, fortress, new Vector3(0f, -11.5f, 82f + zOffset), new Vector3(4.8f, 2f, 1f));

        GameObject door = SpawnPrefab(doorPrefab, new Vector3(0f, -12.5f, 58f + zOffset));
        door.transform.SetParent(fortress);
        GameObject drain = SpawnPrefab(drainPrefab, new Vector3(0f, -14f, 76f + zOffset));
        drain.transform.SetParent(fortress);

        GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beacon.name = "FortressBeacon";
        beacon.transform.SetParent(fortress);
        beacon.transform.position = new Vector3(0, -5f, 70f + zOffset);
        beacon.transform.localScale = new Vector3(0.5f, 9f, 0.5f);
        Object.DestroyImmediate(beacon.GetComponent<Collider>());
        Renderer renderer = beacon.GetComponent<Renderer>();
        renderer.sharedMaterial = CreateMaterial("Beacon", new Color(0.05f, 0.8f, 0.9f));
        fortress.gameObject.SetActive(false);
    }

    private static void SpawnWall(GameObject prefab, Transform parent, Vector3 position, Vector3 scale)
    {
        GameObject wall = SpawnPrefab(prefab, position);
        wall.transform.SetParent(parent);
        wall.transform.localScale = scale;
    }

    private static void BuildLighting()
    {
        GameObject sun = new GameObject("Sun");
        Light light = sun.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.25f;
        light.color = new Color(1f, 0.94f, 0.78f);
        sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
    }

    private static GameObject AddDecorLocal(Transform parent, string name, string modelPath, Vector3 localPosition, float size, Quaternion rotation, bool grass, Material overrideMaterial = null)
    {
        GameObject holder = new GameObject(name);
        holder.transform.SetParent(parent, false);
        holder.transform.localPosition = localPosition;
        GameObject visual = AddModelVisual(holder.transform, modelPath, size, true, rotation);
        if (overrideMaterial != null) ApplyMaterialRecursively(visual, overrideMaterial);
        if (grass)
        {
            if (visual != null)
                visual.transform.localScale = Vector3.Scale(visual.transform.localScale, new Vector3(0.26f, 0.26f, 1f));
            holder.AddComponent<GrassProximityHider>();
        }
        return holder;
    }

    private static GameObject AddPickupVisuals(Transform parent, string path, float targetSize, bool alignBottom,
        Quaternion rotation, Material outlineMaterial)
    {
        GameObject original = AddModelVisual(parent, path, targetSize, alignBottom, rotation);
        if (original == null) return null;
        original.name = "PickupOriginal";

        GameObject outline = AddModelVisual(parent, path, targetSize, alignBottom, rotation);
        if (outline == null) return original;
        outline.name = "PickupOutline";
        outline.transform.localScale *= 1.035f;
        ApplyMaterialRecursively(outline, outlineMaterial);
        return original;
    }

    private static GameObject AddModelVisual(Transform parent, string path, float targetSize, bool alignBottom, Quaternion rotation)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null)
        {
            Debug.LogWarning($"Missing model: {path}");
            return null;
        }
        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        visual.name = asset.name + "_Visual";
        visual.transform.SetParent(parent);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = rotation * GetFbxCorrection(path);
        visual.transform.localScale = Vector3.one;
        Bounds bounds = CalculateBounds(visual);
        float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (largest > 0.0001f) visual.transform.localScale *= targetSize / largest;
        bounds = CalculateBounds(visual);
        Vector3 target = parent.position;
        Vector3 anchor = alignBottom ? new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) : bounds.center;
        visual.transform.position += target - anchor;
        return visual;
    }

    private static Quaternion GetFbxCorrection(string path)
    {
        string modelName = System.IO.Path.GetFileNameWithoutExtension(path).Trim().ToLowerInvariant();
        switch (modelName)
        {
            case "shark":
            case "reft":
            case "gress":
            case "palm":
            case "palm tree":
            case "plane":
            case "wall":
            case "door":
                return Quaternion.Euler(-90f, 0f, 0f);
            case "player":
                return Quaternion.Euler(-90f, 0f, 0f);
            case "reservoir":
                return Quaternion.Euler(0f, 0f, 90f);
            default:
                return Quaternion.identity;
        }
    }

    private static void ApplyMaterialRecursively(GameObject root, Material material)
    {
        if (root == null || material == null) return;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
        {
            Material[] materials = renderer.sharedMaterials;
            if (materials.Length == 0) materials = new Material[1];
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            renderer.sharedMaterials = materials;
        }
    }

    private static void AddMeshColliders(GameObject root)
    {
        if (root == null) return;
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            MeshCollider collider = filter.GetComponent<MeshCollider>();
            if (collider == null) collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            collider.convex = false;
        }
    }

    private static Bounds CalculateBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform) SetLayerRecursively(child.gameObject, layer);
    }

    private static GameObject CreatePrimitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, bool collider)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.position = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private static GameObject AddInvisibleBoxCollider(Transform parent, string name, Vector3 localPosition, Vector3 size)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        BoxCollider collider = child.AddComponent<BoxCollider>();
        collider.size = size;
        return child;
    }

    private static GameObject CreatePrimitiveChild(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 scale, Material material, bool collider)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private static GameObject SavePrefab(GameObject root, string path)
    {
        return PrefabUtility.SaveAsPrefabAsset(root, path);
    }

    private static GameObject SpawnPrefab(GameObject prefab, Vector3 position)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.position = position;
        return instance;
    }

    private static GameObject FindSceneObject(string objectName)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name == objectName) return candidate.gameObject;
        }
        return null;
    }
}
#endif
