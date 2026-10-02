/*********************************************************************************************
 * COMPONENT OF: Editor tools (Tools > I Am A Ball > 2. Build City Details)
 * REQUIRED DEPENDENCIES: the 8 "Base cube" city blocks in the Game scene, Kenney models in
 *                        Assets/ThirdParty/Kenney, ModelAutoSetup, your "sewer enter" model
 * DESCRIPTION: Fills the empty city blocks with low poly buildings, shops, homes, jobs, a
 *              police station, a hospital, parked cars, trees, benches and traffic lights,
 *              and builds the hidden black market under the sewer entrance. Each block gets
 *              a role (Apartments, Food Court, Depot, Police, Downtown, Suburbs).
 *              Everything goes under "== GENERATED CITY DETAILS ==" and running the tool
 *              again replaces it, so it is safe to try. Your Bank block is left alone.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.1
 * VERSION 1.1: Trash, taxi, clerk and street performer jobs, hospital, police car parking,
 *              black market under the sewer, better benches and signs.
 *********************************************************************************************/
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IAmABall.EditorTools
{
    public static class CityBuilder
    {
        public const string RootName = "== GENERATED CITY DETAILS ==";
        public const string WalkPointsName = "Walk Points";
        public const string DeliveryPointsName = "Delivery Points";
        public const string AnchorsName = "Anchors";

        // Extra turn added to Kenney buildings so their front door faces the street
        public static float BuildingFrontYaw = 0f;
        // Gap between the edge of a block and the front of its buildings (room for trees)
        const float Setback = 2.6f;
        // How far in from the block edge trees and benches go
        const float PropInset = 1.1f;
        const float CarLength = GameplayBuilder.CarLength;
        // Where the hidden black market room is built (under the ground, east of the city)
        static readonly Vector3 BlackMarketCenter = new Vector3(60f, -30f, -62f);

        static readonly Vector3 North = Vector3.forward;
        static readonly Vector3 South = Vector3.back;
        static readonly Vector3 East = Vector3.right;
        static readonly Vector3 West = Vector3.left;

        static readonly Color ShopSign = new Color(0.85f, 0.35f, 0.15f);
        static readonly Color HomeSign = new Color(0.20f, 0.45f, 0.75f);
        static readonly Color JobSign = new Color(0.15f, 0.55f, 0.25f);
        static readonly Color PoliceSign = new Color(0.10f, 0.20f, 0.60f);
        static readonly Color SaleSign = new Color(0.55f, 0.20f, 0.60f);
        static readonly Color HospitalSign = new Color(0.80f, 0.12f, 0.15f);
        static readonly Color TaxiSign = new Color(0.95f, 0.70f, 0.10f);

        public class Block
        {
            public Bounds bounds;
            public int col, row;
            public string role;
            public float Top => bounds.max.y;
            public Vector3 Center => new Vector3(bounds.center.x, Top, bounds.center.z);
            public float HalfX => bounds.extents.x;
            public float HalfZ => bounds.extents.z;
        }

        static System.Random random;
        static Transform root, anchors, walkPoints, deliveryPoints, streetProps;
        static List<Vector3> keepClear;
        static List<Vector3> lampSpots;
        static List<Job> jobsNeedingDropOffs;
        static int groundLayer;
        // Trees, benches, bins and parked cars stop drawing when far away (see PerformanceManager)
        static int detailsLayer;

        [MenuItem(BuildTools.MenuRoot + "2. Build City Details", priority = 2)]
        static void BuildMenu()
        {
            Build();
            BuildTools.MarkSceneDirty();
        }

        public static void Build()
        {
            ModelAutoSetup.MakeAllPrefabs(false);
            BuildTools.DeleteRoot(RootName);

            random = new System.Random(2026);
            root = new GameObject(RootName).transform;
            anchors = BuildTools.Group(AnchorsName, root);
            walkPoints = BuildTools.Group(WalkPointsName, root);
            deliveryPoints = BuildTools.Group(DeliveryPointsName, root);
            streetProps = BuildTools.Group("Street Props", root);
            keepClear = new List<Vector3>();
            lampSpots = FindLampSpots();
            jobsNeedingDropOffs = new List<Job>();
            groundLayer = Mathf.Max(0, LayerMask.NameToLayer("Ground"));
            detailsLayer = BuildTools.EnsureLayer("Details");

            List<Block> blocks = FindBlocks();
            foreach (Block block in blocks)
                BuildBlock(block);

            foreach (Block block in blocks)
            {
                AddWalkPoints(block);
                AddTrafficLights(block);
                if (block.role != "Bank")
                    AddStreetProps(block);
            }

            BuildBlackMarket();
            FinishJobs();
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Build City Details");
            Debug.Log("[I Am A Ball] City built: " + blocks.Count + " blocks");
        }

        // ---------- Finding the blocks in your map ----------

        public static List<Block> FindBlocks()
        {
            List<Block> blocks = new List<Block>();
            foreach (Transform t in Object.FindObjectsByType<Transform>())
            {
                Renderer r = t.GetComponent<Renderer>();
                if (t.name.StartsWith("Base cube") && r != null)
                    blocks.Add(new Block { bounds = r.bounds });
            }

            if (blocks.Count == 0)
                return blocks;

            List<float> columns = Cluster(blocks.Select(b => b.bounds.center.x));
            List<float> rows = Cluster(blocks.Select(b => b.bounds.center.z));

            foreach (Block b in blocks)
            {
                b.col = NearestIndex(columns, b.bounds.center.x);
                b.row = NearestIndex(rows, b.bounds.center.z);
                b.role = RoleFor(b, columns.Count, rows.Count);
            }

            return blocks.OrderBy(b => b.row).ThenBy(b => b.col).ToList();
        }

        static List<float> Cluster(IEnumerable<float> values)
        {
            List<float> result = new List<float>();
            foreach (float v in values.OrderBy(v => v))
            {
                if (result.Count == 0 || v - result[result.Count - 1] > 3f)
                    result.Add(v);
            }
            return result;
        }

        static int NearestIndex(List<float> list, float value)
        {
            int best = 0;
            for (int i = 1; i < list.Count; i++)
            {
                if (Mathf.Abs(list[i] - value) < Mathf.Abs(list[best] - value))
                    best = i;
            }
            return best;
        }

        // The 2 x 4 grid in the Game scene, from south (row 0) to north (row 3)
        static string RoleFor(Block b, int columnCount, int rowCount)
        {
            if (columnCount == 2 && rowCount == 4)
            {
                string[,] layout =
                {
                    { "Bank", "Apartments" },
                    { "Depot", "FoodCourt" },
                    { "Police", "Downtown" },
                    { "SuburbWest", "SuburbEast" },
                };
                return layout[b.row, b.col];
            }

            string[] fallback = { "Apartments", "FoodCourt", "Depot", "Police", "Downtown", "SuburbWest", "SuburbEast" };
            return fallback[(b.row * columnCount + b.col) % fallback.Length];
        }

        static List<Vector3> FindLampSpots()
        {
            return Object.FindObjectsByType<Transform>()
                .Where(t => t.name.StartsWith("road light"))
                .Select(t => t.position)
                .ToList();
        }

        // ---------- Blocks ----------

        static void BuildBlock(Block block)
        {
            Transform group = BuildTools.Group("Block - " + block.role, root);

            switch (block.role)
            {
                case "Apartments": BuildApartments(block, group); break;
                case "FoodCourt": BuildFoodCourt(block, group); break;
                case "Depot": BuildDepot(block, group); break;
                case "Police": BuildPolice(block, group); break;
                case "Downtown": BuildDowntown(block, group); break;
                case "SuburbWest": BuildSuburb(block, group, "House on Oak Street", 300, false); break;
                case "SuburbEast": BuildSuburb(block, group, "Dream Villa", 800, true); break;
            }
        }

        // Where the player starts: their first home, a store (and the clerk job)
        static void BuildApartments(Block block, Transform group)
        {
            GameObject home = PlaceBuilding(block, group, "CityCommercial", "building-e", -1, 1, North, 15f, "Starter Apartment");
            Vector3 homeDoor = DoorSpot(home, North);
            HomeProperty apartment = AddInteractable<HomeProperty>("Door - Starter Apartment", homeDoor, North, group);
            apartment.propertyName = "Starter Apartment";
            apartment.ownedAtStart = true;
            apartment.buySound = BuildTools.Sound("Impacts/impactBell_heavy_001");
            Sign("HOME - APARTMENTS", home, North, HomeSign, group);
            MakeAnchor("Starter Apartment", homeDoor, North);

            GameObject store = PlaceBuilding(block, group, "CityCommercial", "building-b", 1, 1, North, 9f, "Corner Store");
            Vector3 storeDoor = DoorSpot(store, North);
            AddShopCounter(group, storeDoor + West * 0.9f, North, "Water", 3, 0f, 40f, "soda-bottle");
            AddShopCounter(group, storeDoor + East * 0.9f, North, "Sandwich", 5, 25f, 5f, "sandwich");
            Sign("CORNER STORE", store, North, ShopSign, group);
            MakeAnchor("Corner Store", storeDoor, North);
            AddClerkJob(group, storeDoor + East * 3.2f);

            GameObject flats = PlaceBuilding(block, group, "CityCommercial", "building-f", -1, -1, South, 15f, "Apartments");
            AddDeliveryPoint(DoorSpot(flats, South), "Apartments South");

            ParkingLot(block, group, 1, -1, South, "sedan", "hatchback-sports", "suv");
        }

        // Places to buy food and drinks, plus the street performer stage
        static void BuildFoodCourt(Block block, Transform group)
        {
            GameObject burger = PlaceBuilding(block, group, "CityCommercial", "building-c", -1, -1, South, 9f, "Burger Shop");
            Vector3 burgerDoor = DoorSpot(burger, South);
            AddShopCounter(group, burgerDoor + East * 0.9f, South, "Burger", 8, 40f, 0f, "burger");
            AddShopCounter(group, burgerDoor + West * 0.9f, South, "Fries", 5, 25f, 0f, "fries");
            Sign("BURGER SHOP", burger, South, ShopSign, group);
            MakeAnchor("Burger Shop", burgerDoor, South);

            GameObject drinks = PlaceBuilding(block, group, "CityCommercial", "building-d", 1, -1, South, 9f, "Drink Bar");
            Vector3 drinksDoor = DoorSpot(drinks, South);
            AddShopCounter(group, drinksDoor + East * 0.9f, South, "Soda", 3, 0f, 35f, "soda");
            AddShopCounter(group, drinksDoor + West * 0.9f, South, "Coffee", 4, 5f, 25f, "cup-coffee");
            Sign("DRINK BAR", drinks, South, ShopSign, group);
            MakeAnchor("Drink Bar", drinksDoor, South);

            GameObject northWest = PlaceBuilding(block, group, "CityCommercial", "building-g", -1, 1, North, 12f, "Offices");
            GameObject northEast = PlaceBuilding(block, group, "CityCommercial", "building-h", 1, 1, North, 12f, "Offices");
            AddDeliveryPoint(DoorSpot(northWest, North), "Food Court Offices West");
            AddDeliveryPoint(DoorSpot(northEast, North), "Food Court Offices East");

            Plaza(block, group, true);
        }

        // Jobs: delivery packages and trash pickup
        static void BuildDepot(Block block, Transform group)
        {
            GameObject depot = PlaceBuilding(block, group, "CityCommercial", "building-m", 1, -1, East, 10f, "Delivery Depot");
            Vector3 depotDoor = DoorSpot(depot, East);
            DeliveryJob delivery = AddInteractable<DeliveryJob>("Job Board - Delivery", depotDoor, East, group);
            delivery.jobName = "Delivery";
            delivery.jobDescription = "deliver packages";
            delivery.completeSound = BuildTools.Sound("Impacts/impactBell_heavy_002");
            delivery.marker = MakeMarker("Delivery Marker", new Color(0.2f, 1f, 0.35f));
            JobBoardModel(delivery.transform);
            Sign("DELIVERY DEPOT - JOBS", depot, East, JobSign, group);
            MakeAnchor("Delivery Depot", depotDoor, East);
            jobsNeedingDropOffs.Add(delivery);

            GameObject warehouse = PlaceBuilding(block, group, "CityCommercial", "building-k", 1, 1, East, 10f, "Warehouse Offices");
            AddDeliveryPoint(DoorSpot(warehouse, East), "Warehouse Offices");

            ParkingLot(block, group, -1, -1, West, "delivery", "delivery-flat", "van");
            ParkingLot(block, group, -1, 1, West, "garbage-truck", "truck");

            // Trash job board next to the garbage truck, facing the west street
            Vector3 trashSpot = new Vector3(block.bounds.min.x + 1.6f, block.Top, block.Center.z + block.HalfZ * 0.5f + 3.2f);
            TrashJob trash = AddInteractable<TrashJob>("Job Board - Trash Pickup", trashSpot, West, group);
            trash.jobName = "Trash Pickup";
            trash.jobDescription = "collect trash bags";
            trash.completeSound = BuildTools.Sound("Impacts/impactBell_heavy_002");
            trash.pickUpSound = BuildTools.Sound("Impacts/impactSoft_medium_000");
            JobBoardModel(trash.transform);
            BuildTools.MakeSign("TRASH PICKUP JOB", trashSpot + Vector3.up * 2.6f + West * 0.2f, West, 2.8f, JobSign, group);
            MakeAnchor("Trash Pickup", trashSpot, West);

            Vector3 back = block.Center + new Vector3(block.HalfX * 0.1f, 0f, -block.HalfZ * 0.15f);
            Prop(group, "CityRoads", "dumpster", back, 0f, 1.3f, true);
            Prop(group, "CityRoads", "dumpster", back + North * 2.2f, 0f, 1.3f, true);
            Prop(group, "CityRoads", "construction-cone", depotDoor + North * 2.5f, 0f, 0.7f, true);
            Prop(group, "CityRoads", "construction-cone", depotDoor + South * 2.5f, 0f, 0.7f, true);
        }

        // Police officers come from here when you are wanted; police cars wait in the lot
        static void BuildPolice(Block block, Transform group)
        {
            GameObject station = PlaceBuilding(block, group, "CityCommercial", "building-l", 1, -1, East, 11f, "Police Station");
            Vector3 stationDoor = DoorSpot(station, East);
            Sign("POLICE", station, East, PoliceSign, group);
            MakeAnchor("Police Station", stationDoor, East);

            GameObject northEast = PlaceBuilding(block, group, "CityCommercial", "building-j", 1, 1, East, 12f, "City Hall");
            Sign("CITY HALL", northEast, East, HomeSign, group);
            AddDeliveryPoint(DoorSpot(northEast, East), "City Hall");

            // One parked police car, plus two spots for the police cars that chase you
            List<Vector3> spots = ParkingSpots(block, group, -1, -1, West, 3);
            float yaw = Quaternion.LookRotation(West).eulerAngles.y + GameplayBuilder.CarFrontYaw;
            Prop(group, "Cars", "police", spots[0], yaw, CarLength, false);
            MakeAnchor("Police Car Spot 1", spots[1], West);
            MakeAnchor("Police Car Spot 2", spots[2], West);

            ParkingLot(block, group, -1, 1, West, "firetruck", "suv");
        }

        // Tall towers, the hospital, the taxi stand and a small park in the middle
        static void BuildDowntown(Block block, Transform group)
        {
            (int qx, int qz, Vector3 face, string model)[] towers =
            {
                (-1, 1, North, "building-skyscraper-a"),
                (-1, -1, South, "building-skyscraper-c"),
                (1, -1, South, "building-skyscraper-d"),
            };

            foreach (var tower in towers)
            {
                GameObject building = PlaceBuilding(block, group, "CityCommercial", tower.model, tower.qx, tower.qz, tower.face, 30f, "Tower");
                AddDeliveryPoint(DoorSpot(building, tower.face), "Downtown " + tower.model);
            }

            // Hospital in the north-east corner, with an ambulance outside
            GameObject hospital = PlaceBuilding(block, group, "CityCommercial", "building-g", 1, 1, North, 13f, "Hospital");
            Vector3 hospitalDoor = DoorSpot(hospital, North);
            Sign("HOSPITAL", hospital, North, HospitalSign, group);
            MakeAnchor("Hospital", hospitalDoor, North);
            Vector3 ambulanceSpot = hospitalDoor + East * 3.5f + South * 0.4f;
            Prop(group, "Cars", "ambulance", ambulanceSpot, Quaternion.LookRotation(North).eulerAngles.y + GameplayBuilder.CarFrontYaw, CarLength, false);

            // Taxi stand on the south side, between the towers
            Vector3 taxiSpot = new Vector3(block.Center.x, block.Top, block.bounds.min.z + 1.8f);
            RideJob ride = AddInteractable<RideJob>("Taxi Stand", taxiSpot, South, group);
            ride.jobName = "Taxi";
            ride.jobDescription = "lead passengers where they want to go";
            ride.completeSound = BuildTools.Sound("Impacts/impactBell_heavy_002");
            ride.marker = MakeMarker("Taxi Marker", new Color(1f, 0.8f, 0.1f));
            BuildTools.MakeSign("TAXI", taxiSpot + Vector3.up * 2.4f + North * 0.3f, South, 1.8f, TaxiSign, group);
            TaxiPole(group, taxiSpot + North * 0.3f);
            MakeAnchor("Taxi Stand", taxiSpot, South);
            jobsNeedingDropOffs.Add(ride);

            Plaza(block, group, false);
        }

        // Houses with yards; one of them is for sale
        static void BuildSuburb(Block block, Transform group, string houseForSale, int price, bool dreamHome)
        {
            string[] models = dreamHome
                ? new[] { "building-type-k", "building-type-m", "building-type-u", "building-type-p" }
                : new[] { "building-type-a", "building-type-c", "building-type-e", "building-type-g" };
            (int qx, int qz, Vector3 face)[] lots = { (-1, 1, North), (1, 1, North), (-1, -1, South), (1, -1, South) };

            for (int i = 0; i < lots.Length; i++)
            {
                bool forSale = i == 2;
                string label = forSale ? houseForSale : "House";
                GameObject house = PlaceBuilding(block, group, "CitySuburban", models[i], lots[i].qx, lots[i].qz, lots[i].face, 9f, label);
                Vector3 door = DoorSpot(house, lots[i].face);

                Vector3 yard = door + lots[i].face * 0.2f + (lots[i].qx > 0 ? East : West) * 3f;
                Prop(group, "CitySuburban", "tree-large", yard, (float)random.NextDouble() * 360f, 5f, true);

                if (!forSale)
                {
                    AddDeliveryPoint(door, "House " + (i + 1));
                    continue;
                }

                HomeProperty property = AddInteractable<HomeProperty>("Door - " + houseForSale, door, lots[i].face, group);
                property.propertyName = houseForSale;
                property.price = price;
                property.isDreamHome = dreamHome;
                property.buySound = BuildTools.Sound("Impacts/impactBell_heavy_001");
                property.forSaleSign = SignAtDoor("FOR SALE  $" + price, door, lots[i].face, SaleSign, group);
                property.ownedSign = SignAtDoor("YOUR HOME", door, lots[i].face, HomeSign, group);
                property.ownedSign.SetActive(false);
                MakeAnchor(houseForSale, door, lots[i].face);
            }
        }

        // ---------- Buildings ----------

        // Places a building in one quarter of the block (qx/qz = -1 or 1), front facing the street
        static GameObject PlaceBuilding(Block block, Transform group, string pack, string model, int qx, int qz, Vector3 facing, float maxHeight, string label)
        {
            GameObject prefab = BuildTools.Prefab(pack, model);
            if (prefab == null)
            {
                GameObject missing = new GameObject(label + " (missing model)");
                missing.transform.SetParent(group, false);
                return missing;
            }

            GameObject go = BuildTools.Spawn(prefab, group);
            go.name = label;

            bool facesX = Mathf.Abs(facing.x) > 0.5f;
            float quarterX = block.HalfX - 0.5f;
            float quarterZ = block.HalfZ - 0.5f;
            float alongStreet = (facesX ? quarterZ : quarterX) - 1.0f;
            float depth = (facesX ? quarterX : quarterZ) - Setback - 0.5f;

            float yaw = Quaternion.LookRotation(facing).eulerAngles.y + BuildingFrontYaw;
            Vector3 quarterCenter = block.Center + new Vector3(qx * block.HalfX * 0.5f, 0f, qz * block.HalfZ * 0.5f);
            BuildTools.FitInside(go, quarterCenter, yaw, facesX ? depth : alongStreet, facesX ? alongStreet : depth, maxHeight);

            // Slide it toward the street so its front is 'Setback' meters from the block edge
            Bounds b = BuildTools.WorldBounds(go);
            float direction = facesX ? Mathf.Sign(facing.x) : Mathf.Sign(facing.z);
            float edge = facesX ? (direction > 0 ? block.bounds.max.x : block.bounds.min.x) : (direction > 0 ? block.bounds.max.z : block.bounds.min.z);
            float front = facesX ? (direction > 0 ? b.max.x : b.min.x) : (direction > 0 ? b.max.z : b.min.z);
            float shift = edge - Setback * direction - front;
            go.transform.position += (facesX ? Vector3.right : Vector3.forward) * shift;

            return go;
        }

        // A spot on the ground just in front of the middle of a building's front wall
        static Vector3 DoorSpot(GameObject building, Vector3 facing)
        {
            Bounds b = BuildTools.WorldBounds(building);
            Vector3 frontMiddle = b.center + Vector3.Scale(b.extents, facing);
            Vector3 spot = frontMiddle + facing * 1.0f;
            spot.y = b.min.y;
            return spot;
        }

        // Parking spaces in a row (with painted lines) in one quarter of a block
        static List<Vector3> ParkingSpots(Block block, Transform group, int qx, int qz, Vector3 noseFacing, int count)
        {
            bool facesX = Mathf.Abs(noseFacing.x) > 0.5f;
            Vector3 along = facesX ? Vector3.forward : Vector3.right;
            Vector3 quarterCenter = block.Center + new Vector3(qx * block.HalfX * 0.5f, 0f, qz * block.HalfZ * 0.5f);
            const float spacing = 2.8f;
            List<Vector3> spots = new List<Vector3>();

            for (int i = 0; i < count; i++)
                spots.Add(quarterCenter + along * ((i - (count - 1) * 0.5f) * spacing) + noseFacing * 0.5f);

            Material paint = BuildTools.GetMaterial("Parking Paint", new Color(0.95f, 0.95f, 0.95f), false);
            for (int i = 0; i <= count; i++)
            {
                GameObject line = BuildTools.Primitive(PrimitiveType.Cube, "Parking Line", group, paint, false);
                line.transform.position = quarterCenter + along * ((i - count * 0.5f) * spacing) + noseFacing * 0.5f + Vector3.up * 0.01f;
                line.transform.rotation = Quaternion.LookRotation(noseFacing);
                line.transform.localScale = new Vector3(0.12f, 0.02f, 3.6f);
            }

            return spots;
        }

        static void ParkingLot(Block block, Transform group, int qx, int qz, Vector3 noseFacing, params string[] cars)
        {
            List<Vector3> spots = ParkingSpots(block, group, qx, qz, noseFacing, cars.Length);
            float yaw = Quaternion.LookRotation(noseFacing).eulerAngles.y + GameplayBuilder.CarFrontYaw;
            for (int i = 0; i < cars.Length; i++)
                Prop(group, "Cars", cars[i], spots[i], yaw, CarLength, false);
        }

        // Benches, umbrellas and trees in the middle of a block; the food court gets a stage
        static void Plaza(Block block, Transform group, bool withStage)
        {
            Vector3 c = block.Center;
            if (withStage)
                Stage(group, c);
            else
                Prop(group, "Nature", "tree_default", c, 0f, 5f, true);

            Prop(group, "CityCommercial", "detail-parasol-a", c + new Vector3(-3.6f, 0f, 3.6f), 0f, 2.6f, true);
            Prop(group, "CityCommercial", "detail-parasol-b", c + new Vector3(3.6f, 0f, -3.6f), 0f, 2.6f, true);
            Bench(group, c + new Vector3(3.6f, 0f, 3.6f), 225f);
            Bench(group, c + new Vector3(-3.6f, 0f, -3.6f), 45f);
            Prop(group, "Nature", "plant_bush", c + new Vector3(0f, 0f, 4.3f), 0f, 1f, true);
            Prop(group, "Nature", "plant_bush", c + new Vector3(0f, 0f, -4.3f), 0f, 1f, true);
        }

        // The street performer stage (on the Ground layer so jumps count)
        static void Stage(Transform group, Vector3 center)
        {
            Material wood = BuildTools.GetMaterial("Stage Wood", new Color(0.55f, 0.36f, 0.2f), false);
            GameObject stage = BuildTools.Primitive(PrimitiveType.Cylinder, "Street Stage", group, wood, true);
            stage.transform.position = center + Vector3.up * 0.08f;
            stage.transform.localScale = new Vector3(5f, 0.08f, 5f);
            stage.layer = groundLayer;

            PerformerJob show = AddInteractable<PerformerJob>("Street Show", center + Vector3.up * 0.16f, South, group);
            show.jobName = "Street Performer";
            show.jobDescription = "jump on the stage for tips";
            show.interactRange = 2.6f;
            show.completeSound = BuildTools.Sound("Impacts/impactBell_heavy_002");
            show.trickSound = BuildTools.Sound("UI/switch3");
            BuildTools.MakeSign("STREET SHOW", center + new Vector3(0f, 2.6f, 2.9f), South, 2.6f, ShopSign, group);
            MakeAnchor("Street Show", center, South);
        }

        // Places a prop; 'size' is its height (useHeight) or its longest side
        static GameObject Prop(Transform group, string pack, string model, Vector3 position, float yaw, float size, bool useHeight)
        {
            GameObject prefab = BuildTools.Prefab(pack, model);
            if (prefab == null)
                return null;

            GameObject go = BuildTools.Spawn(prefab, group);
            float ground = BuildTools.GroundHeight(position, position.y);
            BuildTools.FitSize(go, new Vector3(position.x, ground, position.z), yaw, size, useHeight);
            BuildTools.SetLayer(go, detailsLayer);
            return go;
        }

        // A park bench about 1.6 m wide (sized by width, not height)
        static void Bench(Transform group, Vector3 position, float yaw)
        {
            GameObject prefab = BuildTools.Prefab("Furniture", "bench");
            if (prefab == null)
                return;

            GameObject bench = BuildTools.Spawn(prefab, group);
            float ground = BuildTools.GroundHeight(position, position.y);
            Vector3 size = Quaternion.Euler(0f, yaw, 0f) * new Vector3(1.6f, 0f, 0.7f);
            BuildTools.FitInside(bench, new Vector3(position.x, ground, position.z), yaw, Mathf.Max(0.7f, Mathf.Abs(size.x)), Mathf.Max(0.7f, Mathf.Abs(size.z)), 1.0f);
            BuildTools.SetLayer(bench, detailsLayer);
        }

        // ---------- Interactables, signs and markers ----------

        static T AddInteractable<T>(string name, Vector3 position, Vector3 facing, Transform parent) where T : Interactable
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.LookRotation(facing);
            keepClear.Add(position);
            return go.AddComponent<T>();
        }

        // A small counter with the food floating above it
        static void AddShopCounter(Transform parent, Vector3 position, Vector3 facing, string item, int price, float hunger, float thirst, string foodModel)
        {
            Shop shop = AddInteractable<Shop>("Counter - " + item, position, facing, parent);
            shop.itemName = item;
            shop.price = price;
            shop.hungerRestore = hunger;
            shop.thirstRestore = thirst;
            shop.interactRange = 1.4f;
            shop.buySound = BuildTools.Sound("Impacts/impactBell_heavy_000");
            CounterStand(shop.transform);
            FloatingItem(shop.transform, "Food", foodModel, position + Vector3.up * 1.15f);
        }

        // The store clerk job: a staff counter next to the store, customers come up to it
        static void AddClerkJob(Transform group, Vector3 position)
        {
            ClerkJob clerk = AddInteractable<ClerkJob>("Staff Counter - Clerk Job", position, North, group);
            clerk.jobName = "Store Clerk";
            clerk.jobDescription = "serve customers";
            clerk.interactRange = 1.6f;
            clerk.completeSound = BuildTools.Sound("Impacts/impactBell_heavy_002");
            clerk.serveSound = BuildTools.Sound("UI/switch7");
            CounterStand(clerk.transform);
            BuildTools.MakeSign("STAFF - CLERK JOB", position + Vector3.up * 2.2f + South * 0.4f, North, 2.4f, JobSign, group);

            Transform spot = new GameObject("Clerk Customer Spot").transform;
            spot.SetParent(clerk.transform, false);
            spot.position = position + North * 1.3f;
            clerk.customerSpot = spot;

            Transform spawn = new GameObject("Clerk Customer Spawn").transform;
            spawn.SetParent(clerk.transform, false);
            spawn.position = position + North * 1.2f + East * 5f;
            clerk.customerSpawn = spawn;

            MakeAnchor("Clerk Job", position, North);
        }

        static void CounterStand(Transform counter)
        {
            Material counterColor = BuildTools.GetMaterial("Counter", new Color(0.55f, 0.35f, 0.2f), false);
            GameObject stand = BuildTools.Primitive(PrimitiveType.Cube, "Stand", counter, counterColor, true);
            stand.transform.localPosition = new Vector3(0f, 0.45f, -0.3f);
            stand.transform.localScale = new Vector3(1.2f, 0.9f, 0.5f);
        }

        // A spinning model above a counter so players can see what is sold there
        static void FloatingItem(Transform parent, string pack, string model, Vector3 position)
        {
            GameObject prefab = BuildTools.Prefab(pack, model);
            if (prefab == null)
                return;

            GameObject item = BuildTools.Spawn(prefab, parent);
            BuildTools.FitSize(item, position, 0f, 0.45f, false);
            GameObjectUtility.SetStaticEditorFlags(item, (StaticEditorFlags)0);
            foreach (Collider c in item.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(c);
            item.AddComponent<FloatAndSpin>().floatHeight = 0.08f;
            BuildTools.SetLayer(item, detailsLayer);
        }

        // A board on a post (job boards)
        static void JobBoardModel(Transform job)
        {
            Material wood = BuildTools.GetMaterial("Job Board", new Color(0.35f, 0.25f, 0.15f), false);
            Material paper = BuildTools.GetMaterial("Job Paper", new Color(0.95f, 0.92f, 0.8f), false);

            GameObject post = BuildTools.Primitive(PrimitiveType.Cube, "Post", job, wood, true);
            post.transform.localPosition = new Vector3(0f, 0.8f, -0.4f);
            post.transform.localScale = new Vector3(0.15f, 1.6f, 0.15f);

            GameObject board = BuildTools.Primitive(PrimitiveType.Cube, "Board", job, wood, false);
            board.transform.localPosition = new Vector3(0f, 1.5f, -0.3f);
            board.transform.localScale = new Vector3(1.4f, 0.9f, 0.08f);

            GameObject note = BuildTools.Primitive(PrimitiveType.Cube, "Job Notes", job, paper, false);
            note.transform.localPosition = new Vector3(0f, 1.5f, -0.25f);
            note.transform.localScale = new Vector3(1.1f, 0.65f, 0.02f);
        }

        static void TaxiPole(Transform group, Vector3 position)
        {
            Material yellow = BuildTools.GetMaterial("Taxi Yellow", new Color(0.95f, 0.75f, 0.1f), false);
            GameObject pole = BuildTools.Primitive(PrimitiveType.Cylinder, "Taxi Pole", group, yellow, true);
            pole.transform.position = position + Vector3.up * 1f;
            pole.transform.localScale = new Vector3(0.12f, 1f, 0.12f);
        }

        // A glowing beam that shows where to go during a job (hidden until the job starts)
        static GameObject MakeMarker(string name, Color color)
        {
            GameObject marker = new GameObject(name);
            marker.transform.SetParent(root, false);
            Material glow = BuildTools.GetMaterial(name, color, true);

            GameObject beam = BuildTools.Primitive(PrimitiveType.Cylinder, "Beam", marker.transform, glow, false);
            // 80 m tall so it can be seen from anywhere in the city
            beam.transform.localPosition = new Vector3(0f, 40f, 0f);
            beam.transform.localScale = new Vector3(0.6f, 40f, 0.6f);

            GameObject ring = BuildTools.Primitive(PrimitiveType.Cylinder, "Ring", marker.transform, glow, false);
            ring.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            ring.transform.localScale = new Vector3(2.4f, 0.02f, 2.4f);

            GameObject box = BuildTools.Primitive(PrimitiveType.Cube, "Floating Box", marker.transform, glow, false);
            box.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            box.transform.localScale = Vector3.one * 0.45f;
            box.AddComponent<FloatAndSpin>();

            marker.SetActive(false);
            return marker;
        }

        // A sign above a building's door
        static void Sign(string text, GameObject building, Vector3 facing, Color color, Transform group)
        {
            Bounds b = BuildTools.WorldBounds(building);
            Vector3 frontMiddle = b.center + Vector3.Scale(b.extents, facing);
            Vector3 position = new Vector3(frontMiddle.x, b.min.y + 3.4f, frontMiddle.z) + facing * 0.35f;
            BuildTools.MakeSign(text, position, facing, 4.5f, color, group);
        }

        // A small standing sign next to a door (used for FOR SALE)
        static GameObject SignAtDoor(string text, Vector3 door, Vector3 facing, Color color, Transform group)
        {
            Vector3 side = Vector3.Cross(Vector3.up, facing).normalized;
            GameObject sign = BuildTools.MakeSign(text, door + side * 1.6f + Vector3.up * 1.3f, facing, 2.4f, color, group);

            Material wood = BuildTools.GetMaterial("Job Board", new Color(0.35f, 0.25f, 0.15f), false);
            GameObject post = BuildTools.Primitive(PrimitiveType.Cube, "Post", sign.transform, wood, false);
            post.transform.localPosition = new Vector3(0f, -0.75f, 0.1f);
            post.transform.localScale = new Vector3(0.1f, 1.2f, 0.1f);
            return sign;
        }

        static void MakeAnchor(string name, Vector3 position, Vector3 facing)
        {
            Transform t = new GameObject("Anchor - " + name).transform;
            t.SetParent(anchors, false);
            t.position = position;
            t.rotation = Quaternion.LookRotation(facing);
            keepClear.Add(position);
        }

        static void AddDeliveryPoint(Vector3 position, string name)
        {
            Transform t = new GameObject("Drop Off - " + name).transform;
            t.SetParent(deliveryPoints, false);
            t.position = position;
            keepClear.Add(position);
        }

        // Gives the delivery and taxi jobs the list of places around the city
        static void FinishJobs()
        {
            Transform[] dropOffs = deliveryPoints.Cast<Transform>().ToArray();
            foreach (Job job in jobsNeedingDropOffs)
            {
                if (job is DeliveryJob delivery)
                    delivery.dropOffPoints = dropOffs;
                else if (job is RideJob ride)
                    ride.destinations = dropOffs;
            }
        }

        // ---------- The hidden black market under the sewer ----------

        static void BuildBlackMarket()
        {
            Transform sewer = Object.FindObjectsByType<Transform>().FirstOrDefault(t => t.name.StartsWith("sewer enter") && t.parent == null);
            if (sewer == null)
                return;

            Transform group = BuildTools.Group("Black Market (underground)", root);
            Vector3 c = BlackMarketCenter;
            const float width = 14f, length = 10f, height = 4f;

            Material concrete = BuildTools.GetMaterial("Sewer Concrete", new Color(0.22f, 0.22f, 0.24f), false);
            Material brick = BuildTools.GetMaterial("Sewer Brick", new Color(0.35f, 0.16f, 0.13f), false);
            Wall(group, "Floor", c + Vector3.down * 0.25f, new Vector3(width, 0.5f, length), concrete).layer = groundLayer;
            Wall(group, "Ceiling", c + Vector3.up * (height + 0.25f), new Vector3(width, 0.5f, length), concrete);
            Wall(group, "Wall North", c + new Vector3(0f, height * 0.5f, length * 0.5f), new Vector3(width, height, 0.5f), brick);
            Wall(group, "Wall South", c + new Vector3(0f, height * 0.5f, -length * 0.5f), new Vector3(width, height, 0.5f), brick);
            Wall(group, "Wall East", c + new Vector3(width * 0.5f, height * 0.5f, 0f), new Vector3(0.5f, height, length), brick);
            Wall(group, "Wall West", c + new Vector3(-width * 0.5f, height * 0.5f, 0f), new Vector3(0.5f, height, length), brick);

            RoomLight(group, c + new Vector3(-4f, 3.3f, 0f), new Color(1f, 0.35f, 0.25f));
            RoomLight(group, c + new Vector3(4f, 3.3f, 0f), new Color(1f, 0.55f, 0.3f));
            RoomLight(group, c + new Vector3(0f, 3.3f, 2.5f), new Color(0.6f, 0.3f, 1f));

            // Ladder up to the street (west wall) and the way down from the sewer entrance
            Vector3 ladderSpot = c + new Vector3(-width * 0.5f + 1.2f, 0f, 0f);
            Ladder(group, ladderSpot + West * 0.6f);
            Transform arrive = new GameObject("Black Market Arrival").transform;
            arrive.SetParent(group, false);
            arrive.position = ladderSpot + East * 1f + Vector3.up * 0.6f;

            Transform surface = new GameObject("Sewer Exit Point").transform;
            surface.SetParent(group, false);
            Vector3 sewerTop = BuildTools.WorldBounds(sewer.gameObject).center;
            surface.position = new Vector3(sewerTop.x - 1.8f, BuildTools.GroundHeight(sewerTop + West * 1.8f, 0f) + 0.6f, sewerTop.z);

            Teleporter down = AddInteractable<Teleporter>("Sewer Entrance", new Vector3(sewerTop.x, surface.position.y - 0.6f, sewerTop.z), North, group);
            down.prompt = "Press E to climb down into the sewer";
            down.destination = arrive;
            down.arriveMessage = "You found the hidden BLACK MARKET...";
            down.sound = BuildTools.Sound("Impacts/impactMetal_light_000");
            down.interactRange = 2.2f;

            Teleporter up = AddInteractable<Teleporter>("Ladder Up", ladderSpot, East, group);
            up.prompt = "Press E to climb back up to the street";
            up.destination = surface;
            up.sound = BuildTools.Sound("Impacts/impactMetal_light_001");
            up.interactRange = 1.8f;

            // The dealer's table and the two things for sale
            Vector3 table = c + new Vector3(3.5f, 0f, 2.8f);
            Prop(group, "Furniture", "desk", table, 0f, 2.2f, false);
            BlackMarketCounter pistol = AddInteractable<BlackMarketCounter>("Counter - Pistol", table + South * 1.2f + West * 0.6f, South, group);
            pistol.item = BlackMarketCounter.Item.Pistol;
            pistol.price = 150;
            pistol.interactRange = 1.4f;
            pistol.buySound = BuildTools.Sound("Impacts/impactMetal_medium_000");
            PistolModel(pistol.transform, pistol.transform.position + Vector3.up * 1.2f, false);

            BlackMarketCounter disguise = AddInteractable<BlackMarketCounter>("Counter - Disguise", table + South * 1.2f + East * 0.9f, South, group);
            disguise.item = BlackMarketCounter.Item.Disguise;
            disguise.price = 60;
            disguise.interactRange = 1.4f;
            disguise.buySound = BuildTools.Sound("Impacts/impactSoft_medium_001");
            Material hatColor = BuildTools.GetMaterial("Disguise Hat", new Color(0.1f, 0.1f, 0.12f), false);
            GameObject hat = BuildTools.Primitive(PrimitiveType.Cylinder, "Disguise Hat", disguise.transform, hatColor, false);
            hat.transform.position = disguise.transform.position + Vector3.up * 1.2f;
            hat.transform.localScale = new Vector3(0.35f, 0.12f, 0.35f);
            hat.AddComponent<FloatAndSpin>().floatHeight = 0.08f;

            MakeAnchor("Black Market Dealer", table + North * 1.0f, South);
            MakeAnchor("Black Market", arrive.position, East);
            BuildTools.MakeSign("BLACK MARKET", c + new Vector3(0f, 3.0f, length * 0.5f - 0.35f), South, 4f, new Color(0.45f, 0.05f, 0.08f), group);

            // Junk to make it feel hidden and messy
            Prop(group, "CityRoads", "dumpster", c + new Vector3(-3f, 0f, 3.6f), 10f, 1.3f, true);
            Prop(group, "Furniture", "loungeSofa", c + new Vector3(-2f, 0f, -3.6f), 0f, 2.2f, false);
            Prop(group, "Furniture", "televisionModern", c + new Vector3(-2f, 0f, -1.4f), 180f, 1.2f, false);
            Prop(group, "Furniture", "lampRoundFloor", c + new Vector3(5.8f, 0f, -3.8f), 0f, 1.7f, true);
            Prop(group, "CityRoads", "construction-barrier", c + new Vector3(4f, 0f, -3.5f), 30f, 1.2f, false);
        }

        static GameObject Wall(Transform group, string name, Vector3 center, Vector3 size, Material material)
        {
            GameObject wall = BuildTools.Primitive(PrimitiveType.Cube, name, group, material, true);
            wall.transform.position = center;
            wall.transform.localScale = size;
            return wall;
        }

        static void RoomLight(Transform group, Vector3 position, Color color)
        {
            Light light = new GameObject("Room Light").AddComponent<Light>();
            light.transform.SetParent(group, false);
            light.transform.position = position;
            light.type = LightType.Point;
            light.color = color;
            light.range = 9f;
            light.intensity = 6f;
        }

        static void Ladder(Transform group, Vector3 bottom)
        {
            Material metal = BuildTools.GetMaterial("Ladder Metal", new Color(0.45f, 0.45f, 0.5f), false);
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject rail = BuildTools.Primitive(PrimitiveType.Cube, "Ladder Rail", group, metal, false);
                rail.transform.position = bottom + new Vector3(0f, 2f, side * 0.3f);
                rail.transform.localScale = new Vector3(0.06f, 4f, 0.06f);
            }
            for (int step = 0; step < 8; step++)
            {
                GameObject rung = BuildTools.Primitive(PrimitiveType.Cube, "Ladder Rung", group, metal, false);
                rung.transform.position = bottom + new Vector3(0f, 0.3f + step * 0.5f, 0f);
                rung.transform.localScale = new Vector3(0.05f, 0.05f, 0.6f);
            }
        }

        // A simple low poly pistol made of boxes (also used by the player when they buy one)
        public static GameObject PistolModel(Transform parent, Vector3 position, bool forPlayer)
        {
            Material metal = BuildTools.GetMaterial("Pistol Metal", new Color(0.12f, 0.12f, 0.14f), false);
            Material grip = BuildTools.GetMaterial("Pistol Grip", new Color(0.3f, 0.2f, 0.12f), false);

            GameObject gun = new GameObject("Pistol");
            gun.transform.SetParent(parent, false);
            gun.transform.position = position;

            GameObject slide = BuildTools.Primitive(PrimitiveType.Cube, "Slide", gun.transform, metal, false);
            slide.transform.localPosition = new Vector3(0f, 0.05f, 0.06f);
            slide.transform.localScale = new Vector3(0.07f, 0.08f, 0.32f);

            GameObject handle = BuildTools.Primitive(PrimitiveType.Cube, "Grip", gun.transform, grip, false);
            handle.transform.localPosition = new Vector3(0f, -0.06f, -0.06f);
            handle.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
            handle.transform.localScale = new Vector3(0.065f, 0.18f, 0.08f);

            if (!forPlayer)
            {
                gun.transform.localScale = Vector3.one * 1.6f;
                gun.AddComponent<FloatAndSpin>().floatHeight = 0.06f;
            }

            return gun;
        }

        // ---------- Things along every block edge ----------

        // Corners and edge middles of each block; NPCs walk between these
        static void AddWalkPoints(Block block)
        {
            float x = block.HalfX - PropInset;
            float z = block.HalfZ - PropInset;
            Vector2[] spots = { new Vector2(-x, -z), new Vector2(x, -z), new Vector2(-x, z), new Vector2(x, z), new Vector2(0, -z), new Vector2(0, z), new Vector2(-x, 0), new Vector2(x, 0) };

            foreach (Vector2 s in spots)
            {
                Transform t = new GameObject("Walk Point").transform;
                t.SetParent(walkPoints, false);
                t.position = block.Center + new Vector3(s.x, 0f, s.y);
            }
        }

        // Trees, benches and bins along the edges of a block, skipping doors and lamps
        static void AddStreetProps(Block block)
        {
            Vector3 c = block.Center;
            float x = block.HalfX - PropInset;
            float z = block.HalfZ - PropInset;
            (Vector3 start, Vector3 end, Vector3 facing)[] edges =
            {
                (c + new Vector3(-x, 0, z), c + new Vector3(x, 0, z), North),
                (c + new Vector3(-x, 0, -z), c + new Vector3(x, 0, -z), South),
                (c + new Vector3(x, 0, -z), c + new Vector3(x, 0, z), East),
                (c + new Vector3(-x, 0, -z), c + new Vector3(-x, 0, z), West),
            };

            foreach (var edge in edges)
            {
                float length = Vector3.Distance(edge.start, edge.end);
                int count = Mathf.FloorToInt((length - 4f) / 5.5f);
                for (int i = 0; i <= count; i++)
                {
                    Vector3 spot = Vector3.Lerp(edge.start, edge.end, (2f + i * 5.5f) / length);
                    if (IsTooClose(spot, keepClear, 2.8f) || IsTooClose(spot, lampSpots, 1.6f))
                        continue;

                    PlaceStreetProp(spot, edge.facing, i);
                }
            }
        }

        static void PlaceStreetProp(Vector3 spot, Vector3 facing, int index)
        {
            float yaw = (float)random.NextDouble() * 360f;
            int kind = (index + random.Next(3)) % 5;

            if (kind == 3)
            {
                // Bench facing the street with a bin next to it
                Bench(streetProps, spot, Quaternion.LookRotation(facing).eulerAngles.y);
                Vector3 side = Vector3.Cross(Vector3.up, facing).normalized;
                Prop(streetProps, "Furniture", "trashcan", spot + side * 1.4f, yaw, 0.9f, true);
            }
            else if (kind == 4)
            {
                Prop(streetProps, "Nature", "plant_bushLarge", spot, yaw, 1.1f, true);
            }
            else
            {
                string[] trees = { "tree_default", "tree_oak", "tree_detailed", "tree_fat" };
                float height = 4.2f + (float)random.NextDouble() * 1.4f;
                Prop(streetProps, "Nature", trees[random.Next(trees.Length)], spot, yaw, height, true);
            }
        }

        // Traffic lights on the corners of each block, facing the crossing
        static void AddTrafficLights(Block block)
        {
            float x = block.HalfX - 0.5f;
            float z = block.HalfZ - 0.5f;
            Vector3[] corners = { new Vector3(-x, 0, -z), new Vector3(x, 0, -z), new Vector3(-x, 0, z), new Vector3(x, 0, z) };

            foreach (Vector3 corner in corners)
            {
                Vector3 spot = block.Center + corner;
                if (IsTooClose(spot, lampSpots, 1.2f))
                    continue;

                float yaw = Quaternion.LookRotation(new Vector3(corner.x, 0f, corner.z)).eulerAngles.y;
                Prop(streetProps, "CityRoads", "traffic-light", spot, yaw, 3.4f, true);
            }
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
