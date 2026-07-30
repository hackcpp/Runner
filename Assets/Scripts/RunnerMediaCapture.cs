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
    private const int CaptureLevelNumber = 3;
    private const int FrameRate = 24;
    private const int CaptureFrameCount = FrameRate * 18;
    private const int LevelClearCaptureFrameCount = FrameRate * 5;
    private const float LevelClearLeadDistance = 0.5f;
    private const float LaneDecisionDistance = 7f;
    private const float JumpDecisionDistance = 4.2f;
    private const float SlideDecisionDistance = 3.8f;

    private static readonly WaitForEndOfFrame EndOfFrame = new WaitForEndOfFrame();

    private EndlessRunnerGame game;
    private IReadOnlyList<RunnerObstacleRow> rows;
    private IReadOnlyList<int> lanePath;
    private string outputDirectory;
    private string framesDirectory;
    private string levelClearFramesDirectory;
    private string screenshotsDirectory;
    private int nextRowIndex;
    private int actionRequestedRowIndex = -1;
    private int capturedFrameCount;
    private int capturedLevelClearFrameCount;
    private bool captureStarted;
    private bool levelScreenshotCaptured;
    private bool checkpointScreenshotCaptured;
    private bool clearScreenshotCaptured;
    private bool levelClearTransitionCompleted;

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
        levelClearFramesDirectory = Path.Combine(outputDirectory, "level-clear-frames");
        screenshotsDirectory = Path.Combine(outputDirectory, "screenshots");
        Directory.CreateDirectory(framesDirectory);
        Directory.CreateDirectory(levelClearFramesDirectory);
        Directory.CreateDirectory(screenshotsDirectory);

        Screen.SetResolution(1920, 1080, false);
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        Time.captureFramerate = FrameRate;

        for (int frame = 0; frame < 4; frame++)
        {
            yield return EndOfFrame;
        }

        RunnerLevelDefinition captureLevel = RunnerLevelCatalog.Levels[CaptureLevelNumber - 1];
        RunnerRunSimulationResult plan = RunnerRunSimulator.Simulate(captureLevel);
        if (!plan.IsSurvivable || plan.Rows.Count != plan.LanePath.Count)
        {
            WriteManifest("Capture plan was not survivable.");
            Application.Quit(2);
            yield break;
        }

        rows = plan.Rows;
        lanePath = plan.LanePath;
        game.StartLevelForTests(CaptureLevelNumber, captureLevel.Seed);
        captureStarted = true;

        while (capturedFrameCount < CaptureFrameCount && game.IsPlaying)
        {
            yield return EndOfFrame;
            CaptureCurrentFrame();
            capturedFrameCount++;
        }

        captureStarted = false;
        for (int frame = 0; frame < 30; frame++)
        {
            yield return null;
        }

        yield return CaptureStoreScreenshots();
        yield return CaptureLevelClearTransition();
        Time.captureFramerate = 0;
        string error = null;
        if (capturedFrameCount != CaptureFrameCount)
        {
            error = "The automated run ended before all frames were captured.";
        }
        else if (capturedLevelClearFrameCount != LevelClearCaptureFrameCount)
        {
            error = "The level clear transition ended before all frames were captured.";
        }
        else if (!levelClearTransitionCompleted)
        {
            error = "The level clear transition did not reach campaign completion.";
        }
        else if (!levelScreenshotCaptured || !checkpointScreenshotCaptured || !clearScreenshotCaptured)
        {
            error = "One or more level screenshots were not written.";
        }
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
        CaptureFrame(framesDirectory, capturedFrameCount);
    }

    private IEnumerator CaptureStoreScreenshots()
    {
        game.StartLevelForTests(1, RunnerLevelCatalog.Levels[0].Seed);
        game.AdvanceWorldForTests(36f);
        yield return EndOfFrame;
        levelScreenshotCaptured = CaptureScreenshot("01-level-lives.png");

        game.StartLevelForTests(2, RunnerLevelCatalog.Levels[1].Seed);
        game.AdvanceWorldForTests(RunnerLevelCatalog.Levels[1].CheckpointDistance(1) + 12f);
        game.TakeHitForTests();
        yield return EndOfFrame;
        checkpointScreenshotCaptured = CaptureScreenshot("02-checkpoint-recovery.png");

        game.StartLevelForTests(3, RunnerLevelCatalog.Levels[2].Seed);
        game.AdvanceWorldForTests(game.LevelTargetDistance);
        yield return EndOfFrame;
        clearScreenshotCaptured = CaptureScreenshot("03-level-clear.png");
    }

    private IEnumerator CaptureLevelClearTransition()
    {
        RunnerLevelDefinition captureLevel = RunnerLevelCatalog.Levels[CaptureLevelNumber - 1];
        game.StartLevelForTests(CaptureLevelNumber, captureLevel.Seed);
        game.AdvanceWorldForTests(captureLevel.TargetDistance - LevelClearLeadDistance);
        nextRowIndex = 0;
        actionRequestedRowIndex = -1;
        while (nextRowIndex < rows.Count && game.Distance > rows[nextRowIndex].Z + 0.9f)
        {
            nextRowIndex++;
        }

        captureStarted = true;
        while (capturedLevelClearFrameCount < LevelClearCaptureFrameCount &&
               (game.IsPlaying || game.IsCelebrating || game.IsCampaignComplete))
        {
            yield return EndOfFrame;
            CaptureFrame(levelClearFramesDirectory, capturedLevelClearFrameCount);
            capturedLevelClearFrameCount++;
        }

        captureStarted = false;
        levelClearTransitionCompleted = game.IsCampaignComplete;
    }

    private static void CaptureFrame(string directory, int frameIndex)
    {
        string framePath = Path.Combine(
            directory,
            string.Format(CultureInfo.InvariantCulture, "frame-{0:D4}.png", frameIndex));
        ScreenCapture.CaptureScreenshot(framePath);
    }

    private bool CaptureScreenshot(string fileName)
    {
        ScreenCapture.CaptureScreenshot(Path.Combine(screenshotsDirectory, fileName));
        return true;
    }

    private void WriteManifest(string error)
    {
        string manifest = string.Format(
            CultureInfo.InvariantCulture,
            "level={0}\nframeRate={1}\nframes={2}\nlevelClearFrames={3}\nlevelScreenshot={4}\ncheckpointScreenshot={5}\nclearScreenshot={6}\nerror={7}\n",
            CaptureLevelNumber,
            FrameRate,
            capturedFrameCount,
            capturedLevelClearFrameCount,
            levelScreenshotCaptured,
            checkpointScreenshotCaptured,
            clearScreenshotCaptured,
            error ?? string.Empty);
        File.WriteAllText(Path.Combine(outputDirectory, "capture-manifest.txt"), manifest);
    }
}
