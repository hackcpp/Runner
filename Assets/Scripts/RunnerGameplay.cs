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
        lanePath = Array.Empty<int>();
        if (pattern == null || string.IsNullOrEmpty(pattern.Id) || pattern.Elements.Count == 0)
        {
            return false;
        }

        List<float> offsets = new List<float>();
        for (int index = 0; index < pattern.Elements.Count; index++)
        {
            RunnerPatternElement element = pattern.Elements[index];
            if (element.LaneMask <= 0 ||
                (element.LaneMask & ~AllLanes) != 0 ||
                element.ZOffset < 0f ||
                float.IsNaN(element.ZOffset) ||
                float.IsInfinity(element.ZOffset))
            {
                return false;
            }

            if (!offsets.Contains(element.ZOffset))
            {
                offsets.Add(element.ZOffset);
            }
        }

        offsets.Sort();
        List<PathCandidate> candidates = new List<PathCandidate>();
        float previousOffset = offsets[0];

        for (int offsetIndex = 0; offsetIndex < offsets.Count; offsetIndex++)
        {
            float offset = offsets[offsetIndex];
            float availableDistance = offsetIndex == 0
                ? float.PositiveInfinity
                : offset - previousOffset;
            PathCandidate[] bestForLane = new PathCandidate[3];

            for (int lane = 0; lane < 3; lane++)
            {
                RunnerObstacleKind? laneObstacle;
                if (!TryGetLaneObstacle(pattern, offset, lane, out laneObstacle))
                {
                    return false;
                }

                if (laneObstacle == RunnerObstacleKind.Blocker)
                {
                    continue;
                }

                RunnerRequiredAction requiredAction = laneObstacle.HasValue
                    ? RequiredAction(laneObstacle.Value)
                    : RunnerRequiredAction.None;

                if (offsetIndex == 0)
                {
                    bestForLane[lane] = PathCandidate.Start(lane, offset, requiredAction);
                    continue;
                }

                for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
                {
                    PathCandidate candidate = candidates[candidateIndex];
                    float laneChangeDistance = Math.Abs(lane - candidate.Lane) * MinimumLaneChangeDistance;
                    if (laneChangeDistance > availableDistance + 0.001f)
                    {
                        continue;
                    }

                    if (requiredAction != RunnerRequiredAction.None &&
                        offset - candidate.LastRequiredActionOffset < MinimumRequiredActionSpacing - 0.001f)
                    {
                        continue;
                    }

                    PathCandidate next = candidate.Advance(lane, offset, requiredAction);
                    if (bestForLane[lane] == null ||
                        next.LastRequiredActionOffset < bestForLane[lane].LastRequiredActionOffset)
                    {
                        bestForLane[lane] = next;
                    }
                }
            }

            candidates.Clear();
            for (int lane = 0; lane < bestForLane.Length; lane++)
            {
                if (bestForLane[lane] != null)
                {
                    candidates.Add(bestForLane[lane]);
                }
            }

            if (candidates.Count == 0)
            {
                return false;
            }

            previousOffset = offset;
        }

        lanePath = candidates[0].Lanes;
        return true;
    }

    private static bool TryGetLaneObstacle(
        RunnerPatternDefinition pattern,
        float offset,
        int lane,
        out RunnerObstacleKind? obstacle)
    {
        obstacle = null;
        for (int elementIndex = 0; elementIndex < pattern.Elements.Count; elementIndex++)
        {
            RunnerPatternElement element = pattern.Elements[elementIndex];
            if (Math.Abs(element.ZOffset - offset) >= 0.001f || (element.LaneMask & (1 << lane)) == 0)
            {
                continue;
            }

            if (obstacle.HasValue)
            {
                return false;
            }

            obstacle = element.Kind;
        }

        return true;
    }

    private sealed class PathCandidate
    {
        private PathCandidate(int lane, float lastRequiredActionOffset, List<int> lanes)
        {
            Lane = lane;
            LastRequiredActionOffset = lastRequiredActionOffset;
            Lanes = lanes;
        }

        public int Lane { get; }
        public float LastRequiredActionOffset { get; }
        public IReadOnlyList<int> Lanes { get; }

        public static PathCandidate Start(int lane, float offset, RunnerRequiredAction requiredAction)
        {
            return new PathCandidate(
                lane,
                requiredAction == RunnerRequiredAction.None ? float.NegativeInfinity : offset,
                new List<int> { lane });
        }

        public PathCandidate Advance(int lane, float offset, RunnerRequiredAction requiredAction)
        {
            List<int> lanes = new List<int>(Lanes.Count + 1);
            for (int index = 0; index < Lanes.Count; index++)
            {
                lanes.Add(Lanes[index]);
            }

            lanes.Add(lane);
            return new PathCandidate(
                lane,
                requiredAction == RunnerRequiredAction.None ? LastRequiredActionOffset : offset,
                lanes);
        }
    }

    private static RunnerPatternElement Element(RunnerObstacleKind kind, int laneMask, float zOffset)
    {
        return new RunnerPatternElement(kind, laneMask, zOffset);
    }
}
