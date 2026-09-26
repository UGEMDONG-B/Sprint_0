using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RaftSharkDive.Tests
{
    public sealed class Stage0V7PlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadMainScene()
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;
            SaveSystem.Instance.DiskWritesEnabled = false;
            yield return new WaitForFixedUpdate();
        }

        [UnityTearDown]
        public IEnumerator RestoreTimeScale()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Stage0MaterialsCanBuildRaftBeforeDeadline()
        {
            GameManager manager = Object.FindFirstObjectByType<GameManager>();
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            GameObject island = FindSceneObject("IslandRoot");
            RaftBuildPoint buildPoint = Object.FindFirstObjectByType<RaftBuildPoint>();
            Assert.NotNull(manager);
            Assert.NotNull(player);
            Assert.NotNull(island);
            Assert.NotNull(buildPoint);

            List<PickupItem> remaining = new List<PickupItem>();
            foreach (PickupItem pickup in island.GetComponentsInChildren<PickupItem>(true))
                if (pickup.Item == ItemId.Wood || pickup.Item == ItemId.Rope) remaining.Add(pickup);
            Assert.AreEqual(7, remaining.Count, "Stage 0 must contain Wood 5 + Rope 2.");

            Assert.IsTrue(NavMesh.SamplePosition(player.transform.position, out NavMeshHit current, 3f, NavMesh.AllAreas));
            float routeLength = 0f;
            while (remaining.Count > 0)
            {
                int bestIndex = -1;
                float bestLength = float.MaxValue;
                NavMeshHit bestHit = default;
                for (int i = 0; i < remaining.Count; i++)
                {
                    if (!NavMesh.SamplePosition(remaining[i].transform.position, out NavMeshHit target, 4f, NavMesh.AllAreas))
                        continue;
                    NavMeshPath path = new NavMeshPath();
                    if (!NavMesh.CalculatePath(current.position, target.position, NavMesh.AllAreas, path) ||
                        path.status != NavMeshPathStatus.PathComplete) continue;
                    float length = PathLength(path);
                    if (length >= bestLength) continue;
                    bestLength = length;
                    bestIndex = i;
                    bestHit = target;
                }
                Assert.GreaterOrEqual(bestIndex, 0, "Every Stage 0 pickup must have a complete NavMesh path.");
                routeLength += bestLength;
                current = bestHit;
                remaining.RemoveAt(bestIndex);
            }

            Assert.IsTrue(NavMesh.SamplePosition(buildPoint.transform.position, out NavMeshHit buildTarget, 15f, NavMesh.AllAreas),
                "Raft build point must be reachable from the island NavMesh edge.");
            NavMeshPath buildPath = new NavMeshPath();
            Assert.IsTrue(NavMesh.CalculatePath(current.position, buildTarget.position, NavMesh.AllAreas, buildPath));
            Assert.AreEqual(NavMeshPathStatus.PathComplete, buildPath.status);
            routeLength += PathLength(buildPath);
            float estimatedSeconds = routeLength / player.SprintSpeed + 7f;
            Debug.Log($"[Stage0 v7 Test] Greedy pickup/build route: {routeLength:0.0}m, estimated {estimatedSeconds:0.0}s at sprint speed.");
            Assert.Less(estimatedSeconds, 60f,
                $"Greedy NavMesh route is too long for the 60-second objective: {estimatedSeconds:0.0}s, {routeLength:0.0}m.");

            Inventory inventory = player.GetComponent<Inventory>();
            foreach (PickupItem pickup in island.GetComponentsInChildren<PickupItem>(true))
                if (pickup.Item == ItemId.Wood) pickup.Interact(player);
            Assert.AreEqual(5, inventory.GetCount(ItemId.Wood));
            Assert.AreEqual(5, inventory.OccupiedSlotCount);
            buildPoint.Interact(player);
            Assert.AreEqual(5, buildPoint.DepositedWood);
            Assert.AreEqual(0, inventory.OccupiedSlotCount);

            foreach (PickupItem pickup in island.GetComponentsInChildren<PickupItem>(true))
                if (pickup.Item == ItemId.Rope) pickup.Interact(player);
            Assert.AreEqual(2, inventory.GetCount(ItemId.Rope));
            buildPoint.Interact(player);
            yield return null;
            Assert.IsTrue(manager.RaftBuilt);
            Assert.IsTrue(FindSceneObject("Raft").activeSelf);
            Assert.Greater(manager.LootTimeRemaining, 0f);
        }

        [UnityTest]
        public IEnumerator WaterContactImmediatelyTogglesSharkPursuit()
        {
            GameManager manager = Object.FindFirstObjectByType<GameManager>();
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            SharkAI shark = Object.FindFirstObjectByType<SharkAI>();
            GameObject ocean = FindSceneObject("OceanSurface");
            WaterEntryEffects effects = player.GetComponent<WaterEntryEffects>();
            Assert.NotNull(player.WaterDetector);
            Assert.NotNull(effects);
            Assert.AreEqual(0, effects.GetComponents<AudioSource>().Length,
                "Entering water must not create or play an AudioSource.");
            Assert.IsFalse(manager.SharkThreatEnabled);

            Vector3 nearShark = shark.transform.position + Vector3.right * 5f;
            player.Teleport(new Vector3(nearShark.x, manager.WaterSurfaceY - 1f, nearShark.z), Quaternion.identity);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.IsTrue(player.WaterDetector.IsInWater, "Player water trigger did not register contact.");
            Assert.IsTrue(manager.SharkThreatEnabled);
            Assert.IsTrue(shark.IsChasing);
            Assert.IsTrue(effects.UnderwaterOverlayActive);
            Assert.AreEqual(1, effects.SplashCount);
            Assert.AreEqual(PlayerRigAnimator.MotionState.Swim, player.GetComponent<PlayerRigAnimator>().State);

            player.Teleport(new Vector3(nearShark.x, ocean.GetComponent<Renderer>().bounds.max.y + 4f, nearShark.z), Quaternion.identity);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.IsFalse(player.WaterDetector.IsInWater);
            Assert.IsFalse(manager.SharkThreatEnabled);
            Assert.AreEqual(SharkAI.SharkState.Patrol, shark.State);
            Assert.AreEqual(2, effects.SplashCount);
        }

        [UnityTest]
        public IEnumerator GrassUsesOneIndirectDrawInsteadOfThousandsOfGameObjects()
        {
            GrassIndirectRenderer renderer = Object.FindFirstObjectByType<GrassIndirectRenderer>();
            Assert.NotNull(renderer);
            yield return null;
            Assert.GreaterOrEqual(renderer.InstanceCount, 4400);
            Assert.IsTrue(renderer.BuffersReady);
            Assert.AreEqual(0, Object.FindObjectsByType<GrassProximityHider>(FindObjectsInactive.Include,
                FindObjectsSortMode.None).Length);
            Debug.Log($"[Integrated Test] Grass indirect instances: {renderer.InstanceCount}, grass GameObjects: 0.");
        }

        [UnityTest]
        public IEnumerator SeabedWoodCanBeFoundAndPickedUpAtAnyVisiblePart()
        {
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            PlayerInteractor interactor = player.GetComponent<PlayerInteractor>();
            GameObject field = FindSceneObject("UnderwaterWoodField");
            PickupItem wood = field.GetComponentInChildren<PickupItem>(true);
            BoxCollider collider = wood.GetComponent<BoxCollider>();
            Assert.NotNull(collider);
            Assert.IsTrue(collider.isTrigger);
            Assert.IsFalse(wood.Rotates);
            player.Teleport(collider.bounds.center + Vector3.up * 0.7f, Quaternion.identity);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreSame(wood, interactor.FindBestInteractable());
            int before = player.GetComponent<Inventory>().GetCount(ItemId.Wood);
            wood.Interact(player);
            Assert.AreEqual(before + 1, player.GetComponent<Inventory>().GetCount(ItemId.Wood));
            Assert.IsFalse(wood.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator RaftCanExpandTakeDamageAndRepairWithSeabedWood()
        {
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            Inventory inventory = player.GetComponent<Inventory>();
            RaftUpgradeStation station = Object.FindFirstObjectByType<RaftUpgradeStation>(FindObjectsInactive.Include);
            station.gameObject.SetActive(true);
            Transform raftVisual = station.transform.Find("RaftVisual_reft");
            BoxCollider deck = station.GetComponent<BoxCollider>();
            Vector3 visualScaleBefore = raftVisual.localScale;
            Vector3 deckSizeBefore = deck.size;
            inventory.Add(ItemId.Wood, station.MaxExpansionLevel);
            for (int i = 0; i < station.MaxExpansionLevel; i++) station.Interact(player);
            Assert.IsTrue(station.IsExpanded);
            Assert.IsTrue(station.ExtensionPlankVisible);
            Assert.AreEqual(station.MaxExpansionLevel, station.ExpansionLevel);
            Assert.AreEqual(visualScaleBefore, raftVisual.localScale,
                "Expansion must add one plank instead of scaling the entire raft mesh.");
            Assert.Greater(deck.size.x, deckSizeBefore.x,
                "reft tree logs must expand the raft from its left and right sides.");
            Assert.That(deck.size.z, Is.EqualTo(deckSizeBefore.z).Within(0.001f));
            Transform extensionRoot = station.transform.Find("RaftExtensionPlanks");
            Assert.NotNull(extensionRoot);
            Assert.Greater(extensionRoot.GetComponentsInChildren<Renderer>(true).Length, 0,
                "Expansion must use a visible reft tree model instead of an empty platform.");
            Assert.AreEqual(0, inventory.GetCount(ItemId.Wood));
            inventory.Add(ItemId.Wood, 2);
            station.Damage(40f);
            float damaged = station.Durability;
            station.Interact(player);
            Assert.Greater(station.Durability, damaged);
            Assert.AreEqual(0, inventory.GetCount(ItemId.Wood));
            yield return null;
        }

        [UnityTest]
        public IEnumerator OxygenTankRevealsFortressAndRescueRopePullsDistantTeammate()
        {
            GameManager manager = Object.FindFirstObjectByType<GameManager>();
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            Inventory inventory = player.GetComponent<Inventory>();
            CraftingSystem crafting = player.GetComponent<CraftingSystem>();
            RescueRopeSystem rescue = player.GetComponent<RescueRopeSystem>();
            Assert.IsFalse(manager.FortressVisible);

            inventory.Add(ItemId.Scrap, crafting.ScrapRequired);
            inventory.Add(ItemId.Rubber, crafting.RubberRequired);
            RaftUpgradeStation raftForStorage = Object.FindFirstObjectByType<RaftUpgradeStation>(FindObjectsInactive.Include);
            RaftStorageBox materialStorage = RaftStorageBox.CreateOnRaft(raftForStorage.transform);
            materialStorage.RestoreSlots(new[] { (int)ItemId.Filter });
            crafting.TryCraftOxygenTank();
            Assert.IsTrue(manager.FortressVisible);
            Assert.AreEqual(ItemId.OxygenTank, inventory.SelectedItem);
            GameObject fortress = FindSceneObject("UnderwaterFortress");
            RaftUpgradeStation raft = Object.FindFirstObjectByType<RaftUpgradeStation>(FindObjectsInactive.Include);
            Renderer fortressRenderer = fortress.GetComponentInChildren<Renderer>();
            Assert.Greater(Vector3.Dot(fortressRenderer.bounds.center - raft.transform.position,
                    raft.transform.forward), 50f,
                "The fortress must appear ahead of the moving raft, never behind it.");
            Transform fortressGeometry = fortress.transform.Find("MvpFortressGeometry");
            Assert.NotNull(fortressGeometry, "The fortress must be rebuilt as one coherent underwater structure.");
            Renderer[] fortressParts = fortressGeometry.GetComponentsInChildren<Renderer>();
            Assert.GreaterOrEqual(fortressParts.Length, 10);
            Bounds fortressBounds = fortressParts[0].bounds;
            for (int i = 1; i < fortressParts.Length; i++) fortressBounds.Encapsulate(fortressParts[i].bounds);
            Assert.Less(fortressBounds.max.y, manager.WaterSurfaceY - 1f,
                "The complete fortress, including its beacon, must remain below the surface.");
            Assert.NotNull(fortress.GetComponentInChildren<FortressDoor>());
            Assert.NotNull(fortress.GetComponentInChildren<EndingDrain>());
            HeldItemController heldItems = player.GetComponent<HeldItemController>();
            Assert.NotNull(heldItems);
            Assert.AreEqual(ItemId.OxygenTank, heldItems.DisplayedItem);

            inventory.Add(ItemId.Rope, crafting.RescueRopeCost);
            crafting.TryCraftRescueRope();
            Assert.AreEqual(1, inventory.GetCount(ItemId.RescueRope));
            GameObject teammateObject = new GameObject("DistantTeammate_Test");
            teammateObject.AddComponent<CharacterController>();
            teammateObject.AddComponent<PlayerWaterDetector>();
            PlayerController teammate = teammateObject.AddComponent<PlayerController>();
            teammate.Teleport(player.transform.position + Vector3.forward * 30f, Quaternion.identity);
            Physics.SyncTransforms();
            Assert.IsTrue(rescue.TryUseRescueRope());
            Assert.AreSame(teammate, rescue.LastRescuedPlayer);
            Assert.Less(Vector3.Distance(player.transform.position, teammate.transform.position), 3f);
            Assert.AreEqual(0, inventory.GetCount(ItemId.RescueRope));
            Object.Destroy(teammateObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator InventoryUsesFiveSelectableSlotsAndHeldQItemCanBeThrown()
        {
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            Inventory inventory = player.GetComponent<Inventory>();
            HeldItemController heldItems = player.GetComponent<HeldItemController>();
            inventory.RestoreSerializableCounts(null);

            Assert.IsTrue(inventory.TryAdd(ItemId.Wood, 5));
            Assert.IsFalse(inventory.TryAdd(ItemId.Rope));
            Assert.AreEqual(5, inventory.MaxSlots);
            Assert.AreEqual(5, inventory.OccupiedSlotCount);
            Assert.AreEqual(5, inventory.GetCount(ItemId.Wood));
            for (int i = 0; i < inventory.MaxSlots; i++)
                Assert.AreEqual(ItemId.Wood, inventory.GetSlotItem(i));

            inventory.SelectSlot(0);
            yield return null;
            Assert.AreEqual(ItemId.Wood, heldItems.DisplayedItem);
            Assert.IsTrue(heldItems.ThrowSelected(1f));
            Assert.AreEqual(4, inventory.GetCount(ItemId.Wood));
            GameObject thrown = GameObject.Find("Thrown_Wood");
            Assert.NotNull(thrown);
            Rigidbody thrownBody = thrown.GetComponent<Rigidbody>();
            Assert.NotNull(thrownBody);
            Assert.NotNull(thrown.GetComponent<PickupItem>());
            ThrownPickupMotion waterMotion = thrown.GetComponent<ThrownPickupMotion>();
            Assert.NotNull(waterMotion);
            Vector3 waterPosition = thrown.transform.position;
            waterPosition.y = GameManager.Instance.WaterSurfaceY - 0.1f;
            thrownBody.position = waterPosition;
            yield return null;
            yield return new WaitForSeconds(0.5f);
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(waterMotion.SettledInWater);
            Assert.IsTrue(thrownBody.isKinematic,
                "A thrown item must stop at the water instead of flying forever.");
            Object.Destroy(thrown);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WoodCraftsTenSlotRaftStorageAndDroppedItemsStayOnDeck()
        {
            GameManager manager = Object.FindFirstObjectByType<GameManager>();
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            Inventory inventory = player.GetComponent<Inventory>();
            CraftingSystem crafting = player.GetComponent<CraftingSystem>();
            HeldItemController heldItems = player.GetComponent<HeldItemController>();
            RaftUpgradeStation raft = Object.FindFirstObjectByType<RaftUpgradeStation>(FindObjectsInactive.Include);
            raft.gameObject.SetActive(true);
            manager.NotifyRaftBuilt();
            inventory.RestoreSerializableCounts(null);
            Assert.IsTrue(inventory.TryAdd(ItemId.Wood, 5));

            crafting.TryCraftStorageBox();
            Assert.IsNull(RaftStorageBox.FindExisting());
            Assert.AreEqual(ItemId.RaftStorageBox, inventory.SelectedItem);
            Assert.AreEqual(1, inventory.GetCount(ItemId.RaftStorageBox));
            yield return null;
            Assert.IsTrue(heldItems.InstallSelectedStorage(raft.transform, raft.transform.position));
            RaftStorageBox storage = RaftStorageBox.FindExisting();
            Assert.NotNull(storage);
            Assert.AreEqual(10, storage.Capacity);
            Assert.AreEqual(0, inventory.OccupiedSlotCount);
            Assert.IsTrue(storage.transform.IsChildOf(raft.transform));

            Assert.IsTrue(inventory.TryAdd(ItemId.Rope));
            Assert.IsTrue(storage.TryStoreFrom(inventory, 0));
            Assert.AreEqual(ItemId.Rope, storage.GetSlotItem(0));
            Assert.AreEqual(0, inventory.OccupiedSlotCount);
            Assert.IsTrue(storage.TryTakeTo(inventory, 0));
            Assert.AreEqual(ItemId.Rope, inventory.GetSlotItem(0));

            inventory.RestoreSerializableCounts(null);
            inventory.TryAdd(ItemId.Scrap, 1, true);
            yield return null;
            Assert.IsTrue(heldItems.DropSelected());
            GameObject dropped = GameObject.Find("Thrown_Scrap");
            Rigidbody body = dropped.GetComponent<Rigidbody>();
            BoxCollider deck = raft.GetComponent<BoxCollider>();
            body.position = new Vector3(raft.transform.position.x, deck.bounds.max.y + 0.8f,
                raft.transform.position.z);
            body.linearVelocity = Vector3.down * 2f;
            float timeout = Time.realtimeSinceStartup + 2f;
            ThrownPickupMotion motion = dropped.GetComponent<ThrownPickupMotion>();
            while (!motion.SettledOnRaft && Time.realtimeSinceStartup < timeout) yield return new WaitForFixedUpdate();
            Assert.IsTrue(motion.SettledOnRaft);
            Assert.IsTrue(dropped.transform.IsChildOf(raft.transform));
            Assert.IsTrue(body.isKinematic);
            Object.Destroy(dropped);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PalmTreesBlockMovementAndSmallPalmsCanBeEatenForHealth()
        {
            GameObject island = FindSceneObject("IslandRoot");
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            Inventory inventory = player.GetComponent<Inventory>();
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            HeldItemController heldItems = player.GetComponent<HeldItemController>();
            Transform palmTree = null;
            Transform palm = null;
            foreach (Transform child in island.transform)
            {
                if (palmTree == null && child.name.StartsWith("palm tree_")) palmTree = child;
                else if (palm == null && child.name.StartsWith("palm_")) palm = child;
            }

            Assert.NotNull(palmTree);
            Assert.NotNull(palm);
            Assert.IsFalse(palmTree.GetComponent<CapsuleCollider>().isTrigger);
            CapsuleCollider palmCollider = palm.GetComponent<CapsuleCollider>();
            PickupItem palmPickup = palm.GetComponent<PickupItem>();
            Assert.NotNull(palmCollider);
            Assert.IsFalse(palmCollider.isTrigger);
            Assert.NotNull(palmPickup);
            Assert.AreEqual(ItemId.Palm, palmPickup.Item);

            inventory.RestoreSerializableCounts(null);
            health.Damage(40f);
            float damagedHealth = health.Current;
            palmPickup.Interact(player);
            Assert.AreEqual(1, inventory.GetCount(ItemId.Palm));
            Assert.IsTrue(heldItems.TryEatSelectedPalm());
            Assert.Greater(health.Current, damagedHealth);
            Assert.AreEqual(0, inventory.GetCount(ItemId.Palm));
            yield return null;
        }

        [UnityTest]
        public IEnumerator AnimatorClipsFootIkAndCheckpointSnapshotAreConnected()
        {
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            PlayerRigAnimator rig = player.GetComponent<PlayerRigAnimator>();
            Assert.NotNull(rig);
            Assert.NotNull(rig.RigAnimator.runtimeAnimatorController);
            Assert.GreaterOrEqual(rig.RigAnimator.runtimeAnimatorController.animationClips.Length, 3);
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreEqual(PlayerRigAnimator.MotionState.Idle, rig.State);

            Inventory inventory = player.GetComponent<Inventory>();
            inventory.Add(ItemId.Wood, 4);
            SaveSystem.SaveData snapshot = SaveSystem.Instance.CaptureSnapshot(false);
            inventory.RestoreSerializableCounts(new int[System.Enum.GetValues(typeof(ItemId)).Length]);
            SaveSystem.Instance.RestoreSnapshot(snapshot);
            Assert.AreEqual(4, inventory.GetCount(ItemId.Wood));
            Assert.That(Vector3.Distance(player.transform.position, snapshot.playerPosition), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator IslandContinuesDownAndDeactivatesWithoutTakingOceanOrShark()
        {
            GameManager manager = Object.FindFirstObjectByType<GameManager>();
            GameObject island = FindSceneObject("IslandRoot");
            GameObject ocean = FindSceneObject("OceanSurface");
            GameObject shark = FindSceneObject("Shark");
            manager.BeginRaftSurvival();
            Time.timeScale = 20f;
            float timeout = Time.realtimeSinceStartup + 4f;
            while (island.activeSelf && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.IsTrue(manager.IslandDescentComplete);
            Assert.IsFalse(island.activeSelf);
            Assert.IsTrue(ocean.activeSelf);
            Assert.IsTrue(shark.activeSelf);
            Assert.IsNull(ocean.transform.parent);
            Assert.IsNull(shark.transform.parent);
            Assert.IsTrue(SaveSystem.Instance.LastSaveWasIslandCheckpoint);
            Assert.NotNull(SaveSystem.Instance.LastSnapshot);
            yield return null;
            SharkAI sharkAi = shark.GetComponent<SharkAI>();
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            Assert.AreSame(player, sharkAi.CurrentTarget,
                "After the island disappears, the shark must pursue the active player even on the raft.");
            GameObject raft = FindSceneObject("Raft");
            raft.SetActive(true);
            Vector3 playerBeforeCatchUp = player.transform.position;
            float playerHealthBeforeCatchUp = player.GetComponent<PlayerHealth>().Current;
            shark.transform.position = raft.transform.position + raft.transform.right * 100f;
            yield return null;
            Vector3 sharkOffset = shark.transform.position - raft.transform.position;
            sharkOffset.y = 0f;
            Assert.Less(sharkOffset.magnitude, 40f,
                "A shark left far behind must catch up near the moving raft.");
            Assert.AreEqual(1, sharkAi.VoyageCatchUpCount);
            Vector2 playerHorizontalBefore = new Vector2(playerBeforeCatchUp.x, playerBeforeCatchUp.z);
            Vector2 playerHorizontalAfter = new Vector2(player.transform.position.x, player.transform.position.z);
            Assert.That(Vector2.Distance(playerHorizontalBefore, playerHorizontalAfter), Is.LessThan(0.1f),
                "Shark catch-up must never teleport or reload the player.");
            Assert.AreEqual(playerHealthBeforeCatchUp, player.GetComponent<PlayerHealth>().Current,
                "Shark catch-up grace must prevent an immediate player respawn.");
        }

        [UnityTest]
        public IEnumerator RaftMovesForwardAfterIslandDisappearsAndCarriesDeckPassenger()
        {
            GameManager manager = Object.FindFirstObjectByType<GameManager>();
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            GameObject raft = FindSceneObject("Raft");
            GameObject fortress = FindSceneObject("UnderwaterFortress");
            RaftAutoMover mover = raft.GetComponent<RaftAutoMover>();
            BoxCollider deck = raft.GetComponent<BoxCollider>();
            Assert.NotNull(mover);
            Assert.NotNull(deck);
            Assert.IsFalse(fortress.activeSelf, "The fortress must stay hidden for the current MVP phase.");

            raft.SetActive(true);
            player.Teleport(new Vector3(raft.transform.position.x, deck.bounds.max.y + 0.05f,
                raft.transform.position.z), raft.transform.rotation);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            Vector3 raftStart = raft.transform.position;
            yield return null;
            Assert.That(Vector3.Distance(raft.transform.position, raftStart), Is.LessThan(0.001f),
                "The raft must wait until the island has completely disappeared.");

            manager.BeginRaftSurvival();
            Time.timeScale = 20f;
            float timeout = Time.realtimeSinceStartup + 4f;
            while (!manager.IslandDescentComplete && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.IsTrue(manager.IslandDescentComplete);

            Vector3 relativeBefore = player.transform.position - raft.transform.position;
            Vector3 movingStart = raft.transform.position;
            yield return null;
            Vector3 raftMotion = raft.transform.position - movingStart;
            Assert.IsTrue(mover.IsMoving);
            Assert.Greater(Vector3.Dot(raftMotion, raft.transform.forward), 0f);
            Vector3 relativeAfter = player.transform.position - raft.transform.position;
            Assert.That(Vector3.Distance(relativeBefore, relativeAfter), Is.LessThan(0.08f),
                "A player standing on the deck must travel with the moving raft.");
        }

        [UnityTest]
        public IEnumerator RaftStopsDirectlyAboveRevealedFortress()
        {
            GameManager manager = Object.FindFirstObjectByType<GameManager>();
            GameObject raft = FindSceneObject("Raft");
            RaftAutoMover mover = raft.GetComponent<RaftAutoMover>();
            Assert.NotNull(manager);
            Assert.NotNull(mover);

            raft.SetActive(true);
            manager.BeginRaftSurvival();
            Time.timeScale = 30f;
            float timeout = Time.realtimeSinceStartup + 4f;
            while (!manager.IslandDescentComplete && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.IsTrue(manager.IslandDescentComplete);

            manager.OnOxygenTankCraftedLocal();
            Assert.IsTrue(manager.TryGetFortressTransform(out Vector3 fortressPosition, out _));
            timeout = Time.realtimeSinceStartup + 4f;
            while (!mover.HasReachedFortress && Time.realtimeSinceStartup < timeout) yield return null;

            Assert.IsTrue(mover.HasReachedFortress, "The raft must stop when it reaches the fortress.");
            Vector3 planarOffset = Vector3.ProjectOnPlane(fortressPosition - raft.transform.position, Vector3.up);
            Assert.That(planarOffset.magnitude, Is.LessThan(0.05f),
                "The raft must stop directly above the underwater fortress center.");
            Assert.IsFalse(mover.IsMoving);
            Vector3 stoppedPosition = raft.transform.position;
            yield return null;
            Assert.That(Vector3.Distance(stoppedPosition, raft.transform.position), Is.LessThan(0.001f));
        }

        [UnityTest]
        public IEnumerator RaftProvidesBoardingRampsOnAllFourSides()
        {
            GameObject raft = FindSceneObject("Raft");
            raft.SetActive(true);
            yield return null;
            RaftAutoMover mover = raft.GetComponent<RaftAutoMover>();
            Assert.NotNull(mover);
            Assert.AreEqual(4, mover.BoardingRampCount);

            Transform ramps = raft.transform.Find("AllSideBoarding");
            string[] directions = { "Front", "Back", "Left", "Right" };
            foreach (string direction in directions)
            {
                BoxCollider high = ramps.Find($"{direction}/High").GetComponent<BoxCollider>();
                BoxCollider low = ramps.Find($"{direction}/Low").GetComponent<BoxCollider>();
                Assert.NotNull(high);
                Assert.NotNull(low);
                Assert.Greater(high.bounds.min.y, low.bounds.min.y,
                    $"{direction} boarding colliders must descend toward the sea in two steps.");
            }
            Assert.AreEqual(8, mover.BoardingStepColliderCount);
            Transform legacyRamp = raft.transform.Find("BoardingRamp");
            Assert.IsTrue(legacyRamp == null || !legacyRamp.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator OceanMaterialsKeepSpawningAheadOfMovingRaft()
        {
            GameManager manager = Object.FindFirstObjectByType<GameManager>();
            GameObject raft = FindSceneObject("Raft");
            EndlessOceanItemSpawner spawner = Object.FindFirstObjectByType<EndlessOceanItemSpawner>();
            Assert.NotNull(spawner);
            raft.SetActive(true);
            manager.BeginRaftSurvival();
            Time.timeScale = 20f;
            float timeout = Time.realtimeSinceStartup + 4f;
            while (!manager.IslandDescentComplete && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.IsTrue(manager.IslandDescentComplete);
            yield return null;

            Assert.IsTrue(spawner.IsRunning);
            Assert.GreaterOrEqual(spawner.ActiveSpawnCount, 8);
            int initialTotal = spawner.TotalSpawnedCount;
            timeout = Time.realtimeSinceStartup + 3f;
            while (spawner.TotalSpawnedCount <= initialTotal && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.Greater(spawner.TotalSpawnedCount, initialTotal,
                "New material rows must continue spawning as the raft travels forward.");
        }

        [UnityTest]
        public IEnumerator SharkTargetsNearestSwimmingPlayerWithoutNetworking()
        {
            GameManager manager = Object.FindFirstObjectByType<GameManager>();
            SharkAI shark = Object.FindFirstObjectByType<SharkAI>();
            PlayerController first = Object.FindFirstObjectByType<PlayerController>();
            GameObject secondObject = new GameObject("LocalPlayerTwo_Test");
            secondObject.AddComponent<CharacterController>();
            secondObject.AddComponent<PlayerWaterDetector>();
            PlayerController second = secondObject.AddComponent<PlayerController>();

            float waterY = manager.WaterSurfaceY - 2f;
            Vector3 sharkPosition = shark.transform.position;
            first.Teleport(new Vector3(sharkPosition.x + 11f, waterY, sharkPosition.z), Quaternion.identity);
            second.Teleport(new Vector3(sharkPosition.x + 5f, waterY, sharkPosition.z), Quaternion.identity);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.IsTrue(first.IsSwimming);
            Assert.IsTrue(second.IsSwimming);
            Assert.AreSame(second, shark.CurrentTarget);
            Assert.Less(shark.GetPursuitSpeedFor(second), second.SwimSpeed,
                "The shark must be slightly slower than a swimming player.");

            first.Teleport(new Vector3(shark.transform.position.x + 2.5f, waterY, shark.transform.position.z),
                Quaternion.identity);
            second.Teleport(new Vector3(shark.transform.position.x + 12f, waterY, shark.transform.position.z),
                Quaternion.identity);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return new WaitForSeconds(1.1f);
            Assert.AreSame(first, shark.CurrentTarget);
            Object.Destroy(secondObject);
        }

        private static float PathLength(NavMeshPath path)
        {
            float length = 0f;
            for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            return length;
        }

        private static GameObject FindSceneObject(string objectName)
        {
            foreach (Transform transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
                if (transform.name == objectName && transform.gameObject.scene.IsValid()) return transform.gameObject;
            return null;
        }
    }
}
