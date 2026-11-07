using UnityEngine;

namespace WakpuVR
{
    public sealed class GhostAI : MonoBehaviour
    {
        public enum GhostState { Idle, Investigate, Chase, Attack }

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

        private Vector3 destination;
        private float lastSeenTime;
        private float arrivedTime;
        private float nextPathTime;
        private GhostState state;

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

            bool visible = CanSeePlayer();
            if (visible)
            {
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

            if (state == GhostState.Idle) return;
            if (state == GhostState.Chase && visible) destination = player.position;

            if (state == GhostState.Investigate && arrivedTime > 0f && Time.time - arrivedTime >= investigateWait)
            {
                state = GhostState.Idle;
                movement?.Stop();
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
