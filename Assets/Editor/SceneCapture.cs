/*********************************************************************************************
 * COMPONENT OF: Editor tools (Tools > I Am A Ball > Take Screenshots)
 * REQUIRED DEPENDENCIES: the Game scene, CityBuilder (for the block list and anchors)
 * DESCRIPTION: Takes pictures of the map from above, from the sides and in front of every
 *              important place, in daylight, and saves them in the "Screenshots" folder next
 *              to Assets. Handy for checking the map or putting pictures in the GDD.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace IAmABall.EditorTools
{
    public static class SceneCapture
    {
        public static string DefaultFolder => Path.Combine(Directory.GetCurrentDirectory(), "Screenshots");

        [MenuItem(BuildTools.MenuRoot + "Take Screenshots", priority = 20)]
        static void CaptureMenu()
        {
            CaptureAll(DefaultFolder);
            EditorUtility.RevealInFinder(DefaultFolder);
        }

        public static void CaptureAll(string folder)
        {
            Directory.CreateDirectory(folder);
            Bounds city = CityBounds();
            Vector3 c = city.center;

            using (new DaylightForPictures())
            {
                float size = Mathf.Max(city.size.x, city.size.z) * 0.55f;
                Shot(folder, "01_top_down", c + Vector3.up * 70f, c, true, size, 1600, 1600, Vector3.forward);
                Shot(folder, "02_overview_southeast", new Vector3(city.max.x + 30f, 45f, city.min.z - 30f), c, false, 0f, 1600, 900, Vector3.up);
                Shot(folder, "03_overview_northwest", new Vector3(city.min.x - 30f, 45f, city.max.z + 30f), c, false, 0f, 1600, 900, Vector3.up);

                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    Vector3 p = player.transform.position;
                    Vector3 toCenter = new Vector3(c.x - p.x, 0f, c.z - p.z).normalized;
                    Shot(folder, "04_player_start", p - toCenter * 6f + Vector3.up * 2.5f, p + toCenter * 6f + Vector3.up * 1f, false, 0f, 1600, 900, Vector3.up);
                }

                Transform anchors = BuildTools.FindChildGroup(CityBuilder.RootName, CityBuilder.AnchorsName);
                if (anchors != null)
                {
                    int i = 10;
                    foreach (Transform anchor in anchors)
                    {
                        Vector3 focus = anchor.position + Vector3.up * 1.6f;
                        Vector3 eye = ClearViewPoint(focus, anchor.forward * 7f + Vector3.up * 1.2f);
                        string name = i++ + "_" + anchor.name.Replace("Anchor - ", "").Replace(' ', '_');
                        Shot(folder, name, eye, focus, false, 0f, 1600, 900, Vector3.up);
                    }
                }

                // Street level, looking down the middle road
                Shot(folder, "05_street_level", new Vector3(c.x, 1.7f, city.min.z - 2f), new Vector3(c.x, 1.5f, c.z), false, 0f, 1600, 900, Vector3.up);
            }

            Debug.Log("[I Am A Ball] Screenshots saved to " + folder);
        }

        // Moves the camera closer if a wall or tree is between it and what it looks at
        static Vector3 ClearViewPoint(Vector3 focus, Vector3 offset)
        {
            if (Physics.Raycast(focus, offset.normalized, out RaycastHit hit, offset.magnitude, ~0, QueryTriggerInteraction.Ignore))
                return hit.point - offset.normalized * 0.4f;

            return focus + offset;
        }

        static Bounds CityBounds()
        {
            var blocks = CityBuilder.FindBlocks();
            if (blocks.Count == 0)
                return new Bounds(Vector3.zero, Vector3.one * 100f);

            Bounds b = blocks[0].bounds;
            foreach (var block in blocks)
                b.Encapsulate(block.bounds);

            b.Expand(14f);
            return b;
        }

        public static void Shot(string folder, string name, Vector3 position, Vector3 lookAt, bool ortho, float orthoSize, int width, int height, Vector3 up)
        {
            GameObject go = new GameObject("Screenshot Camera");
            try
            {
                Camera cam = go.AddComponent<Camera>();
                cam.transform.position = position;
                cam.transform.LookAt(lookAt, up);
                cam.fieldOfView = 55f;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 1500f;
                cam.orthographic = ortho;
                cam.orthographicSize = orthoSize;

                int minimapLayer = LayerMask.NameToLayer("Minimap");
                if (minimapLayer >= 0)
                    cam.cullingMask = ~(1 << minimapLayer);

                SavePng(cam, Path.Combine(folder, name + ".png"), width, height);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        public static void SavePng(Camera cam, string path, int width, int height)
        {
            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture oldTarget = cam.targetTexture;
            RenderTexture oldActive = RenderTexture.active;

            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            Texture2D picture = new Texture2D(width, height, TextureFormat.RGB24, false);
            picture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            picture.Apply();

            cam.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            File.WriteAllBytes(path, picture.EncodeToPNG());

            UnityEngine.Object.DestroyImmediate(picture);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }

        // Puts the sun at midday while pictures are taken, then puts everything back
        class DaylightForPictures : IDisposable
        {
            readonly Light sun;
            readonly Quaternion sunRotation;
            readonly float sunIntensity;
            readonly Color sunColor;
            readonly float ambientIntensity;
            readonly Color ambientLight;

            public DaylightForPictures()
            {
                ambientIntensity = RenderSettings.ambientIntensity;
                ambientLight = RenderSettings.ambientLight;
                DayNightCycle cycle = UnityEngine.Object.FindAnyObjectByType<DayNightCycle>();
                sun = cycle != null ? cycle.sun : UnityEngine.Object.FindObjectsByType<Light>().FirstOrDefault(l => l.type == LightType.Directional);

                if (sun == null)
                    return;

                sunRotation = sun.transform.rotation;
                sunIntensity = sun.intensity;
                sunColor = sun.color;

                sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
                sun.intensity = 1.2f;
                sun.color = new Color(1f, 0.97f, 0.9f);
                RenderSettings.ambientIntensity = 1f;
                RenderSettings.ambientLight = new Color(0.6f, 0.62f, 0.68f);
            }

            public void Dispose()
            {
                RenderSettings.ambientIntensity = ambientIntensity;
                RenderSettings.ambientLight = ambientLight;

                if (sun == null)
                    return;

                sun.transform.rotation = sunRotation;
                sun.intensity = sunIntensity;
                sun.color = sunColor;
            }
        }
    }
}
