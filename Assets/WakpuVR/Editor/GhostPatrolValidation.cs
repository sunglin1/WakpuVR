#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace WakpuVR.Editor
{
    public static class GhostPatrolValidation
    {
        private static readonly Vector3[] StairChecks =
        {
            new Vector3(445.35f, 0.05f, 654.75f),
            new Vector3(445.35f, 1.00f, 653.46f),
            new Vector3(445.35f, 1.97f, 652.18f),
            new Vector3(444.00f, 1.97f, 652.18f),
            new Vector3(444.00f, 2.50f, 653.10f),
            new Vector3(444.00f, 3.03f, 654.02f),
            new Vector3(445.35f, 3.03f, 653.77f),
            new Vector3(445.35f, 3.82f, 652.49f),
            new Vector3(445.35f, 4.61f, 651.20f)
        };

        [MenuItem("Tools/WakpuVR/Validate Ghost Patrol Navigation")]
        public static void Validate()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Wakpu_Map.unity");
            var ghost = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GhostAI>(true)).Single();
            var agent = ghost.GetComponent<NavMeshAgent>();
            var patrol = new SerializedObject(ghost).FindProperty("patrolPoints");
            if (agent == null || patrol.arraySize == 0) throw new InvalidOperationException("Ghost patrol points are not configured.");

            var sampled = StairChecks.Select(p => Sample(p, agent.areaMask)).ToArray();
            for (int i = 1; i < sampled.Length; i++) CheckPath(sampled[i - 1], sampled[i], agent.areaMask);
            CheckPath(sampled[0], sampled[sampled.Length - 1], agent.areaMask);
            CheckPath(sampled[sampled.Length - 1], Sample(new Vector3(445.35f, 4.63f, 650.05f), agent.areaMask), agent.areaMask);
            var points = Enumerable.Range(0, patrol.arraySize)
                .Select(i => patrol.GetArrayElementAtIndex(i).objectReferenceValue as Transform).ToArray();
            if (points.Any(p => p == null)) throw new InvalidOperationException("A configured patrol point is missing.");
            for (int i = 0; i < points.Length; i++)
                CheckPath(Sample(points[i].position, agent.areaMask),
                    Sample(points[(i + 1) % points.Length].position, agent.areaMask), agent.areaMask);
            Debug.Log($"GHOST_PATROL_NAV_VALID: {sampled.Length} stair samples, both stair directions, {points.Length} ordered patrol legs including wraparound.");
        }

        private static Vector3 Sample(Vector3 position, int areaMask)
        {
            if (!NavMesh.SamplePosition(position, out var hit, 0.6f, areaMask) || Mathf.Abs(hit.position.y - position.y) > 0.5f)
                throw new InvalidOperationException($"No walkable NavMesh near {position}.");
            return hit.position;
        }

        private static void CheckPath(Vector3 from, Vector3 to, int areaMask)
        {
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(from, to, areaMask, path) || path.status != NavMeshPathStatus.PathComplete)
                throw new InvalidOperationException($"Incomplete Ghost path: {from} -> {to} ({path.status}).");
        }
    }
}
#endif
