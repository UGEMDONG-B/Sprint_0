using UnityEngine;

namespace RaftSharkDive
{
    public sealed class SharkAI : MonoBehaviour
    {
        public enum SharkState { Patrol, Detect, Chase, Attack, Return }

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float patrolSpeed = 2.2f;
        [SerializeField, Min(0.1f)] private float chaseSpeed = 3.6f;
        [SerializeField, Min(1f)] private float patrolRadius = 14f;
        [SerializeField, Min(10f)] private float turnSpeed = 160f;
        [Header("Combat")]
        [SerializeField, Min(1f)] private float detectionRange = 18f;
        [SerializeField, Min(1f)] private float loseRange = 28f;
        [SerializeField, Min(0.5f)] private float attackRange = 2f;
        [SerializeField, Min(1f)] private float attackDamage = 22f;
        [SerializeField, Min(0.1f)] private float attackCooldown = 1.8f;
        [SerializeField, Min(0.1f)] private float targetLockDuration = 1f;
        [SerializeField, Min(0f)] private float retargetDistanceAdvantage = 3f;
        [Header("Voyage Follow")]
        [SerializeField, Min(20f)] private float voyageCatchUpDistance = 65f;
        [SerializeField, Min(5f)] private float voyageCatchUpBehindDistance = 20f;
        [SerializeField, Min(1f)] private float voyageDepth = 4f;
        [SerializeField, Min(0.5f)] private float voyageCatchUpCooldown = 3f;
        [SerializeField, Min(0f)] private float catchUpAttackGraceDuration = 4f;

        private PlayerController targetPlayer;
        private Vector3 home;
        private Vector3 patrolTarget;
        private float nextAttackTime;
        private float targetLockedUntil;
        private float nextVoyageCatchUpTime;
        private float attackSuppressedUntil;
        private RaftUpgradeStation raftStation;

        public SharkState State { get; private set; }
        public bool IsChasing => State == SharkState.Chase || State == SharkState.Attack;
        public PlayerController CurrentTarget => targetPlayer;
        public int VoyageCatchUpCount { get; private set; }
        public float GetPursuitSpeedFor(PlayerController player) => GetChaseSpeed(player);

        private void Start()
        {
            home = transform.position;
            raftStation = FindFirstObjectByType<RaftUpgradeStation>(FindObjectsInactive.Include);
            ChoosePatrolTarget();
        }

        private void Update()
        {
            MaintainVoyageProximity();
            targetPlayer = SelectTarget();
            bool pursuitEnabled = targetPlayer != null;

            if (!pursuitEnabled)
            {
                State = SharkState.Patrol;
                MoveTowards(patrolTarget, patrolSpeed);
                if (Vector3.Distance(transform.position, patrolTarget) < 1f) ChoosePatrolTarget();
                return;
            }

            float distance = Vector3.Distance(transform.position, targetPlayer.transform.position);

            if (State == SharkState.Patrol || State == SharkState.Return || State == SharkState.Detect)
                State = SharkState.Chase;

            switch (State)
            {
                case SharkState.Patrol:
                    break;
                case SharkState.Detect:
                    break;
                case SharkState.Chase:
                    MoveTowards(targetPlayer.transform.position, GetChaseSpeed(targetPlayer));
                    if (distance <= attackRange) State = SharkState.Attack;
                    break;
                case SharkState.Attack:
                    if (Time.time >= nextAttackTime)
                    {
                        bool targetIsSwimming = targetPlayer.IsSwimming;
                        bool targetIsOnRaft = raftStation != null && raftStation.gameObject.activeInHierarchy &&
                                              Vector3.Distance(targetPlayer.transform.position,
                                                  raftStation.transform.position) < 9f;
                        if (Time.time >= attackSuppressedUntil)
                        {
                            if (targetIsSwimming)
                                targetPlayer.GetComponent<PlayerHealth>()?.Damage(attackDamage);
                            else if (targetIsOnRaft)
                                raftStation.Damage(10f);
                        }
                        nextAttackTime = Time.time + attackCooldown;
                    }
                    State = SharkState.Chase;
                    break;
                case SharkState.Return:
                    MoveTowards(home, patrolSpeed);
                    if (Vector3.Distance(transform.position, home) < 1.5f)
                    {
                        ChoosePatrolTarget();
                        State = SharkState.Patrol;
                    }
                    break;
            }
        }

