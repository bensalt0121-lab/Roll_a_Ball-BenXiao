/*********************************************************************************************
 * COMPONENT OF: Editor tools (Tools > I Am A Ball > 4. Build Outside Environment)
 * REQUIRED DEPENDENCIES: the 8 "Base cube" city blocks in the Game scene (CityBuilder),
 *                        Kenney models in Assets/ThirdParty/Kenney, ModelAutoSetup, BuildTools,
 *                        your "Mountain Lowpoly" model in Assets/prefab
 * DESCRIPTION: Builds the land around the city so the world does not end at the city edge:
 *                - a bigger play area with invisible walls and a wooden fence you can see
 *                - green grass north, east and south of the city
 *                - a pine forest with a dirt path and benches (north)
 *                - a lake park with a beach, a dock, trees and benches (east)
 *                - a sports field with goals and bleachers (south)
 *                - "ROAD CLOSED" barriers in front of the three west tunnels
 *                - extra mountains and a ring of trees outside the fence (decoration only)
 *              Small props go on the "Details" layer so the game can skip drawing them far
 *              away. Everything goes under "== GENERATED OUTSIDE ENVIRONMENT ==" and running
 *              the tool again replaces it, so it is safe to try. Your old "boudary wall"
 *              objects are only switched off (not deleted).
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace IAmABall.EditorTools
{
    public static class EnvironmentBuilder
    {
        public const string RootName = "== GENERATED OUTSIDE ENVIRONMENT ==";

        // The edges of the area the player can roll around in (meters)
        public const float WestX = -39.1f;
        public const float EastX = 100f;
        public const float SouthZ = -95f;
        public const float NorthZ = 95f;
        const float WallHeight = 40f;
        const float WallThickness = 2f;
        const float WallBottom = -1f;
        // The fence stands this far inside the invisible walls
        const float FenceInset = 1f;
        const float FencePostGap = 4f;

        // Top of the new grass, and top of the big brown "Grounf" cube outside the walls
        const float GrassTop = 0f;
        const float OutsideGroundY = -0.05f;
        // Extra room around the 8 blocks for the outer road and sidewalk
        const float CityRoadMargin = 4.4f;

        // The west tunnels (roads into the mountain) that get closed off
        static readonly float[] TunnelRoadsZ = { -25.5f, 0f, 25.5f };
        const float BarrierX = -35f;

        static readonly Vector3 LakeCenter = new Vector3(72f, 0f, 0f);
        static readonly Vector2 LakeSize = new Vector2(34f, 22f);
        static readonly Vector2 BeachSize = new Vector2(40f, 28f);
        static readonly Vector3 FieldCenter = new Vector3(12f, 0f, -75f);
        static readonly Vector2 FieldSize = new Vector2(32f, 20f);
        const string MountainPath = "Assets/prefab/Mountain Lowpoly.fbx";

        static readonly Color GrassColor = new Color(0.42f, 0.62f, 0.30f);
        static readonly Color FieldColor = new Color(0.33f, 0.58f, 0.25f);
        static readonly Color WaterColor = new Color(0.25f, 0.50f, 0.85f);
        static readonly Color SandColor = new Color(0.86f, 0.78f, 0.55f);
        static readonly Color PathColor = new Color(0.66f, 0.52f, 0.34f);
        static readonly Color WoodColor = new Color(0.50f, 0.33f, 0.18f);
        static readonly Color ClosedSignColor = new Color(0.80f, 0.15f, 0.12f);

        // The boundary rectangle as a box, so other tools can check "is this inside the map?"
        public static Bounds PlayArea
        {
            get
            {
                Vector3 center = new Vector3((WestX + EastX) * 0.5f, WallBottom + WallHeight * 0.5f, (SouthZ + NorthZ) * 0.5f);
                Vector3 size = new Vector3(EastX - WestX, WallHeight, NorthZ - SouthZ);
                return new Bounds(center, size);
            }
        }

        static System.Random random;
        static Transform root;
        static int groundLayer, details;
        // Outer edge of the city (blocks + outer road + sidewalk)
        static float cityMinX, cityMaxX, cityMinZ, cityMaxZ;
        // Every tree placed so far, so new trees keep their distance
        static List<Vector3> trees;
        // Spots that must stay free of trees (benches, path, dock)
        static List<Vector3> keepClear;

        [MenuItem(BuildTools.MenuRoot + "4. Build Outside Environment", priority = 4)]
        static void BuildMenu()
        {
            Build();
            BuildTools.MarkSceneDirty();
        }

        public static void Build()
        {
            BuildTools.DeleteRoot(RootName);

            random = new System.Random(4040);
            root = new GameObject(RootName).transform;
            groundLayer = Mathf.Max(0, LayerMask.NameToLayer("Ground"));
            details = BuildTools.EnsureLayer("Details");
            trees = new List<Vector3>();
            keepClear = new List<Vector3>();
            FindCityEdges();

            HideOldWalls();
            BuildWalls(BuildTools.Group("Invisible Walls", root));
            BuildFence(BuildTools.Group("Fence", root));
            BuildRoadBlocks(BuildTools.Group("Closed Tunnel Roads", root));
            BuildGrass(BuildTools.Group("Grass", root));
            BuildPineForest(BuildTools.Group("Pine Forest (north)", root));
            BuildLakePark(BuildTools.Group("Lake Park (east)", root));
            BuildSportsField(BuildTools.Group("Sports Field (south)", root));
            BuildMountains(BuildTools.Group("Far Mountains", root));
            BuildForestBand(BuildTools.Group("Forest Outside The Fence", root));

            Undo.RegisterCreatedObjectUndo(root.gameObject, "Build Outside Environment");
            int count = root.GetComponentsInChildren<Transform>(true).Length;
            Debug.Log("[I Am A Ball] Outside environment built (" + count + " objects)");
        }

        // ---------- The city edge ----------

        // Measures the 8 city blocks; if they cannot be found, uses the usual numbers
        static void FindCityEdges()
        {
            cityMinX = -15.3f;
            cityMaxX = 40.7f;
            cityMinZ = -53.6f;
            cityMaxZ = 53.3f;

            List<CityBuilder.Block> blocks = CityBuilder.FindBlocks();
            if (blocks.Count == 0)
                return;

            Bounds all = blocks[0].bounds;
            foreach (CityBuilder.Block block in blocks)
                all.Encapsulate(block.bounds);

            cityMinX = all.min.x - CityRoadMargin;
            cityMaxX = all.max.x + CityRoadMargin;
            cityMinZ = all.min.z - CityRoadMargin;
            cityMaxZ = all.max.z + CityRoadMargin;
        }

        // ---------- 1. Boundary: invisible walls, fence and closed roads ----------

        // Switches off (does not delete) the old walls that were too close to the city
        static void HideOldWalls()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                foreach (GameObject go in scene.GetRootGameObjects())
                {
                    if (go.name.StartsWith("boudary wall") && go.activeSelf)
                    {
                        Undo.RecordObject(go, "Build Outside Environment");
                        go.SetActive(false);
                    }
                }
            }
        }

        // Four walls with only a collider (nothing to draw), their inner side on the boundary
        static void BuildWalls(Transform group)
        {
            float midY = WallBottom + WallHeight * 0.5f;
            float midX = (WestX + EastX) * 0.5f;
            float midZ = (SouthZ + NorthZ) * 0.5f;
            float half = WallThickness * 0.5f;
            float lengthX = EastX - WestX + WallThickness * 2f;
            float lengthZ = NorthZ - SouthZ + WallThickness * 2f;

            InvisibleWall(group, "Wall West", new Vector3(WestX - half, midY, midZ), new Vector3(WallThickness, WallHeight, lengthZ));
            InvisibleWall(group, "Wall East", new Vector3(EastX + half, midY, midZ), new Vector3(WallThickness, WallHeight, lengthZ));
            InvisibleWall(group, "Wall South", new Vector3(midX, midY, SouthZ - half), new Vector3(lengthX, WallHeight, WallThickness));
            InvisibleWall(group, "Wall North", new Vector3(midX, midY, NorthZ + half), new Vector3(lengthX, WallHeight, WallThickness));
        }

        static void InvisibleWall(Transform group, string name, Vector3 center, Vector3 size)
        {
            GameObject wall = new GameObject(name);
            wall.transform.SetParent(group, false);
            wall.transform.position = center;
            wall.AddComponent<BoxCollider>().size = size;
        }

        // A wooden fence 1 m inside the north, east and south walls (the west side stays open)
        static void BuildFence(Transform group)
        {
            Material wood = BuildTools.GetMaterial("Fence Wood", WoodColor, false);
            float west = WestX;
            float east = EastX - FenceInset;
            float south = SouthZ + FenceInset;
            float north = NorthZ - FenceInset;

            FenceSide(group, wood, "North", new Vector3(west, 0f, north), new Vector3(east, 0f, north), false);
            FenceSide(group, wood, "South", new Vector3(west, 0f, south), new Vector3(east, 0f, south), false);
            // The corner posts are already there from the north and south sides
            FenceSide(group, wood, "East", new Vector3(east, 0f, south), new Vector3(east, 0f, north), true);
        }

        // Posts every few meters plus two long rails (the rails stop the ball, the posts do not)
        static void FenceSide(Transform group, Material wood, string side, Vector3 from, Vector3 to, bool skipEnds)
        {
            float length = Vector3.Distance(from, to);
            int gaps = Mathf.CeilToInt(length / FencePostGap);
            int first = skipEnds ? 1 : 0;
            int last = skipEnds ? gaps - 1 : gaps;

            for (int i = first; i <= last; i++)
            {
                Vector3 spot = Vector3.Lerp(from, to, i / (float)gaps);
                Box("Fence Post " + side, group, wood, spot + Vector3.up * 0.6f, new Vector3(0.18f, 1.2f, 0.18f), false);
            }

            Quaternion along = Quaternion.LookRotation(to - from);
            Vector3 middle = (from + to) * 0.5f;
            foreach (float height in new[] { 0.4f, 0.9f })
            {
                GameObject rail = Box("Fence Rail " + side, group, wood, middle + Vector3.up * height, new Vector3(0.08f, 0.14f, length), true);
                rail.transform.rotation = along;
            }
        }

        // Construction barriers and a ROAD CLOSED sign in front of each west tunnel
        static void BuildRoadBlocks(Transform group)
        {
            Physics.SyncTransforms();
            foreach (float roadZ in TunnelRoadsZ)
            {
                Vector3 roadMiddle = new Vector3(BarrierX, 0f, roadZ);
                roadMiddle.y = RoadHeight(roadMiddle);

                for (int i = -1; i <= 1; i++)
                    Prop(group, "CityRoads", "construction-barrier", roadMiddle + Vector3.forward * (i * 2.4f), 90f, 2.2f, false);

                ClosedSign(group, roadMiddle + Vector3.left * 0.9f);
            }
        }

        // Height of the tunnel road (or 0 if the ray hits something odd like the tunnel roof)
        static float RoadHeight(Vector3 point)
        {
            float height = BuildTools.GroundHeight(point, GrassTop);
            return Mathf.Abs(height) > 1.5f ? GrassTop : height;
        }

        // The sign faces east (toward the city) and stands on two posts
        static void ClosedSign(Transform group, Vector3 groundSpot)
        {
            const float width = 3f;
            const float height = 1.9f;
            GameObject sign = BuildTools.MakeSign("ROAD CLOSED", groundSpot + Vector3.up * height, Vector3.right, width, ClosedSignColor, group);

            Material wood = BuildTools.GetMaterial("Fence Wood", WoodColor, false);
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject post = BuildTools.Primitive(PrimitiveType.Cube, "Post", sign.transform, wood, false);
                post.transform.localPosition = new Vector3(side * width * 0.4f, -height * 0.5f, 0.16f);
                post.transform.localScale = new Vector3(0.12f, height, 0.12f);
            }

            BuildTools.SetLayer(sign, details);
        }

        // ---------- 2. Grass ----------

        // Three big flat green boxes around the city (not over the city or the west park)
        static void BuildGrass(Transform group)
        {
            Material grass = BuildTools.GetMaterial("Outside Grass", GrassColor, false);
            float west = WestX;
            float east = EastX;
            float nearNorth = cityMaxZ + 0.5f;
            float nearSouth = cityMinZ - 0.5f;

            GrassPatch(group, grass, "Grass North", west, east, nearNorth, NorthZ);
            GrassPatch(group, grass, "Grass South", west, east, SouthZ, nearSouth);
            GrassPatch(group, grass, "Grass East", cityMaxX + 0.5f, east, nearSouth, nearNorth);

            // West of the city (around the park and tunnel roads): a little lower than the
            // sidewalks there, so it only shows where the ground was bare brown
            GrassPatch(group, grass, "Grass West", west, cityMinX - 0.5f, nearSouth, nearNorth, -0.03f);
        }

        // A 0.1 m thick box whose top is at 'top' (GrassTop by default); the player can jump on it
        static void GrassPatch(Transform group, Material grass, string name, float minX, float maxX, float minZ, float maxZ, float top = GrassTop)
        {
            Vector3 center = new Vector3((minX + maxX) * 0.5f, top - 0.05f, (minZ + maxZ) * 0.5f);
            Vector3 size = new Vector3(maxX - minX, 0.1f, maxZ - minZ);
            GameObject patch = Box(name, group, grass, center, size, true);
            patch.layer = groundLayer;
            NoShadows(patch);
        }

        // ---------- 3. North: Pine Forest ----------

        static void BuildPineForest(Transform group)
        {
            float pathX = (cityMinX + cityMaxX) * 0.5f;
            const float pathEndZ = 85f;
            ForestPath(group, pathX, cityMaxZ, pathEndZ);
            PathBenches(group, pathX, cityMaxZ, pathEndZ);

            // Trees stay off the path, off the road and inside the fence
            Rect area = Rect.MinMaxRect(WestX + 3f, cityMaxZ + 3f, EastX - 4f, NorthZ - 4f);
            bool OffPath(Vector3 p) => Mathf.Abs(p.x - pathX) > 4f || p.z > pathEndZ + 3f;

            string[] pines = { "tree_pineRoundA", "tree_cone", "tree_tall", "tree_default", "tree_simple" };
            ScatterTrees(group, pines, 40, area, 4f, 7f, OffPath);

            string[] rocks = { "rock_smallA", "stone_largeA" };
            for (int i = 0; i < 10; i++)
            {
                if (!FindSpot(area, trees, 2.5f, OffPath, out Vector3 spot))
                    continue;
                bool big = rocks[i % 2] == "stone_largeA";
                float size = big ? Range(1.0f, 1.8f) : Range(0.5f, 0.9f);
                Prop(group, "Nature", rocks[i % 2], spot, Range(0f, 360f), size, true);
            }

            string[] flowers = { "flower_purpleA", "flower_redA", "flower_yellowA", "grass", "grass_large" };
            for (int i = 0; i < 25; i++)
            {
                if (!FindSpot(area, trees, 1.5f, OffPath, out Vector3 spot))
                    continue;
                Decoration(group, "Nature", Pick(flowers), spot, Range(0f, 360f), Range(0.35f, 0.6f));
            }
        }

        // A light brown dirt strip from the city's north road into the forest
        static void ForestPath(Transform group, float pathX, float startZ, float endZ)
        {
            Material dirt = BuildTools.GetMaterial("Forest Path", PathColor, false);
            Vector3 center = new Vector3(pathX, GrassTop + 0.01f, (startZ + endZ) * 0.5f);
            GameObject path = Box("Dirt Path", group, dirt, center, new Vector3(3f, 0.04f, endZ - startZ), false);
            NoShadows(path);

            // Remember the path so nothing grows on it
            for (float z = startZ; z <= endZ; z += 3f)
                keepClear.Add(new Vector3(pathX, 0f, z));
        }

        // Three benches beside the path, facing it, on alternating sides
        static void PathBenches(Transform group, float pathX, float startZ, float endZ)
        {
            for (int i = 1; i <= 3; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                Vector3 spot = new Vector3(pathX + side * 2.6f, GrassTop, Mathf.Lerp(startZ, endZ, i / 4f));
                Vector3 facing = side > 0 ? Vector3.left : Vector3.right;
                Bench(group, spot, Quaternion.LookRotation(facing).eulerAngles.y);
            }
        }

        // ---------- 4. East: Lake Park ----------

        static void BuildLakePark(Transform group)
        {
            Material water = BuildTools.GetMaterial("Lake Water", WaterColor, false);
            water.SetFloat("_Smoothness", 0.85f);
            EditorUtility.SetDirty(water);
            Material sand = BuildTools.GetMaterial("Lake Sand", SandColor, false);

            // Flat cylinders squashed into ovals; no colliders, the grass below holds the ball up
            FlatDisc(group, "Beach", sand, LakeCenter, BeachSize, GrassTop + 0.015f);
            FlatDisc(group, "Lake", water, LakeCenter, LakeSize, GrassTop + 0.03f);
            Dock(group);
            LakeBenches(group);

            // Trees and bushes in a ring around the beach, inside the east grass
            Rect area = Rect.MinMaxRect(cityMaxX + 3f, -32f, EastX - 4f, 32f);
            bool AroundLake(Vector3 p) => OvalDistance(p, BeachSize * 0.5f + Vector2.one * 2f) > 1f
                                       && OvalDistance(p, BeachSize * 0.5f + Vector2.one * 14f) < 1f;

            string[] lakeTrees = { "tree_oak", "tree_palm", "tree_palmTall", "tree_fat" };
            ScatterTrees(group, lakeTrees, 18, area, 4.5f, 7.5f, AroundLake);

            string[] bushes = { "plant_bush", "plant_bushLarge" };
            for (int i = 0; i < 6; i++)
            {
                if (!FindSpot(area, trees, 2.5f, AroundLake, out Vector3 spot))
                    continue;
                Prop(group, "Nature", bushes[i % 2], spot, Range(0f, 360f), Range(0.8f, 1.3f), true);
            }
        }

        // An oval made from a cylinder, 'size' wide (x) and deep (z), with its top at 'top'
        static GameObject FlatDisc(Transform group, string name, Material material, Vector3 center, Vector2 size, float top)
        {
            const float thickness = 0.02f;
            GameObject disc = BuildTools.Primitive(PrimitiveType.Cylinder, name, group, material, false);
            disc.transform.position = new Vector3(center.x, top - thickness * 0.5f, center.z);
            // A Unity cylinder is 1 m wide and 2 m tall, so y scale is half the thickness
            disc.transform.localScale = new Vector3(size.x, thickness * 0.5f, size.y);
            MakeStatic(disc);
            NoShadows(disc);
            return disc;
        }

        // How far a point is from the lake middle: below 1 is inside the oval, above 1 is outside
        static float OvalDistance(Vector3 point, Vector2 halfSize)
        {
            float dx = (point.x - LakeCenter.x) / halfSize.x;
            float dz = (point.z - LakeCenter.z) / halfSize.y;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // A small wooden dock sticking into the lake from its west shore
        static void Dock(Transform group)
        {
            Material wood = BuildTools.GetMaterial("Fence Wood", WoodColor, false);
            float shoreX = LakeCenter.x - BeachSize.x * 0.5f + 1f;
            float endX = LakeCenter.x - LakeSize.x * 0.5f + 7f;
            float deckTop = GrassTop + 0.12f;
            Vector3 deckCenter = new Vector3((shoreX + endX) * 0.5f, deckTop - 0.05f, LakeCenter.z);

            GameObject deck = Box("Dock Deck", group, wood, deckCenter, new Vector3(endX - shoreX, 0.1f, 2.2f), true);
            BuildTools.SetLayer(deck, details);

            // Four short posts at the corners of the far half of the dock
            foreach (float x in new[] { (shoreX + endX) * 0.5f, endX - 0.15f })
            {
                foreach (float z in new[] { -1f, 1f })
                {
                    Vector3 spot = new Vector3(x, deckTop + 0.15f, LakeCenter.z + z);
                    GameObject post = Box("Dock Post", group, wood, spot, new Vector3(0.18f, 0.5f, 0.18f), false);
                    BuildTools.SetLayer(post, details);
                }
            }

            keepClear.Add(new Vector3(shoreX - 2f, 0f, LakeCenter.z));
        }

        // Three benches on the grass around the beach, looking at the water
        static void LakeBenches(Transform group)
        {
            Vector2 ring = BeachSize * 0.5f + Vector2.one * 2.5f;
            foreach (float angle in new[] { 20f, 100f, 260f })
            {
                float radians = angle * Mathf.Deg2Rad;
                Vector3 spot = LakeCenter + new Vector3(Mathf.Cos(radians) * ring.x, 0f, Mathf.Sin(radians) * ring.y);
                spot.y = GrassTop;
                Vector3 toLake = LakeCenter - spot;
                toLake.y = 0f;
                Bench(group, spot, Quaternion.LookRotation(toLake).eulerAngles.y);
            }
        }

        // ---------- 5. South: Sports Field ----------

        static void BuildSportsField(Transform group)
        {
            Material turf = BuildTools.GetMaterial("Sports Field", FieldColor, false);
            Material white = BuildTools.GetMaterial("Field White", Color.white, false);
            Material grey = BuildTools.GetMaterial("Bleacher Grey", new Color(0.6f, 0.6f, 0.63f), false);

            Vector3 turfCenter = new Vector3(FieldCenter.x, GrassTop + 0.01f, FieldCenter.z);
            NoShadows(Box("Field", group, turf, turfCenter, new Vector3(FieldSize.x, 0.02f, FieldSize.y), false));

            FieldLines(group, white);
            Goal(group, white, -1f);
            Goal(group, white, 1f);
            Bleachers(group, grey);

            // Trees around the field, but not on it or on the bleachers
            float halfX = FieldSize.x * 0.5f;
            float halfZ = FieldSize.y * 0.5f;
            Rect area = Rect.MinMaxRect(FieldCenter.x - halfX - 10f, SouthZ + 3f, FieldCenter.x + halfX + 10f, cityMinZ - 4f);
            Rect keepOut = Rect.MinMaxRect(FieldCenter.x - halfX - 3f, FieldCenter.z - halfZ - 6f, FieldCenter.x + halfX + 3f, FieldCenter.z + halfZ + 3f);
            string[] fieldTrees = { "tree_default", "tree_detailed", "tree_oak", "tree_fat" };
            ScatterTrees(group, fieldTrees, 12, area, 4.5f, 6.5f, p => !keepOut.Contains(new Vector2(p.x, p.z)));
        }

        // White lines: the border, the middle line and a ring of 8 short pieces in the center
        static void FieldLines(Transform group, Material white)
        {
            const float inset = 0.6f;
            const float lineWidth = 0.15f;
            float halfX = FieldSize.x * 0.5f - inset;
            float halfZ = FieldSize.y * 0.5f - inset;
            float y = GrassTop + 0.03f;
            Vector3 c = new Vector3(FieldCenter.x, y, FieldCenter.z);

            FieldLine(group, white, c + new Vector3(0f, 0f, halfZ), new Vector3(halfX * 2f, 0.02f, lineWidth));
            FieldLine(group, white, c + new Vector3(0f, 0f, -halfZ), new Vector3(halfX * 2f, 0.02f, lineWidth));
            FieldLine(group, white, c + new Vector3(halfX, 0f, 0f), new Vector3(lineWidth, 0.02f, halfZ * 2f));
            FieldLine(group, white, c + new Vector3(-halfX, 0f, 0f), new Vector3(lineWidth, 0.02f, halfZ * 2f));
            FieldLine(group, white, c, new Vector3(lineWidth, 0.02f, halfZ * 2f));

            const float radius = 3f;
            float pieceLength = 2f * Mathf.PI * radius / 8f;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f;
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;
                GameObject piece = FieldLine(group, white, c + offset, new Vector3(pieceLength, 0.02f, lineWidth));
                piece.transform.rotation = Quaternion.Euler(0f, angle, 0f);
            }
        }

        static GameObject FieldLine(Transform group, Material white, Vector3 center, Vector3 size)
        {
            GameObject line = Box("Field Line", group, white, center, size, false);
            NoShadows(line);
            return line;
        }

        // Two posts and a crossbar at one end of the field (end = -1 for west, 1 for east)
        static void Goal(Transform group, Material white, float end)
        {
            const float width = 5f;
            const float height = 2f;
            const float bar = 0.15f;
            float x = FieldCenter.x + end * (FieldSize.x * 0.5f - 0.6f);
            Transform goal = BuildTools.Group(end < 0 ? "Goal West" : "Goal East", group);

            Box("Goal Post", goal, white, new Vector3(x, height * 0.5f, FieldCenter.z - width * 0.5f), new Vector3(bar, height, bar), true);
            Box("Goal Post", goal, white, new Vector3(x, height * 0.5f, FieldCenter.z + width * 0.5f), new Vector3(bar, height, bar), true);
            Box("Crossbar", goal, white, new Vector3(x, height, FieldCenter.z), new Vector3(bar, bar, width + bar), true);
            BuildTools.SetLayer(goal.gameObject, details);
        }

        // Three grey steps along the south side of the field, the tallest at the back
        static void Bleachers(Transform group, Material grey)
        {
            Transform bleachers = BuildTools.Group("Bleachers", group);
            float frontZ = FieldCenter.z - FieldSize.y * 0.5f - 1.5f;

            for (int step = 0; step < 3; step++)
            {
                float height = 0.45f * (step + 1);
                Vector3 center = new Vector3(FieldCenter.x, GrassTop + height * 0.5f, frontZ - 0.5f - step);
                Box("Bleacher Step", bleachers, grey, center, new Vector3(14f, height, 1f), true);
            }

            BuildTools.SetLayer(bleachers.gameObject, details);
        }

        // ---------- 6. Outside the boundary (looks only, nothing to bump into) ----------

        // Three more copies of your mountain, far away, each turned a different way
        static void BuildMountains(Transform group)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(MountainPath);
            if (model == null)
            {
                Debug.LogWarning("[I Am A Ball] Missing model: " + MountainPath);
                return;
            }

            (Vector3 spot, Vector3 away, float yaw, float height)[] mountains =
            {
                (new Vector3(40f, OutsideGroundY, 175f), Vector3.forward, 35f, 85f),
                (new Vector3(190f, OutsideGroundY, 0f), Vector3.right, 160f, 90f),
                (new Vector3(40f, OutsideGroundY, -175f), Vector3.back, 280f, 72f),
            };

            foreach (var m in mountains)
            {
                GameObject mountain = (GameObject)PrefabUtility.InstantiatePrefab(model, group);
                mountain.name = "Mountain";
                BuildTools.FitSize(mountain, m.spot, m.yaw, m.height, true);
                PushAwayFromPlayArea(mountain, m.away, 16f);
                RemoveColliders(mountain);
                MakeStatic(mountain);
            }
        }

        // If a wide mountain would poke into the play area, slide it further out
        static void PushAwayFromPlayArea(GameObject go, Vector3 away, float gap)
        {
            Bounds play = PlayArea;
            Bounds b = BuildTools.WorldBounds(go);
            Vector3 size = new Vector3(Mathf.Abs(away.x), Mathf.Abs(away.y), Mathf.Abs(away.z));

            float playEdge = Vector3.Dot(play.center, away) + Vector3.Dot(play.extents, size);
            float nearSide = Vector3.Dot(b.center, away) - Vector3.Dot(b.extents, size);
            float limit = playEdge + gap;
            if (nearSide < limit)
                go.transform.position += away * (limit - nearSide);
        }

        // A band of tall pines 4-14 m outside the north, east and south fence lines
        static void BuildForestBand(Transform group)
        {
            string[] pines = { "tree_cone", "tree_pineRoundA", "tree_tall" };
            float fenceNorth = NorthZ - FenceInset;
            float fenceSouth = SouthZ + FenceInset;
            float fenceEast = EastX - FenceInset;
            const float far = 14f;

            BandRow(group, pines, 12, new Vector3(WestX - 6f, 0f, fenceNorth), new Vector3(fenceEast + far, 0f, fenceNorth), Vector3.forward);
            BandRow(group, pines, 16, new Vector3(fenceEast, 0f, fenceSouth - far), new Vector3(fenceEast, 0f, fenceNorth + far), Vector3.right);
            BandRow(group, pines, 12, new Vector3(WestX - 6f, 0f, fenceSouth), new Vector3(fenceEast + far, 0f, fenceSouth), Vector3.back);
        }

        // 'count' trees spread along a fence line, each 4-14 m out from it
        static void BandRow(Transform group, string[] models, int count, Vector3 from, Vector3 to, Vector3 outward)
        {
            for (int i = 0; i < count; i++)
            {
                float along = (i + Range(0.15f, 0.85f)) / count;
                Vector3 spot = Vector3.Lerp(from, to, along) + outward * Range(4f, 14f);
                spot.y = OutsideGroundY;
                GameObject tree = Decoration(group, "Nature", Pick(models), spot, Range(0f, 360f), Range(7f, 12f));
                if (tree != null)
                    trees.Add(spot);
            }
        }

        // ---------- Placing props ----------

        // Places 'count' trees inside 'area' at least 4 m apart, where 'allowed' says yes
        static void ScatterTrees(Transform group, string[] models, int count, Rect area, float minHeight, float maxHeight, System.Func<Vector3, bool> allowed)
        {
            for (int i = 0; i < count; i++)
            {
                if (!FindSpot(area, trees, 4f, allowed, out Vector3 spot))
                    continue;

                trees.Add(spot);
                Prop(group, "Nature", Pick(models), spot, Range(0f, 360f), Range(minHeight, maxHeight), true);
            }
        }

        // Tries random spots in the area (Rect y means our z) until one is far enough from
        // 'others', not on a keep-clear spot, and allowed. Gives up after 60 tries.
        static bool FindSpot(Rect area, List<Vector3> others, float spacing, System.Func<Vector3, bool> allowed, out Vector3 spot)
        {
            for (int attempt = 0; attempt < 60; attempt++)
            {
                spot = new Vector3(Range(area.xMin, area.xMax), GrassTop, Range(area.yMin, area.yMax));
                if (allowed(spot) && !IsTooClose(spot, others, spacing) && !IsTooClose(spot, keepClear, 2.5f))
                    return true;
            }

            spot = Vector3.zero;
            return false;
        }

        // Places a prop on the Details layer; 'size' is its height (useHeight) or its longest side
        static GameObject Prop(Transform group, string pack, string model, Vector3 position, float yaw, float size, bool useHeight)
        {
            GameObject prefab = BuildTools.Prefab(pack, model);
            if (prefab == null)
                return null;

            GameObject go = BuildTools.Spawn(prefab, group);
            BuildTools.FitSize(go, position, yaw, size, useHeight);
            BuildTools.SetLayer(go, details);
            return go;
        }

        // Like Prop, but with no colliders, so the ball rolls through (flowers, far away trees)
        static GameObject Decoration(Transform group, string pack, string model, Vector3 position, float yaw, float height)
        {
            GameObject go = Prop(group, pack, model, position, yaw, height, true);
            if (go != null)
                RemoveColliders(go);
            return go;
        }

        // A park bench about 1.6 m wide (sized by width, not height), on the Details layer
        static void Bench(Transform group, Vector3 position, float yaw)
        {
            GameObject prefab = BuildTools.Prefab("Furniture", "bench");
            if (prefab == null)
                return;

            GameObject bench = BuildTools.Spawn(prefab, group);
            Vector3 size = Quaternion.Euler(0f, yaw, 0f) * new Vector3(1.6f, 0f, 0.7f);
            BuildTools.FitInside(bench, position, yaw, Mathf.Max(0.7f, Mathf.Abs(size.x)), Mathf.Max(0.7f, Mathf.Abs(size.z)), 1.0f);
            BuildTools.SetLayer(bench, details);
            keepClear.Add(position);
        }

        // ---------- Small helpers ----------

        // A cube primitive at a world position and size, marked static so it draws cheaply
        static GameObject Box(string name, Transform parent, Material material, Vector3 center, Vector3 size, bool keepCollider)
        {
            GameObject box = BuildTools.Primitive(PrimitiveType.Cube, name, parent, material, keepCollider);
            box.transform.position = center;
            box.transform.localScale = size;
            MakeStatic(box);
            return box;
        }

        // Lets Unity batch it with other things that never move
        static void MakeStatic(GameObject go)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        }

        // Flat things on the ground do not need to cast shadows (saves work on slow laptops)
        static void NoShadows(GameObject go)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
                r.shadowCastingMode = ShadowCastingMode.Off;
        }

        static void RemoveColliders(GameObject go)
        {
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(c);
        }

        static float Range(float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }

        static string Pick(string[] options)
        {
            return options[random.Next(options.Length)];
        }

        static bool IsTooClose(Vector3 spot, List<Vector3> others, float distance)
        {
            foreach (Vector3 other in others)
            {
                Vector2 a = new Vector2(spot.x, spot.z);
                Vector2 b = new Vector2(other.x, other.z);
                if (Vector2.Distance(a, b) < distance)
                    return true;
            }
            return false;
        }
    }
}
