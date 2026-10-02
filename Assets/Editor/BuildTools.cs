/*********************************************************************************************
 * COMPONENT OF: Editor tools (not part of the game build)
 * REQUIRED DEPENDENCIES: Universal Render Pipeline, TextMeshPro
 * DESCRIPTION: Small helpers shared by the "Tools > I Am A Ball" menu: making folders,
 *              loading models and sounds, sizing models to fit a space, making materials
 *              and signs.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IAmABall.EditorTools
{
    public static class BuildTools
    {
        public const string MenuRoot = "Tools/I Am A Ball/";
        public const string KenneyRoot = "Assets/ThirdParty/Kenney";
        public const string GeneratedRoot = "Assets/Generated";
        public const string GameScenePath = "Assets/Scenes/Game.unity";

        // ---------- Folders, assets and scene ----------

        public static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // Loads the auto-made prefab for a Kenney model, making it first if needed
        public static GameObject Prefab(string pack, string model)
        {
            string prefabPath = ModelAutoSetup.PrefabRoot + "/" + pack + "/" + model + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
                return prefab;

            prefab = ModelAutoSetup.MakePrefab(KenneyRoot + "/" + pack + "/" + model + ".fbx");
            if (prefab == null)
                Debug.LogWarning("[I Am A Ball] Missing model: " + pack + "/" + model);

            return prefab;
        }

        public static AudioClip Sound(string pathInsideAudio)
        {
            return AssetDatabase.LoadAssetAtPath<AudioClip>(KenneyRoot + "/Audio/" + pathInsideAudio + ".ogg");
        }

        public static GameObject Spawn(GameObject asset, Transform parent)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            return go;
        }

        public static Transform Group(string name, Transform parent)
        {
            Transform group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        // Deletes an object made by an earlier run so tools can be run again safely
        public static void DeleteRoot(string name)
        {
            GameObject old = GameObject.Find(name);
            while (old != null)
            {
                Object.DestroyImmediate(old);
                old = GameObject.Find(name);
            }
        }

        public static Transform FindChildGroup(string rootName, string groupName)
        {
            GameObject root = GameObject.Find(rootName);
            return root != null ? root.transform.Find(groupName) : null;
        }

        public static void MarkSceneDirty()
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        public static void OpenGameScene()
        {
            if (EditorSceneManager.GetActiveScene().path != GameScenePath)
                EditorSceneManager.OpenScene(GameScenePath);
        }

        public static void SaveScene()
        {
            MarkSceneDirty();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        // ---------- Measuring and fitting models ----------

        public static Bounds WorldBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(go.transform.position, Vector3.zero);

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers)
                bounds.Encapsulate(r.bounds);

            return bounds;
        }

        // Turns the object, scales it (same amount on every axis) so it fits inside the
        // given box, then stands it on the ground with its middle at 'groundCenter'
        public static float FitInside(GameObject go, Vector3 groundCenter, float yaw, float maxX, float maxZ, float maxHeight)
        {
            Transform t = go.transform;
            t.rotation = Quaternion.Euler(0f, yaw, 0f);
            t.localScale = Vector3.one;
            t.position = Vector3.zero;

            Bounds b = WorldBounds(go);
            float scale = float.MaxValue;
            if (b.size.x > 0.001f) scale = Mathf.Min(scale, maxX / b.size.x);
            if (b.size.z > 0.001f) scale = Mathf.Min(scale, maxZ / b.size.z);
            if (b.size.y > 0.001f) scale = Mathf.Min(scale, maxHeight / b.size.y);
            if (scale == float.MaxValue) scale = 1f;

            t.localScale = Vector3.one * scale;
            StandOnGround(go, groundCenter);
            return scale;
        }

        // Scales the object so its height (or longest side) is exactly 'size'
        public static float FitSize(GameObject go, Vector3 groundCenter, float yaw, float size, bool useHeight)
        {
            Transform t = go.transform;
            t.rotation = Quaternion.Euler(0f, yaw, 0f);
            t.localScale = Vector3.one;
            t.position = Vector3.zero;

            Bounds b = WorldBounds(go);
            float current = useHeight ? b.size.y : Mathf.Max(b.size.x, b.size.z);
            float scale = current > 0.001f ? size / current : 1f;

            t.localScale = Vector3.one * scale;
            StandOnGround(go, groundCenter);
            return scale;
        }

        // Moves the object so the bottom of its model touches the ground point
        public static void StandOnGround(GameObject go, Vector3 groundCenter)
        {
            Bounds b = WorldBounds(go);
            Vector3 offset = new Vector3(groundCenter.x - b.center.x, groundCenter.y - b.min.y, groundCenter.z - b.center.z);
            go.transform.position += offset;
        }

        // Height of whatever solid thing is just under a point (roads, sidewalks, a room floor),
        // or 'fallback'. It looks from a little above the point, so floors under other floors work.
        public static float GroundHeight(Vector3 point, float fallback)
        {
            Vector3 from = new Vector3(point.x, point.y + 3f, point.z);
            if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point.y;

            return fallback;
        }

        // ---------- Materials, layers and signs ----------

        public static Material GetMaterial(string name, Color color, bool unlit)
        {
            EnsureFolder(GeneratedRoot + "/Materials");
            string path = GeneratedRoot + "/Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Material material, bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);

            if (!keepCollider)
                Object.DestroyImmediate(go.GetComponent<Collider>());

            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        // Adds a layer (like "Minimap") to the project if it does not exist yet
        public static int EnsureLayer(string layerName)
        {
            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0)
                return existing;

            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    return i;
                }
            }

            Debug.LogWarning("[I Am A Ball] No free layer for " + layerName);
            return 0;
        }

        public static void SetLayer(GameObject go, int layer)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
        }

        // A board with big text on it, facing out toward 'facing' so people on the street can read it
        public static GameObject MakeSign(string text, Vector3 position, Vector3 facing, float width, Color boardColor, Transform parent)
        {
            GameObject sign = new GameObject("Sign - " + text);
            sign.transform.SetParent(parent, false);
            sign.transform.position = position;
            sign.transform.rotation = Quaternion.LookRotation(-facing, Vector3.up);

            float height = width * 0.26f;
            GameObject board = Primitive(PrimitiveType.Cube, "Board", sign.transform, GetMaterial("Sign " + ColorUtility.ToHtmlStringRGB(boardColor), boardColor, false), false);
            board.transform.localPosition = new Vector3(0f, 0f, 0.08f);
            board.transform.localScale = new Vector3(width, height, 0.12f);

            GameObject label = new GameObject("Text");
            label.transform.SetParent(sign.transform, false);
            TextMeshPro tmp = label.AddComponent<TextMeshPro>();
            tmp.font = TMP_Settings.defaultFontAsset;
            tmp.text = text;
            tmp.color = Color.white;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.5f;
            tmp.fontSizeMax = 40f;
            tmp.rectTransform.sizeDelta = new Vector2(width * 0.92f, height * 0.8f);
            tmp.ForceMeshUpdate();

            return sign;
        }
    }
}
