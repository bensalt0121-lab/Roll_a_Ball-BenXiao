/*********************************************************************************************
 * COMPONENT OF: Game Systems (made by the setup tool)
 * REQUIRED DEPENDENCIES: the road light models in the scene (their Lights), DayNightCycle,
 *                        PerformanceManager (for how many lights may glow)
 * DESCRIPTION: The city has about 144 street lights, and every glowing light costs speed.
 *              This turns all of them off during the day, and at night only turns on the
 *              ones closest to the camera. Every lamp also has a cheap glowing bulb and a
 *              pool of light on the ground (not a real light) that shows at night.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.1
 * VERSION 1.1: Glowing bulbs and light pools at night (nightGlow).
 *********************************************************************************************/
using System.Collections.Generic;
using UnityEngine;

public class StreetLightManager : MonoBehaviour
{
    // Hours when it is dark enough for street lights (18 = 6 PM, 6.5 = 6:30 AM)
    public float lightsOnAt = 18f;
    public float lightsOffAt = 6.5f;
    // Seconds between checks (checking every frame is not needed)
    public float checkEvery = 0.5f;
    // Glowing bulbs and light pools under every lamp (shown only at night)
    public GameObject nightGlow;

    private readonly List<Light> streetLights = new List<Light>();
    private DayNightCycle clock;
    private Camera viewCamera;
    private float nextCheck;

    void Start()
    {
        clock = FindAnyObjectByType<DayNightCycle>();
        FindStreetLights();
    }

    void Update()
    {
        if (Time.time < nextCheck)
            return;

        nextCheck = Time.time + checkEvery;
        UpdateLights();
    }

    // Street lights are the lights that belong to a "road light" model
    void FindStreetLights()
    {
        foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Include))
        {
            if (light.type != LightType.Directional && IsPartOfRoadLight(light.transform))
                streetLights.Add(light);
        }
    }

    bool IsPartOfRoadLight(Transform t)
    {
        for (Transform current = t; current != null; current = current.parent)
        {
            if (current.name.ToLowerInvariant().StartsWith("road light"))
                return true;
        }
        return false;
    }

    bool IsNight()
    {
        if (clock == null)
            return false;

        return clock.timeOfDay >= lightsOnAt || clock.timeOfDay < lightsOffAt;
    }

    // Turns on only the closest few lights at night, and none during the day
    void UpdateLights()
    {
        if (viewCamera == null)
            viewCamera = Camera.main;

        bool night = IsNight();
        if (nightGlow != null && nightGlow.activeSelf != night)
            nightGlow.SetActive(night);

        int allowed = night ? PerformanceManager.Current.maxStreetLights : 0;
        Vector3 eye = viewCamera != null ? viewCamera.transform.position : Vector3.zero;

        if (allowed > 0)
            streetLights.Sort((a, b) => (a.transform.position - eye).sqrMagnitude.CompareTo((b.transform.position - eye).sqrMagnitude));

        for (int i = 0; i < streetLights.Count; i++)
        {
            if (streetLights[i] != null)
                streetLights[i].enabled = i < allowed;
        }
    }
}
