/*********************************************************************************************
 * COMPONENT OF: Editor tools (used by Build Everything and Optimize For Slow Laptops)
 * REQUIRED DEPENDENCIES: road light models in the scene, DayNightCycle, URP assets in
 *                        Assets/Settings
 * DESCRIPTION: Makes the city look better and run faster:
 *                - Street lights: the 144 real lights start switched off (StreetLightManager
 *                  turns on the closest few at night); every lamp gets a glowing bulb and a
 *                  soft pool of light on the ground that cost almost nothing to draw
 *                - Day and night: the game starts at 9 AM, nights have moonlight, and the
 *                  Scene view shows daylight while editing
 *                - Static: everything that never moves is marked "static" so Unity can draw it
 *                  in big batches and skip what is hidden behind buildings
 *                - Cheaper shadows (2 cascades) and half-size ambient occlusion
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace IAmABall.EditorTools
{
    public static class CityLightingSetup
    {
        public const string GlowName = "Street Light Glow";
        const string PoolTexturePath = BuildTools.GeneratedRoot + "/Textures/Light Pool.png";
        static readonly Color LampColor = new Color(1f, 0.82f, 0.55f);

        // ---------- Street lights ----------

        // Sets up the real lights (off, warm, no shadows) and builds the cheap glow; returns the glow group
        public static GameObject BuildStreetLightGlow(Transform parent)
        {
            GameObject glow = new GameObject(GlowName);
            glow.transform.SetParent(parent, false);

            Material bulb = BuildTools.GetMaterial("Lamp Bulb Glow", LampColor * 2f, true);
            Material pool = LightPoolMaterial();
            int groundMask = 1 << Mathf.Max(0, LayerMask.NameToLayer("Ground"));
            int count = 0;

            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (light.type == LightType.Directional || !IsRoadLight(light.transform))
                    continue;

                TuneRealLight(light);
                AddGlow(glow.transform, light.transform.position, bulb, pool, groundMask);
                count++;
            }

            foreach (Transform t in glow.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);

            glow.SetActive(false);
            Debug.Log("[I Am A Ball] Street light glow added to " + count + " lamps");
            return glow;
        }

        static bool IsRoadLight(Transform t)
        {
            for (Transform current = t; current != null; current = current.parent)
            {
                if (current.name.ToLowerInvariant().StartsWith("road light"))
                    return true;
            }
            return false;
        }

        // Warm, no shadows, switched off until StreetLightManager needs it
        static void TuneRealLight(Light light)
        {
            Undo.RecordObject(light, "Street light");
            light.color = LampColor;
            light.range = 10f;
            light.intensity = 2.5f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(light);
        }

        // A glowing bulb at the lamp and a soft circle of light on the ground under it
        static void AddGlow(Transform group, Vector3 lampPosition, Material bulb, Material pool, int groundMask)
        {
            GameObject glowBulb = BuildTools.Primitive(PrimitiveType.Sphere, "Bulb", group, bulb, false);
            glowBulb.transform.position = lampPosition;
            glowBulb.transform.localScale = Vector3.one * 0.3f;
            NoShadows(glowBulb);

            float groundY = 0f;
            if (Physics.Raycast(lampPosition, Vector3.down, out RaycastHit hit, 15f, groundMask, QueryTriggerInteraction.Ignore))
                groundY = hit.point.y;

            GameObject lightPool = BuildTools.Primitive(PrimitiveType.Quad, "Light Pool", group, pool, false);
            lightPool.transform.position = new Vector3(lampPosition.x, groundY + 0.03f, lampPosition.z);
            lightPool.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            lightPool.transform.localScale = Vector3.one * 6f;
            NoShadows(lightPool);
        }

        static void NoShadows(GameObject go)
        {
            Renderer r = go.GetComponent<Renderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        // A see-through material that adds light (brighter in the middle, fading at the edge)
        static Material LightPoolMaterial()
        {
            Material material = BuildTools.GetMaterial("Light Pool", new Color(LampColor.r, LampColor.g, LampColor.b, 0.55f), true);
            material.SetTexture("_BaseMap", PoolTexture());
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        // A small round gradient picture, saved once as a PNG in Assets/Generated/Textures
        static Texture2D PoolTexture()
        {
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(PoolTexturePath);
            if (existing != null)
                return existing;

            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(size - 1, size - 1) * 0.5f) / (size * 0.5f);
                    float alpha = Mathf.Clamp01(1f - distance);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
                }
            }

            BuildTools.EnsureFolder(Path.GetDirectoryName(PoolTexturePath));
            File.WriteAllBytes(PoolTexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(PoolTexturePath);

            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(PoolTexturePath);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(PoolTexturePath);
        }

        // ---------- Day and night ----------

        // Start at 9 AM with brighter nights, and show daylight in the Scene view
        public static void SetUpDayNight()
        {
            DayNightCycle cycle = Object.FindAnyObjectByType<DayNightCycle>();
            if (cycle == null)
                return;

            Undo.RecordObject(cycle, "Day and night");
            cycle.nightAmbientIntensity = 0.45f;
            cycle.nightSpeedMultiplier = 3f;
            cycle.moonIntensity = 0.35f;
            cycle.ApplyTime(9f);
            EditorUtility.SetDirty(cycle);
            if (cycle.sun != null)
                EditorUtility.SetDirty(cycle.sun.transform);
        }

        // ---------- Static objects ----------

        // Marks everything that never moves as static (big speed-up for drawing and culling)
        public static int MarkSceneStatic()
        {
            int count = 0;
            foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == GameplayBuilder.RootName)
                {
                    // Gameplay is mostly moving things; only the street light glow is static
                    Transform glow = root.transform.Find(GlowName);
                    if (glow != null)
                        count += MarkStatic(glow.gameObject);
                    continue;
                }

                count += MarkStatic(root);
            }

            Debug.Log("[I Am A Ball] Marked " + count + " objects static");
            return count;
        }

        static int MarkStatic(GameObject go)
        {
            if (Moves(go))
                return 0;

            StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;
            GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(go) | flags);

            int count = 1;
            foreach (Transform child in go.transform)
                count += MarkStatic(child.gameObject);
            return count;
        }

        // Things that move or change during the game must not be static
        static bool Moves(GameObject go)
        {
            Light light = go.GetComponent<Light>();
            return go.CompareTag("Player")
                || go.name.Contains("Marker")
                || go.GetComponent<Rigidbody>() != null
                || go.GetComponent<NavMeshAgent>() != null
                || go.GetComponent<Animator>() != null
                || go.GetComponent<Camera>() != null
                || go.GetComponent<Canvas>() != null
                || go.GetComponent<FloatAndSpin>() != null
                || go.GetComponent<MoneyCollectible>() != null
                || go.GetComponent<DayNightCycle>() != null
                || (light != null && light.type == LightType.Directional);
        }

        // ---------- Render settings ----------

        // Fewer shadow cascades, no shadows from small lights, and half-size ambient occlusion
        public static void TuneRenderSettings()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets/Settings" }))
            {
                Object asset = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(guid));
                SerializedObject settings = new SerializedObject(asset);
                SetInt(settings, "m_ShadowCascadeCount", 2, true);
                SetBool(settings, "m_AdditionalLightShadowsSupported", false);
                SetFloat(settings, "m_ShadowDistance", 45f, true);
                settings.ApplyModifiedProperties();
            }

            // Renderer features (like ambient occlusion) live inside the renderer .asset files
            foreach (string path in Directory.GetFiles("Assets/Settings", "*.asset"))
            {
                foreach (Object feature in AssetDatabase.LoadAllAssetsAtPath(path.Replace('\\', '/')))
                {
                    if (feature == null || feature.GetType().Name != "ScreenSpaceAmbientOcclusion")
                        continue;

                    SerializedObject ssao = new SerializedObject(feature);
                    SetBool(ssao, "m_Settings.Downsample", true);
                    SetBool(ssao, "m_Settings.AfterOpaque", true);
                    ssao.ApplyModifiedProperties();
                }
            }

            AssetDatabase.SaveAssets();
        }

        static void SetInt(SerializedObject so, string name, int value, bool onlyLower)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p != null && (!onlyLower || p.intValue > value))
                p.intValue = value;
        }

        static void SetFloat(SerializedObject so, string name, float value, bool onlyLower)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p != null && (!onlyLower || p.floatValue > value))
                p.floatValue = value;
        }

        static void SetBool(SerializedObject so, string name, bool value)
        {
            SerializedProperty p = so.FindProperty(name);
            if (p != null)
                p.boolValue = value;
        }
    }
}
