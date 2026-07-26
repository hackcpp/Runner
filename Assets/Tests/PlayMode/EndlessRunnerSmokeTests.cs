using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class EndlessRunnerSmokeTests
{
    [TearDown]
    public void RestoreGlobalPauseState()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    [UnityTest]
    public IEnumerator RuntimeBootstrapCreatesPlayableWorld()
    {
        yield return null;
        yield return null;

        EndlessRunnerGame game = Object.FindObjectOfType<EndlessRunnerGame>();
        Assert.NotNull(game, "Game controller should bootstrap itself.");
        Assert.NotNull(GameObject.Find("Runner"), "Runner object should exist.");
        Assert.NotNull(Camera.main, "Main camera should exist.");
        Assert.NotNull(Camera.main.GetComponent<RunnerCameraRig>(), "Camera feedback should use the dedicated rig.");
        Assert.NotNull(GameObject.Find("Generated Runner World"), "Generated world root should exist.");
        Assert.NotNull(GameObject.Find("Runner HUD Canvas"), "Responsive Canvas HUD should exist.");
        Assert.NotNull(Object.FindObjectOfType<RunnerHud>(), "HUD should use the dedicated presenter component.");
        Assert.NotNull(GameObject.Find("Rooftop Slab 0"), "The playable surface should read as a rooftop slab.");
        Assert.NotNull(GameObject.Find("Left Parapet"), "Rooftops should have visible parapet walls.");
        Assert.NotNull(GameObject.Find("Rooftop HVAC Unit"), "The playable roof should include utility silhouettes.");
        Assert.NotNull(GameObject.Find("Background Water Tank"), "Background roofs should include recognizable equipment.");
        Assert.NotNull(GameObject.Find("Lit Window Band"), "The skyline should include warm facade lights.");
        Assert.IsNull(GameObject.Find("Lane Dash"), "Road-style dashed lane markings should not remain.");
        Assert.IsTrue(RenderSettings.fog, "Layered skyline depth should retain distance fog.");
        Assert.NotNull(RenderSettings.skybox, "The rooftop scene should configure its dusk skybox.");
        Assert.GreaterOrEqual(Object.FindObjectsOfType<MeshRenderer>().Length, 20, "Runtime world should contain visible geometry.");
        Assert.NotNull(GameObject.Find("Runner").GetComponent<RunnerMotor>(), "Runner should use the dedicated motor component.");
        Assert.NotNull(GameObject.Find("Runner").GetComponent<RunnerVisualRig>(), "Runner should use the dedicated visual rig.");
        Assert.NotNull(GameObject.Find("Runner").GetComponent<RunnerMotionEffects>(), "Runner should use bounded motion effects.");
        Assert.NotNull(GameObject.Find("Runner Head"), "The runner silhouette should contain a head.");
        Assert.NotNull(GameObject.Find("Runner Torso"), "The runner silhouette should contain a torso.");
        Assert.NotNull(GameObject.Find("Runner Left Arm"), "The runner silhouette should contain a left arm.");
        Assert.NotNull(GameObject.Find("Runner Right Arm"), "The runner silhouette should contain a right arm.");
        Assert.NotNull(GameObject.Find("Runner Left Leg"), "The runner silhouette should contain a left leg.");
        Assert.NotNull(GameObject.Find("Runner Right Leg"), "The runner silhouette should contain a right leg.");
        Assert.NotNull(GameObject.Find("Runner Visor"), "The runner should expose a clear forward-facing accent.");
        Assert.NotNull(GameObject.Find("Runner Back Pack"), "The runner should expose a distinct rear silhouette.");

        Assert.NotNull(game.Music, "The game should retain its music controller.");
        AudioSource[] audioSources = game.GetComponents<AudioSource>();
        Assert.AreEqual(4, audioSources.Length, "Three music layers and one-shot sound effects should use fixed sources.");

        int loopingSourceCount = 0;
        int oneShotSourceCount = 0;
        for (int index = 0; index < audioSources.Length; index++)
        {
            if (audioSources[index].loop)
            {
                loopingSourceCount++;
            }
            else
            {
                oneShotSourceCount++;
            }
        }

        Assert.AreEqual(ProceduralRunnerMusic.LayerCount, loopingSourceCount);
        Assert.AreEqual(1, oneShotSourceCount, "Action sounds should retain an independent one-shot source.");
        for (int layerIndex = 0; layerIndex < ProceduralRunnerMusic.LayerCount; layerIndex++)
        {
            Assert.NotNull(game.Music.GetClip(layerIndex));
            Assert.IsTrue(game.Music.GetSource(layerIndex).loop);
            Assert.Greater(game.Music.GetClip(layerIndex).length, 30f);
        }
    }

    [UnityTest]
    public IEnumerator LayeredMusicMeetsQualityAndReuseTargets()
    {
        yield return null;
        yield return null;

        EndlessRunnerGame game = Object.FindObjectOfType<EndlessRunnerGame>();
        ProceduralRunnerMusic music = game.Music;
        Assert.NotNull(music);
        Assert.AreEqual(ProceduralRunnerMusic.LayerCount, music.SourceCount);
        Assert.AreEqual(ProceduralRunnerMusic.LayerCount, music.ClipCount);

        AudioSource[] originalSources = new AudioSource[ProceduralRunnerMusic.LayerCount];
        AudioClip[] originalClips = new AudioClip[ProceduralRunnerMusic.LayerCount];
        int expectedSampleCount = music.GetMetrics(0).SampleCount;
        float expectedDuration = music.GetMetrics(0).Duration;

        for (int layerIndex = 0; layerIndex < ProceduralRunnerMusic.LayerCount; layerIndex++)
        {
            originalSources[layerIndex] = music.GetSource(layerIndex);
            originalClips[layerIndex] = music.GetClip(layerIndex);
            RunnerMusicClipMetrics layerMetrics = music.GetMetrics(layerIndex);

            Assert.NotNull(originalSources[layerIndex]);
            Assert.NotNull(originalClips[layerIndex]);
            Assert.AreEqual(expectedSampleCount, layerMetrics.SampleCount);
            Assert.AreEqual(expectedDuration, layerMetrics.Duration, 0.001f);
            Assert.AreEqual(ProceduralRunnerMusic.TotalBeats, 64);
            Assert.GreaterOrEqual(layerMetrics.Duration, 30f);
            Assert.LessOrEqual(layerMetrics.Peak, 0.92f);
            Assert.LessOrEqual(Mathf.Abs(layerMetrics.DcOffset), 0.001f);
            Assert.LessOrEqual(layerMetrics.LoopSeamDelta, 0.02f);
            Assert.IsTrue(layerMetrics.AllSamplesFinite);
        }

        music.SetState(RunnerMusicState.Menu);
        music.Tick(ProceduralRunnerMusic.CrossfadeDuration);
        Assert.AreEqual(RunnerMusicState.Menu, music.State);
        Assert.AreEqual(1f, music.GetCurrentWeight(0), 0.001f);
        Assert.AreEqual(0f, music.GetCurrentWeight(1), 0.001f);
        Assert.AreEqual(0f, music.GetCurrentWeight(2), 0.001f);

        music.SetState(RunnerMusicState.RunningLow);
        music.Tick(ProceduralRunnerMusic.CrossfadeDuration);
        Assert.AreEqual(RunnerMusicState.RunningLow, music.State);
        Assert.AreEqual(0.72f, music.GetCurrentWeight(1), 0.001f);
        Assert.AreEqual(0.18f, music.GetCurrentWeight(2), 0.001f);

        music.SetState(RunnerMusicState.RunningHigh);
        music.Tick(ProceduralRunnerMusic.CrossfadeDuration);
        Assert.AreEqual(RunnerMusicState.RunningHigh, music.State);
        Assert.AreEqual(1f, music.GetCurrentWeight(1), 0.001f);
        Assert.AreEqual(0.9f, music.GetCurrentWeight(2), 0.001f);

        music.TriggerDuck();
        music.Tick(ProceduralRunnerMusic.DuckAttackDuration);
        Assert.AreEqual(ProceduralRunnerMusic.DuckGain, music.CurrentDuckGain, 0.001f);
        music.Tick(0.06f);
        music.Tick(ProceduralRunnerMusic.DuckReleaseDuration);
        Assert.AreEqual(1f, music.CurrentDuckGain, 0.001f);

        for (int runIndex = 0; runIndex < 3; runIndex++)
        {
            game.StartRunForTests(9000 + runIndex);
        }

        float remainingDistance = RunnerRunTuning.AdvancedTierDistance + 1f;
        while (remainingDistance > 0f)
        {
            float stepDistance = Mathf.Min(14f, remainingDistance);
            game.AdvanceWorldForTests(stepDistance);
            remainingDistance -= stepDistance;
        }

        Assert.AreEqual(RunnerMusicState.RunningHigh, music.State);
        game.PauseForTests();
        game.ResumeForTests();

        Assert.AreEqual(4, game.GetComponents<AudioSource>().Length);
        for (int layerIndex = 0; layerIndex < ProceduralRunnerMusic.LayerCount; layerIndex++)
        {
            Assert.AreSame(originalSources[layerIndex], music.GetSource(layerIndex));
            Assert.AreSame(originalClips[layerIndex], music.GetClip(layerIndex));
        }
    }

    [UnityTest]
    public IEnumerator StartingRunCreatesGuaranteedTutorialObstacles()
    {
        yield return null;
        yield return null;

        EndlessRunnerGame game = Object.FindObjectOfType<EndlessRunnerGame>();
        game.StartRunForTests(12345);
        yield return null;

        Assert.NotNull(GameObject.Find("Blocker Obstacle"), "Tutorial should create a lane-change blocker.");
        Assert.NotNull(GameObject.Find("Hurdle Obstacle"), "Tutorial should create a jump hurdle.");
        Assert.NotNull(GameObject.Find("Overhead Obstacle"), "Tutorial should create a slide gate.");
        Assert.AreEqual(0, game.ActionClearCount, "A fresh run should not contain action rewards.");
        Assert.AreEqual(0, game.CurrentScore, "A fresh run should start at zero before advancing a full meter.");
    }

    [UnityTest]
    public IEnumerator GeneratedVisualGeometryDoesNotUsePhysicsColliders()
    {
        yield return null;
        yield return null;

        EndlessRunnerGame game = Object.FindObjectOfType<EndlessRunnerGame>();
        game.StartRunForTests(13579);
        yield return null;
        yield return null;

        Assert.AreEqual(
            0,
            Object.FindObjectsOfType<Collider>().Length,
            "Coordinate-based collision should not leave generated visual colliders in PhysX.");
    }

    [UnityTest]
    public IEnumerator RunnerVisualsExposeDistinctPosesAndBoundedEffects()
    {
        GameObject runner = new GameObject("Visual Rig Test Runner");
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(runner.transform);
        GameObject body = new GameObject("Logic Body");
        body.transform.SetParent(visual.transform);
        GameObject shadow = new GameObject("Shadow");
        shadow.transform.SetParent(runner.transform);

        RunnerMotor motor = runner.AddComponent<RunnerMotor>();
        motor.Configure(visual.transform, body.transform, shadow.transform, RunnerMotor.DefaultLaneWidth);

        Shader shader = Shader.Find("Standard");
        Assert.NotNull(shader);
        Material primary = new Material(shader);
        Material accent = new Material(shader);
        Material dark = new Material(shader);

        RunnerVisualRig rig = runner.AddComponent<RunnerVisualRig>();
        rig.Configure(motor, visual.transform, primary, accent, dark);
        RunnerMotionEffects effects = runner.AddComponent<RunnerMotionEffects>();
        effects.Configure(motor, dark, accent, primary);

        Collider[] visualColliders = visual.GetComponentsInChildren<Collider>(true);
        for (int index = 0; index < visualColliders.Length; index++)
        {
            Assert.IsFalse(visualColliders[index].enabled, "Visual body parts must never enter PhysX.");
        }

        motor.Tick(0f, 0.02f);
        rig.Tick(16f, true, 0.13f);
        Assert.AreEqual(RunnerActionState.Grounded, rig.PoseState);
        Assert.IsTrue(rig.FootstepThisFrame, "Grounded running should produce a cadence event.");
        effects.EmitFootstep(rig.FootstepSideThisFrame);
        Assert.AreEqual(1, effects.FootstepEmissionCount);

        int footstepCount = rig.TotalFootstepEvents;
        rig.Tick(0f, false, 0.5f);
        Assert.IsFalse(rig.FootstepThisFrame, "Idle or paused visuals must not produce footsteps.");
        Assert.AreEqual(footstepCount, rig.TotalFootstepEvents);

        motor.RequestJump();
        motor.Tick(0f, 0.02f);
        rig.Tick(16f, true, 0.02f);
        effects.Tick(16f, true, 0.02f);
        Assert.AreEqual(RunnerActionState.Airborne, rig.PoseState);
        Assert.AreEqual(RunnerMotor.StandingBodyHeight, motor.BodyHeight, 0.001f);

        bool landed = false;
        for (int step = 0; step < 100 && !landed; step++)
        {
            motor.Tick(0f, 0.02f);
            rig.Tick(16f, true, 0.02f);
            effects.Tick(16f, true, 0.02f);
            landed = motor.LandedThisFrame;
        }

        Assert.IsTrue(landed);
        Assert.Greater(rig.LandingPulse, 0f, "Landing should create a short visual rebound.");
        Assert.AreEqual(1, effects.LandingEmissionCount, "Landing dust should emit once.");
        Assert.AreEqual(RunnerMotor.StandingBodyHeight, motor.BodyHeight, 0.001f);

        motor.RequestSlide();
        motor.Tick(0f, 0.02f);
        rig.Tick(16f, true, 0.02f);
        effects.Tick(16f, true, 0.02f);
        Assert.AreEqual(RunnerActionState.Sliding, rig.PoseState);
        Assert.AreEqual(RunnerMotor.SlidingBodyHeight, motor.BodyHeight, 0.001f);
        Assert.Greater(effects.SlideEmissionCount, 0, "Sliding should emit reusable spark particles.");
        Assert.Greater(effects.TrailEmissionCount, 0, "A moving runner should emit a lightweight speed trail.");
        Assert.Greater(effects.DustParticles.particleCount, 0);
        Assert.Greater(effects.SparkParticles.particleCount, 0);

        effects.ResetForRun();
        Assert.AreEqual(0, effects.FootstepEmissionCount);
        Assert.AreEqual(0, effects.LandingEmissionCount);
        Assert.AreEqual(0, effects.SlideEmissionCount);
        Assert.AreEqual(0, effects.TrailEmissionCount);

        Object.Destroy(runner);
        Object.Destroy(primary);
        Object.Destroy(accent);
        Object.Destroy(dark);
        yield return null;
    }

    [UnityTest]
    public IEnumerator RunnerMotorJumpsLandsAndSlides()
    {
        GameObject runner = new GameObject("Motor Test Runner");
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(runner.transform);
        GameObject body = new GameObject("Body");
        body.transform.SetParent(visual.transform);
        GameObject shadow = new GameObject("Shadow");
        shadow.transform.SetParent(runner.transform);

        RunnerMotor motor = runner.AddComponent<RunnerMotor>();
        motor.Configure(visual.transform, body.transform, shadow.transform, 2.2f);
        motor.RequestJump();
        motor.Tick(0f, 0.02f);

        Assert.AreEqual(RunnerActionState.Airborne, motor.State);
        Assert.IsTrue(motor.JumpStartedThisFrame);
        Assert.Greater(motor.FeetHeight, 0f);

        for (int step = 0; step < 100 && motor.State == RunnerActionState.Airborne; step++)
        {
            motor.Tick(0f, 0.02f);
        }

        Assert.AreEqual(RunnerActionState.Grounded, motor.State);
        Assert.AreEqual(0f, motor.FeetHeight, 0.001f);

        motor.RequestSlide();
        motor.Tick(0f, 0.02f);
        Assert.AreEqual(RunnerActionState.Sliding, motor.State);
        Assert.AreEqual(RunnerMotor.SlidingBodyHeight, motor.BodyHeight, 0.001f);

        for (int step = 0; step < 40; step++)
        {
            motor.Tick(0f, 0.02f);
        }

        Assert.AreEqual(RunnerActionState.Grounded, motor.State);
        Assert.AreEqual(RunnerMotor.StandingBodyHeight, motor.BodyHeight, 0.001f);

        Object.Destroy(runner);
        yield return null;
    }

    [UnityTest]
    public IEnumerator RunnerMotorBuffersLandingJumpAndQueuesLaneChanges()
    {
        GameObject runner = new GameObject("Buffered Motor Test Runner");
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(runner.transform);
        GameObject body = new GameObject("Body");
        body.transform.SetParent(visual.transform);
        GameObject shadow = new GameObject("Shadow");
        shadow.transform.SetParent(runner.transform);

        RunnerMotor motor = runner.AddComponent<RunnerMotor>();
        motor.Configure(visual.transform, body.transform, shadow.transform, RunnerMotor.DefaultLaneWidth);

        motor.RequestLaneChange(-1);
        motor.Tick(0f, 0.02f);
        motor.RequestLaneChange(1);

        for (int step = 0; step < 24; step++)
        {
            motor.Tick(0f, 0.02f);
        }

        Assert.AreEqual(1, motor.Lane, "Queued opposite input should return the runner to the center lane.");
        Assert.AreEqual(0f, runner.transform.position.x, 0.001f);

        motor.RequestJump();
        motor.Tick(0f, 0.02f);
        for (int step = 0; step < 29; step++)
        {
            motor.Tick(0f, 0.02f);
        }

        motor.RequestJump();
        bool landed = false;
        bool bufferedJumpStarted = false;
        for (int step = 0; step < 20; step++)
        {
            motor.Tick(0f, 0.02f);
            landed |= motor.LandedThisFrame;
            bufferedJumpStarted |= landed && motor.JumpStartedThisFrame;
        }

        Assert.IsTrue(landed, "The first jump should land.");
        Assert.IsTrue(bufferedJumpStarted, "A jump requested shortly before landing should start after landing.");

        Object.Destroy(runner);
        yield return null;
    }

    [UnityTest]
    public IEnumerator RunnerMotorBuffersSlideBeforeLanding()
    {
        GameObject runner = new GameObject("Slide Buffer Test Runner");
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(runner.transform);
        GameObject body = new GameObject("Body");
        body.transform.SetParent(visual.transform);
        GameObject shadow = new GameObject("Shadow");
        shadow.transform.SetParent(runner.transform);

        RunnerMotor motor = runner.AddComponent<RunnerMotor>();
        motor.Configure(visual.transform, body.transform, shadow.transform, RunnerMotor.DefaultLaneWidth);
        motor.RequestJump();
        motor.Tick(0f, 0.02f);

        float previousHeight = motor.FeetHeight;
        bool descending = false;
        bool slideRequested = false;
        bool slideStarted = false;

        for (int step = 0; step < 100 && !slideStarted; step++)
        {
            motor.Tick(0f, 0.02f);
            slideStarted |= motor.SlideStartedThisFrame;

            if (motor.State == RunnerActionState.Airborne)
            {
                descending |= motor.FeetHeight < previousHeight;
                if (descending && motor.FeetHeight < 0.45f && !slideRequested)
                {
                    motor.RequestSlide();
                    slideRequested = true;
                }
            }

            previousHeight = motor.FeetHeight;
        }

        Assert.IsTrue(slideRequested, "The test should request a slide shortly before landing.");
        Assert.IsTrue(slideStarted, "A buffered slide should begin as soon as the runner lands.");
        Assert.AreEqual(RunnerActionState.Sliding, motor.State);
        Assert.AreEqual(RunnerMotor.SlidingBodyHeight, motor.BodyHeight, 0.001f);

        Object.Destroy(runner);
        yield return null;
    }

    [UnityTest]
    public IEnumerator PausingFreezesAndResumesTheActiveRun()
    {
        yield return null;
        yield return null;

        EndlessRunnerGame game = Object.FindObjectOfType<EndlessRunnerGame>();
        game.StartRunForTests(424242);
        yield return null;

        game.PauseForTests();
        float pausedDistance = game.Distance;
        Assert.IsTrue(game.IsPaused);
        Assert.AreEqual(0f, Time.timeScale, 0.001f);
        Assert.IsTrue(AudioListener.pause);

        yield return null;
        yield return null;
        yield return null;
        Assert.AreEqual(pausedDistance, game.Distance, 0.001f);

        game.ResumeForTests();
        Assert.IsFalse(game.IsPaused);
        Assert.AreEqual(1f, Time.timeScale, 0.001f);
        Assert.IsFalse(AudioListener.pause);
        yield return null;
        Assert.Greater(game.Distance, pausedDistance);
    }

    [Test]
    public void ObstacleRulesRequireTheExpectedAction()
    {
        Assert.IsTrue(RunnerObstacleRules.CausesCollision(
            RunnerObstacleKind.Blocker,
            RunnerActionState.Airborne,
            2f));

        Assert.IsTrue(RunnerObstacleRules.CausesCollision(
            RunnerObstacleKind.Hurdle,
            RunnerActionState.Grounded,
            0f));
        Assert.IsFalse(RunnerObstacleRules.CausesCollision(
            RunnerObstacleKind.Hurdle,
            RunnerActionState.Airborne,
            RunnerObstacleRules.HurdleClearanceHeight + 0.1f));

        Assert.IsTrue(RunnerObstacleRules.CausesCollision(
            RunnerObstacleKind.Overhead,
            RunnerActionState.Grounded,
            0f));
        Assert.IsFalse(RunnerObstacleRules.CausesCollision(
            RunnerObstacleKind.Overhead,
            RunnerActionState.Sliding,
            0f));
    }

    [Test]
    public void ActionRewardCanOnlyBeGrantedOnce()
    {
        RunnerActionReward reward = new RunnerActionReward();

        Assert.IsFalse(reward.TryGrant(false, true, RunnerObstacleKind.Hurdle));
        Assert.IsTrue(reward.TryGrant(true, true, RunnerObstacleKind.Hurdle));
        Assert.IsFalse(reward.TryGrant(true, true, RunnerObstacleKind.Hurdle));
        Assert.AreEqual(347, RunnerScore.Calculate(147.9f, 2));
    }

    [Test]
    public void ComboAwardsIncreasingBonusAndExpiresCleanly()
    {
        RunnerComboTracker combo = new RunnerComboTracker();

        Assert.AreEqual(100, combo.RegisterActionClear());
        Assert.AreEqual(200, combo.RegisterActionClear());
        Assert.AreEqual(2, combo.Multiplier);
        Assert.AreEqual(300, combo.TotalBonusScore);
        Assert.AreEqual(347, RunnerScore.CalculateWithBonus(47.9f, combo.TotalBonusScore));

        combo.Tick(RunnerComboTracker.ComboWindow + 0.01f);
        Assert.AreEqual(0, combo.ComboCount);
        Assert.AreEqual(1, combo.Multiplier);
        Assert.AreEqual(100, combo.RegisterActionClear());
        Assert.AreEqual(400, combo.TotalBonusScore);
        Assert.AreEqual(2, combo.HighestCombo);
    }

    [Test]
    public void PatternCatalogIsValidAndContainsEnoughVariety()
    {
        Assert.GreaterOrEqual(RunnerPatternCatalog.Patterns.Count, 8);

        for (int index = 0; index < RunnerPatternCatalog.Patterns.Count; index++)
        {
            RunnerPatternDefinition pattern = RunnerPatternCatalog.Patterns[index];
            Assert.IsTrue(RunnerPatternCatalog.IsPatternValid(pattern), "Invalid runner pattern: " + pattern.Id);

            IReadOnlyList<int> lanePath;
            Assert.IsTrue(RunnerPatternCatalog.TryFindSurvivalPath(pattern, out lanePath));
            Assert.Greater(lanePath.Count, 0, "A valid pattern should expose a concrete lane path: " + pattern.Id);
        }
    }

    [Test]
    public void PatternSolverRejectsUnreachableLaneAndActionTransitions()
    {
        RunnerPatternDefinition impossibleWeave = new RunnerPatternDefinition(
            "impossible-weave",
            0,
            new RunnerPatternElement(RunnerObstacleKind.Blocker, 0b110, 0f),
            new RunnerPatternElement(RunnerObstacleKind.Blocker, 0b011, 1f));
        RunnerPatternDefinition impossibleActionSwitch = new RunnerPatternDefinition(
            "impossible-action-switch",
            0,
            new RunnerPatternElement(RunnerObstacleKind.Hurdle, 0b111, 0f),
            new RunnerPatternElement(RunnerObstacleKind.Overhead, 0b111, 10f));

        Assert.IsFalse(RunnerPatternCatalog.IsPatternValid(impossibleWeave));
        Assert.IsFalse(RunnerPatternCatalog.IsPatternValid(impossibleActionSwitch));
    }

    [Test]
    public void RunSolverRejectsAnImpossibleCrossPatternLaneTransition()
    {
        RunnerObstacleRow[] rows =
        {
            new RunnerObstacleRow(
                10f,
                null,
                RunnerObstacleKind.Blocker,
                RunnerObstacleKind.Blocker),
            new RunnerObstacleRow(
                11f,
                RunnerObstacleKind.Blocker,
                RunnerObstacleKind.Blocker,
                null)
        };

        IReadOnlyList<int> lanePath;
        float failureZ;
        Assert.IsFalse(RunnerSurvivalSolver.TryFindPath(
            rows,
            1,
            0f,
            out lanePath,
            out failureZ));
        Assert.AreEqual(11f, failureZ, 0.001f);
        Assert.AreEqual(0, lanePath.Count);
    }

    [Test]
    public void CompleteRunSimulationIsDeterministicAndCrossPatternSafe()
    {
        RunnerRunSimulationResult first = RunnerRunSimulator.Simulate(271828, 1200f);
        RunnerRunSimulationResult second = RunnerRunSimulator.Simulate(271828, 1200f);
        RunnerRunSimulationResult different = RunnerRunSimulator.Simulate(271829, 1200f);

        Assert.IsTrue(
            first.IsSurvivable,
            "The generated run should expose a complete survival path. Failure Z: " +
            first.FailureZ + " Sequence: " + first.SequenceFingerprint);
        Assert.AreEqual(first.RowCount, first.LanePath.Count);
        Assert.Greater(first.PatternCount, 40);
        Assert.Greater(first.PatternCountForTier(0), 0);
        Assert.Greater(first.PatternCountForTier(1), 0);
        Assert.Greater(first.PatternCountForTier(2), 0);
        Assert.Greater(first.ObstacleCount(RunnerObstacleKind.Blocker), 0);
        Assert.Greater(first.ObstacleCount(RunnerObstacleKind.Hurdle), 0);
        Assert.Greater(first.ObstacleCount(RunnerObstacleKind.Overhead), 0);
        Assert.GreaterOrEqual(
            first.MinimumActionInterval,
            RunnerPatternCatalog.MinimumActionTime - 0.001f);
        Assert.GreaterOrEqual(first.MinimumLaneChangeTimeMargin, -0.001f);

        Assert.AreEqual(first.SequenceFingerprint, second.SequenceFingerprint);
        Assert.AreNotEqual(first.SequenceFingerprint, different.SequenceFingerprint);
    }

    [Test]
    public void FiveThousandGeneratedRunsRemainSurvivable()
    {
        RunnerRunSimulationBatchResult batch = RunnerRunSimulator.SimulateBatch(
            20260725,
            5000,
            1200f);

        Assert.AreEqual(5000, batch.SeedCount);
        Assert.AreEqual(
            0,
            batch.FailedRunCount,
            "Generated run failed at seed " + batch.FirstFailedSeed);
        Assert.AreEqual(RunnerPatternCatalog.Patterns.Count, batch.UniquePatternCount);
        Assert.Greater(batch.TotalPatternCount, 200000);
        Assert.Greater(batch.TotalRowCount, batch.TotalPatternCount);
        Assert.Greater(batch.PatternCountForTier(0), 0);
        Assert.Greater(batch.PatternCountForTier(1), 0);
        Assert.Greater(batch.PatternCountForTier(2), 0);
        Assert.Greater(batch.ObstacleCount(RunnerObstacleKind.Blocker), 0);
        Assert.Greater(batch.ObstacleCount(RunnerObstacleKind.Hurdle), 0);
        Assert.Greater(batch.ObstacleCount(RunnerObstacleKind.Overhead), 0);
        Assert.GreaterOrEqual(
            batch.MinimumActionInterval,
            RunnerPatternCatalog.MinimumActionTime - 0.001f);
        Assert.GreaterOrEqual(batch.MinimumLaneChangeTimeMargin, -0.001f);
    }

    [Test]
    public void FixedSeedProducesTheSameNonRepeatingSequence()
    {
        RunnerPatternSequence first = new RunnerPatternSequence(314159);
        RunnerPatternSequence second = new RunnerPatternSequence(314159);
        List<string> generatedIds = new List<string>();

        for (int index = 0; index < 30; index++)
        {
            int tier = index < 10 ? 0 : index < 20 ? 1 : 2;
            RunnerPatternDefinition firstPattern = first.Next(tier);
            RunnerPatternDefinition secondPattern = second.Next(tier);
            Assert.AreEqual(firstPattern.Id, secondPattern.Id);

            if (generatedIds.Count > 0)
            {
                Assert.AreNotEqual(generatedIds[generatedIds.Count - 1], firstPattern.Id);
            }

            generatedIds.Add(firstPattern.Id);
            Assert.AreEqual(first.NextSpacing(), second.NextSpacing(), 0.0001f);
        }
    }

    [UnityTest]
    public IEnumerator WorldPoolRemainsBoundedAcrossTenMinuteSimulation()
    {
        yield return null;
        yield return null;

        EndlessRunnerGame game = Object.FindObjectOfType<EndlessRunnerGame>();
        game.StartRunForTests(20260725);
        yield return null;

        AudioSource[] initialAudioSources = game.GetComponents<AudioSource>();
        AudioClip[] initialMusicClips = new AudioClip[ProceduralRunnerMusic.LayerCount];
        for (int layerIndex = 0; layerIndex < initialMusicClips.Length; layerIndex++)
        {
            initialMusicClips[layerIndex] = game.Music.GetClip(layerIndex);
        }

        int simulationSteps = Mathf.CeilToInt(
            RunnerPatternCatalog.MaximumRunnerSpeed * 600f / 14f);
        int warmCreatedCubeCount = 0;

        for (int step = 0; step < simulationSteps; step++)
        {
            game.AdvanceWorldForTests(14f);
            if (step == 200)
            {
                warmCreatedCubeCount = game.TotalCreatedCubeCount;
            }
        }

        Assert.Greater(game.Distance, 9800f);
        Assert.Greater(game.PooledCubeCount, 0, "Long runs should recycle geometry behind the player.");
        Assert.Greater(game.PooledObstacleRootCount, 0, "Obstacle roots should be recycled.");
        Assert.Less(game.ActiveWorldCubeCount, 380, "Active geometry should stay bounded by the look-ahead window.");
        Assert.Less(game.ActiveObstacleCount, 60, "Active obstacles should stay bounded by the look-ahead window.");
        Assert.Less(game.TotalCreatedCubeCount, 460, "Pooling should cap total runtime cube creation.");
        Assert.LessOrEqual(
            game.TotalCreatedCubeCount - warmCreatedCubeCount,
            50,
            "The pool should reach a stable capacity early in a long run.");
        Assert.AreEqual(initialAudioSources.Length, game.GetComponents<AudioSource>().Length);
        Assert.AreEqual(4, initialAudioSources.Length);
        for (int layerIndex = 0; layerIndex < initialMusicClips.Length; layerIndex++)
        {
            Assert.AreSame(initialMusicClips[layerIndex], game.Music.GetClip(layerIndex));
        }
    }
}
