using UnityEditor;
using UnityEngine;

public static class RunnerBuild
{
    private const string AppName = "RooftopRunner";
    private const string ProductName = "Rooftop Runner";
    private const string CompanyName = "hackcpp";
    private const string BundleIdentifier = "com.hackcpp.rooftoprunner";
    private const string Version = "0.1.0";
    private const string AppOutputPath = "Builds/RooftopRunner.app";
    private const string AndroidDevelopmentOutputPath = "Builds/RooftopRunner-android-development.apk";
    private const string AndroidReleaseOutputPath = "Builds/RooftopRunner-android-0.1.0.apk";
    private const string TapTapZipPath = "Builds/RooftopRunner-mac-0.1.0.zip";
    private const string IconPath = "Assets/Brand/AppIcon.png";
    private static readonly Color SplashBackgroundColor = new Color(0.055f, 0.07f, 0.11f);
    private static readonly string[] Scenes = { "Assets/Scenes/SampleScene.unity" };

    [MenuItem("Runner/Build/Mac Development")]
    public static void BuildMacDevelopment()
    {
        BuildMac(AppOutputPath, BuildOptions.Development);
    }

    [MenuItem("Runner/Build/Mac Release")]
    public static void BuildMacRelease()
    {
        BuildMac(AppOutputPath, BuildOptions.None);
    }

    [MenuItem("Runner/Build/Mac Release Zip For TapTap")]
    public static void BuildMacReleaseZipForTapTap()
    {
        BuildMacRelease();
        PackageMacAppForTapTap();
    }

    [MenuItem("Runner/Build/Android Development APK")]
    public static void BuildAndroidDevelopment()
    {
        BuildAndroid(AndroidDevelopmentOutputPath, BuildOptions.Development | BuildOptions.AllowDebugging);
    }

    [MenuItem("Runner/Build/Android Release APK")]
    public static void BuildAndroidRelease()
    {
        BuildAndroid(AndroidReleaseOutputPath, BuildOptions.None);
    }

    public static void BuildMacReleaseForCommandLine()
    {
        BuildMacRelease();
    }

    public static void BuildMacReleaseZipForTapTapCommandLine()
    {
        BuildMacReleaseZipForTapTap();
    }

    public static void BuildAndroidReleaseForCommandLine()
    {
        BuildAndroidRelease();
    }

    private static void BuildMac(string outputPath, BuildOptions buildOptions)
    {
        ApplyMacPlayerSettings();

        BuildReportSummary(BuildPipeline.BuildPlayer(
            Scenes,
            outputPath,
            BuildTarget.StandaloneOSX,
            buildOptions));

        ApplyMacIcon(outputPath);
    }

    private static void BuildAndroid(string outputPath, BuildOptions buildOptions)
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
        {
            throw new System.InvalidOperationException("Android Build Support is not installed for this Unity Editor.");
        }

