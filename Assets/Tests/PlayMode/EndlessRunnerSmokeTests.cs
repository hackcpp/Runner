using System.Collections;
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

        Assert.NotNull(Object.FindObjectOfType<EndlessRunnerGame>(), "Game controller should bootstrap itself.");
        Assert.NotNull(GameObject.Find("Runner"), "Runner object should exist.");
        Assert.NotNull(Camera.main, "Main camera should exist.");
        Assert.NotNull(GameObject.Find("Generated Runner World"), "Generated world root should exist.");
        Assert.GreaterOrEqual(Object.FindObjectsOfType<MeshRenderer>().Length, 20, "Runtime world should contain visible geometry.");
    }
}