        private PlayerController SelectTarget()
        {
            bool voyageActive = GameManager.Instance != null && GameManager.Instance.IslandDescentComplete;
            bool currentValid = IsEligible(targetPlayer, voyageActive) &&
                                (voyageActive || Vector3.Distance(transform.position, targetPlayer.transform.position) <= loseRange);
            PlayerController nearest = null;
            float nearestDistance = voyageActive ? float.MaxValue : detectionRange;
            var players = PlayerController.ActivePlayers;
            for (int i = 0; i < players.Count; i++)
            {
                PlayerController candidate = players[i];
                if (!IsEligible(candidate, voyageActive)) continue;
                float distance = Vector3.Distance(candidate.transform.position, transform.position);
                if ((!voyageActive && distance > detectionRange) || distance >= nearestDistance) continue;
                nearest = candidate;
                nearestDistance = distance;
            }

            if (!currentValid)
            {
                if (nearest != targetPlayer) targetLockedUntil = Time.time + targetLockDuration;
                return nearest;
            }
            if (Time.time < targetLockedUntil || nearest == null || nearest == targetPlayer) return targetPlayer;

            float currentDistance = Vector3.Distance(transform.position, targetPlayer.transform.position);
            if (nearestDistance + retargetDistanceAdvantage >= currentDistance) return targetPlayer;
            targetLockedUntil = Time.time + targetLockDuration;
            return nearest;
        }

        private static bool IsEligible(PlayerController candidate, bool voyageActive)
        {
            return candidate != null && candidate.isActiveAndEnabled && (voyageActive || candidate.IsSwimming);
        }

        private void MaintainVoyageProximity()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IslandDescentComplete ||
                raftStation == null || !raftStation.gameObject.activeInHierarchy) return;
            Vector3 flatOffset = transform.position - raftStation.transform.position;
            flatOffset.y = 0f;
            if (flatOffset.magnitude <= voyageCatchUpDistance || Time.time < nextVoyageCatchUpTime) return;

            float side = Mathf.Sign(Vector3.Dot(flatOffset, raftStation.transform.right));
            if (Mathf.Approximately(side, 0f)) side = 1f;
            Vector3 catchUpPosition = raftStation.transform.position -
                                      raftStation.transform.forward * voyageCatchUpBehindDistance +
                                      raftStation.transform.right * (side * 7f);
            catchUpPosition.y = GameManager.Instance.WaterSurfaceY - voyageDepth;
            transform.position = catchUpPosition;
            home = catchUpPosition;
            nextVoyageCatchUpTime = Time.time + voyageCatchUpCooldown;
            attackSuppressedUntil = Time.time + catchUpAttackGraceDuration;
            VoyageCatchUpCount++;
        }

        private void MoveTowards(Vector3 target, float speed)
        {
            if (GameManager.Instance != null)
                target.y = Mathf.Min(target.y, GameManager.Instance.WaterSurfaceY - 0.8f);
            Vector3 direction = target - transform.position;
            if (direction.sqrMagnitude < 0.01f) return;
            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            transform.position += transform.forward * speed * Time.deltaTime;
        }

        private float GetChaseSpeed(PlayerController player)
        {
            if (player == null) return chaseSpeed;
            return Mathf.Min(chaseSpeed, player.SwimSpeed * 0.9f);
        }

        private void ChoosePatrolTarget()
        {
            Vector2 circle = Random.insideUnitCircle * patrolRadius;
            patrolTarget = home + new Vector3(circle.x, Random.Range(-2f, 2f), circle.y);
        }
    }
}
