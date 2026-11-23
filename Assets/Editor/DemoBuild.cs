using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DemoBuild
{
    private const string ScenePath = "Assets/Scenes/LineChartDemo.unity";
    private const string WebGLPath = "Builds/WebGL";

    [MenuItem("XCharts Demo/重新生成示例场景", false, 1)]
    public static void CreateDemoScene()
    {
        if (!Directory.Exists("Assets/Scenes"))
        {
            Directory.CreateDirectory("Assets/Scenes");
            AssetDatabase.Refresh();
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(244, 246, 251, 255);
        cam.orthographic = true;
        cam.transform.position = new Vector3(0f, 0f, -10f);

        var boot = new GameObject("LineChartDemo");
        boot.AddComponent<LineChartController>();

        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            throw new Exception("场景保存失败：" + ScenePath);
        }

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BUILD] SCENE_OK " + ScenePath);
    }

    [MenuItem("XCharts Demo/构建 WebGL", false, 2)]
    public static void BuildWebGL()
    {
        if (!File.Exists(ScenePath))
        {
            CreateDemoScene();
        }

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        PlayerSettings.companyName = "XChartsDemo";
        PlayerSettings.productName = "XChartsLineDemo";
        PlayerSettings.runInBackground = true;

        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;

        PlayerSettings.WebGL.template = "PROJECT:Blank";

        if (Directory.Exists(WebGLPath))
        {
            Directory.Delete(WebGLPath, true);
        }

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = WebGLPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;
        Debug.Log(string.Format("[BUILD] RESULT={0} SIZE={1:N1}MB TIME={2:N1}s ERRORS={3}",
            summary.result,
            summary.totalSize / 1024f / 1024f,
            summary.totalTime.TotalSeconds,
            summary.totalErrors));

        if (summary.result != BuildResult.Succeeded)
        {
            throw new Exception("WebGL 构建失败：" + summary.result);
        }

        Debug.Log("[BUILD] BUILD_OK " + Path.GetFullPath(WebGLPath));
    }

    public static void BatchBuild()
    {
        CreateDemoScene();
        BuildWebGL();
    }
}
