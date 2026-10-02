/*********************************************************************************************
 * COMPONENT OF: Editor tools (runs by itself whenever a model is imported)
 * REQUIRED DEPENDENCIES: none
 * DESCRIPTION: Automatic setup for 3D models. Any model put in Assets/Models (your own
 *              Blender exports) or Assets/ThirdParty (downloaded packs) gets:
 *                - sensible import settings (no cameras/lights, animations only for characters,
 *                  looping walk/idle animations)
 *                - a ready-to-use prefab in Assets/Prefabs/Auto with a collider that fits it
 *              Drop a model into Assets/Models/<Category>/ and its prefab appears by itself.
 *              Menu: Tools > I Am A Ball > 1. Make Prefabs From Models (re-makes them all).
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IAmABall.EditorTools
{
    public class ModelAutoSetup : AssetPostprocessor
    {
        public const string PrefabRoot = "Assets/Prefabs/Auto";
        const string UserModelFolder = "Assets/Models/";
        static readonly string[] ManagedFolders = { "Assets/ThirdParty/", UserModelFolder };
        static readonly string[] ModelExtensions = { ".fbx", ".obj", ".blend", ".dae" };
        static readonly string[] LoopingClips = { "idle", "walk", "sprint", "sit", "drive", "crouch", "holding" };
        // Packs that get no prefab: characters are built into NPCs, audio is not a model
        static readonly string[] SkipPrefabFolders = { "Characters", "Audio" };

        static bool IsManaged(string path) => ManagedFolders.Any(folder => path.StartsWith(folder));
        static bool IsCharacter(string path) => path.Contains("/Characters/");
        static bool IsModel(string path) => ModelExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

        // ---------- Import settings (first import only, so your own changes are kept) ----------

        void OnPreprocessModel()
        {
            if (!IsManaged(assetPath))
                return;

            ModelImporter importer = (ModelImporter)assetImporter;
            if (!importer.importSettingsMissing)
                return;

            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = IsCharacter(assetPath);
            importer.animationType = IsCharacter(assetPath) ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
        }

        // Makes walk, idle and run animations loop
        void OnPreprocessAnimation()
        {
            if (!IsManaged(assetPath) || !IsCharacter(assetPath))
                return;

            ModelImporter importer = (ModelImporter)assetImporter;
            if (!importer.importSettingsMissing)
                return;

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                string name = clip.name.ToLowerInvariant();
                clip.loopTime = LoopingClips.Any(loop => name.Contains(loop));
            }

            importer.clipAnimations = clips;
        }

        // New models in Assets/Models get their prefab made automatically
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            List<string> newUserModels = imported.Where(p => p.StartsWith(UserModelFolder) && IsModel(p)).ToList();
            if (newUserModels.Count == 0)
                return;

            EditorApplication.delayCall += () =>
            {
                foreach (string path in newUserModels)
                    MakePrefab(path);

                AssetDatabase.SaveAssets();
                Debug.Log("[I Am A Ball] Made prefabs for " + newUserModels.Count + " new model(s) in " + PrefabRoot);
            };
        }

        // ---------- Prefabs ----------

        [MenuItem(BuildTools.MenuRoot + "1. Make Prefabs From Models", priority = 1)]
        static void MakeAllPrefabsMenu()
        {
            int made = MakeAllPrefabs(true);
            Debug.Log("[I Am A Ball] Made " + made + " prefabs in " + PrefabRoot);
        }

        public static int MakeAllPrefabs(bool remakeExisting)
        {
            List<string> models = new List<string>();
            foreach (string folder in new[] { BuildTools.KenneyRoot, "Assets/Models" })
            {
                if (!AssetDatabase.IsValidFolder(folder))
                    continue;

                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!SkipPrefabFolders.Any(s => path.Contains("/" + s + "/")))
                        models.Add(path);
                }
            }

            int made = 0;
            foreach (string path in models)
            {
                if (!remakeExisting && File.Exists(PrefabPathFor(path)))
                    continue;

                if (MakePrefab(path) != null)
                    made++;
            }

            AssetDatabase.SaveAssets();
            return made;
        }

        static string PrefabPathFor(string modelPath)
        {
            string category = Path.GetFileName(Path.GetDirectoryName(modelPath));
            return PrefabRoot + "/" + category + "/" + Path.GetFileNameWithoutExtension(modelPath) + ".prefab";
        }

        // Makes one prefab (a variant of the model, so changes to the model still show up)
        public static GameObject MakePrefab(string modelPath)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
                return null;

            string prefabPath = PrefabPathFor(modelPath);
            BuildTools.EnsureFolder(Path.GetDirectoryName(prefabPath));

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                string category = Path.GetFileName(Path.GetDirectoryName(modelPath));
                AddCollider(instance, model.name.ToLowerInvariant(), category);

                if (category != "Cars")
                    GameObjectUtility.SetStaticEditorFlags(instance, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic);

                return PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        // Picks a collider shape from the model's name and size
        static void AddCollider(GameObject go, string name, string category)
        {
            if (go.GetComponentInChildren<Collider>() != null)
                return;

            // Small decorations you should be able to roll through
            if (category == "Food" || name.Contains("flower") || name.Contains("grass") || name.Contains("rug") || name.Contains("awning") || name.Contains("overhang"))
                return;

            Bounds b = BuildTools.WorldBounds(go);
            Vector3 center = b.center - go.transform.position;

            bool isPole = name.Contains("light") || name.Contains("sign") || name.Contains("pole") || name.Contains("parasol");
            bool isTree = name.Contains("tree") || name.Contains("palm");

            if (isPole || isTree)
            {
                // A thin upright capsule: the trunk or pole, not the leaves or lamp arm
                CapsuleCollider capsule = go.AddComponent<CapsuleCollider>();
                capsule.direction = 1;
                capsule.height = b.size.y;
                capsule.radius = Mathf.Max(0.04f, Mathf.Min(b.size.x, b.size.z) * (isTree ? 0.15f : 0.12f));
                capsule.center = new Vector3(isTree ? center.x : 0f, center.y, isTree ? center.z : 0f);
                return;
            }

            BoxCollider box = go.AddComponent<BoxCollider>();
            box.center = center;
            box.size = b.size;
        }
    }
}
