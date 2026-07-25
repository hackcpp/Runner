using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class EndlessRunnerSmokeTests
{
    [UnityTest]
    public IEnumerator RuntimeBootstrapCreatesPlayableWorld()
    {
        yield return null;
        yield return null;

        EndlessRunnerGame game = Object.FindObjectOfType<EndlessRunnerGame>();
        Assert.NotNull(game, "Game controller should bootstrap itself.");
        Assert.NotNull(GameObject.Find("Runner"), "Runner object should exist.");
        Assert.NotNull(Camera.main, "Main camera should exist.");
        Assert.NotNull(GameObject.Find("Generated Runner World"), "Generated world root should exist.");
        Assert.GreaterOrEqual(Object.FindObjectsOfType<MeshRenderer>().Length, 20, "Runtime world should contain visible geometry.");
        Assert.NotNull(GameObject.Find("Runner").GetComponent<RunnerMotor>(), "Runner should use the dedicated motor component.");

        AudioSource[] audioSources = game.GetComponents<AudioSource>();
        Assert.GreaterOrEqual(audioSources.Length, 2, "Music and one-shot sound effects should use separate sources.");

        AudioSource music = null;
        for (int index = 0; index < audioSources.Length; index++)
        {
            if (audioSources[index].loop)
            {
                music = audioSources[index];
                break;
            }
        }

        Assert.NotNull(music, "Background music source should exist.");
        Assert.NotNull(music.clip, "Background music clip should exist.");
        Assert.IsTrue(music.loop, "Background music should loop.");
        Assert.Greater(music.clip.length, 10f, "Background music loop should contain a complete musical phrase.");
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
    public void PatternCatalogIsValidAndContainsEnoughVariety()
    {
        Assert.GreaterOrEqual(RunnerPatternCatalog.Patterns.Count, 8);

        for (int index = 0; index < RunnerPatternCatalog.Patterns.Count; index++)
        {
            RunnerPatternDefinition pattern = RunnerPatternCatalog.Patterns[index];
            Assert.IsTrue(RunnerPatternCatalog.IsPatternValid(pattern), "Invalid runner pattern: " + pattern.Id);
        }
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
            Assert.AreEqual(first.NextSpacing(12f), second.NextSpacing(12f), 0.0001f);
        }
    }
}
