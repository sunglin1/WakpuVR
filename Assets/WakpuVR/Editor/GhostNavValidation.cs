#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace WakpuVR.Editor
{
    public static class GhostNavValidation
    {
        private static GhostAI testAI;
        private static GhostNavMovement testMovement;
        private static Vector3 testStart;
        private static double testEnd;
        private static GhostAI noiseAI;
        private static Vector3 noiseStart;
        private static double noiseEnd;
        private static GameObject noiseTestPlayer;
        private static Transform noiseOriginalPlayer;
        private static PrototypeGameOver noiseOriginalGameOver;
        private static GameObject chaseTarget;
        private static Transform originalPlayer;
        private static PrototypeGameOver originalGameOver;
        private static GhostAI chaseAI;
        private static Vector3 chaseStart;
        private static float chaseFear;
        private static double chaseEnd;

        [MenuItem("Tools/WakpuVR/Validate Ghost Navigation")]
        public static void Validate()
        {
            GameObject ghost = GameObject.Find("Ghost");
            NavMeshAgent agent = ghost != null ? ghost.GetComponent<NavMeshAgent>() : null;
            GhostNavMovement movement = ghost != null ? ghost.GetComponent<GhostNavMovement>() : null;
            GhostAI ai = ghost != null ? ghost.GetComponent<GhostAI>() : null;
            if (agent == null || movement == null || ai == null)
                throw new InvalidOperationException("Ghost navigation components are missing.");

            NavMeshTriangulation mesh = NavMesh.CalculateTriangulation();
            if (mesh.vertices.Length == 0 || mesh.indices.Length == 0)
                throw new InvalidOperationException("The scene has no active baked NavMesh.");
            if (!NavMesh.SamplePosition(ghost.transform.position, out NavMeshHit start, 2f, agent.areaMask))
                throw new InvalidOperationException("Ghost is outside the baked NavMesh.");
            if (!NavMesh.SamplePosition(Vector3.zero, out NavMeshHit center, 2f, agent.areaMask))
                throw new InvalidOperationException("The center of the prototype floor is not walkable.");
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(start.position, center.position, agent.areaMask, path) || path.status != NavMeshPathStatus.PathComplete)
                throw new InvalidOperationException("Ghost has no complete path to the center of the floor.");
            Debug.Log($"Ghost navigation valid: {mesh.indices.Length / 3} NavMesh triangles, " +
                      $"Ghost-to-center path with {path.corners.Length} corners, " +
                      $"agent on mesh: {agent.isOnNavMesh}, AI movement reference: {ai != null}.");
        }

        [MenuItem("Tools/WakpuVR/Test Ghost Movement (Play Mode)")]
        public static void TestMovement()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("Enter Play Mode before testing movement.");
            Validate();
            GameObject ghost = GameObject.Find("Ghost");
            testAI = ghost.GetComponent<GhostAI>();
            testMovement = ghost.GetComponent<GhostNavMovement>();
            NavMeshAgent agent = ghost.GetComponent<NavMeshAgent>();
            if (!agent.isOnNavMesh) throw new InvalidOperationException("Ghost agent is not bound to the NavMesh in Play Mode.");
            if (!NavMesh.SamplePosition(Vector3.zero, out NavMeshHit destination, 2f, agent.areaMask))
                throw new InvalidOperationException("Test destination is not walkable.");
            testAI.enabled = false;
            testStart = ghost.transform.position;
            if (!testMovement.TryMoveTo(destination.position))
            {
                testAI.enabled = true;
                throw new InvalidOperationException("Ghost rejected a complete path in Play Mode.");
            }
            testEnd = EditorApplication.timeSinceStartup + 3.0;
            EditorApplication.update -= FinishMovementTest;
            EditorApplication.update += FinishMovementTest;
        }

        private static void FinishMovementTest()
        {
            if (EditorApplication.timeSinceStartup < testEnd && EditorApplication.isPlaying) return;
            EditorApplication.update -= FinishMovementTest;
            if (!EditorApplication.isPlaying || testMovement == null) return;
            Vector3 end = testMovement.transform.position;
            float distance = Vector3.Distance(testStart, end);
            NavMeshAgent agent = testMovement.GetComponent<NavMeshAgent>();
            testMovement.Stop();
            bool rejectedInvalid = !testMovement.TryMoveTo(new Vector3(1000f, 0f, 1000f));
            testAI.enabled = true;
            if (distance < 0.5f || !agent.isOnNavMesh || !rejectedInvalid)
                Debug.LogError($"Ghost movement test failed: moved {distance:F2}m, on NavMesh {agent.isOnNavMesh}, invalid target rejected {rejectedInvalid}.");
            else
                Debug.Log($"Ghost movement test passed: moved {distance:F2}m in 3s; remained on NavMesh; invalid target rejected.");
        }

        [MenuItem("Tools/WakpuVR/Test Ghost Noise (Play Mode)")]
        public static void TestNoise()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("Enter Play Mode before testing noise response.");
            GameObject ghost = GameObject.Find("Ghost");
            noiseAI = ghost != null ? ghost.GetComponent<GhostAI>() : null;
            if (noiseAI == null || !noiseAI.enabled)
                throw new InvalidOperationException("Ghost AI is unavailable for noise test.");
            var serialized = new SerializedObject(noiseAI);
            noiseOriginalPlayer = serialized.FindProperty("player").objectReferenceValue as Transform;
            noiseOriginalGameOver = serialized.FindProperty("gameOver").objectReferenceValue as PrototypeGameOver;
            NavMeshAgent agent = ghost.GetComponent<NavMeshAgent>();
            Vector3 desired = ghost.transform.position + new Vector3(2f, -1f, -2f);
            if (!NavMesh.SamplePosition(desired, out NavMeshHit hit, 2f, agent.areaMask))
                throw new InvalidOperationException("No walkable noise test destination.");
            var path = new NavMeshPath();
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
                throw new InvalidOperationException("Noise test destination is unreachable.");
            noiseTestPlayer = new GameObject("Temporary Distant Noise Test Player");
            noiseTestPlayer.transform.position = new Vector3(1000f, 1000f, 1000f);
            noiseAI.Configure(noiseTestPlayer.transform, noiseOriginalGameOver);
            noiseStart = ghost.transform.position;
            NoiseSystem.Generate(hit.position + Vector3.up * 1.2f, 10f, ghost);
            if (noiseAI.State != GhostAI.GhostState.Investigate && noiseAI.State != GhostAI.GhostState.Chase)
                throw new InvalidOperationException("Ghost did not react to audible noise.");
            noiseEnd = EditorApplication.timeSinceStartup + 10.0;
            EditorApplication.update -= FinishNoiseTest;
            EditorApplication.update += FinishNoiseTest;
        }

        private static void FinishNoiseTest()
        {
            if (EditorApplication.timeSinceStartup < noiseEnd && EditorApplication.isPlaying) return;
            EditorApplication.update -= FinishNoiseTest;
            if (!EditorApplication.isPlaying || noiseAI == null) return;
            float distance = Vector3.Distance(noiseStart, noiseAI.transform.position);
            GhostAI.GhostState finalState = noiseAI.State;
            NavMeshAgent agent = noiseAI.GetComponent<NavMeshAgent>();
            float remaining = agent.remainingDistance;
            bool hasPath = agent.hasPath;
            float speed = agent.velocity.magnitude;
            noiseAI.Configure(noiseOriginalPlayer, noiseOriginalGameOver);
            UnityEngine.Object.Destroy(noiseTestPlayer);
            if (distance < 0.2f || finalState != GhostAI.GhostState.Idle)
                Debug.LogError($"Ghost noise response failed: moved {distance:F2}m, state {finalState}, remaining {remaining:F2}m, path {hasPath}, speed {speed:F2}m/s.");
            else
                Debug.Log($"Ghost noise response passed: moved {distance:F2}m after elevated noise and returned to Idle.");
        }

        [MenuItem("Tools/WakpuVR/Test Ghost Chase (Play Mode)")]
        public static void TestChase()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("Enter Play Mode before testing chase.");
            GameObject ghost = GameObject.Find("Ghost");
            chaseAI = ghost != null ? ghost.GetComponent<GhostAI>() : null;
            NavMeshAgent agent = ghost != null ? ghost.GetComponent<NavMeshAgent>() : null;
            if (chaseAI == null || agent == null || !agent.isOnNavMesh)
                throw new InvalidOperationException("Ghost is not ready for chase test.");
            var serialized = new SerializedObject(chaseAI);
            originalPlayer = serialized.FindProperty("player").objectReferenceValue as Transform;
            originalGameOver = serialized.FindProperty("gameOver").objectReferenceValue as PrototypeGameOver;
            Vector3 desired = ghost.transform.position + ghost.transform.forward * 4f;
            if (!NavMesh.SamplePosition(desired, out NavMeshHit hit, 2f, agent.areaMask))
                throw new InvalidOperationException("No walkable chase test destination.");
            chaseTarget = new GameObject("Temporary Ghost Chase Target");
            chaseTarget.transform.position = hit.position + Vector3.up * 1.2f;
            chaseAI.Configure(chaseTarget.transform, originalGameOver);
            chaseStart = ghost.transform.position;
            FearSystem fear = UnityEngine.Object.FindFirstObjectByType<FearSystem>();
            chaseFear = fear != null ? fear.CurrentFear : -1f;
            chaseEnd = EditorApplication.timeSinceStartup + 1.5;
            EditorApplication.update -= FinishChaseTest;
            EditorApplication.update += FinishChaseTest;
        }

        private static void FinishChaseTest()
        {
            if (EditorApplication.timeSinceStartup < chaseEnd && EditorApplication.isPlaying) return;
            EditorApplication.update -= FinishChaseTest;
            if (!EditorApplication.isPlaying || chaseAI == null) return;
            float distance = Vector3.Distance(chaseStart, chaseAI.transform.position);
            bool chased = chaseAI.State == GhostAI.GhostState.Chase;
            FearSystem fear = UnityEngine.Object.FindFirstObjectByType<FearSystem>();
            float fearIncrease = fear != null ? fear.CurrentFear - chaseFear : -1f;
            chaseAI.Configure(originalPlayer, originalGameOver);
            UnityEngine.Object.Destroy(chaseTarget);
            if (!chased || distance < 0.2f || fearIncrease < 2f)
                Debug.LogError($"Ghost chase test failed: state {chaseAI.State}, moved {distance:F2}m, fear +{fearIncrease:F2}.");
            else
                Debug.Log($"Ghost chase test passed: moved {distance:F2}m in Chase, fear +{fearIncrease:F2}.");
        }
    }
}
#endif
