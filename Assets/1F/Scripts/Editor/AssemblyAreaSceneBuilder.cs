#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Harbourview.Journey;

namespace Harbourview.EditorTools
{
    /// <summary>
    /// Generates the 1F ground-level safe endpoint: the open assembly area in front of the
    /// academic building. This is T12 in the Module D trigger catalogue (ENDPOINT_REACHED).
    ///
    /// Run it from the menu "Harbourview / Build 1F Assembly Area Scene". Running it twice is
    /// safe - materials and meshes are reused, geometry is rebuilt from scratch.
    ///
    /// Everything is procedural placeholders built from primitives. They are intentionally
    /// cheap so Module C can swap in real models later without touching the logic objects.
    /// </summary>
    public static class AssemblyAreaSceneBuilder
    {
        private const string ScenePath = "Assets/1F/Scenes/AssemblyArea_1F.unity";
        private const string ArtRoot = "Assets/1F/Art";
        private const string MaterialFolder = "Assets/1F/Art/Materials";
        private const string MeshFolder = "Assets/1F/Art/Meshes";

        // Deliverable per the group leader's "scene setup" note: every owner ships a floor PREFAB
        // that can be dropped into the assembled building scene, not just a private scene.
        private const string PrefabFolder = "Assets/Playground Prefab";
        private const string FloorPrefabPath = "Assets/Playground Prefab/AssemblyArea_1F.prefab";

        // Layout constants, grouped so the whole plan can be nudged in one place.
        private const float GroundScale = 12f;      // Unity plane primitive is 10x10 units
        private const float TrackCentreZ = -6f;
        private const float TrackInner = 19f;
        private const float TrackOuter = 25f;
        private const float PlazaDepth = 11f;
        private const float PlazaCentreZ = 25f;
        private const float AssemblyX = -12f;
        private const float AssemblyZ = 23.5f;
        private const float AssemblyRadius = 4.5f;
        private const float FacadeZ = 32f;

        private static Transform _root;

        [MenuItem("Harbourview/Build 1F Assembly Area Scene", false, 10)]
        public static void Build()
        {
            EnsureFolder(ArtRoot);
            EnsureFolder(MaterialFolder);
            EnsureFolder(MeshFolder);

            // Everything is rebuilt from scratch on each run, so the scene is reproducible:
            // change a constant above, re-run the menu item, review the diff.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject root = new GameObject("Environment");
            _root = root.transform;

            CreateGround();
            CreateRunningField();
            CreatePlaza();
            CreateBuildingFacade();
            CreatePerimeterFence();
            CreateLandscape();
            CreateAssemblyZone();
            CreatePlayerHook();
            CreateSystems();
            CreateLighting();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            AddToBuildSettings();
            ExportFloorPrefab(root);

            Debug.Log("[Harbourview] Assembly area scene written to " + ScenePath
                      + " and floor prefab exported to " + FloorPrefabPath);
        }

        /// <summary>
        /// Exports the Environment root as the shareable floor prefab. The XR rig and the
        /// Systems objects deliberately stay scene-level, so a floor never drags a second
        /// director or camera into the assembled building scene.
        /// </summary>
        [MenuItem("Harbourview/Export 1F Assembly Area Prefab", false, 11)]
        public static void ExportFloorPrefabMenu()
        {
            GameObject root = GameObject.Find("Environment");
            if (root == null)
            {
                Debug.LogError("[Harbourview] No 'Environment' root found. Run 'Build 1F Assembly Area Scene' first.");
                return;
            }

            ExportFloorPrefab(root);
        }

        private static void ExportFloorPrefab(GameObject root)
        {
            EnsureFolder(PrefabFolder);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, FloorPrefabPath);
            AssetDatabase.SaveAssets();

            if (prefab == null)
            {
                Debug.LogError("[Harbourview] Prefab export failed for " + FloorPrefabPath);
                return;
            }

            Debug.Log("[Harbourview] Floor prefab ready: " + FloorPrefabPath);
        }

        // ---------------------------------------------------------------- environment

        private static GameObject CreateGround()
        {
            GameObject ground = Prim(PrimitiveType.Plane, _root, Vector3.zero,
                new Vector3(GroundScale, 1f, GroundScale), "Ground_Grass", Mat("Mat_Grass", Col(0.30f, 0.45f, 0.22f), 0.05f));
            return ground;
        }

