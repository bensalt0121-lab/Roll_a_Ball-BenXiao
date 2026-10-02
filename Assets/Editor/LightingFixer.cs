/*********************************************************************************************
 * COMPONENT OF: Editor tools (Tools > I Am A Ball > Turn Off Street Light Shadows)
 * REQUIRED DEPENDENCIES: none
 * DESCRIPTION: The 144 road lights each cast their own shadow, which is very slow (Unity has
 *              to squeeze 200+ shadow maps into one texture). This turns off shadows for every
 *              light except the sun, so the city runs smoothly. The lights still glow.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using UnityEditor;
using UnityEngine;

namespace IAmABall.EditorTools
{
    public static class LightingFixer
    {
        [MenuItem(BuildTools.MenuRoot + "Turn Off Street Light Shadows", priority = 22)]
        static void FixMenu()
        {
            int changed = TurnOffSmallLightShadows();
            BuildTools.MarkSceneDirty();
            Debug.Log("[I Am A Ball] Turned off shadows on " + changed + " lights. Save the scene to keep it.");
        }

        // Every light that is not the sun (directional) stops casting shadows
        public static int TurnOffSmallLightShadows()
        {
            int changed = 0;
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (light.type == LightType.Directional || light.shadows == LightShadows.None)
                    continue;

                Undo.RecordObject(light, "Turn off light shadows");
                light.shadows = LightShadows.None;
                PrefabUtility.RecordPrefabInstancePropertyModifications(light);
                changed++;
            }
            return changed;
        }
    }
}
