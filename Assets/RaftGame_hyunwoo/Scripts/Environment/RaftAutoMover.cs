using System.Collections.Generic;
using UnityEngine;

namespace RaftSharkDive
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class RaftAutoMover : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float forwardSpeed = 2.5f;
        [SerializeField] private Vector3 localForward = Vector3.forward;
        [SerializeField, Min(0f)] private float fortressStopDistance;
        [SerializeField, Min(0f)] private float passengerEdgeTolerance = 0.35f;
        [SerializeField, Min(0f)] private float passengerHeightTolerance = 0.75f;
        [Header("Boarding")]
        [SerializeField, Range(0.25f, 0.75f)] private float highStepDepth = 0.46f;
        [SerializeField, Range(0.25f, 0.75f)] private float lowStepDepth = 0.54f;
        [SerializeField, Range(0.1f, 0.3f)] private float boardingStepHeight = 0.18f;
        [SerializeField, Range(0.1f, 0.3f)] private float boardingStepDrop = 0.18f;

        private readonly List<PlayerController> passengers = new List<PlayerController>(2);
        private BoxCollider deckCollider;

        public bool IsMoving { get; private set; }
        public float DistanceTravelled { get; private set; }
        public float ForwardSpeed => forwardSpeed;
        public bool HasReachedFortress { get; private set; }
        public int BoardingRampCount => transform.Find("AllSideBoarding/Front") != null &&
                                        transform.Find("AllSideBoarding/Back") != null &&
                                        transform.Find("AllSideBoarding/Left") != null &&
                                        transform.Find("AllSideBoarding/Right") != null ? 4 : 0;
        public int BoardingStepColliderCount => CountBoardingStepColliders();

        private void Awake()
        {
            deckCollider = GetComponent<BoxCollider>();
            RefreshBoardingRamps();
        }

        public void RefreshBoardingRamps()
        {
            if (deckCollider == null) deckCollider = GetComponent<BoxCollider>();
            EnsureAllSideBoarding();
        }

        private void EnsureAllSideBoarding()
        {
            DisableLegacyAirRamps();
            Transform boardingRoot = transform.Find("AllSideBoarding");
            if (boardingRoot == null)
            {
                boardingRoot = new GameObject("AllSideBoarding").transform;
                boardingRoot.SetParent(transform, false);
            }

            Vector3 deckCenter = deckCollider != null ? deckCollider.center : Vector3.zero;
            float deckHalfX = deckCollider != null ? deckCollider.size.x * 0.5f : 4f;
            float deckHalfZ = deckCollider != null ? deckCollider.size.z * 0.5f : 4f;
            float deckTop = deckCollider != null
                ? deckCenter.y + deckCollider.size.y * 0.5f
                : 0.175f;
            float overlap = 0.06f;
            float highTop = deckTop - 0.16f;
            float lowTop = highTop - boardingStepDrop;
            float highY = highTop - boardingStepHeight * 0.5f;
            float lowY = lowTop - boardingStepHeight * 0.5f;
            float highOffset = highStepDepth * 0.5f - overlap;
            float lowOffset = highStepDepth - overlap * 2f + lowStepDepth * 0.5f;

            CreateTwoStepEdge(boardingRoot, "Front",
                new Vector3(deckCenter.x, highY, deckCenter.z + deckHalfZ + highOffset),
                new Vector3(deckHalfX * 2f + 0.1f, boardingStepHeight, highStepDepth),
                new Vector3(deckCenter.x, lowY, deckCenter.z + deckHalfZ + lowOffset),
                new Vector3(deckHalfX * 2f + 0.1f, boardingStepHeight, lowStepDepth));
            CreateTwoStepEdge(boardingRoot, "Back",
                new Vector3(deckCenter.x, highY, deckCenter.z - deckHalfZ - highOffset),
                new Vector3(deckHalfX * 2f + 0.1f, boardingStepHeight, highStepDepth),
                new Vector3(deckCenter.x, lowY, deckCenter.z - deckHalfZ - lowOffset),
                new Vector3(deckHalfX * 2f + 0.1f, boardingStepHeight, lowStepDepth));
            CreateTwoStepEdge(boardingRoot, "Left",
                new Vector3(deckCenter.x - deckHalfX - highOffset, highY, deckCenter.z),
                new Vector3(highStepDepth, boardingStepHeight, deckHalfZ * 2f + 0.1f),
                new Vector3(deckCenter.x - deckHalfX - lowOffset, lowY, deckCenter.z),
                new Vector3(lowStepDepth, boardingStepHeight, deckHalfZ * 2f + 0.1f));
            CreateTwoStepEdge(boardingRoot, "Right",
                new Vector3(deckCenter.x + deckHalfX + highOffset, highY, deckCenter.z),
                new Vector3(highStepDepth, boardingStepHeight, deckHalfZ * 2f + 0.1f),
                new Vector3(deckCenter.x + deckHalfX + lowOffset, lowY, deckCenter.z),
                new Vector3(lowStepDepth, boardingStepHeight, deckHalfZ * 2f + 0.1f));
        }

        private void DisableLegacyAirRamps()
        {
            string[] legacyNames = { "BoardingRamp", "ClimbStepLow", "ClimbStepHigh" };
            for (int i = 0; i < legacyNames.Length; i++)
            {
                Transform legacy = transform.Find(legacyNames[i]);
                if (legacy != null) legacy.gameObject.SetActive(false);
            }
        }

        private int CountBoardingStepColliders()
        {
            string[] directions = { "Front", "Back", "Left", "Right" };
            string[] levels = { "High", "Low" };
            int count = 0;
            for (int i = 0; i < directions.Length; i++)
            {
                for (int j = 0; j < levels.Length; j++)
                {
                    Transform step = transform.Find($"AllSideBoarding/{directions[i]}/{levels[j]}");
                    BoxCollider collider = step != null ? step.GetComponent<BoxCollider>() : null;
                    if (collider != null && collider.enabled) count++;
                }
            }
            return count;
        }

        private static void CreateTwoStepEdge(Transform parent, string edgeName,
            Vector3 highPosition, Vector3 highSize, Vector3 lowPosition, Vector3 lowSize)
        {
            Transform existing = parent.Find(edgeName);
            GameObject edge = existing != null ? existing.gameObject : new GameObject(edgeName);
            edge.transform.SetParent(parent, false);
            edge.transform.localPosition = Vector3.zero;
            edge.transform.localRotation = Quaternion.identity;
            BoxCollider oldCollider = edge.GetComponent<BoxCollider>();
            if (oldCollider != null) oldCollider.enabled = false;
            CreateStep(edge.transform, "High", highPosition, highSize);
            CreateStep(edge.transform, "Low", lowPosition, lowSize);
        }

        private static void CreateStep(Transform parent, string stepName, Vector3 localPosition,
            Vector3 size)
        {
            Transform existing = parent.Find(stepName);
            GameObject step = existing != null ? existing.gameObject : new GameObject(stepName);
            step.transform.SetParent(parent, false);
            step.transform.localPosition = localPosition;
            step.transform.localRotation = Quaternion.identity;
            BoxCollider collider = step.GetComponent<BoxCollider>();
            if (collider == null) collider = step.AddComponent<BoxCollider>();
            collider.enabled = true;
            collider.center = Vector3.zero;
            collider.size = size;
            collider.isTrigger = false;
        }

        private void Update()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IslandDescentComplete)
            {
                IsMoving = false;
                return;
            }

            Vector3 direction = localForward.sqrMagnitude > 0.0001f
                ? transform.TransformDirection(localForward.normalized)
                : transform.forward;
            float travelDistance = forwardSpeed * Time.deltaTime;
            if (GameManager.Instance.FortressVisible &&
                GameManager.Instance.TryGetFortressTransform(out Vector3 fortressPosition, out _))
            {
                Vector3 toFortress = Vector3.ProjectOnPlane(fortressPosition - transform.position, Vector3.up);
                float forwardRemaining = Vector3.Dot(toFortress, direction);
                float allowedTravel = forwardRemaining - fortressStopDistance;
                if (allowedTravel <= 0.0001f)
                {
                    HasReachedFortress = true;
                    IsMoving = false;
                    return;
                }
                travelDistance = Mathf.Min(travelDistance, allowedTravel);
            }
            else
            {
                HasReachedFortress = false;
            }

            Vector3 displacement = direction * travelDistance;
            CollectPassengers();
            transform.position += displacement;
            for (int i = 0; i < passengers.Count; i++)
                passengers[i].MoveWithPlatform(displacement);

            DistanceTravelled += displacement.magnitude;
            IsMoving = displacement.sqrMagnitude > 0.000001f;
        }

        private void CollectPassengers()
        {
            passengers.Clear();
            if (deckCollider == null) return;

            Bounds deckBounds = deckCollider.bounds;
            IReadOnlyList<PlayerController> players = PlayerController.ActivePlayers;
            for (int i = 0; i < players.Count; i++)
            {
                PlayerController player = players[i];
                if (player == null || !player.isActiveAndEnabled || player.IsSwimming) continue;
                CharacterController character = player.GetComponent<CharacterController>();
                if (character == null || !character.enabled) continue;

                Bounds playerBounds = character.bounds;
                Vector3 center = playerBounds.center;
                bool aboveDeck = center.x >= deckBounds.min.x - passengerEdgeTolerance &&
                                 center.x <= deckBounds.max.x + passengerEdgeTolerance &&
                                 center.z >= deckBounds.min.z - passengerEdgeTolerance &&
                                 center.z <= deckBounds.max.z + passengerEdgeTolerance;
                float feetHeight = playerBounds.min.y;
                bool atDeckHeight = feetHeight >= deckBounds.max.y - passengerHeightTolerance &&
                                    feetHeight <= deckBounds.max.y + passengerHeightTolerance;
                if (aboveDeck && atDeckHeight) passengers.Add(player);
            }
        }
    }
}
