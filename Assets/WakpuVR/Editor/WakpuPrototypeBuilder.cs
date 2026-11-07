#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace WakpuVR.Editor
{
    public static class WakpuPrototypeBuilder
    {
        private const string SourceScenePath = "Assets/Scenes/BasicScene.unity";
        private const string PrototypeScenePath = "Assets/Scenes/WakpuVR_Prototype.unity";
        private const string RootPath = "Assets/WakpuVR";
        private const string PrefabPath = RootPath + "/Prefabs/WakpuBall.prefab";
        private const string MaterialPath = RootPath + "/Materials";
        private const string MeshPath = RootPath + "/Meshes";
        private const string InputActionsPath = "Assets/Samples/XR Interaction Toolkit/3.4.1/Starter Assets/XRI Default Input Actions.inputactions";
        private const string LeftHandPath = "Assets/Animated Hands/Prefabs/Left Hand Model.prefab";
        private const string RightHandPath = "Assets/Animated Hands/Prefabs/Right Hand Model.prefab";

        [MenuItem("Tools/WakpuVR/Rebuild Prototype Assets")]
        public static void RebuildPrototypeAssets()
        {
            EnsureFolders();
            GameObject ballPrefab = BuildBallPrefab();
            BuildScene(ballPrefab);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"WakpuVR prototype rebuilt: {PrototypeScenePath}");
        }

        public static void RebuildFromCommandLine()
        {
            RebuildPrototypeAssets();
        }

        [MenuItem("Tools/WakpuVR/Validate Prototype Assets")]
        public static void ValidatePrototypeAssets()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                throw new FileNotFoundException("WakpuBall prefab is missing.", PrefabPath);

            Scene scene = EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);
            FearSystem fear = Object.FindFirstObjectByType<FearSystem>();
            PrototypeHUD hud = Object.FindFirstObjectByType<PrototypeHUD>();
            WakpuBall[] balls = Object.FindObjectsByType<WakpuBall>(FindObjectsSortMode.None);
            XRGrabInteractable[] grabs = Object.FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None);
            AnimatedHandInputDriver[] hands = Object.FindObjectsByType<AnimatedHandInputDriver>(FindObjectsSortMode.None);
            WakpuBallRespawner[] respawners = Object.FindObjectsByType<WakpuBallRespawner>(FindObjectsSortMode.None);

            Require(fear != null, "FearSystem is missing.");
            Require(hud != null, "PrototypeHUD is missing.");
            Require(balls.Length >= 3, "At least three WakpuBall placements are required.");
            Require(grabs.Length >= balls.Length, "Every WakpuBall needs an XRGrabInteractable.");
            Require(hands.Length == 2, "Animated left and right hand visuals are required.");
            Require(respawners.Length >= 3, "Every prototype placement needs a respawner.");
            Transform oldLeftModel = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(item => item.name == "XR Controller Left");
            Transform oldRightModel = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(item => item.name == "XR Controller Right");
            Require((oldLeftModel == null || !oldLeftModel.gameObject.activeSelf) && (oldRightModel == null || !oldRightModel.gameObject.activeSelf), "Legacy controller visuals must be disabled.");
            Require(balls.All(ball => ball.GetComponent<Rigidbody>() != null && ball.GetComponent<Collider>() != null), "Every WakpuBall needs physics components.");
            Require(!Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(item => item.name == "Crack Detail"), "Legacy Crack Detail objects must be removed.");
            Require(GameObject.Find("XR Origin (XR Rig)") != null, "The copied BasicScene XR Origin is missing.");
            Require(GameObject.Find("Move") != null && GameObject.Find("Turn") != null, "Existing locomotion objects are missing.");
            Require(EditorBuildSettings.scenes.Any(item => item.enabled && item.path == PrototypeScenePath), "Prototype scene is not enabled in Build Settings.");
            Require(scene.isLoaded, "Prototype scene could not be loaded.");

            Debug.Log($"WakpuVR prototype validation passed: {balls.Length} balls, {hands.Length} animated hands, staged break meshes, respawn, XR locomotion, HUD, grab, physics, and build settings are present.");
        }

        public static void ValidateFromCommandLine()
        {
            ValidatePrototypeAssets();
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new System.InvalidOperationException(message);
        }

        private static void EnsureFolders()
        {
            Directory.CreateDirectory(RootPath + "/Prefabs");
            Directory.CreateDirectory(MaterialPath);
            Directory.CreateDirectory(MeshPath);
        }

        private static Material GetOrCreateMaterial(string name, Color color, float metallic = 0f, float smoothness = 0.35f)
        {
            string path = $"{MaterialPath}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject BuildBallPrefab()
        {
            Material ballMaterial = GetOrCreateMaterial("WakpuBall", new Color(0.68f, 0.16f, 0.82f), 0.02f, 0.62f);
            Material interiorMaterial = GetOrCreateMaterial("WakpuInterior", new Color(0.16f, 0.025f, 0.21f), 0f, 0.18f);
            Material crumbMaterial = GetOrCreateMaterial("WakpuCrumbs", new Color(0.95f, 0.3f, 0.75f), 0f, 0.15f);

            var root = new GameObject("WakpuBall");
            var rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.mass = 0.35f;
            rigidbody.linearDamping = 0.2f;
            rigidbody.angularDamping = 0.15f;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var collider = root.AddComponent<SphereCollider>();
            collider.radius = 0.11f;

            root.AddComponent<XRGrabInteractable>();
            var audioSource = root.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.minDistance = 0.25f;
            audioSource.maxDistance = 8f;

            var visual = new GameObject("Deformable Visual");
            visual.transform.SetParent(root.transform, false);
            GameObject[] stages = new GameObject[4];
            stages[0] = CreateSingleMeshStage("Stage 0 - Soft", visual.transform, SaveMesh("WakpuSoft", CreateSoftSphereMesh(Vector3.one, 0f)), ballMaterial);
            stages[1] = CreateSingleMeshStage("Stage 1 - Compressed", visual.transform, SaveMesh("WakpuCompressed", CreateSoftSphereMesh(new Vector3(1.08f, 0.76f, 1.06f), 0.16f)), ballMaterial);
            stages[2] = CreateSplitStage("Stage 2 - Split", visual.transform, 0.006f, 4f, ballMaterial, interiorMaterial);
            stages[3] = CreateSplitStage("Stage 3 - Broken", visual.transform, 0.018f, 13f, ballMaterial, interiorMaterial);
            for (int i = 1; i < stages.Length; i++)
                stages[i].SetActive(false);
            Renderer renderer = stages[0].GetComponentInChildren<Renderer>();

            var particleObject = new GameObject("Break Particles");
            particleObject.transform.SetParent(root.transform, false);
            var particles = particleObject.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.playOnAwake = false;
            main.duration = 0.25f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.025f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.2f, 0.8f), new Color(0.2f, 0.04f, 0.3f));
            main.maxParticles = 24;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10, 16) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.09f;
            var particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = crumbMaterial;

            WakpuBall ball = root.AddComponent<WakpuBall>();
            var serializedBall = new SerializedObject(ball);
            serializedBall.FindProperty("visualRoot").objectReferenceValue = visual.transform;
            serializedBall.FindProperty("ballRenderer").objectReferenceValue = renderer;
            SerializedProperty stagesProperty = serializedBall.FindProperty("damageStages");
            stagesProperty.arraySize = stages.Length;
            for (int i = 0; i < stages.Length; i++)
                stagesProperty.GetArrayElementAtIndex(i).objectReferenceValue = stages[i];
            serializedBall.FindProperty("crumbleParticles").objectReferenceValue = particles;
            serializedBall.FindProperty("audioSource").objectReferenceValue = audioSource;
            serializedBall.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateSingleMeshStage(string name, Transform parent, Mesh mesh, Material material)
        {
            var stage = new GameObject(name);
            stage.transform.SetParent(parent, false);
            stage.AddComponent<MeshFilter>().sharedMesh = mesh;
            stage.AddComponent<MeshRenderer>().sharedMaterial = material;
            return stage;
        }

        private static GameObject CreateSplitStage(string name, Transform parent, float gap, float angle, Material outside, Material inside)
        {
            var stage = new GameObject(name);
            stage.transform.SetParent(parent, false);
            for (int side = -1; side <= 1; side += 2)
            {
                var half = new GameObject(side < 0 ? "Left Half" : "Right Half");
                half.transform.SetParent(stage.transform, false);
                half.transform.localRotation = Quaternion.Euler(0f, 0f, side * angle);
                half.AddComponent<MeshFilter>().sharedMesh = SaveMesh($"Wakpu{name.Replace(" ", string.Empty)}{(side < 0 ? "Left" : "Right")}", CreateSplitHalfMesh(side, gap, angle * 0.1f));
                half.AddComponent<MeshRenderer>().sharedMaterials = new[] { outside, inside };
            }
            return stage;
        }

        private static Mesh CreateSoftSphereMesh(Vector3 scale, float dimple)
        {
            const int latitude = 14;
            const int longitude = 22;
            const float radius = 0.11f;
            var vertices = new Vector3[(latitude + 1) * (longitude + 1)];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[latitude * longitude * 6];
            int vertex = 0;
            for (int y = 0; y <= latitude; y++)
            {
                float v = y / (float)latitude;
                float phi = v * Mathf.PI;
                for (int x = 0; x <= longitude; x++)
                {
                    float u = x / (float)longitude;
                    float theta = u * Mathf.PI * 2f;
                    Vector3 direction = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                    float softness = 1f + 0.025f * Mathf.Sin(theta * 3f) * Mathf.Pow(Mathf.Sin(phi), 2f);
                    float press = 1f - dimple * Mathf.Pow(Mathf.Max(0f, direction.z), 2f);
                    vertices[vertex] = Vector3.Scale(direction, scale) * radius * softness * press;
                    uvs[vertex++] = new Vector2(u, v);
                }
            }
            int triangle = 0;
            for (int y = 0; y < latitude; y++)
            for (int x = 0; x < longitude; x++)
            {
                int current = y * (longitude + 1) + x;
                int next = current + longitude + 1;
                triangles[triangle++] = current;
                triangles[triangle++] = next;
                triangles[triangle++] = current + 1;
                triangles[triangle++] = current + 1;
                triangles[triangle++] = next;
                triangles[triangle++] = next + 1;
            }
            var mesh = new Mesh { name = "Soft Wakpu Ball" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateSplitHalfMesh(int side, float gap, float phase)
        {
            const int rings = 9;
            const int segments = 22;
            const float radius = 0.11f;
            var vertices = new System.Collections.Generic.List<Vector3>();
            var surface = new System.Collections.Generic.List<int>();
            var interior = new System.Collections.Generic.List<int>();
            for (int ring = 0; ring <= rings; ring++)
            {
                float p = ring / (float)rings * Mathf.PI * 0.5f;
                for (int segment = 0; segment <= segments; segment++)
                {
                    float a = segment / (float)segments * Mathf.PI * 2f;
                    float boundary = ring == rings ? 0.0045f * (1f + Mathf.Sin(a * 3f + phase)) : 0f;
                    vertices.Add(new Vector3(side * (gap + radius * Mathf.Cos(p) + boundary), radius * Mathf.Sin(p) * Mathf.Cos(a), radius * Mathf.Sin(p) * Mathf.Sin(a)));
                }
            }
            for (int ring = 0; ring < rings; ring++)
            for (int segment = 0; segment < segments; segment++)
            {
                int current = ring * (segments + 1) + segment;
                int next = current + segments + 1;
                if (side > 0)
                {
                    surface.Add(current); surface.Add(next); surface.Add(current + 1);
                    surface.Add(current + 1); surface.Add(next); surface.Add(next + 1);
                }
                else
                {
                    surface.Add(current + 1); surface.Add(next); surface.Add(current);
                    surface.Add(next + 1); surface.Add(next); surface.Add(current + 1);
                }
            }
            int center = vertices.Count;
            vertices.Add(new Vector3(side * gap, 0f, 0f));
            int boundaryStart = rings * (segments + 1);
            for (int segment = 0; segment < segments; segment++)
            {
                if (side > 0)
                {
                    interior.Add(center); interior.Add(boundaryStart + segment); interior.Add(boundaryStart + segment + 1);
                }
                else
                {
                    interior.Add(center); interior.Add(boundaryStart + segment + 1); interior.Add(boundaryStart + segment);
                }
            }
            var mesh = new Mesh { name = side < 0 ? "Wakpu Left Half" : "Wakpu Right Half", subMeshCount = 2 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(surface, 0);
            mesh.SetTriangles(interior, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh SaveMesh(string name, Mesh mesh)
        {
            string path = $"{MeshPath}/{name}.asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static void BuildScene(GameObject ballPrefab)
        {
            if (!File.Exists(SourceScenePath))
                throw new FileNotFoundException("BasicScene is required to build the prototype.", SourceScenePath);

            File.Copy(SourceScenePath, PrototypeScenePath, true);
            AssetDatabase.ImportAsset(PrototypeScenePath, ImportAssetOptions.ForceSynchronousImport);

            Scene scene = EditorSceneManager.OpenScene(PrototypeScenePath, OpenSceneMode.Single);
            RemoveExistingPrototypeRoot();
            InstallAnimatedHands();

            var root = new GameObject("WakpuVR Prototype");
            var fearObject = new GameObject("Fear System");
            fearObject.transform.SetParent(root.transform);
            FearSystem fearSystem = fearObject.AddComponent<FearSystem>();

            Material floorMaterial = GetOrCreateMaterial("PrototypeFloor", new Color(0.10f, 0.13f, 0.17f), 0f, 0.18f);
            Material wallMaterial = GetOrCreateMaterial("PrototypeWalls", new Color(0.19f, 0.22f, 0.28f), 0f, 0.22f);
            Material pedestalMaterial = GetOrCreateMaterial("PrototypePedestal", new Color(0.18f, 0.42f, 0.48f), 0.15f, 0.4f);

            Transform environment = new GameObject("Test Space").transform;
            environment.SetParent(root.transform);
            CreatePrimitive("Prototype Floor", PrimitiveType.Cube, environment, new Vector3(0f, -0.1f, 2.5f), new Vector3(8f, 0.2f, 10f), floorMaterial);
            CreatePrimitive("Back Wall", PrimitiveType.Cube, environment, new Vector3(0f, 1.5f, 7.4f), new Vector3(8f, 3f, 0.2f), wallMaterial);
            CreatePrimitive("Left Wall", PrimitiveType.Cube, environment, new Vector3(-3.9f, 1.5f, 2.5f), new Vector3(0.2f, 3f, 10f), wallMaterial);
            CreatePrimitive("Right Wall", PrimitiveType.Cube, environment, new Vector3(3.9f, 1.5f, 2.5f), new Vector3(0.2f, 3f, 10f), wallMaterial);

            Vector3[] ballPositions =
            {
                new Vector3(-1.4f, 1.05f, 2.2f),
                new Vector3(0f, 1.05f, 3.6f),
                new Vector3(1.5f, 1.05f, 5.1f)
            };

            Transform placements = new GameObject("Wakpu Ball Placements").transform;
            placements.SetParent(root.transform);
            for (int i = 0; i < ballPositions.Length; i++)
            {
                Vector3 position = ballPositions[i];
                CreatePrimitive($"Pedestal {i + 1}", PrimitiveType.Cylinder, placements, new Vector3(position.x, 0.4f, position.z), new Vector3(0.42f, 0.4f, 0.42f), pedestalMaterial);
                var spawnObject = new GameObject($"WakpuBall Spawn {i + 1}");
                spawnObject.transform.SetParent(placements);
                spawnObject.transform.position = position;
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(ballPrefab, scene);
                instance.name = $"WakpuBall {i + 1}";
                instance.transform.SetParent(spawnObject.transform);
                instance.transform.position = position;
                WakpuBall initialBall = instance.GetComponent<WakpuBall>();
                WakpuBall prefabBall = ballPrefab.GetComponent<WakpuBall>();
                spawnObject.AddComponent<WakpuBallRespawner>().Configure(prefabBall, spawnObject.transform, initialBall);
            }

            CreateHud(root.transform, fearSystem);
            EnsureEventSystem();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void InstallAnimatedHands()
        {
            GameObject leftController = FindSceneObject("Left Controller");
            GameObject rightController = FindSceneObject("Right Controller");
            if (leftController == null || rightController == null)
                throw new MissingReferenceException("The copied XR Origin must contain Left Controller and Right Controller objects.");

            InstallAnimatedHand(leftController, LeftHandPath, "Animated Left Hand", "XRI Left Interaction", "Select Value", "Activate Value", "XR Controller Left");
            InstallAnimatedHand(rightController, RightHandPath, "Animated Right Hand", "XRI Right Interaction", "Select Value", "Activate Value", "XR Controller Right");
        }

        private static void InstallAnimatedHand(GameObject controller, string prefabPath, string handName, string actionMap, string gripAction, string triggerAction, string oldModelName)
        {
            Transform oldModel = controller.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == oldModelName);
            if (oldModel != null)
                oldModel.gameObject.SetActive(false);

            GameObject handPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (handPrefab == null)
                throw new FileNotFoundException("Animated Hands prefab is missing.", prefabPath);

            GameObject hand = (GameObject)PrefabUtility.InstantiatePrefab(handPrefab, controller.transform);
            hand.name = handName;
            Animator animator = hand.GetComponentInChildren<Animator>();
            AnimatedHandInputDriver driver = hand.AddComponent<AnimatedHandInputDriver>();
            driver.Configure(animator, FindInputActionReference(actionMap, gripAction), FindInputActionReference(actionMap, triggerAction));
        }

        private static InputActionReference FindInputActionReference(string mapName, string actionName)
        {
            InputActionReference reference = AssetDatabase.LoadAllAssetsAtPath(InputActionsPath)
                .OfType<InputActionReference>()
                .FirstOrDefault(item => item.action != null && item.action.actionMap != null && item.action.actionMap.name == mapName && item.action.name == actionName);
            if (reference == null)
                throw new MissingReferenceException($"Input action reference not found: {mapName}/{actionName}");
            return reference;
        }

        private static GameObject FindSceneObject(string name)
        {
            return Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(item => item.name == name)?.gameObject;
        }

        private static void RemoveExistingPrototypeRoot()
        {
            GameObject existing = GameObject.Find("WakpuVR Prototype");
            if (existing != null)
                Object.DestroyImmediate(existing);
        }

        private static GameObject CreatePrimitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject gameObject = GameObject.CreatePrimitive(type);
            gameObject.name = name;
            gameObject.transform.SetParent(parent);
            gameObject.transform.position = position;
            gameObject.transform.localScale = scale;
            gameObject.GetComponent<Renderer>().sharedMaterial = material;
            return gameObject;
        }

        private static void CreateHud(Transform root, FearSystem fearSystem)
        {
            Camera camera = Camera.main;
            if (camera == null)
                camera = Object.FindFirstObjectByType<Camera>();
            if (camera == null)
                throw new MissingReferenceException("The BasicScene XR Origin must contain a camera.");

            GameObject canvasObject = new GameObject("Prototype HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(PrototypeHUD));
            canvasObject.transform.SetParent(camera.transform, false);
            canvasObject.transform.localPosition = new Vector3(0f, -0.18f, 1.35f);
            canvasObject.transform.localRotation = Quaternion.identity;
            canvasObject.transform.localScale = Vector3.one * 0.0015f;

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            var canvasRect = (RectTransform)canvasObject.transform;
            canvasRect.sizeDelta = new Vector2(620f, 190f);

            Image panel = CreateImage("Panel", canvasRect, new Color(0.025f, 0.03f, 0.055f, 0.88f));
            Stretch(panel.rectTransform, 0f, 0f, 0f, 0f);

            Text fearText = CreateText("Fear Label", panel.rectTransform, "FEAR  50 / 100", 30, TextAnchor.MiddleLeft);
            SetRect(fearText.rectTransform, new Vector2(24f, -15f), new Vector2(400f, 44f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            Slider fearSlider = CreateSlider(panel.rectTransform);
            SetRect(fearSlider.GetComponent<RectTransform>(), new Vector2(24f, -62f), new Vector2(572f, 22f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            Text ballText = CreateText("Ball Label", panel.rectTransform, "WAKPU BALL  --", 27, TextAnchor.MiddleLeft);
            SetRect(ballText.rectTransform, new Vector2(24f, -96f), new Vector2(572f, 38f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            Text hintText = CreateText("Hint", panel.rectTransform, "Grab a Wakpu Ball", 20, TextAnchor.MiddleLeft);
            hintText.color = new Color(0.72f, 0.8f, 0.9f, 1f);
            SetRect(hintText.rectTransform, new Vector2(24f, -137f), new Vector2(572f, 34f), new Vector2(0f, 1f), new Vector2(0f, 1f));

            canvasObject.GetComponent<PrototypeHUD>().Configure(fearSystem, fearSlider, fearText, ballText, hintText);
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(string name, Transform parent, string value, int size, TextAnchor alignment)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            Text text = gameObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            return text;
        }

        private static Slider CreateSlider(Transform parent)
        {
            var sliderObject = new GameObject("Fear Slider", typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(parent, false);
            Image background = CreateImage("Background", sliderObject.transform, new Color(0.16f, 0.18f, 0.24f, 1f));
            Stretch(background.rectTransform, 0f, 0f, 0f, 0f);
            Image fill = CreateImage("Fill", sliderObject.transform, new Color(0.82f, 0.15f, 0.32f, 1f));
            Stretch(fill.rectTransform, 3f, 3f, 3f, 3f);

            Slider slider = sliderObject.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.targetGraphic = fill;
            slider.direction = Slider.Direction.LeftToRight;
            slider.interactable = false;
            return slider;
        }

        private static void SetRect(RectTransform rect, Vector2 anchoredPosition, Vector2 size, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static void AddSceneToBuildSettings()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool found = false;
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path != PrototypeScenePath)
                    continue;
                scenes[i] = new EditorBuildSettingsScene(PrototypeScenePath, true);
                found = true;
                break;
            }

            if (!found)
                scenes.Add(new EditorBuildSettingsScene(PrototypeScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
