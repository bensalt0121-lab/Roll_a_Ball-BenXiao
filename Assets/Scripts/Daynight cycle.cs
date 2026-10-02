/*********************************************************************************************
 * COMPONENT OF: Daynight manager
 * REQUIRED DEPENDENCIES: Directional Light (sun) in the sun field, a Skybox material in the
 *                        Lighting settings (used for colors)
 * DESCRIPTION: Runs a 24-hour clock where one full day lasts dayLength seconds. Moves the
 *              sun across the sky, makes it orange at sunrise (6-8) and sunset (16-18),
 *              bright during the day, dim at night, and fades the ambient light to match.
 * AUTHOR: Ben Xiao
 * VERSION: 1.0
 * VERSION 1.1: Starts in the morning, nights go by faster, soft blue moonlight at night so
 *              the city is not pitch black, and the Scene view previews the chosen time.
 *********************************************************************************************/
using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    [Header("Time")]
    [Range(0f, 24f)]
    public float timeOfDay = 9f;

    [Tooltip("How many real seconds one full day takes.")]
    public float dayLength = 300f;

    [Header("Sun")]
    public Light sun;
    public float maxSunIntensity = 1.2f;

    [Header("Skybox Auto Detect")]
    public bool autoDetectSkyboxColor = true;

    private Color detectedSkyboxColor = Color.gray;

    [Header("Ambient Light")]
    public float dayAmbientIntensity = 1f;
    public float nightAmbientIntensity = 0.45f;

    [Header("Night")]
    [Tooltip("Nights go by this many times faster than days.")]
    public float nightSpeedMultiplier = 3f;
    // Soft blue light from the moon so the city is still visible at night
    public float moonIntensity = 0.35f;
    public Color moonColor = new Color(0.55f, 0.65f, 1f);

    // A copy of the sky material so it can get darker at night without changing the asset
    private Material skyCopy;
    private float skyBrightness = 1f;

    // True between 6 PM and 6 AM
    public bool IsNight => timeOfDay >= 18f || timeOfDay < 6f;

    void Start()
    {
        DetectSkyboxColor();
        CopySkybox();
    }

    // Uses a copy of the sky so changing its brightness never changes the project file
    void CopySkybox()
    {
        Material sky = RenderSettings.skybox;
        if (sky == null || !sky.HasProperty("_Exposure"))
            return;

        skyCopy = new Material(sky);
        skyBrightness = skyCopy.GetFloat("_Exposure");
        RenderSettings.skybox = skyCopy;
    }

    void Update()
    {
        UpdateTime();
        UpdateSun();
        UpdateLighting();
    }

    // Changing the time in the Inspector updates the sun in the Scene view right away
    void OnValidate()
    {
        if (!Application.isPlaying)
            ApplyTime(timeOfDay);
    }

    // Jumps the clock to an hour and updates the sun and light right away (used by tools and sleeping)
    public void ApplyTime(float hour)
    {
        timeOfDay = Mathf.Repeat(hour, 24f);
        DetectSkyboxColor();
        UpdateSun();
        UpdateLighting();
    }

    void DetectSkyboxColor()
    {
        if (!autoDetectSkyboxColor)
            return;

        Material skybox = RenderSettings.skybox;

        if (skybox == null)
        {
            Debug.LogWarning("No skybox material detected.");
            return;
        }

        // Try common skybox color properties
        if (skybox.HasProperty("_Tint"))
        {
            detectedSkyboxColor = skybox.GetColor("_Tint");
        }
        else if (skybox.HasProperty("_SkyTint"))
        {
            detectedSkyboxColor = skybox.GetColor("_SkyTint");
        }
        else if (skybox.HasProperty("_Color"))
        {
            detectedSkyboxColor = skybox.GetColor("_Color");
        }
        else
        {
            detectedSkyboxColor = RenderSettings.ambientLight;
        }
    }

    void UpdateTime()
    {
        float speed = IsNight ? nightSpeedMultiplier : 1f;
        timeOfDay += (24f / dayLength) * speed * Time.deltaTime;

        if (timeOfDay >= 24f)
        {
            timeOfDay -= 24f;
        }
    }

    void UpdateSun()
    {
        if (sun == null)
            return;

        float sunRotation =
            (timeOfDay / 24f) * 360f - 90f;

        sun.transform.rotation =
            Quaternion.Euler(sunRotation, 170f, 0f);

        float sunHeight =
            Mathf.Sin(
                (timeOfDay - 6f) / 24f *
                Mathf.PI * 2f
            );

        // Below the horizon: the same light becomes the moon
        if (sunHeight <= 0f)
        {
            ShowMoon(sunRotation, -sunHeight);
            return;
        }

        sunHeight = Mathf.Clamp01(sunHeight);

        sun.intensity =
            sunHeight * maxSunIntensity;

        // Use detected skybox color
        Color nightColor =
            detectedSkyboxColor * 0.15f;

        Color sunriseColor =
            Color.Lerp(
                detectedSkyboxColor,
                new Color(1f, 0.55f, 0.3f),
                0.7f
            );

        Color dayColor =
            Color.Lerp(
                detectedSkyboxColor,
                Color.white,
                0.7f
            );

        if (timeOfDay >= 6f && timeOfDay < 8f)
        {
            float t =
                Mathf.InverseLerp(
                    6f,
                    8f,
                    timeOfDay
                );

            sun.color =
                Color.Lerp(
                    sunriseColor,
                    dayColor,
                    t
                );
        }
        else if (timeOfDay >= 16f && timeOfDay < 18f)
        {
            float t =
                Mathf.InverseLerp(
                    16f,
                    18f,
                    timeOfDay
                );

            sun.color =
                Color.Lerp(
                    dayColor,
                    sunriseColor,
                    t
                );
        }
        else if (timeOfDay >= 8f && timeOfDay < 16f)
        {
            sun.color = dayColor;
        }
        else
        {
            sun.color = nightColor;
        }
    }

    // Turns the light around so it shines from above, dim and blue like the moon
    void ShowMoon(float sunRotation, float moonHeight)
    {
        sun.transform.rotation = Quaternion.Euler(sunRotation - 180f, 170f, 0f);
        sun.intensity = moonIntensity * Mathf.Clamp01(moonHeight * 3f);
        sun.color = moonColor;
    }

    void UpdateLighting()
    {
        float daylight =
            Mathf.Sin(
                (timeOfDay - 6f) / 24f *
                Mathf.PI * 2f
            );

        daylight = Mathf.Clamp01(daylight);

        RenderSettings.ambientIntensity =
            Mathf.Lerp(
                nightAmbientIntensity,
                dayAmbientIntensity,
                daylight
            );

        // The sky gets darker at night (only while playing, using the copy)
        if (skyCopy != null)
            skyCopy.SetFloat("_Exposure", Mathf.Lerp(skyBrightness * 0.15f, skyBrightness, daylight));

        // Also tint the ambient light toward the skybox
        RenderSettings.ambientLight =
            Color.Lerp(
                detectedSkyboxColor * 0.15f,
                detectedSkyboxColor,
                daylight
            );
    }

    // Useful if you change the skybox during gameplay
    public void RefreshSkyboxColor()
    {
        DetectSkyboxColor();
    }
}