using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    [Header("Time")]
    [Range(0f, 24f)]
    public float timeOfDay = 8f;

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
    public float nightAmbientIntensity = 0.15f;

    void Start()
    {
        DetectSkyboxColor();
    }

    void Update()
    {
        UpdateTime();
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
        timeOfDay += (24f / dayLength) * Time.deltaTime;

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