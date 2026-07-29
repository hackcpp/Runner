using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public sealed class RunnerMediaCapture : MonoBehaviour
{
    private const string CaptureFlag = "-taptapCapture";
    private const string OutputArgument = "-captureOutput";
    private const int CaptureSeed = 20260729;
    private const int FrameRate = 24;
    private const int CaptureFrameCount = FrameRate * 18;
    private const float CapturePlanDistance = 240f;
    private const float LaneDecisionDistance = 7f;
    private const float JumpDecisionDistance = 4.2f;
    private const float SlideDecisionDistance = 3.8f;

    private static readonly WaitForEndOfFrame EndOfFrame = new WaitForEndOfFrame();

    private EndlessRunnerGame game;
    private IReadOnlyList<RunnerObstacleRow> rows;
    private IReadOnlyList<int> lanePath;
    private string outputDirectory;
    private string framesDirectory;
    private string screenshotsDirectory;
    private int nextRowIndex;
    private int actionRequestedRowIndex = -1;
    private int capturedFrameCount;
    private int routeScreenshotFrame = -1;
    private int jumpScreenshotFrame = -1;
    private int slideScreenshotFrame = -1;
    private bool captureStarted;
    private bool routeScreenshotCaptured;
    private bool jumpScreenshotCaptured;
    private bool slideScreenshotCaptured;

    public static void AttachIfRequested(GameObject host, EndlessRunnerGame runnerGame)
    {
        string requestedOutput;
        if (!TryReadOutputDirectory(Environment.GetCommandLineArgs(), out requestedOutput))
        {
            return;
        }

        RunnerMediaCapture capture = host.AddComponent<RunnerMediaCapture>();
        capture.game = runnerGame;
        capture.outputDirectory = requestedOutput;
    }

    public static bool TryReadOutputDirectory(IReadOnlyList<string> arguments, out string directory)
    {
        directory = null;
        if (arguments == null)
        {
            return false;
        }

        bool captureRequested = false;
        for (int index = 0; index < arguments.Count; index++)
        {
            if (arguments[index] == CaptureFlag)
            {
                captureRequested = true;
            }
            else if (arguments[index] == OutputArgument && index + 1 < arguments.Count)
            {
                directory = arguments[index + 1];
                index++;
            }
        }

        return captureRequested && !string.IsNullOrWhiteSpace(directory);
    }

    private IEnumerator Start()
    {
        framesDirectory = Path.Combine(outputDirectory, "frames");
        screenshotsDirectory = Path.Combine(outputDirectory, "screenshots");
        Directory.CreateDirectory(framesDirectory);
        Directory.CreateDirectory(screenshotsDirectory);

        Screen.SetResolution(1920, 1080, false);
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        Time.captureFramerate = FrameRate;

        for (int frame = 0; frame < 4; frame++)
        {
            yield return EndOfFrame;
        }

        RunnerRunSimulationResult plan = RunnerRunSimulator.Simulate(
            CaptureSeed,
            CapturePlanDistance);
        if (!plan.IsSurvivable || plan.Rows.Count != plan.LanePath.Count)
        {
            WriteManifest("Capture plan was not survivable.");
            Application.Quit(2);
            yield break;
        }

        rows = plan.Rows;
        lanePath = plan.LanePath;
        game.StartRunForTests(CaptureSeed);
        captureStarted = true;

        while (capturedFrameCount < CaptureFrameCount && game.IsPlaying)
        {
            yield return EndOfFrame;
            CaptureCurrentFrame();
            capturedFrameCount++;
        }

        captureStarted = false;
        Time.captureFramerate = 0;
        for (int frame = 0; frame < 30; frame++)
        {
            yield return null;
        }

        bool screenshotsExported = ExportStoreScreenshots();
        string error = capturedFrameCount != CaptureFrameCount
            ? "The automated run ended before all frames were captured."
            : screenshotsExported
                ? null
                : "One or more selected screenshot frames were not written.";
        WriteManifest(error);
        Application.Quit(string.IsNullOrEmpty(error) ? 0 : 3);
    }

    private void Update()
    {
        if (!captureStarted || !game.IsPlaying || rows == null)
        {
            return;
        }

        float distance = game.Distance;
        while (nextRowIndex < rows.Count && distance > rows[nextRowIndex].Z + 0.9f)
        {
            nextRowIndex++;
        }

        if (nextRowIndex >= rows.Count)
        {
            return;
        }

        RunnerObstacleRow row = rows[nextRowIndex];
        float distanceToRow = row.Z - distance;
        int desiredLane = lanePath[nextRowIndex];
        if (distanceToRow <= LaneDecisionDistance && game.Motor.Lane != desiredLane)
        {
            game.Motor.RequestLaneChange(Math.Sign(desiredLane - game.Motor.Lane));
        }

        RunnerObstacleKind? obstacle = row.ObstacleInLane(desiredLane);
        if (!obstacle.HasValue || actionRequestedRowIndex == nextRowIndex)
        {
            return;
        }

        RunnerRequiredAction action = RunnerPatternCatalog.RequiredAction(obstacle.Value);
        if (action == RunnerRequiredAction.Jump && distanceToRow <= JumpDecisionDistance)
        {
            game.Motor.RequestJump();
            actionRequestedRowIndex = nextRowIndex;
        }
        else if (action == RunnerRequiredAction.Slide && distanceToRow <= SlideDecisionDistance)
        {
            game.Motor.RequestSlide();
            actionRequestedRowIndex = nextRowIndex;
        }
    }

    private void CaptureCurrentFrame()
    {
        string framePath = Path.Combine(
            framesDirectory,
            string.Format(CultureInfo.InvariantCulture, "frame-{0:D4}.png", capturedFrameCount));
        ScreenCapture.CaptureScreenshot(framePath);

        float distance = game.Distance;
        if (!routeScreenshotCaptured && distance >= 23f && distance <= 27f)
        {
            routeScreenshotFrame = capturedFrameCount;
            routeScreenshotCaptured = true;
        }

        if (!jumpScreenshotCaptured &&
            game.Motor.State == RunnerActionState.Airborne &&
            distance >= 46f && distance <= 49f)
        {
            jumpScreenshotFrame = capturedFrameCount;
            jumpScreenshotCaptured = true;
        }

        if (!slideScreenshotCaptured &&
            game.Motor.State == RunnerActionState.Sliding &&
            distance >= 68f && distance <= 71f)
        {
            slideScreenshotFrame = capturedFrameCount;
            slideScreenshotCaptured = true;
        }
    }

    private bool ExportStoreScreenshots()
    {
        return ExportStoreScreenshot(routeScreenshotFrame, "01-route-reading.png") &&
               ExportStoreScreenshot(jumpScreenshotFrame, "02-jump-clear.png") &&
               ExportStoreScreenshot(slideScreenshotFrame, "03-slide-clear.png");
    }

    private bool ExportStoreScreenshot(int frameIndex, string fileName)
    {
        if (frameIndex < 0)
        {
            return false;
        }

        string source = Path.Combine(
            framesDirectory,
            string.Format(CultureInfo.InvariantCulture, "frame-{0:D4}.png", frameIndex));
        if (!File.Exists(source))
        {
            return false;
        }

        File.Copy(source, Path.Combine(screenshotsDirectory, fileName), true);
        return true;
    }

    private void WriteManifest(string error)
    {
        string manifest = string.Format(
            CultureInfo.InvariantCulture,
            "seed={0}\nframeRate={1}\nframes={2}\nrouteScreenshot={3}\njumpScreenshot={4}\nslideScreenshot={5}\nerror={6}\n",
            CaptureSeed,
            FrameRate,
            capturedFrameCount,
            routeScreenshotCaptured,
            jumpScreenshotCaptured,
            slideScreenshotCaptured,
            error ?? string.Empty);
        File.WriteAllText(Path.Combine(outputDirectory, "capture-manifest.txt"), manifest);
    }
}
