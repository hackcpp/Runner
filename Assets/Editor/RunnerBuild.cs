using UnityEditor;

public static class RunnerBuild
{
    private const string OutputPath = "Builds/RooftopRunnerDemo.app";

    [MenuItem("Runner/Build/Mac Development")]
    public static void BuildMacDevelopment()
    {
        BuildMacDevelopment(OutputPath);
    }

    private static void BuildMacDevelopment(string outputPath)
    {
        BuildPipeline.BuildPlayer(
            new[] { "Assets/Scenes/SampleScene.unity" },
            outputPath,
            BuildTarget.StandaloneOSX,
            BuildOptions.Development);
    }
}
