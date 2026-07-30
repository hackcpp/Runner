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

public enum RunnerLevelMood
{
    BlueDusk,
    GoldenSunset,
    VioletNight
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

    public static int CalculateWithBonus(float distance, int actionBonusScore)
    {
        return Math.Max(0, (int)Math.Floor(distance)) + Math.Max(0, actionBonusScore);
    }
}

public sealed class RunnerComboTracker
{
    public const float ComboWindow = 3.2f;
    public const int MaximumMultiplier = 4;

    public int ComboCount { get; private set; }
    public int HighestCombo { get; private set; }
    public int TotalBonusScore { get; private set; }
    public float RemainingTime { get; private set; }
    public int Multiplier => Math.Min(MaximumMultiplier, Math.Max(1, ComboCount));

    public int RegisterActionClear()
    {
        ComboCount++;
        HighestCombo = Math.Max(HighestCombo, ComboCount);
        RemainingTime = ComboWindow;

        int awardedPoints = RunnerScore.ActionClearPoints * Multiplier;
        TotalBonusScore += awardedPoints;
        return awardedPoints;
    }

    public void Tick(float deltaTime)
    {
        if (ComboCount == 0 || deltaTime <= 0f)
        {
            return;
        }

        RemainingTime = Math.Max(0f, RemainingTime - deltaTime);
        if (RemainingTime <= 0f)
        {
            ComboCount = 0;
        }
    }

    public void Reset()
    {
        ComboCount = 0;
        HighestCombo = 0;
        TotalBonusScore = 0;
        RemainingTime = 0f;
    }

    public void ClearActiveCombo()
    {
        ComboCount = 0;
        RemainingTime = 0f;
    }
}

public sealed class RunnerLevelDefinition
{
    public RunnerLevelDefinition(
        int number,
        string name,
        float targetDistance,
        int maximumTier,
        int seed,
        RunnerLevelMood mood,
        float startingSpeed,
        float speedAcceleration,
        float maximumSpeed)
    {
        Number = number;
        Name = name;
        TargetDistance = targetDistance;
        MaximumTier = maximumTier;
        Seed = seed;
        Mood = mood;
        StartingSpeed = startingSpeed;
        SpeedAcceleration = speedAcceleration;
        MaximumSpeed = maximumSpeed;
    }

    public int Number { get; }
    public string Name { get; }
    public float TargetDistance { get; }
    public int MaximumTier { get; }
    public int Seed { get; }
    public RunnerLevelMood Mood { get; }
    public float StartingSpeed { get; }
    public float SpeedAcceleration { get; }
    public float MaximumSpeed { get; }

    public float SpeedAtDistance(float distance)
    {
        double speedSquared = StartingSpeed * StartingSpeed +
                              2d * SpeedAcceleration * Math.Max(0f, distance);
        return (float)Math.Min(MaximumSpeed, Math.Sqrt(speedSquared));
    }

    public float TimeAtDistance(float distance)
    {
        double nonNegativeDistance = Math.Max(0f, distance);
        double timeToMaximumSpeed = (MaximumSpeed - StartingSpeed) / SpeedAcceleration;
        double distanceToMaximumSpeed =
            (MaximumSpeed * MaximumSpeed - StartingSpeed * StartingSpeed) /
            (2d * SpeedAcceleration);

        if (nonNegativeDistance <= distanceToMaximumSpeed)
        {
            return (float)((Math.Sqrt(
                StartingSpeed * StartingSpeed +
                2d * SpeedAcceleration * nonNegativeDistance) - StartingSpeed) /
                SpeedAcceleration);
        }

        return (float)(timeToMaximumSpeed +
                       (nonNegativeDistance - distanceToMaximumSpeed) / MaximumSpeed);
    }

    public float CheckpointDistance(int checkpointIndex)
    {
        if (checkpointIndex <= 0)
        {
            return 0f;
        }

        if (checkpointIndex == 1)
        {
            return TargetDistance / 3f;
        }

        return TargetDistance * 2f / 3f;
    }

    public int TierForDistance(float distance)
    {
        return Math.Min(MaximumTier, RunnerRunTuning.TierForDistance(distance));
    }
}

public static class RunnerLevelCatalog
{
    public const int LivesPerLevel = 3;
    public const int CheckpointCount = 2;

    private static readonly RunnerLevelDefinition[] Definitions =
    {
        new RunnerLevelDefinition(1, "ROOFTOP BASICS", 360f, 0, 17031, RunnerLevelMood.BlueDusk, 9.4f, 0.12f, 12.5f),
        new RunnerLevelDefinition(2, "CITY RHYTHM", 520f, 1, 27059, RunnerLevelMood.GoldenSunset, 10.4f, 0.16f, 14.5f),
        new RunnerLevelDefinition(3, "SUNSET SPRINT", 720f, 2, 37087, RunnerLevelMood.VioletNight, 11.4f, 0.2f, 16.5f)
    };

    public static IReadOnlyList<RunnerLevelDefinition> Levels => Definitions;
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

    public float NextSpacing()
    {
        return RunnerPatternCatalog.MinimumRequiredActionSpacing +
               (float)random.NextDouble() * 3f;
    }
}

public static class RunnerPatternCatalog
{
    public const float MaximumRunnerSpeed = 16.5f;
    public const float MinimumActionTime = 0.9f;
    public const float MinimumRequiredActionSpacing = MaximumRunnerSpeed * MinimumActionTime;
    public const float MinimumLaneChangeDistance =
        MaximumRunnerSpeed * RunnerMotor.DefaultLaneWidth / RunnerMotor.LaneMoveSpeed;

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
        IReadOnlyList<int> ignoredPath;
        return TryFindSurvivalPath(pattern, out ignoredPath);
    }

    public static bool TryFindSurvivalPath(
        RunnerPatternDefinition pattern,
        out IReadOnlyList<int> lanePath)
    {
        IReadOnlyList<RunnerObstacleRow> rows;
        if (!RunnerObstacleRowBuilder.TryBuildPatternRows(pattern, out rows))
        {
            lanePath = Array.Empty<int>();
            return false;
        }

        return RunnerSurvivalSolver.TryFindPathFromAnyLane(rows, out lanePath);
    }

    private static RunnerPatternElement Element(RunnerObstacleKind kind, int laneMask, float zOffset)
    {
        return new RunnerPatternElement(kind, laneMask, zOffset);
    }
}
