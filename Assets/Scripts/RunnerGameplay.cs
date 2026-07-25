using System;
using System.Collections.Generic;

public enum RunnerActionState
{
    Grounded,
    Airborne,
    Sliding
}

public enum RunnerObstacleKind
{
    Blocker,
    Hurdle,
    Overhead
}

public enum RunnerRequiredAction
{
    None,
    Jump,
    Slide
}

public static class RunnerObstacleRules
{
    public const float HurdleClearanceHeight = 0.82f;

    public static bool CausesCollision(RunnerObstacleKind kind, RunnerActionState state, float feetHeight)
    {
        if (kind == RunnerObstacleKind.Blocker)
        {
            return true;
        }

        if (kind == RunnerObstacleKind.Hurdle)
        {
            return feetHeight < HurdleClearanceHeight;
        }

        return state != RunnerActionState.Sliding;
    }

    public static bool IsActionClear(RunnerObstacleKind kind, RunnerActionState state, float feetHeight)
    {
        return kind != RunnerObstacleKind.Blocker && !CausesCollision(kind, state, feetHeight);
    }
}

public sealed class RunnerActionReward
{
    public bool IsGranted { get; private set; }

    public bool TryGrant(bool isSameLane, bool actionCleared, RunnerObstacleKind kind)
    {
        if (IsGranted || !isSameLane || !actionCleared || kind == RunnerObstacleKind.Blocker)
        {
            return false;
        }

        IsGranted = true;
        return true;
    }
}

public static class RunnerScore
{
    public const int ActionClearPoints = 100;

    public static int Calculate(float distance, int actionClearCount)
    {
        return Math.Max(0, (int)Math.Floor(distance)) + Math.Max(0, actionClearCount) * ActionClearPoints;
    }
}

public readonly struct RunnerPatternElement
{
    public RunnerPatternElement(RunnerObstacleKind kind, int laneMask, float zOffset)
    {
        Kind = kind;
        LaneMask = laneMask;
        ZOffset = zOffset;
    }

    public RunnerObstacleKind Kind { get; }
    public int LaneMask { get; }
    public float ZOffset { get; }
}

public sealed class RunnerPatternDefinition
{
    public RunnerPatternDefinition(string id, int minimumTier, params RunnerPatternElement[] elements)
    {
        Id = id;
        MinimumTier = minimumTier;
        Elements = elements;
    }

    public string Id { get; }
    public int MinimumTier { get; }
    public IReadOnlyList<RunnerPatternElement> Elements { get; }

    public float Length
    {
        get
        {
            float length = 0f;
            for (int index = 0; index < Elements.Count; index++)
            {
                length = Math.Max(length, Elements[index].ZOffset);
            }

            return length;
        }
    }
}

public sealed class RunnerPatternSequence
{
    private readonly Random random;
    private string previousPatternId;

    public RunnerPatternSequence(int seed)
    {
        random = new Random(seed);
    }

    public RunnerPatternDefinition Next(int tier)
    {
        List<RunnerPatternDefinition> candidates = new List<RunnerPatternDefinition>();
        IReadOnlyList<RunnerPatternDefinition> patterns = RunnerPatternCatalog.Patterns;

        for (int index = 0; index < patterns.Count; index++)
        {
            RunnerPatternDefinition pattern = patterns[index];
            if (pattern.MinimumTier <= tier && pattern.Id != previousPatternId)
            {
                candidates.Add(pattern);
            }
        }

        RunnerPatternDefinition selected = candidates[random.Next(candidates.Count)];
        previousPatternId = selected.Id;
        return selected;
    }

    public float NextSpacing(float currentSpeed)
    {
        return Math.Max(13f, currentSpeed * RunnerPatternCatalog.MinimumActionTime) + (float)random.NextDouble() * 3f;
    }
}

public static class RunnerPatternCatalog
{
    public const float MaximumRunnerSpeed = 16.5f;
    public const float MinimumActionTime = 0.9f;
    public const float MinimumRequiredActionSpacing = MaximumRunnerSpeed * MinimumActionTime;

    private const int LeftLane = 1 << 0;
    private const int CenterLane = 1 << 1;
    private const int RightLane = 1 << 2;
    private const int AllLanes = LeftLane | CenterLane | RightLane;

