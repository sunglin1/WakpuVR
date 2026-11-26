#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace WakpuVR.Editor
{
    public static class GhostWalkwayNavInstaller
    {
        private const string ScenePath = "Assets/Scenes/Wakpu_Map.unity";
        private const string NavDataPath = "Assets/Scenes/Wakpu_Map/NavMesh-WakpuVR-Ghost.asset";
        private const string AgentName = "Ghost Walkway";

        [MenuItem("Tools/WakpuVR/Bake Walkway Ghost NavMesh")]
        public static void Bake()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var surface = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NavMeshSurface>(true)).Single();
            var ghost = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GhostAI>(true)).Single();
            var agent = ghost.GetComponent<NavMeshAgent>();
            if (surface == null || agent == null) throw new InvalidOperationException("Ghost NavMesh components are missing.");

            int typeId = GetOrCreateGhostAgentType();
            surface.agentTypeID = typeId;
            surface.minRegionArea = 1f;
            agent.agentTypeID = typeId;
            if (Mathf.Approximately(agent.height, 1.8f)) agent.height = 1.6f;
            var collect = typeof(NavMeshSurface).GetMethod("CollectSources", BindingFlags.Instance | BindingFlags.NonPublic);
            var calculateBounds = typeof(NavMeshSurface).GetMethod("CalculateWorldBounds", BindingFlags.Instance | BindingFlags.NonPublic);
            if (collect == null || calculateBounds == null) throw new InvalidOperationException("NavMeshSurface source collection API changed.");
            var sources = (List<NavMeshBuildSource>)collect.Invoke(surface, null);
            int originalSourceCount = sources.Count;
            var walkway = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MeshCollider>(true))
                .Single(c => c.name == "Walkway.001");
            int removed = sources.RemoveAll(s => s.component == walkway);
            if (removed != 1) throw new InvalidOperationException($"Expected one Walkway collider source, found {removed}.");
            AddUpperWalkwayFloor(sources, walkway);
            AddRamp(sources, 1.2f, new Vector3(445.35f, 0.05f, 654.75f), new Vector3(445.35f, 1.97f, 652.18f));
            AddLanding(sources, new Vector3(444.7f, 1.97f, 651.85f), new Vector3(2.7f, 0.08f, 1.2f));
            AddRamp(sources, 1.2f, new Vector3(444f, 1.97f, 652.18f), new Vector3(444f, 3.03f, 654.02f));
            AddLanding(sources, new Vector3(444.7f, 3.03f, 654.15f), new Vector3(2.7f, 0.08f, 1.1f));
            AddRamp(sources, 1.2f, new Vector3(445.35f, 3.03f, 653.77f), new Vector3(445.35f, 4.61f, 651.20f));
            var bounds = (Bounds)calculateBounds.Invoke(surface, new object[] { sources });
            var data = NavMeshBuilder.BuildNavMeshData(surface.GetBuildSettings(), sources, bounds,
                surface.transform.position, surface.transform.rotation);
            if (data == null) throw new InvalidOperationException("Walkway NavMesh bake produced no data.");
            surface.RemoveData();
            surface.navMeshData = data;
            surface.AddData();
            if (surface.navMeshData == null) throw new InvalidOperationException("Walkway NavMesh bake failed.");
            var previous = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavDataPath);
            if (previous != null) AssetDatabase.DeleteAsset(NavDataPath);
            AssetDatabase.CreateAsset(surface.navMeshData, NavDataPath);
            AssetDatabase.SaveAssets();
            EditorUtility.SetDirty(surface);
            EditorUtility.SetDirty(agent);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"GHOST_WALKWAY_BAKE agent={typeId} sources={originalSourceCount}-1+6 triangles={NavMesh.CalculateTriangulation().indices.Length / 3} data={NavDataPath}");
        }

        internal static void AddRamp(List<NavMeshBuildSource> sources, float width, Vector3 lower, Vector3 upper)
        {
            // The imported Walkway is one MeshCollider. Its narrow treads are about 0.24 m high
            // and 0.31 m deep. Bake-only boxes bridge those gaps without changing play collisions.
            Vector3 direction = upper - lower;
            Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
            Vector3 center = (lower + upper) * 0.5f - rotation * Vector3.up * 0.04f + Vector3.up * 0.14f;
            sources.Add(new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                transform = Matrix4x4.TRS(center, rotation, Vector3.one),
                size = new Vector3(width, 0.08f, direction.magnitude),
                area = 0
            });
        }

        internal static void AddLanding(List<NavMeshBuildSource> sources, Vector3 center, Vector3 size)
        {
            sources.Add(new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                transform = Matrix4x4.TRS(center, Quaternion.identity, Vector3.one),
                size = size,
                area = 0
            });
        }

        internal static void AddUpperWalkwayFloor(List<NavMeshBuildSource> sources, MeshCollider walkway)
        {
            Mesh original = walkway.sharedMesh;
            Vector3[] vertices = original.vertices;
            int[] triangles = original.triangles;
            var floorTriangles = new List<int>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = walkway.transform.TransformPoint(vertices[triangles[i]]);
                Vector3 b = walkway.transform.TransformPoint(vertices[triangles[i + 1]]);
                Vector3 c = walkway.transform.TransformPoint(vertices[triangles[i + 2]]);
                if (Mathf.Abs(a.y - 4.63f) > 0.03f || Mathf.Abs(b.y - 4.63f) > 0.03f ||
                    Mathf.Abs(c.y - 4.63f) > 0.03f || Vector3.Cross(b - a, c - a).y <= 0f) continue;
                floorTriangles.Add(triangles[i]);
                floorTriangles.Add(triangles[i + 1]);
                floorTriangles.Add(triangles[i + 2]);
            }
            if (floorTriangles.Count == 0) throw new InvalidOperationException("Upper Walkway floor triangles were not found.");
            var floor = new Mesh { name = "Ghost Walkway upper floor bake mesh", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            floor.vertices = vertices;
            floor.triangles = floorTriangles.ToArray();
            floor.RecalculateBounds();
            sources.Add(new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Mesh,
                sourceObject = floor,
                transform = walkway.transform.localToWorldMatrix,
                area = 0
            });
            Debug.Log($"GHOST_WALKWAY_FLOOR triangles={floorTriangles.Count / 3}");
        }

        private static int GetOrCreateGhostAgentType()
        {
            var parameters = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]);
            var names = parameters.FindProperty("m_SettingNames");
            var settings = parameters.FindProperty("m_Settings");
            for (int i = 0; i < names.arraySize; i++)
                if (names.GetArrayElementAtIndex(i).stringValue == AgentName)
                {
                    var item = settings.GetArrayElementAtIndex(i);
                    var height = item.FindPropertyRelative("agentHeight");
                    if (Mathf.Approximately(height.floatValue, 1.8f))
                    {
                        height.floatValue = 1.6f;
                        parameters.ApplyModifiedPropertiesWithoutUndo();
                    }
                    return item.FindPropertyRelative("agentTypeID").intValue;
                }

            int id = NavMesh.CreateSettings().agentTypeID;
            parameters.Update();
            names = parameters.FindProperty("m_SettingNames");
            settings = parameters.FindProperty("m_Settings");
            for (int i = 0; i < settings.arraySize; i++)
            {
                var item = settings.GetArrayElementAtIndex(i);
                if (item.FindPropertyRelative("agentTypeID").intValue != id) continue;
                names.GetArrayElementAtIndex(i).stringValue = AgentName;
                item.FindPropertyRelative("agentRadius").floatValue = 0.25f;
                item.FindPropertyRelative("agentHeight").floatValue = 1.6f;
                item.FindPropertyRelative("agentSlope").floatValue = 45f;
                item.FindPropertyRelative("agentClimb").floatValue = 0.75f;
                parameters.ApplyModifiedPropertiesWithoutUndo();
                return id;
            }
            throw new InvalidOperationException("Could not create Ghost NavMesh agent settings.");
        }
    }
}
#endif