        private static void CreateRunningField()
        {
            // Inner grass pitch as a flattened cylinder rather than another plane, so the
            // track that surrounds it reads as raised kerbing.
            Prim(PrimitiveType.Cylinder, _root, new Vector3(0f, 0.01f, TrackCentreZ),
                new Vector3(TrackInner * 2f, 0.02f, TrackInner * 2f), "Pitch_Infield", Mat("Mat_Pitch", Col(0.26f, 0.40f, 0.20f), 0.05f));

            Mesh track = RingMesh("running track", TrackInner, TrackOuter, 96);
            AddMeshGO("Ground_Track", SaveMesh(track, "Mesh_TrackRing"), new Vector3(0f, 0.02f, TrackCentreZ),
                Mat("Mat_Track", Col(0.71f, 0.38f, 0.25f), 0.10f));

            Mesh centre = RingMesh("centre circle", 8.7f, 9.2f, 48);
            AddMeshGO("Ground_CentreCircle", SaveMesh(centre, "Mesh_CentreCircle"), new Vector3(0f, 0.03f, TrackCentreZ),
                Mat("Mat_LineWhite", Col(0.93f, 0.93f, 0.90f), 0.20f));
        }

        private static void CreatePlaza()
        {
            Prim(PrimitiveType.Cube, _root, new Vector3(0f, 0.05f, PlazaCentreZ),
                new Vector3(40f, 0.08f, PlazaDepth), "Plaza_Paving", Mat("Mat_Paving", Col(0.72f, 0.71f, 0.67f), 0.15f));
        }

        private static void CreateBuildingFacade()
        {
            Transform facade = new GameObject("Building_Facade_Placeholder").transform;
            facade.SetParent(_root, false);

            Material wall = Mat("Mat_Wall", Col(0.84f, 0.82f, 0.77f), 0.10f);
            Material glass = Mat("Mat_Glass", Col(0.24f, 0.35f, 0.43f), 0.65f);
            Material trim = Mat("Mat_Trim", Col(0.45f, 0.45f, 0.43f), 0.20f);

            // Two wings leaving a 4 m wide exit opening in the middle.
            Prim(PrimitiveType.Cube, facade, new Vector3(-20f, 7f, FacadeZ), new Vector3(28f, 14f, 3f), "Wing_West", wall);
            Prim(PrimitiveType.Cube, facade, new Vector3(20f, 7f, FacadeZ), new Vector3(28f, 14f, 3f), "Wing_East", wall);
            Prim(PrimitiveType.Cube, facade, new Vector3(0f, 14.4f, FacadeZ), new Vector3(68f, 0.8f, 3.4f), "Roof_Slab", trim);

            // Glazing around the exit either side of the opening.
            Prim(PrimitiveType.Cube, facade, new Vector3(-4f, 1.6f, FacadeZ - 1.6f), new Vector3(4f, 3.2f, 0.3f), "Glazing_West", glass);
            Prim(PrimitiveType.Cube, facade, new Vector3(4f, 1.6f, FacadeZ - 1.6f), new Vector3(4f, 3.2f, 0.3f), "Glazing_East", glass);
            Prim(PrimitiveType.Cube, facade, new Vector3(0f, 4.9f, FacadeZ - 1.6f), new Vector3(12f, 3.2f, 0.3f), "Transom", glass);
            Prim(PrimitiveType.Cube, facade, new Vector3(0f, 3.4f, FacadeZ - 1.6f), new Vector3(12f, 0.4f, 0.5f), "Lintel", trim);

            // Ribbon windows on each wing.
            float[] rows = { 3.5f, 8.5f };
            foreach (float y in rows)
            {
                Prim(PrimitiveType.Cube, facade, new Vector3(-20f, y, FacadeZ - 1.6f), new Vector3(26f, 1.8f, 0.2f), "Ribbon_W_" + y, glass);
                Prim(PrimitiveType.Cube, facade, new Vector3(20f, y, FacadeZ - 1.6f), new Vector3(26f, 1.8f, 0.2f), "Ribbon_E_" + y, glass);
            }

            // Exit threshold marker - where the staircase journey hands the user over.
            Prim(PrimitiveType.Cube, facade, new Vector3(0f, 0.09f, FacadeZ - 2.6f), new Vector3(4.2f, 0.1f, 1.6f), "Exit_Threshold", Mat("Mat_LineWhite", Col(0.93f, 0.93f, 0.90f), 0.20f));
        }

        private static void CreatePerimeterFence()
        {
            Transform fence = new GameObject("Perimeter_Fence").transform;
            fence.SetParent(_root, false);

            Material metal = Mat("Mat_Metal", Col(0.54f, 0.54f, 0.52f), 0.30f);

            // Three sides only: the building closes off the fourth.
            FenceRun(fence, metal, new Vector3(-38f, 0f, 29f), new Vector3(-38f, 0f, -34f));
            FenceRun(fence, metal, new Vector3(38f, 0f, 29f), new Vector3(38f, 0f, -34f));
            FenceRun(fence, metal, new Vector3(-38f, 0f, -34f), new Vector3(38f, 0f, -34f));
        }

