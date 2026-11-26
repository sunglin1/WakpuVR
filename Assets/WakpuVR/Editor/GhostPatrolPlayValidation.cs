#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace WakpuVR.Editor
{
    [InitializeOnLoad]
    public static class GhostPatrolPlayValidation
    {
        private const string ActiveKey = "WakpuVR.GhostPatrolPlayValidation";
        private static readonly FieldInfo PatrolIndexField = typeof(GhostAI).GetField("patrolIndex", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo PatrolPointsField = typeof(GhostAI).GetField("patrolPoints", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo StateField = typeof(GhostAI).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo CanSeePlayerMethod = typeof(GhostAI).GetMethod("CanSeePlayer", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo UpdateMethod = typeof(GhostAI).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
        private static GhostAI ai;
        private static NavMeshAgent agent;
        private static GameObject target;
        private static Transform[] originalPoints;
        private static int phase;
        private static float phaseStart;
        private static float highestY;
        private static int chasePatrolIndex;
        private static bool sawUpperPoint;
        private static int returnIndex;

        static GhostPatrolPlayValidation()
        {
            if (SessionState.GetBool(ActiveKey, false)) EditorApplication.update += Tick;
        }

        public static void Begin()
        {
            GhostPatrolValidation.Validate();
            SessionState.SetBool(ActiveKey, true);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            try
            {
                if (ai == null) Initialize();
                if (Time.time - phaseStart > 75f) throw new InvalidOperationException($"Play Mode phase {phase} timed out.");
                highestY = Mathf.Max(highestY, ai.transform.position.y);
                int index = (int)PatrolIndexField.GetValue(ai);
                switch (phase)
                {
                    case 0:
                        if (index == 2) sawUpperPoint = true;
                        if (!sawUpperPoint || index != 0 || highestY < 4.5f || ai.transform.position.y > 2f) return;
                        Debug.Log($"GHOST_PATROL_PLAY cycle complete, peakY={highestY:F2}, onMesh={agent.isOnNavMesh}");
                        if (!agent.isOnNavMesh) throw new InvalidOperationException("Ghost left the NavMesh on the stairs.");
                        chasePatrolIndex = index;
                        agent.speed = 1f;
                        PlaceVisibleTarget();
                        UpdateMethod.Invoke(ai, null);
                        if (ai.State != GhostAI.GhostState.Chase) throw new InvalidOperationException($"Visible, reachable player did not enter Chase: {ai.State}.");
                        if ((int)PatrolIndexField.GetValue(ai) != chasePatrolIndex)
                            throw new InvalidOperationException("Patrol advanced during Chase.");
                        target.transform.position = new Vector3(1000f, 1000f, 1000f);
                        phase = 2;
                        phaseStart = Time.time;
                        Debug.Log("GHOST_PATROL_PLAY Chase detected; patrol paused.");
                        break;
                    case 2:
                        if (ai.State != GhostAI.GhostState.Patrol) return;
                        int nearest = NearestPoint();
                        if (index != nearest) throw new InvalidOperationException($"Returned to point {index}, nearest is {nearest}.");
                        returnIndex = index;
                        Debug.Log($"GHOST_PATROL_PLAY returned to nearest point {index} after Chase.");
                        phase = 3;
                        phaseStart = Time.time;
                        break;
                    case 3:
                        if (index != (returnIndex + 1) % originalPoints.Length) return;
                        Debug.Log($"GHOST_PATROL_PLAY continued from point {returnIndex} to {index}.");
                        var list = (List<Transform>)PatrolPointsField.GetValue(ai);
                        list.Clear();
                        list.Add(null);
                        list.Add(originalPoints[1]);
                        list.Add(null);
                        phase = 4;
                        phaseStart = Time.time;
                        break;
                    case 4:
                        if (Time.time - phaseStart < 0.5f) return;
                        if (ai.State != GhostAI.GhostState.Patrol) throw new InvalidOperationException("A patrol list with null entries stopped Patrol.");
                        ((List<Transform>)PatrolPointsField.GetValue(ai)).Clear();
                        phase = 5;
                        phaseStart = Time.time;
                        break;
                    case 5:
                        if (Time.time - phaseStart < 0.5f) return;
                        if (ai.State != GhostAI.GhostState.Idle) throw new InvalidOperationException("An empty patrol list did not return to Idle.");
                        Debug.Log("GHOST_PATROL_PLAY_VALID: ordered cycle, stairs movement, Chase pause, nearest return, null and empty lists.");
                        Finish(0);
                        break;
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("GHOST_PATROL_PLAY_FAILED: " + exception);
                Finish(1);
            }
        }

        private static void Initialize()
        {
            ai = UnityEngine.Object.FindFirstObjectByType<GhostAI>();
            if (ai == null) throw new InvalidOperationException("GhostAI did not load in Play Mode.");
            agent = ai.GetComponent<NavMeshAgent>();
            originalPoints = ((List<Transform>)PatrolPointsField.GetValue(ai)).ToArray();
            if (originalPoints.Length != 3) throw new InvalidOperationException("Expected three configured patrol points.");
            target = new GameObject("Temporary Distant Patrol Test Player");
            target.transform.position = new Vector3(1000f, 1000f, 1000f);
            ai.Configure(target.transform, null);
            StateField.SetValue(ai, GhostAI.GhostState.Idle);
            ai.GetComponent<GhostNavMovement>().Stop();
            Time.timeScale = 1f;
            agent.speed = 4f;
            phase = 0;
            phaseStart = Time.time;
            highestY = ai.transform.position.y;
        }

        private static int NearestPoint()
        {
            int nearest = -1;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < originalPoints.Length; i++)
            {
                float candidate = (originalPoints[i].position - ai.transform.position).sqrMagnitude;
                if (candidate >= distance) continue;
                nearest = i;
                distance = candidate;
            }
            return nearest;
        }

        private static void PlaceVisibleTarget()
        {
            foreach (float distance in new[] { 2f, 1.5f, 1.2f, 0.9f })
            foreach (float angle in new[] { 0f, -20f, 20f, -40f, 40f })
            {
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * ai.transform.forward;
                target.transform.position = ai.transform.position + direction * distance + Vector3.up * 0.25f;
                if (!(bool)CanSeePlayerMethod.Invoke(ai, null)) continue;
                if (!NavMesh.SamplePosition(target.transform.position, out var hit, 1.5f, agent.areaMask) ||
                    new Vector2(hit.position.x - target.transform.position.x, hit.position.z - target.transform.position.z).magnitude > 0.4f) continue;
                var path = new NavMeshPath();
                if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                var movement = ai.GetComponent<GhostNavMovement>();
                if (!movement.TryMoveTo(target.transform.position)) continue;
                movement.Stop();
                Debug.Log($"GHOST_PATROL_PLAY visible target at {distance:F1}m, {angle:F0} degrees.");
                return;
            }
            throw new InvalidOperationException("No visible chase test position near Ghost after patrol loop.");
        }

        private static void Finish(int exitCode)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.Exit(exitCode);
        }
    }
}
#endif
