using System.Linq;
using UnityEditor;
using UnityEngine;

// Build WebGL para GitHub Pages: sin compresión (Pages no envía Content-Encoding para .br/.gz).
// Uso: Unity -batchmode -quit -projectPath . -executeMethod WebGLBuild.Build
public static class WebGLBuild
{
    [MenuItem("Pixel Z/Build WebGL")]
    public static void Build()
    {
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = false;

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        var report = BuildPipeline.BuildPlayer(scenes, "Builds/WebGL", BuildTarget.WebGL, BuildOptions.None);
        Debug.Log("WebGL build: " + report.summary.result);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
