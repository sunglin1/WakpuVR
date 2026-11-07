using UnityEngine;
using UnityEngine.AI;

namespace WakpuVR
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class GhostNavMovement : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float destinationSampleRadius = 2.2f;
        [SerializeField, Min(0.1f)] private float maxHorizontalCorrection = 0.75f;
        [SerializeField, Min(0.1f)] private float maxVerticalCorrection = 2.1f;
        [SerializeField, Min(0.1f)] private float agentRecoveryRadius = 2f;
        [SerializeField, Min(0.1f)] private float stuckSeconds = 2f;
        private NavMeshAgent agent;
        private NavMeshPath path;
        private Vector3 lastPosition;
        private float lastProgressTime;
        private bool hasIssuedDestination;

        public bool HasArrived => hasIssuedDestination && agent != null && agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f;
        public bool IsMoving => agent != null && agent.isOnNavMesh && agent.hasPath && !agent.isStopped;

        private void Awake() { agent = GetComponent<NavMeshAgent>(); path = new NavMeshPath(); lastPosition = transform.position; lastProgressTime = Time.time; }

        private void Update()
        {
            if (!IsMoving) return;
            if ((transform.position - lastPosition).sqrMagnitude > 0.01f)
            {
                lastPosition = transform.position;
                lastProgressTime = Time.time;
            }
            else if (Time.time - lastProgressTime > stuckSeconds)
            {
                Stop();
            }
        }

        public bool TryMoveTo(Vector3 worldDestination)
        {
            if (agent == null || !EnsureOnNavMesh()) return false;
            bool found = NavMesh.SamplePosition(worldDestination, out NavMeshHit hit, destinationSampleRadius, agent.areaMask);
            Vector3 correction = found ? hit.position - worldDestination : Vector3.zero;
            if (!found || new Vector2(correction.x, correction.z).magnitude > maxHorizontalCorrection ||
                Mathf.Abs(correction.y) > maxVerticalCorrection)
            {
                Stop();
                return false;
            }
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
            {
                Stop();
                return false;
            }
            agent.isStopped = false;
            bool accepted = agent.SetDestination(hit.position);
            if (!accepted) Stop();
            else { hasIssuedDestination = true; lastPosition = transform.position; lastProgressTime = Time.time; }
            return accepted;
        }

        public void Stop()
        {
            hasIssuedDestination = false;
            if (agent == null || !agent.isOnNavMesh) return;
            agent.isStopped = true;
            agent.ResetPath();
        }

        private bool EnsureOnNavMesh()
        {
            if (agent.isOnNavMesh) return true;
            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, agentRecoveryRadius, agent.areaMask)) return false;
            return agent.Warp(hit.position + Vector3.up * agent.baseOffset);
        }
    }
}
