/*********************************************************************************************
 * COMPONENT OF: "Map Screen" (creates itself when the game starts - no setup needed)
 * REQUIRED DEPENDENCIES: Player with PlayerInteractor, the "Ground" layer (to find the size of
 *                        the map), "Anchor - ..." points made by the city builder (for labels),
 *                        DayNightCycle, WantedLevel, MoneyManager, PlayerNeeds, GoalBeacons,
 *                        TextMeshPro, Input System package
 * DESCRIPTION: The big map (press M). Shows the whole city from above with a label on every
 *              place and where you are. A note next to it says what you should do right now,
 *              how to use the map, and what the colors mean. The game pauses while it is open.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class MapScreen : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    // How many times the player opened the map (the tutorial checks this)
    public static int TimesOpened { get; private set; }

    static readonly Color Orange = new Color(1f, 0.55f, 0.1f);
    static readonly Color Green = new Color(0.3f, 0.9f, 0.35f);
    static readonly Color Yellow = new Color(1f, 0.9f, 0.2f);
    static readonly Color Purple = new Color(0.8f, 0.45f, 1f);
    static readonly Color Blue = new Color(0.35f, 0.55f, 1f);
    static readonly Color Red = new Color(1f, 0.3f, 0.3f);

    const int TextureSize = 1024;
    const float MapPixels = 920f;

    private Camera mapCamera;
    private Bounds area;
    private GameObject screen;
    private RectTransform mapRect;
    private RectTransform youDot;
    private TMP_Text noteText;
    private Material outlineMaterial;
    private readonly List<Rect> usedLabelSpots = new List<Rect>();

    private PlayerInteractor player;
    private MoneyManager money;
    private Camera viewCamera;
    private float nextRender;
    private float timeScaleBeforeMap = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateAutomatically()
    {
        if (FindAnyObjectByType<MapScreen>() != null || FindAnyObjectByType<PlayerInteractor>() == null)
            return;

        new GameObject("Map Screen").AddComponent<MapScreen>();
    }

    void Start()
    {
        player = FindAnyObjectByType<PlayerInteractor>();
        money = FindAnyObjectByType<MoneyManager>();
        viewCamera = Camera.main;
        outlineMaterial = Resources.Load<Material>("Fonts & Materials/LiberationSans SDF - Outline");

        area = FindPlayArea();
        BuildMapCamera();
        BuildScreen();
        AddPlaceLabels();
        screen.SetActive(false);
    }

    void OnDestroy()
    {
        if (IsOpen)
            Time.timeScale = timeScaleBeforeMap;
        IsOpen = false;
    }

    void Update()
    {
        CheckKeys();
        if (!IsOpen)
            return;

        RenderMapWhenItIsTime();
        UpdateYouDot();
        UpdateNote();
    }

    // ---------- Opening and closing ----------

    void CheckKeys()
    {
        Keyboard keys = Keyboard.current;
        if (keys == null)
            return;

        if (keys.mKey.wasPressedThisFrame)
            SetOpen(!IsOpen);
        else if (IsOpen && keys.escapeKey.wasPressedThisFrame)
            SetOpen(false);
    }

    // Opening pauses the game (time stops), closing starts it again
    void SetOpen(bool open)
    {
        IsOpen = open;
        screen.SetActive(open);

        if (open)
        {
            TimesOpened++;
            timeScaleBeforeMap = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            nextRender = 0f;
        }
        else
        {
            Time.timeScale = timeScaleBeforeMap;
        }
    }

    // ---------- The camera that takes the map picture ----------

    // The area covered by streets, blocks and grass (everything on the Ground layer, but not
    // the giant ground under the whole world)
    Bounds FindPlayArea()
    {
        int ground = LayerMask.NameToLayer("Ground");
        Bounds bounds = new Bounds();
        bool found = false;

        foreach (Renderer r in FindObjectsByType<Renderer>())
        {
            if (r.gameObject.layer != ground || r.bounds.size.x > 250f || r.bounds.size.z > 250f)
                continue;

            if (found)
                bounds.Encapsulate(r.bounds);
            else
                bounds = r.bounds;
            found = true;
        }

        if (!found)
            bounds = new Bounds(Vector3.zero, Vector3.one * 200f);

        bounds.Expand(6f);
        return bounds;
    }

    void BuildMapCamera()
    {
        GameObject go = new GameObject("Map Camera");
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(area.center.x, 150f, area.center.z);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        mapCamera = go.AddComponent<Camera>();
        mapCamera.orthographic = true;
        mapCamera.orthographicSize = Mathf.Max(area.size.x, area.size.z) * 0.5f;
        mapCamera.clearFlags = CameraClearFlags.SolidColor;
        mapCamera.backgroundColor = new Color(0.3f, 0.45f, 0.3f);
        mapCamera.farClipPlane = 400f;
        mapCamera.enabled = false;

        int hidden = (1 << LayerMask.NameToLayer("UI"));
        int details = LayerMask.NameToLayer("Details");
        if (details >= 0)
            hidden |= 1 << details;
        mapCamera.cullingMask = ~hidden;
        mapCamera.GetUniversalAdditionalCameraData().renderShadows = false;
        mapCamera.targetTexture = new RenderTexture(TextureSize, TextureSize, 16);
    }

    // The map is redrawn once a second while open, always in daylight and without fog
    void RenderMapWhenItIsTime()
    {
        if (Time.unscaledTime < nextRender)
            return;

        nextRender = Time.unscaledTime + 1f;

        DayNightCycle clock = FindAnyObjectByType<DayNightCycle>();
        Light sun = clock != null ? clock.sun : null;
        bool fog = RenderSettings.fog;
        float ambient = RenderSettings.ambientIntensity;
        Quaternion sunRotation = sun != null ? sun.transform.rotation : Quaternion.identity;
        float sunIntensity = sun != null ? sun.intensity : 0f;
        Color sunColor = sun != null ? sun.color : Color.white;

        RenderSettings.fog = false;
        RenderSettings.ambientIntensity = 1f;
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(60f, 30f, 0f);
            sun.intensity = 1.1f;
            sun.color = Color.white;
        }

        mapCamera.Render();

        RenderSettings.fog = fog;
        RenderSettings.ambientIntensity = ambient;
        if (sun != null)
        {
            sun.transform.rotation = sunRotation;
            sun.intensity = sunIntensity;
            sun.color = sunColor;
        }
    }

    // Where a world position appears on the map picture
    Vector2 MapPosition(Vector3 world)
    {
        Vector3 viewport = mapCamera.WorldToViewportPoint(world);
        return new Vector2((viewport.x - 0.5f) * MapPixels, (viewport.y - 0.5f) * MapPixels);
    }

    // The white dot is you; the yellow line points where the camera looks
    void UpdateYouDot()
    {
        if (player == null)
            return;

        youDot.anchoredPosition = MapPosition(player.transform.position);
        float yaw = viewCamera != null ? viewCamera.transform.eulerAngles.y : 0f;
        youDot.localRotation = Quaternion.Euler(0f, 0f, -yaw);
    }

    // ---------- "What to do now" ----------

    void UpdateNote()
    {
        noteText.text = WhatToDoNow();
    }

    string WhatToDoNow()
    {
        if (WantedLevel.Instance != null && WantedLevel.Instance.Stars > 0)
            return "<color=#FF6060>The police are after you!</color> Get far away from them. The stars at the top right slowly disappear.";

        if (Job.Active != null)
            return "You are doing the <b>" + Job.Active.jobName + "</b> job. Follow the goal at the top of the screen (look for the tall beam).";

        PlayerNeeds needs = player != null ? player.Needs : null;
        if (needs != null && (needs.HungerPercent < 0.3f || needs.ThirstPercent < 0.3f))
            return "You are hungry or thirsty! Go to an <color=#FF8C1A>orange</color> place and press E at a counter to buy food or a drink.";

        HomeProperty house = GoalBeacons.NextHouse;
        int cash = money != null ? money.currentMoney : 0;
        if (house == null)
            return "<color=#7CFC7C>You own every house - you won!</color> Keep exploring, doing jobs and finding secrets.";

        if (cash >= house.price)
            return "You have <b>$" + cash + "</b>: enough for the <b>" + house.propertyName + "</b>! Go to the <color=#C080FF>purple beam</color> and press E at the door.";

        return "Earn money! Start a job at a <color=#7CFC7C>green</color> place.\nYou have <b>$" + cash + "</b> and need <b>$" + (house.price - cash) + "</b> more for the <b>" + house.propertyName + "</b> (purple beam).";
    }

    // ---------- Screen layout ----------

    void BuildScreen()
    {
        GameObject canvasObject = new GameObject("Map Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        screen = canvasObject;
        RectTransform root = canvasObject.GetComponent<RectTransform>();

        // Dark background over the game
        RectTransform dim = MakeRect("Background", root, Vector2.zero, Vector2.zero);
        dim.anchorMin = Vector2.zero;
        dim.anchorMax = Vector2.one;
        Paint(dim, new Color(0f, 0f, 0f, 0.8f));

        // The map picture on the left
        RectTransform frame = MakeRect("Map Frame", root, new Vector2(-330f, 0f), new Vector2(MapPixels + 16f, MapPixels + 16f));
        Paint(frame, new Color(0.05f, 0.05f, 0.05f, 1f));
        mapRect = MakeRect("Map", frame, Vector2.zero, new Vector2(MapPixels, MapPixels));
        mapRect.gameObject.AddComponent<RawImage>().texture = mapCamera.targetTexture;

        youDot = MakeRect("You", mapRect, Vector2.zero, new Vector2(18f, 18f));
        Paint(youDot, Color.white);
        RectTransform lookLine = MakeRect("Looking", youDot, new Vector2(0f, 9f), new Vector2(5f, 26f));
        lookLine.pivot = new Vector2(0.5f, 0f);
        lookLine.anchoredPosition = new Vector2(0f, 6f);
        Paint(lookLine, Yellow);

        // Notes on the right
        RectTransform panel = MakeRect("Notes", root, new Vector2(500f, 0f), new Vector2(760f, MapPixels + 16f));
        Paint(panel, new Color(0.08f, 0.08f, 0.1f, 0.95f));
        float y = 30f;
        Note(panel, "CITY MAP", 40f, new Color(1f, 0.85f, 0.3f), true, ref y, 56f);
        Note(panel, "WHAT TO DO NOW", 24f, new Color(1f, 0.85f, 0.3f), true, ref y, 36f);
        noteText = Note(panel, "", 23f, Color.white, false, ref y, 150f);
        Note(panel, "HOW TO USE THE MAP", 24f, new Color(1f, 0.85f, 0.3f), true, ref y, 36f);
        Note(panel, "The <b>white dot</b> is you. The <b>yellow line</b> shows the way you are looking.\n" +
                    "Every place you can use has a name on the map.\n" +
                    "In the city, <b>tall beams</b> show where to go: <color=#59BFFF>blue</color> = tutorial, " +
                    "<color=#7CFC7C>green</color> = delivery, <color=#FFD54A>yellow</color> = taxi passenger, " +
                    "<color=#C080FF>purple</color> = next house to buy.\n" +
                    "The game is paused while the map is open.", 20f, Color.white, false, ref y, 230f);
        Note(panel, "MAP COLORS", 24f, new Color(1f, 0.85f, 0.3f), true, ref y, 36f);
        Note(panel, "<color=#FF8C1A>Orange</color> = food and drinks     <color=#33D94D>Green</color> = jobs\n" +
                    "<color=#FFE633>Yellow</color> = your home     <color=#C080FF>Purple</color> = house for sale\n" +
                    "<color=#5A8CFF>Blue</color> = police     <color=#FF5050>Red</color> = hospital", 21f, Color.white, false, ref y, 110f);
        Note(panel, "M or Esc = close the map", 19f, new Color(0.7f, 0.7f, 0.7f), false, ref y, 30f);
    }

    // A label for every important place (made from the "Anchor - ..." points of the city)
    void AddPlaceLabels()
    {
        GameObject city = GameObject.Find("== GENERATED CITY DETAILS ==");
        Transform anchors = city != null ? city.transform.Find("Anchors") : null;
        if (anchors == null)
            return;

        foreach (Transform anchor in anchors)
        {
            string place = anchor.name.Replace("Anchor - ", "");
            if (place.Contains("Police Car") || place.Contains("Black Market"))
                continue;

            Label(place == "Starter Apartment" ? "Your Home" : place, anchor.position, ColorFor(place));
        }
    }

    static Color ColorFor(string place)
    {
        if (place.Contains("Shop") || place.Contains("Bar") || place.Contains("Store"))
            return Orange;
        if (place.Contains("Depot") || place.Contains("Trash") || place.Contains("Taxi") || place.Contains("Clerk") || place.Contains("Show"))
            return Green;
        if (place.Contains("Apartment"))
            return Yellow;
        if (place.Contains("House") || place.Contains("Villa"))
            return Purple;
        if (place.Contains("Police"))
            return Blue;
        if (place.Contains("Hospital"))
            return Red;
        return Color.white;
    }

    // A colored dot on the place and its name next to it (moved down if it would overlap another)
    void Label(string text, Vector3 world, Color color)
    {
        Vector2 spot = MapPosition(world);

        RectTransform dot = MakeRect("Dot - " + text, mapRect, spot, new Vector2(12f, 12f));
        Paint(dot, color);

        Vector2 labelSpot = spot + new Vector2(0f, 16f);
        Rect box = new Rect(labelSpot.x - 70f, labelSpot.y - 10f, 140f, 20f);
        for (int tries = 0; tries < 5 && Overlaps(box); tries++)
        {
            labelSpot.y -= 22f;
            box.y -= 22f;
        }
        usedLabelSpots.Add(box);

        RectTransform rect = MakeRect("Label - " + text, mapRect, labelSpot, new Vector2(180f, 24f));
        TMP_Text label = MakeText(rect, 17f, color, true);
        label.alignment = TextAlignmentOptions.Center;
        label.text = text;
        if (outlineMaterial != null)
            label.fontSharedMaterial = outlineMaterial;
    }

    bool Overlaps(Rect box)
    {
        foreach (Rect used in usedLabelSpots)
        {
            if (used.Overlaps(box))
                return true;
        }
        return false;
    }

    // ---------- Small UI helpers ----------

    static RectTransform MakeRect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    static void Paint(RectTransform rect, Color color)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    static TMP_Text MakeText(RectTransform rect, float fontSize, Color color, bool bold)
    {
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.color = color;
        text.richText = true;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        if (bold)
            text.fontStyle = FontStyles.Bold;
        return text;
    }

    // A text block in the notes panel, stacked from the top
    static TMP_Text Note(RectTransform panel, string content, float fontSize, Color color, bool bold, ref float y, float height)
    {
        RectTransform rect = new GameObject("Note", typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(panel, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(30f, -y);
        rect.sizeDelta = new Vector2(700f, height);
        y += height;

        TMP_Text text = MakeText(rect, fontSize, color, bold);
        text.text = content;
        return text;
    }
}
