using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Builds a standalone Windows player into Builds/Windows (git-ignored), for trying LAN multiplayer with two running copies:
/// menu Tools > Build Windows Player, or in batch mode (Unity closed):
///   Unity -batchmode -nographics -projectPath . -executeMethod BuildPlayer.Build -quit -logFile build.log
/// Add "-development" after the method name's arguments for a development build (console output, profiler).
/// </summary>
public static class BuildPlayer
{
    private const string OutputDir = "Builds/Windows";
    private const string ExeName = "WintermaulTD.exe";

    [MenuItem("Tools/Build Windows Player")]
    public static void BuildMenu()
    {
        Build();
    }

    public static void Build()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0) throw new InvalidOperationException("No scenes enabled in the build settings.");

        bool development = Environment.GetCommandLineArgs().Contains("-development");
        Directory.CreateDirectory(OutputDir);

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(OutputDir, ExeName),
            target = BuildTarget.StandaloneWindows64,
            options = development ? BuildOptions.Development : BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        if (summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"BuildPlayer: {summary.result} ({summary.totalErrors} errors)");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        Debug.Log($"BuildPlayer: built {options.locationPathName} ({summary.totalSize / (1024 * 1024)} MB in {summary.totalTime.TotalSeconds:F0} s). " +
                  "Run it from two folders' worth of windows, or next to the editor, to test LAN play.");
        if (!Application.isBatchMode) EditorUtility.RevealInFinder(options.locationPathName);
    }
}
