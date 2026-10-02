/*********************************************************************************************
 * COMPONENT OF: (helper, not a component) used by TutorialManager, GoalBeacons, DeliveryJob
 *               and RideJob
 * REQUIRED DEPENDENCIES: Universal Render Pipeline (Unlit shader), the "Minimap" layer
 * DESCRIPTION: Makes the tall glowing beams that show the player where to go: a bright core,
 *              a soft see-through glow around it, a ring on the ground, and a square that only
 *              the minimap sees. Brighten() upgrades the job markers already in the scene.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using UnityEngine;
using UnityEngine.Rendering;

public static class GuideBeam
{
    // How much brighter than normal the beam core is (above 1 makes it glow with bloom)
    const float CoreBrightness = 3f;

    // A brand new beam (hidden parts are already set up; move it and SetActive(true) to show it)
    public static GameObject Create(string name, Color color, float height)
    {
        GameObject root = new GameObject(name);

        GameObject core = Part(PrimitiveType.Cylinder, "Beam", root.transform, CoreMaterial(color));
        core.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
        core.transform.localScale = new Vector3(0.6f, height * 0.5f, 0.6f);

        GameObject ring = Part(PrimitiveType.Cylinder, "Ring", root.transform, CoreMaterial(color));
        ring.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        ring.transform.localScale = new Vector3(2.6f, 0.02f, 2.6f);

        AddHalo(root.transform, color, height);
        AddMapIcon(root.transform, color);
        return root;
    }

    // Makes an existing marker (made by the setup tool) as bright and tall as the new beams
    public static void Brighten(GameObject marker, Color color, float height)
    {
        if (marker == null || marker.transform.Find("Glow Halo") != null)
            return;

        Transform beam = marker.transform.Find("Beam");
        if (beam != null)
        {
            beam.localPosition = new Vector3(0f, height * 0.5f, 0f);
            beam.localScale = new Vector3(0.6f, height * 0.5f, 0.6f);
            beam.GetComponent<Renderer>().sharedMaterial = CoreMaterial(color);
        }

        AddHalo(marker.transform, color, height);
    }

    // A wide, faint, see-through cylinder around the beam so it looks like it glows
    static void AddHalo(Transform parent, Color color, float height)
    {
        GameObject halo = Part(PrimitiveType.Cylinder, "Glow Halo", parent, HaloMaterial(color));
        halo.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
        halo.transform.localScale = new Vector3(2f, height * 0.5f, 2f);
    }

    // A square high above the beam that only the minimap and big map cameras see
    static void AddMapIcon(Transform parent, Color color)
    {
        int mapLayer = LayerMask.NameToLayer("Minimap");
        if (mapLayer < 0)
            return;

        GameObject icon = Part(PrimitiveType.Quad, "Map Icon", parent, CoreMaterial(color));
        icon.transform.localPosition = new Vector3(0f, 40f, 0f);
        icon.transform.localRotation = Quaternion.Euler(90f, 45f, 0f);
        icon.transform.localScale = Vector3.one * 5f;
        icon.layer = mapLayer;
    }

    static GameObject Part(PrimitiveType type, string name, Transform parent, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        Object.Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);

        Renderer renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return part;
    }

    // ---------- Materials ----------

    static Material NewUnlit()
    {
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        return new Material(unlit != null ? unlit : Shader.Find("Sprites/Default"));
    }

    // Solid, extra bright color (not affected by sunlight or night)
    static Material CoreMaterial(Color color)
    {
        Material material = NewUnlit();
        SetColor(material, new Color(color.r * CoreBrightness, color.g * CoreBrightness, color.b * CoreBrightness, 1f));
        return material;
    }

    // See-through light that adds to whatever is behind it
    static Material HaloMaterial(Color color)
    {
        Material material = NewUnlit();
        SetColor(material, new Color(color.r, color.g, color.b, 0.22f));
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 2f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.One);
        material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        return material;
    }

    static void SetColor(Material material, Color color)
    {
        material.color = color;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
    }
}
