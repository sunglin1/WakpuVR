#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace WakpuVR.Editor
{
    public static class GhostPrototypeInstaller
    {
        private const string ScenePath = "Assets/Scenes/WakpuVR_Prototype.unity";

        [MenuItem("Tools/WakpuVR/Install Ghost Gameplay")]
        public static void Install()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject ghostObject = scene.GetRootGameObjects().FirstOrDefault(item => item.name == "Ghost");
            if (ghostObject == null) throw new System.InvalidOperationException("Existing Ghost object is missing.");
            FearSystem fear = Object.FindFirstObjectByType<FearSystem>();
            PrototypeHUD hud = Object.FindFirstObjectByType<PrototypeHUD>();
            Camera camera = Camera.main;
            if (fear == null || hud == null || camera == null) throw new System.InvalidOperationException("Existing Fear, HUD, or XR camera is missing.");

            // Preserve the existing Ghost and all its components. Its old position is outside the left wall.
            ghostObject.transform.position = new Vector3(-2.4f, 1f, 5.8f);
            ghostObject.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, -1f));
            ghostObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            GhostAI ghost = ghostObject.GetComponent<GhostAI>() ?? ghostObject.AddComponent<GhostAI>();

            GameObject stateObject = scene.GetRootGameObjects().FirstOrDefault(item => item.name == "WakpuVR Prototype");
            if (stateObject == null) throw new System.InvalidOperationException("Prototype root is missing.");
            PrototypeGameOver gameOver = stateObject.GetComponent<PrototypeGameOver>() ?? stateObject.AddComponent<PrototypeGameOver>();
            Canvas canvas = hud.GetComponent<Canvas>();
            if (canvas == null) throw new System.InvalidOperationException("Existing HUD canvas is missing.");

            Image fill = canvas.GetComponentsInChildren<Image>(true).FirstOrDefault(item => item.name == "Fill");
            Image fade = GetOrCreateImage("Game Over Fade", canvas.transform, Color.clear);
            RectTransform fadeRect = fade.rectTransform;
            fadeRect.anchorMin = Vector2.zero;
            fadeRect.anchorMax = Vector2.one;
            fadeRect.offsetMin = Vector2.zero;
            fadeRect.offsetMax = Vector2.zero;
            fade.raycastTarget = false;
            fade.transform.SetAsLastSibling();

            Text gameOverLabel = fade.GetComponentInChildren<Text>(true);
            if (gameOverLabel == null)
            {
                var labelObject = new GameObject("Game Over Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelObject.transform.SetParent(fade.transform, false);
                gameOverLabel = labelObject.GetComponent<Text>();
                gameOverLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                gameOverLabel.fontSize = 44;
                gameOverLabel.alignment = TextAnchor.MiddleCenter;
                gameOverLabel.color = Color.white;
                gameOverLabel.text = "GAME OVER";
                RectTransform rect = gameOverLabel.rectTransform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            }
            gameOverLabel.gameObject.SetActive(false);
            hud.ConfigureGameOver(fill, gameOverLabel);

            Behaviour[] locomotion = new[] { "Move", "Turn", "Grab Move", "Teleportation", "Climb", "Jump" }
                .SelectMany(name => camera.transform.root.GetComponentsInChildren<Transform>(true)
                    .Where(item => item.name == name).SelectMany(item => item.GetComponents<Behaviour>()))
                .ToArray();
            Behaviour[] controls = locomotion.Concat(camera.transform.root.GetComponentsInChildren<XRBaseInteractor>(true)).Distinct().ToArray();
            gameOver.Configure(fear, hud, fade, controls);
            ghost.Configure(camera.transform, gameOver);
            fear.Configure(ghost, gameOver);

            EditorUtility.SetDirty(ghostObject);
            EditorUtility.SetDirty(fear);
            EditorUtility.SetDirty(hud);
            EditorUtility.SetDirty(gameOver);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Ghost gameplay installed on the existing scene and Ghost object.");
        }

        private static Image GetOrCreateImage(string name, Transform parent, Color color)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.GetComponent<Image>();
            var objectWithImage = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            objectWithImage.transform.SetParent(parent, false);
            Image image = objectWithImage.GetComponent<Image>();
            image.color = color;
            return image;
        }
    }
}
#endif
