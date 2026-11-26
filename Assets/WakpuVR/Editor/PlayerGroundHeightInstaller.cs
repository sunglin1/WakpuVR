#if UNITY_EDITOR
using System;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WakpuVR.Editor
{
    public static class PlayerGroundHeightInstaller
    {
        [MenuItem("Tools/WakpuVR/Configure Player Ground Height")]
        public static void Install()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Wakpu_Map.unity");
            var origin = UnityEngine.Object.FindFirstObjectByType<XROrigin>();
            if (origin == null) throw new InvalidOperationException("Wakpu_Map XR Origin was not found.");
            var controller = origin.GetComponent<CharacterController>();
            if (controller == null) throw new InvalidOperationException("XR Origin CharacterController was not found.");

            if (origin.GetComponent<PlayerAdaptiveStepOffset>() == null)
                origin.gameObject.AddComponent<PlayerAdaptiveStepOffset>();
            controller.stepOffset = 0.12f;
            controller.skinWidth = 0.03f;
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("PLAYER_GROUND_HEIGHT_INSTALLED: adaptive step offset, CharacterController step=0.12, skin=0.03.");
        }
    }
}
#endif