        ApplyAndroidPlayerSettings();
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outputPath));

        BuildReportSummary(BuildPipeline.BuildPlayer(
            Scenes,
            outputPath,
            BuildTarget.Android,
            buildOptions));
    }

    private static void ApplyMacPlayerSettings()
    {
        Texture2D icon = ApplyCommonPlayerSettings();
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, BundleIdentifier);
        PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Standalone, new[] { icon });
    }

    private static void ApplyAndroidPlayerSettings()
    {
        Texture2D icon = ApplyCommonPlayerSettings();
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, BundleIdentifier);
        PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Android, new[] { icon });
        PlayerSettings.Android.bundleVersionCode = 1;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel35;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.useCustomKeystore = false;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        EditorUserBuildSettings.buildAppBundle = false;
    }

    private static Texture2D ApplyCommonPlayerSettings()
    {
        PlayerSettings.companyName = CompanyName;
        PlayerSettings.productName = ProductName;
        PlayerSettings.bundleVersion = Version;
        ApplySplashScreenSettings();

        Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (icon == null)
        {
            throw new System.IO.FileNotFoundException("Missing app icon asset: " + IconPath);
        }

        return icon;
    }

    private static void ApplySplashScreenSettings()
    {
        // Unity Personal requires its splash and logo, so keep them static and visually aligned with the game.
        PlayerSettings.SplashScreen.show = true;
        PlayerSettings.SplashScreen.showUnityLogo = true;
        PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Static;
        PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.LightOnDark;
        PlayerSettings.SplashScreen.backgroundColor = SplashBackgroundColor;
        PlayerSettings.SplashScreen.background = null;
        PlayerSettings.SplashScreen.backgroundPortrait = null;
        PlayerSettings.SplashScreen.blurBackgroundImage = false;
        PlayerSettings.SplashScreen.overlayOpacity = 1f;
    }

    private static void PackageMacAppForTapTap()
    {
        if (!System.IO.Directory.Exists(AppOutputPath))
        {
            throw new System.IO.DirectoryNotFoundException("Missing macOS app build: " + AppOutputPath);
        }

        if (System.IO.File.Exists(TapTapZipPath))
        {
            System.IO.File.Delete(TapTapZipPath);
        }

        string arguments = "-c -k --norsrc --keepParent \"" + AppName + ".app\" \"" + System.IO.Path.GetFileName(TapTapZipPath) + "\"";
        string output = RunCommand("/usr/bin/ditto", arguments, "Builds");
        Debug.Log("TapTap macOS package ready: " + TapTapZipPath + "\n" + output);
    }

    private static void ApplyMacIcon(string appPath)
    {
        string iconsetPath = "Builds/AppIcon.iconset";
        string iconPath = appPath + "/Contents/Resources/PlayerIcon.icns";

        if (System.IO.Directory.Exists(iconsetPath))
        {
            System.IO.Directory.Delete(iconsetPath, true);
        }

        System.IO.Directory.CreateDirectory(iconsetPath);

        CreateIconPng(16, iconsetPath + "/icon_16x16.png");
        CreateIconPng(32, iconsetPath + "/icon_16x16@2x.png");
        CreateIconPng(32, iconsetPath + "/icon_32x32.png");
        CreateIconPng(64, iconsetPath + "/icon_32x32@2x.png");
        CreateIconPng(128, iconsetPath + "/icon_128x128.png");
        CreateIconPng(256, iconsetPath + "/icon_128x128@2x.png");
        CreateIconPng(256, iconsetPath + "/icon_256x256.png");
        CreateIconPng(512, iconsetPath + "/icon_256x256@2x.png");
        CreateIconPng(512, iconsetPath + "/icon_512x512.png");
        CreateIconPng(1024, iconsetPath + "/icon_512x512@2x.png");

        RunCommand("/usr/bin/iconutil", "-c icns \"" + iconsetPath + "\" -o \"" + iconPath + "\"", ".");
        RunCommand("/usr/libexec/PlistBuddy", "-c \"Set :CFBundleIconFile PlayerIcon.icns\" \"" + appPath + "/Contents/Info.plist\"", ".");

        try
        {
            RunCommand("/usr/bin/codesign", "--force --deep --sign - \"" + appPath + "\"", ".");
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("Ad-hoc codesign failed. The app still built, but macOS may report a stale signature: " + exception.Message);
        }
        finally
        {
            System.IO.Directory.Delete(iconsetPath, true);
        }
    }

    private static void CreateIconPng(int size, string outputPath)
    {
        RunCommand("/usr/bin/sips", "-z " + size + " " + size + " \"" + IconPath + "\" --out \"" + outputPath + "\"", ".");
    }

    private static void BuildReportSummary(UnityEditor.Build.Reporting.BuildReport report)
    {
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            throw new System.Exception("Build failed: " + report.summary.result);
        }

        Debug.Log("Build succeeded: " + report.summary.outputPath);
    }

    private static string RunCommand(string fileName, string arguments, string workingDirectory)
    {
        System.Diagnostics.Process process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            }
        };

        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new System.Exception(fileName + " failed with exit code " + process.ExitCode + ": " + error);
        }

        return output;
    }
}