    private static readonly RunnerPatternDefinition[] PatternDefinitions =
    {
        new RunnerPatternDefinition(
            "single-center-blocker",
            0,
            Element(RunnerObstacleKind.Blocker, CenterLane, 0f)),
        new RunnerPatternDefinition(
            "leave-left-open",
            0,
            Element(RunnerObstacleKind.Blocker, CenterLane | RightLane, 0f)),
        new RunnerPatternDefinition(
            "leave-right-open",
            0,
            Element(RunnerObstacleKind.Blocker, LeftLane | CenterLane, 0f)),
        new RunnerPatternDefinition(
            "jump-all",
            0,
            Element(RunnerObstacleKind.Hurdle, AllLanes, 0f)),
        new RunnerPatternDefinition(
            "slide-all",
            0,
            Element(RunnerObstacleKind.Overhead, AllLanes, 0f)),
        new RunnerPatternDefinition(
            "left-right-weave",
            1,
            Element(RunnerObstacleKind.Blocker, CenterLane | RightLane, 0f),
            Element(RunnerObstacleKind.Blocker, LeftLane | CenterLane, 10f)),
        new RunnerPatternDefinition(
            "jump-left-or-center",
            1,
            Element(RunnerObstacleKind.Hurdle, LeftLane | CenterLane, 0f),
            Element(RunnerObstacleKind.Blocker, RightLane, 0f)),
        new RunnerPatternDefinition(
            "slide-center-or-right",
            1,
            Element(RunnerObstacleKind.Blocker, LeftLane, 0f),
            Element(RunnerObstacleKind.Overhead, CenterLane | RightLane, 0f)),
        new RunnerPatternDefinition(
            "center-thread",
            1,
            Element(RunnerObstacleKind.Blocker, LeftLane | RightLane, 0f),
            Element(RunnerObstacleKind.Blocker, LeftLane | CenterLane, 10f)),
        new RunnerPatternDefinition(
            "jump-then-slide",
            2,
            Element(RunnerObstacleKind.Hurdle, AllLanes, 0f),
            Element(RunnerObstacleKind.Overhead, AllLanes, 16f)),
        new RunnerPatternDefinition(
            "slide-then-jump",
            2,
            Element(RunnerObstacleKind.Overhead, AllLanes, 0f),
            Element(RunnerObstacleKind.Hurdle, AllLanes, 16f)),
        new RunnerPatternDefinition(
            "action-switchback",
            2,
            Element(RunnerObstacleKind.Hurdle, LeftLane | CenterLane, 0f),
            Element(RunnerObstacleKind.Blocker, RightLane, 0f),
            Element(RunnerObstacleKind.Blocker, LeftLane, 16f),
            Element(RunnerObstacleKind.Overhead, CenterLane | RightLane, 16f))
    };

    public static IReadOnlyList<RunnerPatternDefinition> Patterns => PatternDefinitions;

    public static RunnerRequiredAction RequiredAction(RunnerObstacleKind kind)
    {
        if (kind == RunnerObstacleKind.Hurdle)
        {
            return RunnerRequiredAction.Jump;
        }

        if (kind == RunnerObstacleKind.Overhead)
        {
            return RunnerRequiredAction.Slide;
        }

        return RunnerRequiredAction.None;
    }

    public static bool IsPatternValid(RunnerPatternDefinition pattern)
    {
        if (pattern == null || string.IsNullOrEmpty(pattern.Id) || pattern.Elements.Count == 0)
        {
            return false;
        }

        List<float> offsets = new List<float>();
        for (int index = 0; index < pattern.Elements.Count; index++)
        {
            RunnerPatternElement element = pattern.Elements[index];
            if (element.LaneMask <= 0 || (element.LaneMask & ~AllLanes) != 0 || element.ZOffset < 0f)
            {
                return false;
            }

            if (!offsets.Contains(element.ZOffset))
            {
                offsets.Add(element.ZOffset);
            }
        }

        offsets.Sort();
        float previousRequiredActionOffset = float.NegativeInfinity;

        for (int offsetIndex = 0; offsetIndex < offsets.Count; offsetIndex++)
        {
            float offset = offsets[offsetIndex];
            bool hasSurvivableLane = false;
            bool hasRequiredAction = false;

            for (int lane = 0; lane < 3; lane++)
            {
                RunnerObstacleKind? laneObstacle = null;
                for (int elementIndex = 0; elementIndex < pattern.Elements.Count; elementIndex++)
                {
                    RunnerPatternElement element = pattern.Elements[elementIndex];
                    if (Math.Abs(element.ZOffset - offset) < 0.001f && (element.LaneMask & (1 << lane)) != 0)
                    {
                        laneObstacle = element.Kind;
                        break;
                    }
                }

                if (!laneObstacle.HasValue || laneObstacle.Value != RunnerObstacleKind.Blocker)
                {
                    hasSurvivableLane = true;
                }

                if (laneObstacle.HasValue && RequiredAction(laneObstacle.Value) != RunnerRequiredAction.None)
                {
                    hasRequiredAction = true;
                }
            }

            if (!hasSurvivableLane)
            {
                return false;
            }

            if (hasRequiredAction)
            {
                if (offset - previousRequiredActionOffset < MinimumRequiredActionSpacing)
                {
                    return false;
                }

                previousRequiredActionOffset = offset;
            }
        }

        return true;
    }

    private static RunnerPatternElement Element(RunnerObstacleKind kind, int laneMask, float zOffset)
    {
        return new RunnerPatternElement(kind, laneMask, zOffset);
    }
}
