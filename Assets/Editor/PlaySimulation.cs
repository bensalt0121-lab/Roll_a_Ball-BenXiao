/*********************************************************************************************
 * COMPONENT OF: Editor tools (Tools > I Am A Ball > Run Test Simulation)
 * REQUIRED DEPENDENCIES: SimulationDriver, the Game scene
 * DESCRIPTION: Presses Play, lets SimulationDriver test the game by itself (shops, job,
 *              police, NPCs, traffic, buying a home), saves pictures and a results.txt in
 *              Screenshots/Simulation, then stops. Also works from the command line.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using System.IO;
using UnityEditor;
using UnityEngine;

namespace IAmABall.EditorTools
{
    [InitializeOnLoad]
    public static class PlaySimulation
    {
        const string RunningKey = "IAmABall.SimulationRunning";
        const string FolderKey = "IAmABall.SimulationFolder";

        static PlaySimulation()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem(BuildTools.MenuRoot + "Run Test Simulation", priority = 21)]
        static void RunMenu()
        {
            Start(Path.Combine(SceneCapture.DefaultFolder, "Simulation"));
        }

        public static void RunFromCommandLine()
        {
            BuildTools.OpenGameScene();
            Start(IAmABallCommands.ArgumentAfter("-captureFolder") ?? Path.Combine(SceneCapture.DefaultFolder, "Simulation"));
        }

        static void Start(string folder)
        {
            Directory.CreateDirectory(folder);
            SessionState.SetBool(RunningKey, true);
            SessionState.SetString(FolderKey, folder);
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(RunningKey, false))
                return;

            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                GameObject go = new GameObject("Simulation Driver");
                SimulationDriver driver = go.AddComponent<SimulationDriver>();
                driver.outputFolder = SessionState.GetString(FolderKey, SceneCapture.DefaultFolder);
                driver.onFinished = EditorApplication.ExitPlaymode;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.EraseBool(RunningKey);
                if (Application.isBatchMode)
                    EditorApplication.Exit(0);
            }
        }
    }
}
