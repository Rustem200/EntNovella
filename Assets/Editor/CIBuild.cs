using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Сборка из командной строки — её вызывает GitHub Actions.
/// Локально не нужна: в редакторе всё так же собирается через Build Profiles.
/// </summary>
public static class CIBuild
{
    /// <summary>Собрать Xcode-проект в папку build/iOS.</summary>
    public static void BuildIOS()
    {
        var scenes = new List<string>();
        foreach (var scene in EditorBuildSettings.scenes)
            if (scene.enabled)
                scenes.Add(scene.path);

        if (scenes.Count == 0)
        {
            Debug.LogError("[CIBuild] В Build Settings нет ни одной включённой сцены.");
            EditorApplication.Exit(1);
            return;
        }

        var options = new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = "build/iOS",
            target = BuildTarget.iOS,
            targetGroup = BuildTargetGroup.iOS,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        Debug.Log($"[CIBuild] Результат: {summary.result}, размер {summary.totalSize} байт, " +
                  $"время {summary.totalTime}");

        EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
