/*********************************************************************************************
 * COMPONENT OF: Game Systems (made by the setup tool)
 * REQUIRED DEPENDENCIES: main camera, Universal Render Pipeline, quality levels "Mobile" and
 *                        "PC" (Project Settings > Quality), GameHUD, Input System package
 * DESCRIPTION: Graphics settings for slow and fast computers. Low / Medium / High change how
 *              sharp the picture is, how far away things are drawn, how far small things
 *              (trees, people, cars) are drawn, shadows and how many street lights glow.
 *              The first time the game runs it picks a setting for the computer (weak laptops
 *              get Low). F1 = change setting, F3 = show/hide frames per second (FPS).
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PerformanceManager : MonoBehaviour
{
    public enum Preset { Low, Medium, High }

    [System.Serializable]
    public class PresetSettings
    {
        // Quality level name from Project Settings > Quality ("Mobile" is cheaper than "PC")
        public string qualityLevel = "PC";
        // How sharp the 3D picture is (1 = full sharpness, 0.7 = 70%, much faster)
        [Range(0.5f, 1f)] public float renderScale = 1f;
        // Shadows further than this (meters) are not drawn
        public float shadowDistance = 50f;
        // Nothing further than this (meters) is drawn; fog hides the edge
        public float viewDistance = 400f;
        // Trees, benches, people and cars further than this are not drawn
        public float detailDistance = 120f;
        // How many street lights can glow at night at the same time (closest ones)
        public int maxStreetLights = 16;
        // How many times per second the minimap is redrawn
        public float minimapFps = 20f;
        // Part of the people and traffic cars that are kept (0.5 = half of them)
        [Range(0.1f, 1f)] public float crowdAmount = 1f;
        // Screen effects like bloom and vignette
        public bool postEffects = true;
    }

    // Lets other scripts read the current settings, for example PerformanceManager.Current
    public static PresetSettings Current { get; private set; } = new PresetSettings();

    [Header("Presets")]
    public PresetSettings low = new PresetSettings { qualityLevel = "Mobile", renderScale = 0.7f, shadowDistance = 25f, viewDistance = 150f, detailDistance = 45f, maxStreetLights = 4, minimapFps = 5f, crowdAmount = 0.5f, postEffects = false };
    public PresetSettings medium = new PresetSettings { qualityLevel = "PC", renderScale = 0.85f, shadowDistance = 35f, viewDistance = 250f, detailDistance = 75f, maxStreetLights = 8, minimapFps = 10f, crowdAmount = 0.8f, postEffects = true };
    public PresetSettings high = new PresetSettings { qualityLevel = "PC", renderScale = 1f, shadowDistance = 50f, viewDistance = 500f, detailDistance = 140f, maxStreetLights = 12, minimapFps = 20f, crowdAmount = 1f, postEffects = true };

    [Header("Other")]
    // Caps the frame rate so laptops do not overheat
    public int targetFrameRate = 60;
    // Small text in the corner showing frames per second (F3)
    public TMP_Text fpsText;

    public Preset CurrentPreset { get; private set; }

    private const string SaveKey = "IAmABall.GraphicsPreset";
    private Camera mainCamera;
    private UniversalRenderPipelineAsset pipeline;
    // Original sharpness and shadow distance of every pipeline asset we changed
    private readonly Dictionary<UniversalRenderPipelineAsset, Vector2> originalPipelineSettings = new Dictionary<UniversalRenderPipelineAsset, Vector2>();
    private int originalQualityLevel;
    private float fpsTimer;
    private int fpsFrames;
    private GameObject[] people;
    private GameObject[] cars;

    void Awake()
    {
        RememberOriginalSettings();
        Application.targetFrameRate = targetFrameRate;
        Apply(LoadSavedPreset());
    }

    void Update()
    {
        CheckKeys();
        UpdateFpsCounter();
    }

    // Unity changes settings files when they are changed in Play mode, so put them back
    void OnDestroy()
    {
        RestoreOriginalSettings();
    }

    public void Apply(Preset preset)
    {
        CurrentPreset = preset;
        Current = preset == Preset.Low ? low : preset == Preset.Medium ? medium : high;

        SetQualityLevel(Current.qualityLevel);
        SetPipelineSettings();
        SetCameraDistances();
        SetPostEffects();
        SetCrowdSize();
        PlayerPrefs.SetInt(SaveKey, (int)preset);
    }

    // ---------- Choosing a preset ----------

    // The saved choice, or a guess based on this computer's graphics card and memory
    Preset LoadSavedPreset()
    {
        if (PlayerPrefs.HasKey(SaveKey))
            return (Preset)Mathf.Clamp(PlayerPrefs.GetInt(SaveKey), 0, 2);

        string gpu = SystemInfo.graphicsDeviceName.ToLowerInvariant();
        bool builtInGraphics = gpu.Contains("intel") || gpu.Contains("uhd") || gpu.Contains("iris") || gpu.Contains("vega") || gpu.Contains("radeon(tm) graphics");

        if (builtInGraphics || SystemInfo.graphicsMemorySize <= 2048 || SystemInfo.systemMemorySize < 8000)
            return Preset.Low;
        if (SystemInfo.graphicsMemorySize <= 4096)
            return Preset.Medium;
        return Preset.High;
    }

    void CheckKeys()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.f1Key.wasPressedThisFrame)
        {
            Preset next = (Preset)(((int)CurrentPreset + 1) % 3);
            Apply(next);
            GameHUD.Say("Graphics: " + next + "  (F1 to change, F3 for FPS)", 2.5f);
        }

        if (Keyboard.current.f3Key.wasPressedThisFrame && fpsText != null)
            fpsText.gameObject.SetActive(!fpsText.gameObject.activeSelf);
    }

    // ---------- Applying settings ----------

    void SetQualityLevel(string levelName)
    {
        string[] names = QualitySettings.names;
        for (int i = 0; i < names.Length; i++)
        {
            if (names[i] == levelName && QualitySettings.GetQualityLevel() != i)
            {
                QualitySettings.SetQualityLevel(i, true);
                pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                RememberPipelineSettings();
                return;
            }
        }
    }

    void SetPipelineSettings()
    {
        if (pipeline == null)
            return;

        pipeline.renderScale = Current.renderScale;
        pipeline.shadowDistance = Current.shadowDistance;
    }

    // Far clipping, fog to hide the edge, and a shorter draw distance for small things
    void SetCameraDistances()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        mainCamera.farClipPlane = Current.viewDistance;

        RenderSettings.fog = Current.viewDistance < 400f;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = Current.viewDistance * 0.6f;
        RenderSettings.fogEndDistance = Current.viewDistance * 0.98f;

        int details = LayerMask.NameToLayer("Details");
        if (details >= 0)
        {
            float[] distances = new float[32];
            distances[details] = Current.detailDistance;
            mainCamera.layerCullDistances = distances;
            mainCamera.layerCullSpherical = true;
        }
    }

    // Bloom, vignette and other screen effects are turned off on Low
    void SetPostEffects()
    {
        foreach (Volume volume in FindObjectsByType<Volume>(FindObjectsInactive.Include))
            volume.enabled = Current.postEffects;
    }

    // Keeps only part of the people and traffic cars on slow computers
    void SetCrowdSize()
    {
        if (people == null)
        {
            people = FindAll<NpcWalker>();
            cars = FindAll<TrafficCar>();
        }

        KeepPart(people, Current.crowdAmount);
        KeepPart(cars, Current.crowdAmount);
    }

    GameObject[] FindAll<T>() where T : Component
    {
        T[] found = FindObjectsByType<T>(FindObjectsInactive.Include);
        GameObject[] objects = new GameObject[found.Length];
        for (int i = 0; i < found.Length; i++)
            objects[i] = found[i].gameObject;
        return objects;
    }

    void KeepPart(GameObject[] objects, float amount)
    {
        int keep = Mathf.CeilToInt(objects.Length * amount);
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
                objects[i].SetActive(i < keep);
        }
    }

    // ---------- Keeping the project's settings files unchanged ----------

    void RememberOriginalSettings()
    {
        originalQualityLevel = QualitySettings.GetQualityLevel();
        pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        RememberPipelineSettings();
    }

    void RememberPipelineSettings()
    {
        if (pipeline != null && !originalPipelineSettings.ContainsKey(pipeline))
            originalPipelineSettings[pipeline] = new Vector2(pipeline.renderScale, pipeline.shadowDistance);
    }

    void RestoreOriginalSettings()
    {
        foreach (KeyValuePair<UniversalRenderPipelineAsset, Vector2> saved in originalPipelineSettings)
        {
            if (saved.Key == null)
                continue;

            saved.Key.renderScale = saved.Value.x;
            saved.Key.shadowDistance = saved.Value.y;
        }

        if (QualitySettings.GetQualityLevel() != originalQualityLevel)
            QualitySettings.SetQualityLevel(originalQualityLevel, true);
    }

    // ---------- FPS counter ----------

    // Counts frames for half a second, then shows the average
    void UpdateFpsCounter()
    {
        if (fpsText == null || !fpsText.gameObject.activeSelf)
            return;

        fpsFrames++;
        fpsTimer += Time.unscaledDeltaTime;
        if (fpsTimer < 0.5f)
            return;

        fpsText.text = Mathf.RoundToInt(fpsFrames / fpsTimer) + " FPS  (" + CurrentPreset + ")";
        fpsFrames = 0;
        fpsTimer = 0f;
    }
}
