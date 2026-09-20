using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sprint0.Prototype
{
    [DisallowMultipleComponent]
    public sealed class TentacleProceduralController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] GameObject tentaclePrefab;
        [SerializeField] Camera aimingCamera;
        [SerializeField] LayerMask aimMask = ~0;
        [SerializeField] LayerMask bodyMask;

        [Header("Aim")]
        [SerializeField, Min(0.5f)] float fallbackAimDistance = 15f;
        [SerializeField, Min(0f)] float targetSmoothTime = 0.075f;
        [SerializeField, Min(0f)] float targetMaxSpeed = 80f;

        [Header("Chain")]
        [SerializeField, Range(1, 32)] int solverIterations = 12;
        [SerializeField, Min(0.00001f)] float solverTolerance = 0.001f;
        [SerializeField, Min(0.001f)] float segmentSmoothTime = 0.055f;
        [SerializeField, Min(0f)] float segmentMaxSpeed = 60f;
        [SerializeField, Range(0f, 1f)] float rotationWeight = 1f;
        [SerializeField, Range(0.5f, 4f)] float motionResponsiveness = 1.8f;

        [Header("Body Avoidance")]
        [SerializeField, Min(0.001f)] float collisionRadius = 0.12f;
        [SerializeField, Min(0f)] float collisionSkin = 0.025f;

        [Header("Debug")]
        [SerializeField] bool drawDebug = true;
        [SerializeField] bool showDebugHud = true;

        readonly List<Transform> chain = new();
        Transform visualRoot;
        float[] segmentLengths;
        Vector3[] solvedPoints;
        Vector3[] smoothedPoints;
        Vector3[] previousSmoothedPoints;
        Vector3[] pointVelocities;
        readonly Collider[] overlapBuffer = new Collider[16];
        GameObject collisionProbeObject;
        SphereCollider collisionProbe;
        Vector3 smoothedTarget;
        Vector3 targetVelocity;
        Vector3 rawTarget;
        bool initialized;
        int collisionCorrections;
        bool useExternalTarget;
        Vector3 externalTarget;
        bool slotActive = true;

        public Vector3 CurrentTarget => smoothedTarget;
        public int BoneCount => chain.Count;
        public bool SlotActive => slotActive;
        public Transform Tip
        {
            get
            {
                EnsureInitialized();
                return chain.Count > 0 ? chain[^1] : null;
            }
        }

        public void SetLocalControl(bool localControl)
        {
            useExternalTarget = !localControl;
            showDebugHud = slotActive && localControl;
        }

        public void SetSlotActive(bool value)
        {
            slotActive = value;
            showDebugHud = slotActive && !useExternalTarget;
            EnsureInitialized();
            SetVisualVisibility(slotActive);
        }

        public void SetExternalTarget(Vector3 target)
        {
            externalTarget = target;
            useExternalTarget = true;
        }

        public void ReplaceVisual(GameObject replacementPrefab)
        {
            if (replacementPrefab == null)
            {
                return;
            }

            var preservedLocalPosition = Vector3.zero;
            var preservedLocalRotation = Quaternion.identity;
            var preservedLocalScale = Vector3.one;
            if (visualRoot != null)
            {
                preservedLocalPosition = visualRoot.localPosition;
                preservedLocalRotation = visualRoot.localRotation;
                preservedLocalScale = visualRoot.localScale;
                visualRoot.gameObject.SetActive(false);
                Destroy(visualRoot.gameObject);
            }

            var instance = Instantiate(replacementPrefab, transform);
            instance.name = replacementPrefab.name;
            instance.transform.SetLocalPositionAndRotation(preservedLocalPosition, preservedLocalRotation);
            instance.transform.localScale = preservedLocalScale;
            visualRoot = instance.transform;
            chain.Clear();
            initialized = false;
            EnsureInitialized();
            SetVisualVisibility(slotActive);
        }

        void Awake()
        {
            EnsureInitialized();
        }

        void OnEnable()
        {
            EnsureInitialized();
        }

        void LateUpdate()
        {
            if (!slotActive || !EnsureInitialized() || aimingCamera == null)
            {
                return;
            }

            rawTarget = useExternalTarget ? externalTarget : FindAimPoint();
            smoothedTarget = Vector3.SmoothDamp(
                smoothedTarget,
                rawTarget,
                ref targetVelocity,
                targetSmoothTime / motionResponsiveness,
                targetMaxSpeed * motionResponsiveness,
                Time.deltaTime);

            for (var i = 0; i < chain.Count; i++)
            {
                solvedPoints[i] = chain[i].position;
            }

            var anchor = chain[0].position;
            Array.Copy(smoothedPoints, previousSmoothedPoints, smoothedPoints.Length);
            TentacleChainSolver.Solve(
                solvedPoints,
                segmentLengths,
                anchor,
                smoothedTarget,
                solverIterations,
                solverTolerance);

            collisionCorrections = ApplyBodyAvoidance(solvedPoints, previousSmoothedPoints);
            SmoothPoints(anchor);
            collisionCorrections += ApplyBodyAvoidance(smoothedPoints, previousSmoothedPoints);
            ApplyRotations();
        }

        bool EnsureInitialized()
        {
            if (initialized && chain.Count >= 2 && chain[0] != null)
            {
                if (aimingCamera == null)
                {
                    aimingCamera = Camera.main;
                }

                return aimingCamera != null;
            }

            if (visualRoot == null)
            {
                var existing = transform.Find("Tentacle_Standard_Rig");
                if (existing != null)
                {
                    visualRoot = existing;
                }
                else if (tentaclePrefab != null)
                {
                    var instance = Instantiate(tentaclePrefab, transform);
                    instance.name = "Tentacle_Standard_Rig";
                    instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    visualRoot = instance.transform;
                }
            }

            if (visualRoot == null)
            {
                return false;
            }

            chain.Clear();
            var bone = FindDescendant(visualRoot, "Bone");
            if (bone == null)
            {
                Debug.LogError("[Step 1] Tentacle root bone named 'Bone' was not found.", this);
                return false;
            }

            chain.Add(bone);
            for (var index = 1; index <= 5; index++)
            {
                var next = FindDirectChild(bone, $"Bone.{index:000}");
                if (next == null)
                {
                    break;
                }

                chain.Add(next);
                bone = next;
            }

            var end = FindDirectChild(bone, "Bone.005_end");
            if (end != null)
            {
                chain.Add(end);
            }

            if (chain.Count < 2)
            {
                Debug.LogError("[Step 1] At least two connected tentacle bones are required.", this);
                return false;
            }

            segmentLengths = new float[chain.Count - 1];
            solvedPoints = new Vector3[chain.Count];
            smoothedPoints = new Vector3[chain.Count];
            previousSmoothedPoints = new Vector3[chain.Count];
            pointVelocities = new Vector3[chain.Count];

            for (var i = 0; i < chain.Count; i++)
            {
                solvedPoints[i] = chain[i].position;
                smoothedPoints[i] = chain[i].position;
                previousSmoothedPoints[i] = chain[i].position;
                if (i > 0)
                {
                    segmentLengths[i - 1] = Mathf.Max(
                        Vector3.Distance(chain[i - 1].position, chain[i].position),
                        0.0001f);
                }
            }

            if (aimingCamera == null)
            {
                aimingCamera = Camera.main;
            }

            rawTarget = chain[^1].position;
            smoothedTarget = rawTarget;
            initialized = true;
            return aimingCamera != null;
        }

        void SetVisualVisibility(bool visible)
        {
            if (visualRoot == null)
            {
                return;
            }

            foreach (var renderer in visualRoot.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = visible;
            }
        }

        Vector3 FindAimPoint()
        {
            var mouse = Mouse.current;
            var screenPoint = mouse != null && !mouse.rightButton.isPressed
                ? mouse.position.ReadValue()
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var ray = aimingCamera.ScreenPointToRay(screenPoint);
            var hits = Physics.RaycastAll(ray, 250f, aimMask, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            foreach (var hit in hits)
            {
                if (!hit.transform.IsChildOf(transform.root))
                {
                    return hit.point;
                }
            }

            return ray.GetPoint(fallbackAimDistance);
        }

        int ApplyBodyAvoidance(Vector3[] points, Vector3[] previousPoints)
        {
            var corrections = 0;
            for (var i = 1; i < points.Length; i++)
            {
                // Sweep from last frame's valid position first. This prevents a fast-moving
                // point from teleporting through the body before the segment cast runs.
                corrections += SweepPoint(previousPoints[i], ref points[i]);

                var origin = points[i - 1];
                var delta = points[i] - origin;
                var distance = delta.magnitude;
                if (distance > 0.0001f && Physics.SphereCast(
                    origin,
                    collisionRadius,
                    delta / distance,
                    out var hit,
                    distance,
                    bodyMask,
                    QueryTriggerInteraction.Ignore)
                    && Vector3.Dot(delta, hit.normal) < -0.0001f)
                {
                    points[i] = hit.point + hit.normal * (collisionRadius + collisionSkin);
                    corrections++;
                }

                corrections += ResolveBodyOverlap(ref points[i]);

                var fromPrevious = points[i] - origin;
                if (fromPrevious.sqrMagnitude > 0.000001f)
                {
                    points[i] = origin + fromPrevious.normalized * segmentLengths[i - 1];
                }

                // Restoring the bone length can move the point back into the collider.
                corrections += ResolveBodyOverlap(ref points[i]);
            }

            return corrections;
        }

        int SweepPoint(Vector3 origin, ref Vector3 destination)
        {
            var delta = destination - origin;
            var distance = delta.magnitude;
            if (distance <= 0.0001f || !Physics.SphereCast(
                origin,
                collisionRadius,
                delta / distance,
                out var hit,
                distance,
                bodyMask,
                QueryTriggerInteraction.Ignore)
                || Vector3.Dot(delta, hit.normal) >= -0.0001f)
            {
                return 0;
            }

            destination = hit.point + hit.normal * (collisionRadius + collisionSkin);
            return 1;
        }

        int ResolveBodyOverlap(ref Vector3 point)
        {
            EnsureCollisionProbe();
            var overlapCount = Physics.OverlapSphereNonAlloc(
                point,
                collisionRadius,
                overlapBuffer,
                bodyMask,
                QueryTriggerInteraction.Ignore);
            var corrections = 0;

            for (var pass = 0; pass < 2; pass++)
            {
                var changed = false;
                for (var i = 0; i < overlapCount; i++)
                {
                    var bodyCollider = overlapBuffer[i];
                    if (bodyCollider == null || !Physics.ComputePenetration(
                        collisionProbe,
                        point,
                        Quaternion.identity,
                        bodyCollider,
                        bodyCollider.transform.position,
                        bodyCollider.transform.rotation,
                        out var direction,
                        out var distance))
                    {
                        continue;
                    }

                    point += direction * (distance + collisionSkin);
                    corrections++;
                    changed = true;
                }

                if (!changed)
                {
                    break;
                }

                overlapCount = Physics.OverlapSphereNonAlloc(
                    point,
                    collisionRadius,
                    overlapBuffer,
                    bodyMask,
                    QueryTriggerInteraction.Ignore);
            }

            return corrections;
        }

        void EnsureCollisionProbe()
        {
            if (collisionProbe != null)
            {
                collisionProbe.radius = collisionRadius;
                return;
            }

            collisionProbeObject = new GameObject("TentacleCollisionProbe")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = Physics.IgnoreRaycastLayer
            };
            collisionProbeObject.transform.localScale = Vector3.one;
            collisionProbe = collisionProbeObject.AddComponent<SphereCollider>();
            collisionProbe.radius = collisionRadius;
            collisionProbe.enabled = false;
        }

        void OnDestroy()
        {
            if (collisionProbeObject != null)
            {
                Destroy(collisionProbeObject);
            }
        }

        void SmoothPoints(Vector3 anchor)
        {
            smoothedPoints[0] = anchor;
            for (var i = 1; i < smoothedPoints.Length; i++)
            {
                smoothedPoints[i] = Vector3.SmoothDamp(
                    smoothedPoints[i],
                    solvedPoints[i],
                    ref pointVelocities[i],
                    segmentSmoothTime / motionResponsiveness,
                    segmentMaxSpeed * motionResponsiveness,
                    Time.deltaTime);

                var offset = smoothedPoints[i] - smoothedPoints[i - 1];
                if (offset.sqrMagnitude < 0.000001f)
                {
                    offset = solvedPoints[i] - solvedPoints[i - 1];
                }

                smoothedPoints[i] = smoothedPoints[i - 1]
                    + offset.normalized * segmentLengths[i - 1];
            }
        }

        void ApplyRotations()
        {
            for (var i = 0; i < chain.Count - 1; i++)
            {
                var currentDirection = chain[i + 1].position - chain[i].position;
                var desiredDirection = smoothedPoints[i + 1] - smoothedPoints[i];
                if (currentDirection.sqrMagnitude < 0.000001f || desiredDirection.sqrMagnitude < 0.000001f)
                {
                    continue;
                }

                var delta = Quaternion.FromToRotation(currentDirection, desiredDirection);
                var weightedDelta = Quaternion.Slerp(Quaternion.identity, delta, rotationWeight);
                chain[i].rotation = weightedDelta * chain[i].rotation;
            }
        }

        static Transform FindDescendant(Transform root, string objectName)
        {
            foreach (var item in root.GetComponentsInChildren<Transform>(true))
            {
                if (item.name == objectName)
                {
                    return item;
                }
            }

            return null;
        }

        static Transform FindDirectChild(Transform parent, string objectName)
        {
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == objectName)
                {
                    return child;
                }
            }

            return null;
        }

        void OnGUI()
        {
            if (!showDebugHud)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(16f, 16f, 420f, 108f), GUI.skin.box);
            GUILayout.Label("Step 1 · 촉수 시점 추적");
            GUILayout.Label("우클릭 드래그: 카메라 회전 / 휠: 줌 / 마우스: 촉수 조준");
            GUILayout.Label($"본: {BoneCount} · 본체 충돌 보정: {collisionCorrections}");
            GUILayout.EndArea();
        }

        void OnDrawGizmosSelected()
        {
            if (!drawDebug || smoothedPoints == null || smoothedPoints.Length < 2)
            {
                return;
            }

            Gizmos.color = collisionCorrections > 0 ? Color.red : Color.cyan;
            for (var i = 1; i < smoothedPoints.Length; i++)
            {
                Gizmos.DrawLine(smoothedPoints[i - 1], smoothedPoints[i]);
                Gizmos.DrawWireSphere(smoothedPoints[i], collisionRadius);
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(smoothedTarget, collisionRadius * 0.75f);
        }
    }
}
