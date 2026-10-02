/*********************************************************************************************
 * COMPONENT OF: Editor tools (Tools > I Am A Ball > Optimize For Slow Laptops)
 * REQUIRED DEPENDENCIES: the Game scene, the "City NavMesh" made by GameplayBuilder
 * DESCRIPTION: One-time speed-ups that are done in the editor instead of while playing:
 *                - Bake NavMesh: saves the walking map for NPCs so it is not built every
 *                  time you press Play (faster start, no "read access" warnings)
 *                - Bake Occlusion Culling: Unity works out which things are hidden behind
 *                  buildings, and skips drawing them while playing
 *                - Shrink Big Textures: 4K textures become 1K (looks almost the same in a
 *                  low poly game, uses much less graphics memory)
 *                - Street light shadows off (see LightingFixer)
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IAmABall.EditorTools
{
    public static class PerformanceBaker
    {
        const int MaxTextureSize = 1024;
        static readonly string[] TextureFolders = { "Assets/Textures", "Assets/Materials", "Assets/prefab" };

        [MenuItem(BuildTools.MenuRoot + "Optimize For Slow Laptops (do all)", priority = 40)]
        static void OptimizeAllMenu()
        {
            OptimizeAll();
            Debug.Log("[I Am A Ball] Optimized. Save the scene (Ctrl+S) to keep it.");
        }

        [MenuItem(BuildTools.MenuRoot + "Speed/Bake NavMesh", priority = 41)]
        static void BakeNavMeshMenu() => BakeNavMesh();

        [MenuItem(BuildTools.MenuRoot + "Speed/Bake Occlusion Culling", priority = 42)]
        static void BakeOcclusionMenu() => BakeOcclusion();

        [MenuItem(BuildTools.MenuRoot + "Speed/Shrink Big Textures", priority = 43)]
        static void ShrinkTexturesMenu() => ShrinkBigTextures();

        public static void OptimizeAll()
        {
            LightingFixer.TurnOffSmallLightShadows();
            ShrinkBigTextures();
            BakeNavMesh();
            BakeOcclusion();
            BuildTools.MarkSceneDirty();
        }

        // Builds the NPC walking map now and saves it next to the scene
        public static void BakeNavMesh()
        {
            NavMeshSurface surface = Object.FindAnyObjectByType<NavMeshSurface>();
            if (surface == null)
            {
                Debug.LogWarning("[I Am A Ball] No NavMeshSurface found. Run Build Everything first.");
                return;
            }

            surface.RemoveData();
            surface.BuildNavMesh();

            string folder = SceneDataFolder();
            string path = folder + "/City NavMesh.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(surface.navMeshData, path);

            EditorUtility.SetDirty(surface);
            AssetDatabase.SaveAssets();
            Debug.Log("[I Am A Ball] NavMesh baked to " + path);
        }

        // Works out which objects hide others, so hidden things are not drawn while playing
        public static void BakeOcclusion()
        {
            StaticOcclusionCulling.smallestOccluder = 3f;
            StaticOcclusionCulling.smallestHole = 0.25f;
            StaticOcclusionCulling.backfaceThreshold = 100f;

            if (StaticOcclusionCulling.Compute())
                Debug.Log("[I Am A Ball] Occlusion culling baked");
            else
                Debug.LogWarning("[I Am A Ball] Occlusion culling bake did not finish");
        }

        // Caps every large texture in the project's own folders at 1024 pixels
        public static int ShrinkBigTextures()
        {
            int changed = 0;
            foreach (string folder in TextureFolders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                    continue;

                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null || importer.maxTextureSize <= MaxTextureSize)
                        continue;

                    importer.maxTextureSize = MaxTextureSize;
                    importer.SaveAndReimport();
                    changed++;
                }
            }

            Debug.Log("[I Am A Ball] Shrunk " + changed + " big textures to " + MaxTextureSize + " px");
            return changed;
        }

        // Assets/Scenes/Game (Unity's usual place for data that belongs to the Game scene)
        static string SceneDataFolder()
        {
            string scenePath = EditorSceneManager.GetActiveScene().path;
            string folder = Path.GetDirectoryName(scenePath).Replace('\\', '/') + "/" + Path.GetFileNameWithoutExtension(scenePath);
            BuildTools.EnsureFolder(folder);
            return folder;
        }
    }
}