        private static void FenceRun(Transform parent, Material metal, Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            Vector3 dir = delta.normalized;
            int posts = Mathf.Max(2, Mathf.RoundToInt(length / 6f) + 1);

            for (int i = 0; i <= posts; i++)
            {
                Vector3 pos = from + dir * (length * (i / (float)posts));
                Prim(PrimitiveType.Cylinder, parent, pos + Vector3.up * 1.2f, new Vector3(0.22f, 2.4f, 0.22f), "Post", metal);
            }

            bool alongZ = Mathf.Abs(delta.z) > Mathf.Abs(delta.x);
            Vector3 railScale = alongZ ? new Vector3(0.08f, 0.12f, length) : new Vector3(length, 0.12f, 0.08f);
            Vector3 mid = (from + to) * 0.5f;
            Prim(PrimitiveType.Cube, parent, mid + Vector3.up * 0.7f, railScale, "Rail_Low", metal);
            Prim(PrimitiveType.Cube, parent, mid + Vector3.up * 1.8f, railScale, "Rail_High", metal);
        }

        private static void CreateLandscape()
        {
            Transform green = new GameObject("Landscape").transform;
            green.SetParent(_root, false);

            Material trunk = Mat("Mat_Trunk", Col(0.42f, 0.31f, 0.23f), 0.05f);
            Material leaf = Mat("Mat_Leaf", Col(0.24f, 0.42f, 0.20f), 0.05f);

            float[] xs = { -32f, -22f, -11f, 0f, 11f, 22f, 32f };
            foreach (float x in xs)
            {
                Tree(green, new Vector3(x, 0f, -31f), trunk, leaf);
            }

            Material bench = Mat("Mat_Bench", Col(0.55f, 0.41f, 0.28f), 0.10f);
            float[] benchXs = { -18f, 18f };
            foreach (float x in benchXs)
            {
                Bench(green, new Vector3(x, 0f, 21f), bench);
            }
        }

        private static void Tree(Transform parent, Vector3 pos, Material trunk, Material leaf)
        {
            Prim(PrimitiveType.Cylinder, parent, pos + Vector3.up * 1.5f, new Vector3(0.6f, 3f, 0.6f), "Trunk", trunk);
            Prim(PrimitiveType.Sphere, parent, pos + Vector3.up * 4.2f, new Vector3(3.4f, 3f, 3.4f), "Canopy", leaf);
        }

        private static void Bench(Transform parent, Vector3 pos, Material wood)
        {
            Prim(PrimitiveType.Cube, parent, pos + Vector3.up * 0.45f, new Vector3(2f, 0.12f, 0.6f), "Seat", wood);
            Prim(PrimitiveType.Cube, parent, pos + new Vector3(-0.8f, 0.2f, 0f), new Vector3(0.14f, 0.4f, 0.5f), "Leg_L", wood);
            Prim(PrimitiveType.Cube, parent, pos + new Vector3(0.8f, 0.2f, 0f), new Vector3(0.14f, 0.4f, 0.5f), "Leg_R", wood);
        }

        // ---------------------------------------------------------------- assembly point

        private static void CreateAssemblyZone()
        {
            Transform zone = new GameObject("AssemblyPoint").transform;
            zone.SetParent(_root, false);

            Vector3 centre = new Vector3(AssemblyX, 0f, AssemblyZ);

            Prim(PrimitiveType.Cylinder, zone, centre + Vector3.up * 0.055f,
                new Vector3(AssemblyRadius * 2f, 0.02f, AssemblyRadius * 2f), "Zone_Disc", Mat("Mat_ZoneFill", Col(0.78f, 0.78f, 0.72f), 0.15f));

            Mesh outline = RingMesh("assembly outline", AssemblyRadius + 0.1f, AssemblyRadius + 0.5f, 64);
            AddMeshGO("Zone_Outline", SaveMesh(outline, "Mesh_AssemblyOutline"), centre + Vector3.up * 0.07f,
                Mat("Mat_LineWhite", Col(0.93f, 0.93f, 0.90f), 0.20f), zone);

            Material post = Mat("Mat_Metal", Col(0.54f, 0.54f, 0.52f), 0.30f);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 2f / 8f;
                Vector3 p = centre + new Vector3(Mathf.Cos(angle) * (AssemblyRadius + 0.9f), 0.55f, Mathf.Sin(angle) * (AssemblyRadius + 0.9f));
                Prim(PrimitiveType.Cylinder, zone, p, new Vector3(0.28f, 1.1f, 0.28f), "Bollard", post);
            }

