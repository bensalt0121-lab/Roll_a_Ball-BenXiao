/*********************************************************************************************
 * COMPONENT OF: Editor tools (Tools > I Am A Ball > 3. Set Up Gameplay)
 * REQUIRED DEPENDENCIES: Player (tagged "Player"), MoneyManager, the city made by
 *                        CityBuilder, Kenney characters and cars, AI Navigation package
 * DESCRIPTION: Adds the game systems from the GDD to the scene: hunger/thirst, the E key
 *              and the pistol on the Player, the HUD and minimap, music, the NavMesh, people
 *              walking around, police officers and police cars, the hospital, traffic, job
 *              helpers (taxi passenger, store customer, trash bags) and money pickups.
 *              Everything goes under "== GENERATED GAMEPLAY ==" and running it again
 *              replaces it.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.1
 * VERSION 1.1: Medium-size player, pistol, music, police cars, hospital, job helpers,
 *              new cash pickups.
 *********************************************************************************************/
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace IAmABall.EditorTools
{
    public static class GameplayBuilder
    {
        public const string RootName = "== GENERATED GAMEPLAY ==";
        const string CharacterFolder = BuildTools.KenneyRoot + "/Characters/";
        const string ControllerPath = BuildTools.GeneratedRoot + "/Animation/CityPerson.controller";
        const string PrefabFolder = BuildTools.GeneratedRoot + "/Prefabs";
        const string MoneyParticlesPath = "Assets/Particles/money.prefab";
        const string MoneySoundPath = "Assets/sounds/freesound_community-money-pickup-2-89563.mp3";

        // Extra turn so Kenney cars and characters face forward
        public static float CarFrontYaw = 0f;
        public static float CharacterFrontYaw = 0f;
        // How tall people are in this city (road lights are about 2.5 m)
        public const float PersonHeight = 1.6f;
        // Length of cars (lanes are about 2 m wide, so cars stay narrow)
        public const float CarLength = 2.9f;

        // Medium-size player: about waist height of a person
        const float PlayerScale = 0.5f;
        const float PlayerSpeed = 3.2f;

        static readonly string[] CivilianModels =
        {
            "character-female-a", "character-female-b", "character-female-c", "character-female-d", "character-female-e", "character-female-f",
            "character-male-a", "character-male-b", "character-male-d", "character-male-e", "character-male-f",
        };
        const string PoliceModel = "character-male-c";
        static readonly string[] TrafficModels = { "sedan", "taxi", "suv", "hatchback-sports", "van", "sedan-sports", "suv-luxury", "truck" };

        static System.Random random;
        static int minimapLayer;

        [MenuItem(BuildTools.MenuRoot + "3. Set Up Gameplay", priority = 3)]
        static void BuildMenu()
        {
            Build();
            BuildTools.MarkSceneDirty();
        }

        public static void Build()
        {
            BuildTools.DeleteRoot(RootName);
            random = new System.Random(7);
            Transform root = new GameObject(RootName).transform;
            minimapLayer = BuildTools.EnsureLayer("Minimap");

            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
                SetUpPlayer(player, root);

            BuildSystems(root);
            BuildNavMesh(root);
            BuildHud(root, out RawImage minimapImage);
            BuildMinimap(root, player, minimapImage);

            RuntimeAnimatorController controller = BuildAnimatorController();
            Transform[] walk = Children(BuildTools.FindChildGroup(CityBuilder.RootName, CityBuilder.WalkPointsName));
            BuildPeople(root, controller, walk);
            BuildPolice(root, controller);
            BuildPoliceCars(root);
            BuildHospital(root);
            BuildJobHelpers(root, controller, walk);
            BuildDealer(root, controller);
            BuildTraffic(root);
            PlaceMoney(root, walk);
            AddMapIcons(root);

            Undo.RegisterCreatedObjectUndo(root.gameObject, "Set Up Gameplay");
            Debug.Log("[I Am A Ball] Gameplay set up");
        }

        static Transform[] Children(Transform group)
        {
            return group == null ? new Transform[0] : group.Cast<Transform>().ToArray();
        }

        static Transform FindAnchor(string name)
        {
            Transform anchors = BuildTools.FindChildGroup(CityBuilder.RootName, CityBuilder.AnchorsName);
            return anchors != null ? anchors.Find("Anchor - " + name) : null;
        }

        static void IgnoreForNavMesh(GameObject go)
        {
            NavMeshModifier modifier = go.GetComponent<NavMeshModifier>();
            if (modifier == null)
                modifier = go.AddComponent<NavMeshModifier>();

            modifier.ignoreFromBuild = true;
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        // ---------- Player and systems ----------

        static void SetUpPlayer(GameObject player, Transform root)
        {
            Undo.RecordObject(player.transform, "Player size");
            player.transform.localScale = Vector3.one * PlayerScale;

            // Bigger ball: move the ground check down so jumping still works, and roll a bit faster
            PlayerMovement movement = player.GetComponent<PlayerMovement>();
            if (movement != null)
            {
                Undo.RecordObject(movement, "Player movement");
                movement.moveSpeed = PlayerSpeed;
                movement.groundCheckOffset = new Vector3(0f, -0.38f, 0f);
                movement.groundCheckRadius = 0.14f;
                EditorUtility.SetDirty(movement);
            }

            // Pull the camera back to fit the bigger player
            ThirdPersonCamera follow = Object.FindAnyObjectByType<ThirdPersonCamera>();
            if (follow != null)
            {
                SerializedObject cameraSettings = new SerializedObject(follow);
                cameraSettings.FindProperty("distance").floatValue = 2.6f;
                cameraSettings.FindProperty("height").floatValue = 0.6f;
                cameraSettings.ApplyModifiedProperties();
            }

            GetOrAdd<PlayerNeeds>(player);
            PlayerInteractor interactor = GetOrAdd<PlayerInteractor>(player);
            interactor.interactSound = BuildTools.Sound("UI/click3");

            PlayerWeapon weapon = GetOrAdd<PlayerWeapon>(player);
            weapon.hasPistol = false;
            BuildPlayerPistol(root, weapon);

            IgnoreForNavMesh(player);
        }

        // The pistol the player holds after buying it (hidden until then)
        static void BuildPlayerPistol(Transform root, PlayerWeapon weapon)
        {
            GameObject gun = CityBuilder.PistolModel(root, Vector3.zero, true);
            gun.name = "Player Pistol";

            Light flash = new GameObject("Muzzle Flash").AddComponent<Light>();
            flash.transform.SetParent(gun.transform, false);
            flash.transform.localPosition = new Vector3(0f, 0.05f, 0.3f);
            flash.type = LightType.Point;
            flash.color = new Color(1f, 0.8f, 0.4f);
            flash.range = 4f;
            flash.intensity = 5f;
            flash.enabled = false;

            LineRenderer trail = gun.AddComponent<LineRenderer>();
            trail.sharedMaterial = BuildTools.GetMaterial("Bullet Trail", new Color(1f, 0.9f, 0.5f), true);
            trail.widthMultiplier = 0.03f;
            trail.positionCount = 2;
            trail.useWorldSpace = true;
            trail.enabled = false;

            weapon.gunModel = gun;
            weapon.muzzleFlash = flash;
            weapon.bulletTrail = trail;
            gun.SetActive(false);
        }

        static void BuildSystems(Transform root)
        {
            GameObject systems = new GameObject("Game Systems");
            systems.transform.SetParent(root, false);

            WantedLevel wanted = systems.AddComponent<WantedLevel>();
            wanted.crimeSound = BuildTools.Sound("Impacts/impactPunch_heavy_000");
            wanted.bustedSound = BuildTools.Sound("Impacts/impactMetal_heavy_000");

            Transform station = FindAnchor("Police Station");
            if (station != null)
                wanted.releasePoint = Point("Police Release Point", systems.transform, station.position + station.forward * 2.5f + Vector3.up * 0.6f);

            // Music and city noise (2D sound, same volume everywhere)
            MusicPlayer music = systems.AddComponent<MusicPlayer>();
            music.music = systems.AddComponent<AudioSource>();
            music.ambience = systems.AddComponent<AudioSource>();
            foreach (AudioSource source in new[] { music.music, music.ambience })
            {
                source.playOnAwake = false;
                source.spatialBlend = 0f;
            }
        }

        static Transform Point(string name, Transform parent, Vector3 position)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.position = position;
            return t;
        }

        static void BuildNavMesh(Transform root)
        {
            GameObject go = new GameObject("City NavMesh");
            go.transform.SetParent(root, false);

            NavMeshSurface surface = go.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~((1 << LayerMask.NameToLayer("UI")) | (1 << minimapLayer));

            go.AddComponent<RuntimeNavMesh>();
        }

        // ---------- HUD ----------

        static GameHUD BuildHud(Transform root, out RawImage minimapImage)
        {
            GameObject canvasGo = new GameObject("Sim HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(root, false);
            canvasGo.layer = LayerMask.NameToLayer("UI");

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameHUD hud = canvasGo.AddComponent<GameHUD>();
            RectTransform c = canvasGo.GetComponent<RectTransform>();
            Material outline = FindTextOutlineMaterial();

            // Hunger and thirst, bottom left
            RectTransform needs = NewRect("Needs", c, new Vector2(0, 0), new Vector2(0, 0), new Vector2(30, 30), new Vector2(430, 110));
            AddImage(needs, new Color(0f, 0f, 0f, 0.45f));
            hud.hungerFill = Bar("Hunger", needs, 62f, out hud.hungerFillImage);
            hud.thirstFill = Bar("Thirst", needs, 14f, out hud.thirstFillImage);

            // "Press E" prompt, bottom middle
            RectTransform prompt = NewRect("Prompt", c, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 180), new Vector2(1000, 64));
            AddImage(prompt, new Color(0f, 0f, 0f, 0.6f));
            hud.promptText = AddText(Stretch("Prompt Text", prompt), "Press E", 30f, TextAlignmentOptions.Center, Color.white, true, null);
            hud.promptPanel = prompt.gameObject;

            // Pop-up messages, upper middle
            RectTransform message = NewRect("Message", c, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(1500, 120));
            hud.messageText = AddText(message, "", 40f, TextAlignmentOptions.Center, new Color(1f, 0.92f, 0.4f), true, outline);

            // Objective, top middle
            RectTransform objective = NewRect("Objective", c, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(1100, 52));
            AddImage(objective, new Color(0f, 0f, 0f, 0.5f));
            hud.objectiveText = AddText(Stretch("Objective Text", objective), "", 26f, TextAlignmentOptions.Center, Color.white, false, null);
            hud.defaultObjective = "Goal: get jobs (green on the map) and save $800 for the Dream Villa";

            // Wanted stars, top right
            RectTransform wanted = NewRect("Wanted", c, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -30), new Vector2(260, 50));
            hud.wantedStars = new Image[5];
            for (int i = 0; i < 5; i++)
            {
                RectTransform star = NewRect("Star " + (i + 1), wanted, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(25 + i * 52, 0), new Vector2(30, 30));
                star.localRotation = Quaternion.Euler(0f, 0f, 45f);
                hud.wantedStars[i] = AddImage(star, new Color(1f, 1f, 1f, 0.15f));
            }

            // Crosshair in the middle (only while holding the pistol)
            RectTransform crosshair = NewRect("Crosshair", c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
            AddImage(NewRect("Line Across", crosshair, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26, 3)), Color.white);
            AddImage(NewRect("Line Up", crosshair, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3, 26)), Color.white);
            hud.crosshair = crosshair.gameObject;
            crosshair.gameObject.SetActive(false);

            // Minimap, bottom right
            RectTransform frame = NewRect("Minimap", c, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 30), new Vector2(300, 300));
            AddImage(frame, new Color(0f, 0f, 0f, 0.8f));
            RectTransform mapRect = Stretch("Map", frame);
            mapRect.offsetMin = new Vector2(6, 6);
            mapRect.offsetMax = new Vector2(-6, -6);
            minimapImage = mapRect.gameObject.AddComponent<RawImage>();
            minimapImage.raycastTarget = false;
            RectTransform you = NewRect("You", frame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16, 16));
            you.localRotation = Quaternion.Euler(0f, 0f, 45f);
            AddImage(you, Color.white);

            return hud;
        }

        static Material FindTextOutlineMaterial()
        {
            // The outlined text style that comes with TextMeshPro, so messages are readable anywhere
            string path = AssetDatabase.FindAssets("t:Material Outline")
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == "LiberationSans SDF - Outline");
            return path != null ? AssetDatabase.LoadAssetAtPath<Material>(path) : null;
        }

        static RectTransform NewRect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        // A child that fills its parent completely
        static RectTransform Stretch(string name, Transform parent)
        {
            RectTransform rt = NewRect(name, parent, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        static Image AddImage(RectTransform rt, Color color)
        {
            Image image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static TextMeshProUGUI AddText(RectTransform rt, string text, float size, TextAlignmentOptions align, Color color, bool bold, Material material)
        {
            TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = TMP_Settings.defaultFontAsset;
            if (material != null)
                tmp.fontSharedMaterial = material;

            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = color;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            if (bold)
                tmp.fontStyle = FontStyles.Bold;

            return tmp;
        }

        // A label and a bar; returns the colored fill part
        static RectTransform Bar(string label, RectTransform panel, float y, out Image fillImage)
        {
            RectTransform labelRect = NewRect(label + " Label", panel, Vector2.zero, Vector2.zero, new Vector2(16, y), new Vector2(120, 36));
            AddText(labelRect, label.ToUpper(), 22f, TextAlignmentOptions.Left, Color.white, true, null);

            RectTransform back = NewRect(label + " Bar", panel, Vector2.zero, Vector2.zero, new Vector2(140, y + 7), new Vector2(270, 22));
            AddImage(back, new Color(1f, 1f, 1f, 0.15f));

            RectTransform fill = Stretch(label + " Fill", back);
            fill.pivot = new Vector2(0f, 0.5f);
            fillImage = AddImage(fill, new Color(0.30f, 0.80f, 0.35f));
            return fill;
        }

        // ---------- Minimap ----------

        static void BuildMinimap(Transform root, GameObject player, RawImage image)
        {
            GameObject go = new GameObject("Minimap Camera");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(0f, 60f, 0f);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            Camera cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.25f, 0.3f, 0.25f);
            cam.cullingMask = ~(1 << LayerMask.NameToLayer("UI"));
            cam.depth = -10f;
            cam.farClipPlane = 200f;
            cam.GetUniversalAdditionalCameraData().renderShadows = false;

            MinimapCamera map = go.AddComponent<MinimapCamera>();
            map.target = player != null ? player.transform : null;
            map.displayImage = image;

            Camera main = Camera.main;
            if (main != null)
            {
                map.viewCamera = main.transform;
                main.cullingMask &= ~(1 << minimapLayer);
            }
        }

        // Colored squares high above important places; only the minimap camera sees them
        static void AddMapIcons(Transform root)
        {
            Transform icons = BuildTools.Group("Map Icons", root);
            List<Vector3> used = new List<Vector3>();
            Color jobGreen = new Color(0.2f, 0.85f, 0.3f);

            foreach (Shop shop in Object.FindObjectsByType<Shop>())
                MapIcon(icons, shop.transform.position, new Color(1f, 0.55f, 0.1f), used, 3.5f);

            foreach (HomeProperty home in Object.FindObjectsByType<HomeProperty>())
                MapIcon(icons, home.transform.position, home.ownedAtStart ? new Color(1f, 0.9f, 0.2f) : new Color(0.75f, 0.3f, 0.9f), used, 3.5f);

            foreach (Job job in Object.FindObjectsByType<Job>())
                MapIcon(icons, job.transform.position, jobGreen, used, 3.5f);

            // Job markers get their own big icon so the target shows on the map
            foreach (DeliveryJob job in Object.FindObjectsByType<DeliveryJob>(FindObjectsInactive.Include))
                MarkerIcon(job.marker, new Color(0.2f, 1f, 0.35f));
            foreach (RideJob job in Object.FindObjectsByType<RideJob>(FindObjectsInactive.Include))
                MarkerIcon(job.marker, new Color(1f, 0.8f, 0.1f));

            Transform station = FindAnchor("Police Station");
            if (station != null)
                MapIcon(icons, station.position, new Color(0.2f, 0.4f, 1f), used, 3.5f);

            Transform hospital = FindAnchor("Hospital");
            if (hospital != null)
                MapIcon(icons, hospital.position, new Color(1f, 0.25f, 0.25f), used, 3.5f);
        }

        static void MarkerIcon(GameObject marker, Color color)
        {
            if (marker == null)
                return;

            GameObject icon = MapIcon(marker.transform, marker.transform.position, color, new List<Vector3>(), 5f);
            icon.transform.localPosition = new Vector3(0f, 40f, 0f);
        }

        static GameObject MapIcon(Transform parent, Vector3 position, Color color, List<Vector3> used, float size)
        {
            // Two counters at one shop would overlap; keep just one icon
            foreach (Vector3 other in used)
            {
                if (Vector3.Distance(other, position) < 4f)
                    return null;
            }
            used.Add(position);

            Material material = BuildTools.GetMaterial("Map Icon " + ColorUtility.ToHtmlStringRGB(color), color, true);
            GameObject icon = BuildTools.Primitive(PrimitiveType.Quad, "Map Icon", parent, material, false);
            icon.transform.position = new Vector3(position.x, 40f, position.z);
            icon.transform.rotation = Quaternion.Euler(90f, 45f, 0f);
            icon.transform.localScale = Vector3.one * size;
            BuildTools.SetLayer(icon, minimapLayer);
            return icon;
        }

        // ---------- People ----------

        // One Animator Controller for every person: idle / walk / run, and falling over
        static RuntimeAnimatorController BuildAnimatorController()
        {
            BuildTools.EnsureFolder(BuildTools.GeneratedRoot + "/Animation");
            AssetDatabase.DeleteAsset(ControllerPath);

            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(CharacterFolder + "character-male-a.fbx")
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .ToArray();

            AnimationClip idle = FindClip(clips, "idle");
            AnimationClip walk = FindClip(clips, "walk");
            AnimationClip sprint = FindClip(clips, "sprint");
            AnimationClip die = FindClip(clips, "die");

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Down", AnimatorControllerParameterType.Bool);

            AnimatorState move = controller.CreateBlendTreeInController("Move", out BlendTree tree, 0);
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            if (idle != null) tree.AddChild(idle, 0f);
            if (walk != null) tree.AddChild(walk, 1.3f);
            if (sprint != null) tree.AddChild(sprint, 3f);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            machine.defaultState = move;

            AnimatorState down = machine.AddState("Down");
            down.motion = die;

            AnimatorStateTransition fall = move.AddTransition(down);
            fall.AddCondition(AnimatorConditionMode.If, 0f, "Down");
            fall.hasExitTime = false;
            fall.duration = 0.1f;

            AnimatorStateTransition getUp = down.AddTransition(move);
            getUp.AddCondition(AnimatorConditionMode.IfNot, 0f, "Down");
            getUp.hasExitTime = false;
            getUp.duration = 0.25f;

            if (idle == null || walk == null)
                Debug.LogWarning("[I Am A Ball] Character animations not found. Clips: " + string.Join(", ", clips.Select(c => c.name)));

            AssetDatabase.SaveAssets();
            return controller;
        }

        static AnimationClip FindClip(AnimationClip[] clips, string key)
        {
            foreach (AnimationClip clip in clips)
            {
                string n = clip.name.ToLowerInvariant();
                if (n == key || n.EndsWith("|" + key) || n.EndsWith("_" + key) || n.EndsWith("." + key))
                    return clip;
            }
            return null;
        }

        static void BuildPeople(Transform root, RuntimeAnimatorController controller, Transform[] walkPoints)
        {
            if (walkPoints.Length == 0)
                return;

            Transform group = BuildTools.Group("People", root);
            AudioClip hit = BuildTools.Sound("Impacts/impactPunch_medium_000");

            for (int i = 0; i < 20; i++)
            {
                Transform spot = walkPoints[random.Next(walkPoints.Length)];
                Vector3 jitter = new Vector3((float)random.NextDouble() - 0.5f, 0f, (float)random.NextDouble() - 0.5f) * 2f;
                string model = CivilianModels[i % CivilianModels.Length];

                GameObject person = MakePerson("Person - " + model, model, spot.position + jitter, group, controller, true);
                NpcWalker walker = person.AddComponent<NpcWalker>();
                walker.walkPoints = walkPoints;
                walker.hitSound = hit;
                walker.animator = person.GetComponentInChildren<Animator>();
                walker.walkSpeed = 1.1f + (float)random.NextDouble() * 0.5f;
            }
        }

        static void BuildPolice(Transform root, RuntimeAnimatorController controller)
        {
            Transform station = FindAnchor("Police Station");
            if (station == null)
                return;

            Transform group = BuildTools.Group("Police", root);
            Vector3 side = Vector3.Cross(Vector3.up, station.forward).normalized;

            for (int i = 0; i < 2; i++)
            {
                Vector3 spot = station.position + station.forward * 1.5f + side * (i == 0 ? -1.5f : 1.5f);
                GameObject officer = MakePerson("Police Officer " + (i + 1), PoliceModel, spot, group, controller, true);
                officer.transform.rotation = station.rotation;

                PoliceOfficer police = officer.AddComponent<PoliceOfficer>();
                police.crimeStars = 2;
                police.crimeName = "You knocked over a police officer";
                police.hitSound = BuildTools.Sound("Impacts/impactPunch_heavy_001");
                police.animator = officer.GetComponentInChildren<Animator>();
            }
        }

        // Police cars that chase you at 3+ stars (parked at the station until then)
        static void BuildPoliceCars(Transform root)
        {
            GameObject prefab = BuildTools.Prefab("Cars", "police");
            if (prefab == null)
                return;

            Transform group = BuildTools.Group("Police Cars", root);
            for (int i = 1; i <= 2; i++)
            {
                Transform spot = FindAnchor("Police Car Spot " + i);
                if (spot == null)
                    continue;

                GameObject car = new GameObject("Police Car " + i);
                car.transform.SetParent(group, false);
                car.transform.position = spot.position;
                car.transform.rotation = spot.rotation;

                GameObject body = BuildTools.Spawn(prefab, car.transform);
                GameObjectUtility.SetStaticEditorFlags(body, (StaticEditorFlags)0);
                BuildTools.FitSize(body, spot.position, car.transform.eulerAngles.y + CarFrontYaw, CarLength, false);

                Rigidbody rb = car.AddComponent<Rigidbody>();
                rb.isKinematic = true;

                NavMeshAgent agent = car.AddComponent<NavMeshAgent>();
                agent.radius = 0.9f;
                agent.height = 1.6f;
                agent.speed = 6f;
                agent.angularSpeed = 240f;
                agent.acceleration = 14f;
                agent.stoppingDistance = 1f;
                agent.enabled = false;

                PoliceCar police = car.AddComponent<PoliceCar>();
                Bounds b = BuildTools.WorldBounds(body);
                police.sirenLights = new[]
                {
                    SirenLight(car.transform, new Vector3(b.center.x, b.max.y + 0.15f, b.center.z) - car.transform.right * 0.35f, Color.red),
                    SirenLight(car.transform, new Vector3(b.center.x, b.max.y + 0.15f, b.center.z) + car.transform.right * 0.35f, new Color(0.2f, 0.4f, 1f)),
                };
                police.sirenSound = Speaker(car, 25f, 0.6f);

                IgnoreForNavMesh(car);
            }
        }

        static Light SirenLight(Transform car, Vector3 position, Color color)
        {
            Light light = new GameObject("Siren Light").AddComponent<Light>();
            light.transform.SetParent(car, true);
            light.transform.position = position;
            light.type = LightType.Point;
            light.color = color;
            light.range = 7f;
            light.intensity = 6f;
            light.enabled = false;
            return light;
        }

        // A 3D sound source (gets quieter far away)
        static AudioSource Speaker(GameObject owner, float maxDistance, float volume)
        {
            AudioSource source = owner.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2f;
            source.maxDistance = maxDistance;
            source.volume = volume;
            return source;
        }

        static void BuildHospital(Transform root)
        {
            Transform door = FindAnchor("Hospital");
            if (door == null)
                return;

            GameObject go = new GameObject("Hospital");
            go.transform.SetParent(root, false);
            go.transform.position = door.position;

            Hospital hospital = go.AddComponent<Hospital>();
            hospital.releasePoint = Point("Hospital Release Point", go.transform, door.position + door.forward * 1.5f + Vector3.up * 0.6f);
            hospital.sound = BuildTools.Sound("Impacts/impactBell_heavy_003");
        }

        // The taxi passenger, the store customer and the trash bags for the jobs
        static void BuildJobHelpers(Transform root, RuntimeAnimatorController controller, Transform[] walkPoints)
        {
            Transform group = BuildTools.Group("Job Helpers", root);

            RideJob ride = Object.FindAnyObjectByType<RideJob>();
            if (ride != null)
            {
                Vector3 spot = ride.transform.position + ride.transform.forward * 0.8f + ride.transform.right * 1.2f;
                GameObject passenger = MakePerson("Taxi Passenger", "character-female-e", spot, group, controller, true);
                passenger.transform.rotation = ride.transform.rotation;
                ride.passenger = AddScriptedWalker(passenger);
            }

            ClerkJob clerk = Object.FindAnyObjectByType<ClerkJob>();
            if (clerk != null && clerk.customerSpawn != null)
            {
                GameObject customer = MakePerson("Store Customer", "character-male-b", clerk.customerSpawn.position, group, controller, true);
                clerk.customer = AddScriptedWalker(customer);
            }

            TrashJob trash = Object.FindAnyObjectByType<TrashJob>();
            if (trash != null)
            {
                trash.bagSpots = walkPoints;
                trash.bagPrefab = TrashBagPrefab();
            }
        }

        static ScriptedWalker AddScriptedWalker(GameObject person)
        {
            ScriptedWalker walker = person.AddComponent<ScriptedWalker>();
            walker.animator = person.GetComponentInChildren<Animator>();
            walker.hitSound = BuildTools.Sound("Impacts/impactPunch_medium_001");
            return walker;
        }

        // A low poly trash bag with a map icon, saved as a prefab the trash job copies
        static GameObject TrashBagPrefab()
        {
            BuildTools.EnsureFolder(PrefabFolder);
            GameObject bag = new GameObject("Trash Bag");
            try
            {
                Material plastic = BuildTools.GetMaterial("Trash Bag", new Color(0.08f, 0.12f, 0.1f), false);
                GameObject body = BuildTools.Primitive(PrimitiveType.Sphere, "Bag", bag.transform, plastic, false);
                body.transform.localPosition = new Vector3(0f, 0.3f, 0f);
                body.transform.localScale = new Vector3(0.55f, 0.6f, 0.5f);

                GameObject knot = BuildTools.Primitive(PrimitiveType.Cube, "Knot", bag.transform, plastic, false);
                knot.transform.localPosition = new Vector3(0f, 0.65f, 0f);
                knot.transform.localRotation = Quaternion.Euler(0f, 45f, 20f);
                knot.transform.localScale = new Vector3(0.12f, 0.15f, 0.12f);

                Material iconColor = BuildTools.GetMaterial("Map Icon Trash", new Color(0.2f, 0.85f, 0.3f), true);
                GameObject icon = BuildTools.Primitive(PrimitiveType.Quad, "Map Icon", bag.transform, iconColor, false);
                icon.transform.localPosition = new Vector3(0f, 40f, 0f);
                icon.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                icon.transform.localScale = Vector3.one * 2.5f;
                BuildTools.SetLayer(icon, minimapLayer);

                return PrefabUtility.SaveAsPrefabAsset(bag, PrefabFolder + "/Trash Bag.prefab");
            }
            finally
            {
                Object.DestroyImmediate(bag);
            }
        }

        // The black market dealer just stands behind the table
        static void BuildDealer(Transform root, RuntimeAnimatorController controller)
        {
            Transform spot = FindAnchor("Black Market Dealer");
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterFolder + "character-male-f.fbx");
            if (spot == null || model == null)
                return;

            GameObject dealer = new GameObject("Black Market Dealer");
            dealer.transform.SetParent(root, false);
            dealer.transform.position = spot.position;
            dealer.transform.rotation = spot.rotation;
            AddCharacterModel(dealer, model, controller);
        }

        // A person: collider, physics body and NavMesh agent on the outside, animated model inside
        static GameObject MakePerson(string name, string modelName, Vector3 position, Transform parent, RuntimeAnimatorController controller, bool walks)
        {
            GameObject person = new GameObject(name);
            person.transform.SetParent(parent, false);
            person.transform.position = position;
            person.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterFolder + modelName + ".fbx");
            if (model != null)
                AddCharacterModel(person, model, controller);

            CapsuleCollider capsule = person.AddComponent<CapsuleCollider>();
            capsule.height = PersonHeight;
            capsule.radius = 0.3f;
            capsule.center = new Vector3(0f, PersonHeight * 0.5f, 0f);

            Rigidbody rb = person.AddComponent<Rigidbody>();
            rb.mass = 60f;
            rb.isKinematic = true;

            if (walks)
            {
                NavMeshAgent agent = person.AddComponent<NavMeshAgent>();
                agent.radius = 0.3f;
                agent.height = PersonHeight;
                agent.angularSpeed = 360f;
                agent.acceleration = 8f;
                agent.stoppingDistance = 0.3f;
                agent.enabled = false;
            }

            IgnoreForNavMesh(person);
            return person;
        }

        static void AddCharacterModel(GameObject person, GameObject model, RuntimeAnimatorController controller)
        {
            GameObject body = (GameObject)PrefabUtility.InstantiatePrefab(model, person.transform);
            body.transform.localRotation = Quaternion.Euler(0f, CharacterFrontYaw, 0f);
            body.transform.localPosition = Vector3.zero;
            Bounds b = BuildTools.WorldBounds(body);
            float scale = b.size.y > 0.01f ? PersonHeight / b.size.y : 1f;
            body.transform.localScale = Vector3.one * scale;
            b = BuildTools.WorldBounds(body);
            body.transform.position += Vector3.up * (person.transform.position.y - b.min.y);

            Animator animator = body.GetComponent<Animator>();
            if (animator == null)
                animator = body.AddComponent<Animator>();

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        }

        // ---------- Traffic ----------

        // Cars loop around the roads: one big loop around the city and one around each column
        static void BuildTraffic(Transform root)
        {
            List<CityBuilder.Block> blocks = CityBuilder.FindBlocks();
            if (blocks.Count < 2)
                return;

            List<float> roadXs = RoadLines(blocks, true);
            List<float> roadZs = RoadLines(blocks, false);
            if (roadXs.Count < 2 || roadZs.Count < 2)
                return;

            Transform group = BuildTools.Group("Traffic", root);
            float south = roadZs.First(), north = roadZs.Last();
            float west = roadXs.First(), east = roadXs.Last();

            int carNumber = 0;
            AddLoop(group, Loop(west, south, east, north, true), 3, ref carNumber);

            for (int i = 0; i + 1 < roadXs.Count; i++)
                AddLoop(group, Loop(roadXs[i], south, roadXs[i + 1], north, false), 2, ref carNumber);
        }

        // Road center lines: halfway between neighboring blocks, and just outside the outer blocks
        static List<float> RoadLines(List<CityBuilder.Block> blocks, bool alongX)
        {
            var groups = blocks.GroupBy(b => alongX ? b.col : b.row).OrderBy(g => g.Key).ToList();
            List<float> mins = groups.Select(g => g.Min(b => alongX ? b.bounds.min.x : b.bounds.min.z)).ToList();
            List<float> maxs = groups.Select(g => g.Max(b => alongX ? b.bounds.max.x : b.bounds.max.z)).ToList();

            float gap = 4.4f;
            if (groups.Count > 1)
                gap = mins[1] - maxs[0];

            List<float> lines = new List<float> { mins[0] - gap * 0.5f };
            for (int i = 0; i + 1 < groups.Count; i++)
                lines.Add((maxs[i] + mins[i + 1]) * 0.5f);
            lines.Add(maxs[maxs.Count - 1] + gap * 0.5f);
            return lines;
        }

        // The route around a rectangle, driving on the right side, with rounded corners
        static Vector3[] Loop(float x0, float z0, float x1, float z1, bool clockwise)
        {
            Vector3[] corners = clockwise
                ? new[] { new Vector3(x0, 0, z0), new Vector3(x0, 0, z1), new Vector3(x1, 0, z1), new Vector3(x1, 0, z0) }
                : new[] { new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), new Vector3(x0, 0, z1) };

            const float lane = 0.95f;
            const float cornerCut = 2.2f;
            List<Vector3> route = new List<Vector3>();

            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 previous = corners[(i + corners.Length - 1) % corners.Length];
                Vector3 corner = corners[i];
                Vector3 next = corners[(i + 1) % corners.Length];

                Vector3 inDir = (corner - previous).normalized;
                Vector3 outDir = (next - corner).normalized;
                Vector3 inRight = new Vector3(inDir.z, 0f, -inDir.x);
                Vector3 outRight = new Vector3(outDir.z, 0f, -outDir.x);

                route.Add(corner - inDir * cornerCut + inRight * lane);
                route.Add(corner + outDir * cornerCut + outRight * lane);
            }

            for (int i = 0; i < route.Count; i++)
            {
                Vector3 p = route[i];
                p.y = BuildTools.GroundHeight(p, 0f);
                route[i] = p;
            }

            return route.ToArray();
        }

        static void AddLoop(Transform group, Vector3[] route, int cars, ref int carNumber)
        {
            for (int i = 0; i < cars; i++)
            {
                int start = (i * route.Length / cars) % route.Length;
                string model = TrafficModels[carNumber % TrafficModels.Length];
                carNumber++;

                GameObject prefab = BuildTools.Prefab("Cars", model);
                if (prefab == null)
                    continue;

                GameObject car = new GameObject("Car - " + model);
                car.transform.SetParent(group, false);
                Vector3 from = route[start];
                Vector3 to = route[(start + 1) % route.Length];
                car.transform.position = from;
                car.transform.rotation = Quaternion.LookRotation((to - from).normalized);

                GameObject body = BuildTools.Spawn(prefab, car.transform);
                GameObjectUtility.SetStaticEditorFlags(body, (StaticEditorFlags)0);
                BuildTools.FitSize(body, from, car.transform.eulerAngles.y + CarFrontYaw, CarLength, false);

                Rigidbody rb = car.AddComponent<Rigidbody>();
                rb.isKinematic = true;

                TrafficCar traffic = car.AddComponent<TrafficCar>();
                traffic.route = route;
                traffic.startIndex = (start + 1) % route.Length;
                traffic.speed = 4.5f + (float)random.NextDouble() * 1.5f;
                traffic.engineSound = Speaker(car, 18f, 0.35f);

                IgnoreForNavMesh(car);
            }
        }

        // ---------- Money ----------

        // Cash lying around the city (uses your Collectibles script, sparkles and sound)
        static void PlaceMoney(Transform root, Transform[] walkPoints)
        {
            GameObject cash = CashPrefab();
            if (cash == null || walkPoints.Length == 0)
                return;

            Transform group = BuildTools.Group("Money Pickups", root);
            for (int i = 0; i < 12; i++)
            {
                Transform spot = walkPoints[(i * 7 + 3) % walkPoints.Length];
                GameObject pickup = BuildTools.Spawn(cash, group);
                pickup.transform.position = spot.position + new Vector3(0.8f, 0.6f, 0.8f);
            }
        }

        static GameObject CashPrefab()
        {
            BuildTools.EnsureFolder(PrefabFolder);
            GameObject cash = new GameObject("Cash Pickup");
            try
            {
                Material green = BuildTools.GetMaterial("Cash Green", new Color(0.35f, 0.7f, 0.35f), false);
                Material band = BuildTools.GetMaterial("Cash Band", new Color(0.95f, 0.9f, 0.6f), false);

                for (int i = 0; i < 3; i++)
                {
                    GameObject bills = BuildTools.Primitive(PrimitiveType.Cube, "Bills", cash.transform, green, false);
                    bills.transform.localPosition = new Vector3(0f, i * 0.09f, 0f);
                    bills.transform.localRotation = Quaternion.Euler(0f, i * 12f, 0f);
                    bills.transform.localScale = new Vector3(0.6f, 0.08f, 0.3f);
                }

                GameObject paperBand = BuildTools.Primitive(PrimitiveType.Cube, "Band", cash.transform, band, false);
                paperBand.transform.localPosition = new Vector3(0f, 0.12f, 0f);
                paperBand.transform.localScale = new Vector3(0.12f, 0.3f, 0.32f);

                BoxCollider trigger = cash.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.center = new Vector3(0f, 0.1f, 0f);
                trigger.size = new Vector3(0.9f, 0.6f, 0.6f);

                MoneyCollectible money = cash.AddComponent<MoneyCollectible>();
                money.moneyValue = 10;
                GameObject particles = AssetDatabase.LoadAssetAtPath<GameObject>(MoneyParticlesPath);
                money.collectParticles = particles != null ? particles.GetComponent<ParticleSystem>() : null;
                money.collectSound = AssetDatabase.LoadAssetAtPath<AudioClip>(MoneySoundPath);

                IgnoreForNavMesh(cash);
                return PrefabUtility.SaveAsPrefabAsset(cash, PrefabFolder + "/Cash Pickup.prefab");
            }
            finally
            {
                Object.DestroyImmediate(cash);
            }
        }
    }
}
