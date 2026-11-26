#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace WakpuVR.Editor
{
    public static class GhostPatrolSceneInstaller
    {
        [MenuItem("Tools/WakpuVR/Configure Ghost Patrol Points")]
        public static void Configure()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Wakpu_Map.unity");
            var ghost = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GhostAI>(true)).Single();
            var root = scene.GetRootGameObjects().Single(r => r.name == "WakpuVR");
            var serialized = new SerializedObject(ghost);
            var list = serialized.FindProperty("patrolPoints");
            if (list.arraySize != 0)
            {
                Debug.Log("Ghost patrol points already have Inspector entries; preserving them.");
                return;
            }

            var group = root.transform.Find("Ghost Patrol Points");
            if (group == null)
            {
                group = new GameObject("Ghost Patrol Points").transform;
                group.SetParent(root.transform, false);
                group.localPosition = Vector3.zero;
            }
            Vector3[] positions =
            {
                new Vector3(434.21f, 0f, 639.49f),
                new Vector3(445.35f, 0.05f, 654.75f),
                new Vector3(445.35f, 4.61f, 651.20f)
            };
            var agent = ghost.GetComponent<NavMeshAgent>();
            for (int i = 0; i < positions.Length; i++)
            {
                if (!NavMesh.SamplePosition(positions[i], out var hit, 1f, agent.areaMask) ||
                    Mathf.Abs(hit.position.y - positions[i].y) > 0.5f)
                    throw new InvalidOperationException($"Patrol point {i + 1} is not on the baked NavMesh.");
                string name = $"PatrolPoint_{i + 1:00}";
                var point = group.Find(name);
                if (point == null)
                {
                    point = new GameObject(name).transform;
                    point.SetParent(group, false);
                    point.position = hit.position;
                }
                list.InsertArrayElementAtIndex(i);
                list.GetArrayElementAtIndex(i).objectReferenceValue = point;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ghost);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Configured three ordered Ghost patrol points, including the lower and upper Walkway stairs.");
        }
    }
}
#endif
