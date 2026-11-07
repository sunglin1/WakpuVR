#if UNITY_EDITOR
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace WakpuVR.Editor
{
    public static class GhostNavInstaller
    {
        private const string ScenePath = "Assets/Scenes/WakpuVR_Prototype.unity";

        [MenuItem("Tools/WakpuVR/Bake Ghost NavMesh")]
        public static void Bake()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject root = scene.GetRootGameObjects().FirstOrDefault(item => item.name == "WakpuVR Prototype");
            GameObject ghost = scene.GetRootGameObjects().FirstOrDefault(item => item.name == "Ghost");
            if (root == null || ghost == null) throw new System.InvalidOperationException("Existing prototype root or Ghost is missing.");
            Transform floor = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == "Prototype Floor");
            if (floor == null || floor.GetComponent<Collider>() == null) throw new System.InvalidOperationException("Walkable floor collider is missing.");

            NavMeshSurface surface = root.GetComponent<NavMeshSurface>() ?? root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.defaultArea = 0;
            surface.BuildNavMesh();
            if (surface.navMeshData == null) throw new System.InvalidOperationException("NavMesh bake produced no data.");

            GhostNavMovement movement = ghost.GetComponent<GhostNavMovement>() ?? ghost.AddComponent<GhostNavMovement>();
            var movementSettings = new SerializedObject(movement);
            movementSettings.FindProperty("destinationSampleRadius").floatValue = 2.2f;
            movementSettings.FindProperty("maxHorizontalCorrection").floatValue = 0.75f;
            movementSettings.FindProperty("maxVerticalCorrection").floatValue = 2.1f;
            movementSettings.ApplyModifiedPropertiesWithoutUndo();
            NavMeshAgent agent = ghost.GetComponent<NavMeshAgent>();
            if (agent == null) throw new System.InvalidOperationException("Ghost NavMeshAgent could not be added.");
            agent.radius = 0.25f;
            agent.height = 1.8f;
            agent.speed = 1.6f;
            agent.acceleration = 6f;
            agent.angularSpeed = 360f;
            agent.stoppingDistance = 0.55f;
            agent.autoBraking = true;
            agent.baseOffset = 1f; // Existing Ghost mesh pivot is at body center.
            GhostAI ai = ghost.GetComponent<GhostAI>();
            if (ai == null) throw new System.InvalidOperationException("GhostAI must remain on the existing Ghost.");
            var serialized = new SerializedObject(ai);
            serialized.FindProperty("movement").objectReferenceValue = movement;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (!NavMesh.SamplePosition(ghost.transform.position, out NavMeshHit hit, 2f, agent.areaMask))
                throw new System.InvalidOperationException("Ghost is not near the baked NavMesh.");
            if (Vector3.Distance(hit.position, ghost.transform.position) > 1f)
                throw new System.InvalidOperationException("Ghost is too far from the baked NavMesh.");

            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(ghost);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Ghost NavMesh baked. Ghost distance to walkable surface: {Vector3.Distance(hit.position, ghost.transform.position):F2}m");
        }
    }
}
#endif
