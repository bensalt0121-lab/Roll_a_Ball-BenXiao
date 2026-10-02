/*********************************************************************************************
 * COMPONENT OF: Editor tools (Tools > I Am A Ball menu and command line)
 * REQUIRED DEPENDENCIES: CityBuilder, GameplayBuilder, SceneCapture, PlaySimulation
 * DESCRIPTION: The one-click "Build Everything" button and "Remove Generated Stuff", plus
 *              versions that can run from the command line without opening the editor.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using System;
using UnityEditor;
using UnityEngine;

namespace IAmABall.EditorTools
{
    public static class IAmABallCommands
    {
        [MenuItem(BuildTools.MenuRoot + "Build Everything (City + Gameplay)", priority = 0)]
        static void BuildEverythingMenu()
        {
            bool ok = EditorUtility.DisplayDialog("Build Everything",
                "This adds generated buildings, props, NPCs, police, traffic and the HUD to the open scene.\n\n" +
                "Anything made by an earlier run is replaced. Your own objects are not touched.", "Build", "Cancel");

            if (!ok)
                return;

            BuildEverything();
            BuildTools.MarkSceneDirty();
        }

        [MenuItem(BuildTools.MenuRoot + "Remove Generated Stuff", priority = 30)]
        static void RemoveGeneratedMenu()
        {
            BuildTools.DeleteRoot(CityBuilder.RootName);
            BuildTools.DeleteRoot(GameplayBuilder.RootName);
            BuildTools.MarkSceneDirty();
        }

        public static void BuildEverything()
        {
            ModelAutoSetup.MakeAllPrefabs(false);
            CityBuilder.Build();
            GameplayBuilder.Build();
        }

        // ---------- Command line (used for testing without opening the editor window) ----------

        public static void BuildFromCommandLine()
        {
            BuildTools.OpenGameScene();
            BuildEverything();
            BuildTools.SaveScene();
        }

        public static void CaptureFromCommandLine()
        {
            BuildTools.OpenGameScene();
            SceneCapture.CaptureAll(ArgumentAfter("-captureFolder") ?? SceneCapture.DefaultFolder);
        }

        public static void BuildAndCaptureFromCommandLine()
        {
            BuildFromCommandLine();
            SceneCapture.CaptureAll(ArgumentAfter("-captureFolder") ?? SceneCapture.DefaultFolder);
        }

        public static string ArgumentAfter(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == name)
                    return args[i + 1];
            }
            return null;
        }
    }
}
