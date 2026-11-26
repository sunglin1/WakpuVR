using System.Collections.Generic;
using UnityEngine;

namespace WakpuVR
{
    public sealed class GhostAI : MonoBehaviour
    {
        public enum GhostState { Idle, Investigate, Chase, Attack, Patrol }

        [SerializeField] private Transform player;
        [SerializeField] private PrototypeGameOver gameOver;
        [SerializeField, Min(0f)] private float attackDistance = 0.7f;
        [SerializeField, Min(0f)] private float hearingRange = 10f;
        [SerializeField, Min(0f)] private float sightDistance = 7f;
        [SerializeField, Range(0f, 360f)] private float sightAngle = 130f;
        [SerializeField, Min(0f)] private float memorySeconds = 4f;
        [SerializeField, Min(0f)] private float investigateWait = 2f;
        [SerializeField, Min(0.01f)] private float pathRefreshInterval = 0.2f;
        [SerializeField] private LayerMask obstacleMask = 1;
        [SerializeField] private GhostNavMovement movement;
        [SerializeField] private List<Transform> patrolPoints = new List<Transform>();
        [SerializeField, Min(0.05f)] private float patrolArrivalDistance = 0.6f;

        private Vector3 destination;
        private float lastSeenTime;
        private float arrivedTime;
        private float nextPathTime;
        private GhostState state;
        private int patrolIndex = -1;
        private Vector3 patrolDestination;
        private bool patrolDestinationIssued;

        public GhostState State => state;
        public bool IsChasing => state == GhostState.Chase;

        public void Configure(Transform target, PrototypeGameOver gameState)
        {
            player = target;
            gameOver = gameState;
        }

        private void Awake() { if (movement == null) movement = GetComponent<GhostNavMovement>(); }

        private void OnEnable() => NoiseSystem.NoiseGenerated += OnNoise;
        private void OnDisable() => NoiseSystem.NoiseGenerated -= OnNoise;

        private void Update()
        {
            if (player == null || Time.timeScale <= 0f) { movement?.Stop(); return; }
            if (gameOver != null && gameOver.IsGameOver) { movement?.Stop(); return; }
            if (state == GhostState.Attack) return;

            if (state == GhostState.Idle && HasPatrolPoints()) EnterPatrol(false);

            bool visible = CanSeePlayer();
            if (visible)
            {
                if (state != GhostState.Chase)
                {
                    movement?.Stop();
                    nextPathTime = 0f;
                    patrolDestinationIssued = false;
                }
                lastSeenTime = Time.time;
                destination = player.position;
                state = GhostState.Chase;
                arrivedTime = 0f;
            }

            if (state == GhostState.Chase && !visible && Time.time - lastSeenTime > memorySeconds)
            {
                state = GhostState.Investigate;
                arrivedTime = 0f;
            }

            if (state == GhostState.Chase && HorizontalDistance(player.position) <= attackDistance)
            {
                state = GhostState.Attack;
                movement?.Stop();
                gameOver?.EndGame();
                return;
            }

            if (state == GhostState.Patrol)
            {
                UpdatePatrol();
                return;
            }
            if (state == GhostState.Idle) return;
            if (state == GhostState.Chase && visible) destination = player.position;

            if (state == GhostState.Investigate && arrivedTime > 0f && Time.time - arrivedTime >= investigateWait)
            {
                state = GhostState.Idle;
                movement?.Stop();
                if (HasPatrolPoints()) EnterPatrol(true);
                return;
            }

            if (HorizontalDistance(destination) < 0.25f || (movement != null && movement.HasArrived && state == GhostState.Investigate))
            {
                if (arrivedTime <= 0f) arrivedTime = Time.time;
                return;
            }
            if (Time.time >= nextPathTime)
            {
                nextPathTime = Time.time + pathRefreshInterval;
                if (movement == null || !movement.TryMoveTo(destination))
                {
                    if (state == GhostState.Chase) state = GhostState.Investigate;
                    if (arrivedTime <= 0f) arrivedTime = Time.time;
                }
                else arrivedTime = 0f;
            }
        }

        private bool HasPatrolPoints()
        {
            if (patrolPoints == null) return false;
            for (int i = 0; i < patrolPoints.Count; i++)
                if (patrolPoints[i] != null) return true;
            return false;
        }

        private void EnterPatrol(bool resumeNearest)
        {
            float nearestDistance = float.PositiveInfinity;
            patrolIndex = -1;
            for (int i = 0; i < patrolPoints.Count; i++)
            {
                if (patrolPoints[i] == null) continue;
                if (!resumeNearest) { patrolIndex = i; break; }
                float distance = (patrolPoints[i].position - transform.position).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearestDistance = distance;
                patrolIndex = i;
            }
            if (patrolIndex < 0) return;
            movement?.Stop();
            patrolDestinationIssued = false;
            state = GhostState.Patrol;
            nextPathTime = 0f;
        }

        private void UpdatePatrol()
        {
            if (!HasPatrolPoints())
            {
                state = GhostState.Idle;
                patrolIndex = -1;
                movement?.Stop();
                return;
            }
            if (patrolIndex < 0 || patrolIndex >= patrolPoints.Count || patrolPoints[patrolIndex] == null)
                AdvancePatrolPoint();
            if (patrolIndex < 0) return;

            Vector3 target = patrolPoints[patrolIndex].position;
            if ((HorizontalDistance(target) <= patrolArrivalDistance && Mathf.Abs(target.y - transform.position.y) < 1.5f) ||
                (patrolDestinationIssued && movement != null && movement.HasArrived))
            {
                AdvancePatrolPoint();
                if (patrolIndex < 0) return;
                target = patrolPoints[patrolIndex].position;
                patrolDestinationIssued = false;
                nextPathTime = 0f;
            }
            if (patrolDestinationIssued && movement != null && movement.IsMoving &&
                (target - patrolDestination).sqrMagnitude < 0.0625f) return;
            if (Time.time < nextPathTime) return;
            nextPathTime = Time.time + pathRefreshInterval;
            patrolDestinationIssued = movement != null && movement.TryMoveTo(target);
            if (patrolDestinationIssued) patrolDestination = target;
        }

        private void AdvancePatrolPoint()
        {
            for (int step = 1; step <= patrolPoints.Count; step++)
            {
                int candidate = (patrolIndex + step) % patrolPoints.Count;
                if (patrolPoints[candidate] == null) continue;
                patrolIndex = candidate;
                return;
            }
            patrolIndex = -1;
        }

        private void OnNoise(NoiseEvent noise)
        {
            if (state == GhostState.Attack || state == GhostState.Chase || (gameOver != null && gameOver.IsGameOver)) return;
            if (Vector3.Distance(transform.position, noise.Position) > Mathf.Min(hearingRange, noise.Radius)) return;
            destination = noise.Position;
            arrivedTime = 0f;
            state = GhostState.Investigate;
            nextPathTime = 0f;
        }

        private bool CanSeePlayer()
        {
            Vector3 origin = transform.position + Vector3.up * 0.25f;
            Vector3 target = player.position;
            Vector3 toPlayer = target - origin;
            if (toPlayer.magnitude > sightDistance || Vector3.Angle(transform.forward, toPlayer) > sightAngle * 0.5f) return false;
            return !Physics.Raycast(origin, toPlayer.normalized, toPlayer.magnitude, obstacleMask, QueryTriggerInteraction.Ignore);
        }

        private float HorizontalDistance(Vector3 point)
        {
            Vector3 delta = point - transform.position;
            delta.y = 0f;
            return delta.magnitude;
        }
    }
}
