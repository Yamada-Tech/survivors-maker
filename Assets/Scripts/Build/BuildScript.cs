#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildScript
{
    private const string RequiredUnityVersion = "6000.4.1f1";
    private static readonly string[] Scenes = { "Assets/Scenes/SampleScene.unity" };

    private static readonly string[] DefaultProjectFiles =
    {
        "player.json",
        "map.json",
        "enemies.json",
        "weapons.json",
        "waves.json"
    };

    [MenuItem("Build/Build Windows")]
    public static void BuildWindows()
    {
        Build(BuildTarget.StandaloneWindows64, "Builds/Windows/SurvivorsMaker.exe");
    }

    [MenuItem("Build/Build macOS")]
    public static void BuildMac()
    {
        Build(BuildTarget.StandaloneOSX, "Builds/Mac/SurvivorsMaker.app");
    }

    [MenuItem("Build/Build Linux")]
    public static void BuildLinux()
    {
        Build(BuildTarget.StandaloneLinux64, "Builds/Linux/SurvivorsMaker");
    }

    private static void Build(BuildTarget target, string outputPath)
    {
        EnsureUnityVersion();
        EnsureDefaultStreamingAssetsDataExists();
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? "Builds");

        var options = new BuildPlayerOptions
        {
            scenes = Scenes,
            locationPathName = outputPath,
            target = target,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"[Build] {target} → {report.summary.result}");

        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new BuildFailedException($"Build failed for {target}: {report.summary.result}");
        }
    }

    private static void EnsureDefaultStreamingAssetsDataExists()
    {
        const string defaultFolder = "Assets/StreamingAssets/ProjectData";
        if (!Directory.Exists(defaultFolder))
        {
            throw new BuildFailedException($"Default data folder not found: {defaultFolder}");
        }

        foreach (var fileName in DefaultProjectFiles)
        {
            var path = Path.Combine(defaultFolder, fileName);
            if (!File.Exists(path))
            {
                throw new BuildFailedException($"Default data file not found: {path}");
            }
        }
    }

    private static void EnsureUnityVersion()
    {
        if (!string.Equals(Application.unityVersion, RequiredUnityVersion, StringComparison.Ordinal))
        {
            throw new BuildFailedException(
                $"Unsupported Unity version: {Application.unityVersion}. Required: {RequiredUnityVersion}");
        }
    }
}
#endif