            CreateAssemblySign(zone, centre);
            CreateEndpointTrigger(zone, centre);
        }

        private static void CreateAssemblySign(Transform parent, Vector3 zoneCentre)
        {
            Vector3 signPos = new Vector3(zoneCentre.x + AssemblyRadius + 0.6f, 0f, zoneCentre.z);

            GameObject board = Prim(PrimitiveType.Cube, parent, signPos + Vector3.up * 2.3f,
                new Vector3(2.6f, 1.1f, 0.1f), "Sign_Board", Mat("Mat_SignBoard", Col(0.95f, 0.95f, 0.93f), 0.25f));
            board.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            board.isStatic = true;

            Prim(PrimitiveType.Cylinder, parent, signPos + Vector3.up * 1.1f, new Vector3(0.18f, 2.2f, 0.18f), "Sign_Post",
                Mat("Mat_Metal", Col(0.54f, 0.54f, 0.52f), 0.30f));

            // World-space canvas so text stays crisp without needing TextMeshPro installed yet.
            // Module F will replace this with the bilingual TMP version later.
            GameObject canvasGO = new GameObject("Sign_Canvas");
            canvasGO.transform.SetParent(board.transform, false);
            canvasGO.transform.localPosition = new Vector3(0f, 0f, -0.06f);
            canvasGO.transform.localRotation = Quaternion.identity;
            canvasGO.transform.localScale = new Vector3(0.01f, 0.01f, 1f);

            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(240f, 110f);

            Font font = GetUiFont();

            GameObject labelGO = new GameObject("Label");
            labelGO.transform.SetParent(canvasGO.transform, false);
            Text label = labelGO.AddComponent<Text>();
            if (font != null)
            {
                label.font = font;
            }
            label.text = "ASSEMBLY POINT";
            label.fontSize = 26;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.color = new Color(0.10f, 0.28f, 0.17f);
            Stretch(labelGO);

            GameObject subGO = new GameObject("SubLabel");
            subGO.transform.SetParent(canvasGO.transform, false);
            Text sub = subGO.AddComponent<Text>();
            if (font != null)
            {
                sub.font = font;
            }
            sub.text = "Fire assembly area - wait here for staff";
            sub.fontSize = 13;
            sub.alignment = TextAnchor.UpperCenter;
            sub.horizontalOverflow = HorizontalWrapMode.Overflow;
            sub.verticalOverflow = VerticalWrapMode.Overflow;
            sub.color = new Color(0.25f, 0.25f, 0.25f);

            RectTransform subRect = subGO.GetComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0f, 0f);
            subRect.anchorMax = new Vector2(1f, 0.35f);
            subRect.offsetMin = Vector2.zero;
            subRect.offsetMax = Vector2.zero;

            // Recognition evidence for evaluation indicators 2 and 3.
            NotableCue cue = board.gameObject.AddComponent<NotableCue>();
            SerializedObjectUtility.SetString(cue, "cueId", "sign.assembly_point");
        }

        /// <summary>
        /// Unity 2022 removed the Arial built-in font; asking for it throws an ArgumentException which
        /// aborts the whole scene build. LegacyRuntime.ttf is the supported replacement, and a null
        /// font is still fine because Text falls back to its own default.
        /// </summary>
        private static Font GetUiFont()
        {
            foreach (string name in new[] { "LegacyRuntime.ttf", "Arial.ttf" })
            {
                try
                {
                    Font font = Resources.GetBuiltinResource<Font>(name);
                    if (font != null)
                    {
                        return font;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[Harbourview] Built-in font '" + name + "' unavailable: " + e.Message);
                }
            }

            return null;
        }

        private static void Stretch(GameObject go)
        {
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.35f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void CreateEndpointTrigger(Transform parent, Vector3 centre)
        {
            GameObject zone = new GameObject("T12_GroundReached_Zone");
            zone.transform.SetParent(parent, false);
            zone.transform.position = centre + Vector3.up * 1.5f;

            BoxCollider trigger = zone.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(AssemblyRadius * 2f + 1f, 3f, AssemblyRadius * 2f + 1f);

            GeoFence fence = zone.AddComponent<GeoFence>();
            SerializedObjectUtility.SetFloat(fence, "dwellSeconds", 0.8f);
            SerializedObjectUtility.SetBool(fence, "fireOnce", true);

            SafeZoneEndpoint endpoint = zone.AddComponent<SafeZoneEndpoint>();
            SerializedObjectUtility.SetObject(endpoint, fence, "zone");
            SerializedObjectUtility.SetInt(endpoint, "endpointType", 0);
        }

        // ---------------------------------------------------------------- player + systems

        private static void CreatePlayerHook()
        {
            // Placeholder rig. Replace the whole "PlayerStart_BuildingExit" object with the
            // XROrigin prefab once XR Interaction Toolkit is imported; keep a CharacterController
            // or collider on whatever replaces it, otherwise T12 can never fire.
            // Scene-level, not inside Environment: the XR rig must not end up in the floor prefab.
            GameObject start = new GameObject("PlayerStart_BuildingExit");
            start.transform.SetParent(null, false);
            start.transform.position = new Vector3(0f, 0f, FacadeZ - 3.2f);
            start.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            GameObject cameraGO = new GameObject("PreviewCamera");
            cameraGO.tag = "MainCamera";
            cameraGO.transform.SetParent(start.transform, false);
            cameraGO.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            Camera cam = cameraGO.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cameraGO.AddComponent<AudioListener>();

            GameObject proxy = new GameObject("PlayerColliderProxy");
            proxy.transform.SetParent(start.transform, false);
            proxy.transform.localPosition = Vector3.zero;
            proxy.layer = LayerMask.NameToLayer("Default");

            CapsuleCollider body = proxy.AddComponent<CapsuleCollider>();
            body.radius = 0.3f;
            body.height = 1.7f;
            body.center = new Vector3(0f, 0.85f, 0f);

            Rigidbody rb = proxy.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        private static void CreateSystems()
        {
            GameObject systems = new GameObject("Systems");
            systems.transform.SetParent(null, false);

            GameObject director = new GameObject("JourneyDirector");
            director.transform.SetParent(systems.transform, false);
            JourneyDirector journey = director.AddComponent<JourneyDirector>();
            SerializedObjectUtility.SetInt(journey, "initialState", (int)JourneyState.Descending);

            GameObject logger = new GameObject("SessionLogger");
            logger.transform.SetParent(systems.transform, false);
            logger.AddComponent<SessionLogger>();
        }

        private static void CreateLighting()
        {
            GameObject sun = new GameObject("Sun");
            sun.transform.SetParent(_root, false);
            sun.transform.rotation = Quaternion.Euler(48f, 30f, 0f);
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.97f, 0.92f);
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;

            Material sky = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            if (sky != null)
            {
                RenderSettings.skybox = sky;
            }

            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = false;
        }

        // ---------------------------------------------------------------- helpers

        private static GameObject Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, string name, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.isStatic = true;

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null && mat != null)
            {
                renderer.sharedMaterial = mat;
            }

            // Decoration does not need colliders; keeping them off avoids pointless broadphase work.
            Collider collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            return go;
        }

        private static GameObject AddMeshGO(string name, Mesh mesh, Vector3 position, Material mat, Transform parent = null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : _root, false);
            go.transform.position = position;

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            go.isStatic = true;
            return go;
        }

        private static Mesh RingMesh(string label, float innerRadius, float outerRadius, int segments)
        {
            var vertices = new List<Vector3>(segments * 2 + 2);
            var triangles = new List<int>(segments * 6);

            for (int i = 0; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                float c = Mathf.Cos(angle);
                float s = Mathf.Sin(angle);
                vertices.Add(new Vector3(c * innerRadius, 0f, s * innerRadius));
                vertices.Add(new Vector3(c * outerRadius, 0f, s * outerRadius));
            }

            for (int i = 0; i < segments; i++)
            {
                int b = i * 2;
                triangles.Add(b);
                triangles.Add(b + 2);
                triangles.Add(b + 1);
                triangles.Add(b + 1);
                triangles.Add(b + 2);
                triangles.Add(b + 3);
            }

            Mesh mesh = new Mesh { name = label };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh SaveMesh(Mesh mesh, string assetName)
        {
            EnsureFolder(MeshFolder);
            string path = MeshFolder + "/" + assetName + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.CreateAsset(mesh, path);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        private static Color Col(float r, float g, float b)
        {
            return new Color(r, g, b, 1f);
        }

        private static Material Mat(string name, Color color, float smoothness)
        {
            EnsureFolder(MaterialFolder);
            string path = MaterialFolder + "/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.color = color;
            if (mat.HasProperty("_Smoothness"))
            {
                mat.SetFloat("_Smoothness", smoothness);
            }
            else if (mat.HasProperty("_Glossiness"))
            {
                mat.SetFloat("_Glossiness", smoothness);
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static void AddToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (EditorBuildSettingsScene existing in scenes)
            {
                if (existing.path == ScenePath)
                {
                    return;
                }
            }

            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
