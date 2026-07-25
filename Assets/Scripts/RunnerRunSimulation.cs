using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

public static class RunnerRunTuning
{
    public const float StartingSpeed = 9.4f;
    public const float SpeedAcceleration = 0.16f;
    public const float LookAheadDistance = 90f;
    public const float FirstRandomPatternZ = 96f;
    public const float TutorialLaneChangeZ = 28f;
    public const float TutorialJumpZ = 48f;
    public const float TutorialSlideZ = 70f;
    public const float IntermediateTierDistance = 150f;
    public const float AdvancedTierDistance = 400f;

    public static int TierForDistance(float distance)
    {
        if (distance < IntermediateTierDistance)
        {
            return 0;
        }

        return distance < AdvancedTierDistance ? 1 : 2;
    }

    public static float SpeedAtDistance(float distance)
    {
        double nonNegativeDistance = Math.Max(0d, distance);
        double speedSquared = StartingSpeed * StartingSpeed +
                              2d * SpeedAcceleration * nonNegativeDistance;
        return (float)Math.Min(RunnerPatternCatalog.MaximumRunnerSpeed, Math.Sqrt(speedSquared));
    }

    public static float TimeAtDistance(float distance)
    {
        double nonNegativeDistance = Math.Max(0d, distance);
        double maximumSpeed = RunnerPatternCatalog.MaximumRunnerSpeed;
        double timeToMaximumSpeed = (maximumSpeed - StartingSpeed) / SpeedAcceleration;
        double distanceToMaximumSpeed =
            (maximumSpeed * maximumSpeed - StartingSpeed * StartingSpeed) /
            (2d * SpeedAcceleration);

        if (nonNegativeDistance <= distanceToMaximumSpeed)
        {
            return (float)((Math.Sqrt(
                StartingSpeed * StartingSpeed +
                2d * SpeedAcceleration * nonNegativeDistance) - StartingSpeed) /
                SpeedAcceleration);
        }

        return (float)(timeToMaximumSpeed +
                       (nonNegativeDistance - distanceToMaximumSpeed) / maximumSpeed);
    }

    public static float GenerationDistanceForPattern(float patternStartZ)
    {
        return Math.Max(0f, patternStartZ - LookAheadDistance);
    }
}

public readonly struct RunnerObstacleRow
{
    private readonly RunnerObstacleKind? left;
    private readonly RunnerObstacleKind? center;
    private readonly RunnerObstacleKind? right;

    public RunnerObstacleRow(
        float z,
        RunnerObstacleKind? leftObstacle,
        RunnerObstacleKind? centerObstacle,
        RunnerObstacleKind? rightObstacle)
    {
        Z = z;
        left = leftObstacle;
        center = centerObstacle;
        right = rightObstacle;
    }

    public float Z { get; }

    public RunnerObstacleKind? ObstacleInLane(int lane)
    {
        if (lane == 0)
        {
            return left;
        }

        if (lane == 1)
        {
            return center;
        }

        if (lane == 2)
        {
            return right;
        }

        throw new ArgumentOutOfRangeException(nameof(lane));
    }
}

public static class RunnerSurvivalSolver
{
    public static bool TryFindPath(
        IReadOnlyList<RunnerObstacleRow> rows,
        int initialLane,
        float initialZ,
        out IReadOnlyList<int> lanePath,
        out float failureZ)
    {
        if (initialLane < 0 || initialLane > 2)
        {
            lanePath = Array.Empty<int>();
            failureZ = initialZ;
            return false;
        }

        List<PathCandidate> candidates = new List<PathCandidate>
        {
            PathCandidate.Start(initialLane)
        };

        return TryFindPath(
            rows,
            candidates,
            initialZ,
            DistanceAtMaximumSpeed,
            out lanePath,
            out failureZ);
    }

    internal static bool TryFindPathUsingRunTiming(
        IReadOnlyList<RunnerObstacleRow> rows,
        int initialLane,
        float initialZ,
        out IReadOnlyList<int> lanePath,
        out float failureZ)
    {
        if (initialLane < 0 || initialLane > 2)
        {
            lanePath = Array.Empty<int>();
            failureZ = initialZ;
            return false;
        }

        List<PathCandidate> candidates = new List<PathCandidate>
        {
            PathCandidate.Start(initialLane)
        };

        return TryFindPath(
            rows,
            candidates,
            initialZ,
            RunnerRunTuning.TimeAtDistance,
            out lanePath,
            out failureZ);
    }

    internal static bool TryFindPathFromAnyLane(
        IReadOnlyList<RunnerObstacleRow> rows,
        out IReadOnlyList<int> lanePath)
    {
        lanePath = Array.Empty<int>();
        if (rows == null || rows.Count == 0)
        {
            return false;
        }

        List<PathCandidate> candidates = new List<PathCandidate>
        {
            PathCandidate.Start(0),
            PathCandidate.Start(1),
            PathCandidate.Start(2)
        };

        float ignoredFailureZ;
        return TryFindPath(
            rows,
            candidates,
            rows[0].Z,
            DistanceAtMaximumSpeed,
            out lanePath,
            out ignoredFailureZ);
    }

    private static bool TryFindPath(
        IReadOnlyList<RunnerObstacleRow> rows,
        List<PathCandidate> candidates,
        float initialZ,
        Func<float, float> timeAtDistance,
        out IReadOnlyList<int> lanePath,
        out float failureZ)
    {
        lanePath = Array.Empty<int>();
        failureZ = initialZ;
        if (rows == null)
        {
            return false;
        }

        if (rows.Count == 0)
        {
            return true;
        }

        float previousZ = initialZ;
        float previousTime = timeAtDistance(initialZ);
        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            RunnerObstacleRow row = rows[rowIndex];
            if (float.IsNaN(row.Z) || float.IsInfinity(row.Z) || row.Z < previousZ - 0.001f)
            {
                failureZ = row.Z;
                return false;
            }

            float rowTime = timeAtDistance(row.Z);
            float availableTime = Math.Max(0f, rowTime - previousTime);
            PathCandidate[] bestForLane = new PathCandidate[3];

            for (int lane = 0; lane < 3; lane++)
            {
                RunnerObstacleKind? obstacle = row.ObstacleInLane(lane);
                if (obstacle.HasValue && !Enum.IsDefined(typeof(RunnerObstacleKind), obstacle.Value))
                {
                    failureZ = row.Z;
                    return false;
                }

                if (obstacle == RunnerObstacleKind.Blocker)
                {
                    continue;
                }

                RunnerRequiredAction requiredAction = obstacle.HasValue
                    ? RunnerPatternCatalog.RequiredAction(obstacle.Value)
                    : RunnerRequiredAction.None;

                for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
                {
                    PathCandidate candidate = candidates[candidateIndex];
                    float laneChangeTime = Math.Abs(lane - candidate.Lane) *
                                           RunnerMotor.DefaultLaneWidth /
                                           RunnerMotor.LaneMoveSpeed;
                    if (laneChangeTime > availableTime + 0.001f)
                    {
                        continue;
                    }

                    if (requiredAction != RunnerRequiredAction.None &&
                        rowTime - candidate.LastRequiredActionTime <
                        RunnerPatternCatalog.MinimumActionTime - 0.001f)
                    {
                        continue;
                    }

                    float lastRequiredActionTime = requiredAction == RunnerRequiredAction.None
                        ? candidate.LastRequiredActionTime
                        : rowTime;
                    PathCandidate next = candidate.Advance(lane, lastRequiredActionTime);
                    if (bestForLane[lane] == null ||
                        next.LastRequiredActionTime < bestForLane[lane].LastRequiredActionTime)
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
                failureZ = row.Z;
                return false;
            }

            previousZ = row.Z;
            previousTime = rowTime;
        }

        lanePath = BuildLanePath(candidates[0]);
        failureZ = float.NaN;
        return true;
    }

    private static float DistanceAtMaximumSpeed(float distance)
    {
        return distance / RunnerPatternCatalog.MaximumRunnerSpeed;
    }

    private static IReadOnlyList<int> BuildLanePath(PathCandidate candidate)
    {
        List<int> reversed = new List<int>();
        while (candidate.Parent != null)
        {
            reversed.Add(candidate.Lane);
            candidate = candidate.Parent;
        }

        reversed.Reverse();
        return reversed;
    }

    private sealed class PathCandidate
    {
        private PathCandidate(int lane, float lastRequiredActionTime, PathCandidate parent)
        {
            Lane = lane;
            LastRequiredActionTime = lastRequiredActionTime;
            Parent = parent;
        }

        public int Lane { get; }
        public float LastRequiredActionTime { get; }
        public PathCandidate Parent { get; }

        public static PathCandidate Start(int lane)
        {
            return new PathCandidate(lane, float.NegativeInfinity, null);
        }

        public PathCandidate Advance(int lane, float lastRequiredActionTime)
        {
            return new PathCandidate(lane, lastRequiredActionTime, this);
        }
    }
}

public sealed class RunnerRunSimulationResult
{
    internal RunnerRunSimulationResult(
        int seed,
        float targetDistance,
        bool isSurvivable,
        float failureZ,
        IReadOnlyList<string> patternIds,
        IReadOnlyList<int> lanePath,
        string sequenceFingerprint,
        int[] tierPatternCounts,
        int[] obstacleCounts,
        int rowCount,
        float minimumActionInterval,
        float minimumLaneChangeTimeMargin)
    {
        Seed = seed;
        TargetDistance = targetDistance;
        IsSurvivable = isSurvivable;
        FailureZ = failureZ;
        PatternIds = patternIds;
        LanePath = lanePath;
        SequenceFingerprint = sequenceFingerprint;
        TierPatternCounts = tierPatternCounts;
        ObstacleCounts = obstacleCounts;
        RowCount = rowCount;
        MinimumActionInterval = minimumActionInterval;
        MinimumLaneChangeTimeMargin = minimumLaneChangeTimeMargin;
    }

    private int[] TierPatternCounts { get; }
    private int[] ObstacleCounts { get; }

    public int Seed { get; }
    public float TargetDistance { get; }
    public bool IsSurvivable { get; }
    public float FailureZ { get; }
    public IReadOnlyList<string> PatternIds { get; }
    public IReadOnlyList<int> LanePath { get; }
    public string SequenceFingerprint { get; }
    public int PatternCount => PatternIds.Count;
    public int RowCount { get; }
    public float MinimumActionInterval { get; }
    public float MinimumLaneChangeTimeMargin { get; }

    public int PatternCountForTier(int tier)
    {
        return tier >= 0 && tier < TierPatternCounts.Length ? TierPatternCounts[tier] : 0;
    }

    public int ObstacleCount(RunnerObstacleKind kind)
    {
        int index = (int)kind;
        return index >= 0 && index < ObstacleCounts.Length ? ObstacleCounts[index] : 0;
    }
}

public sealed class RunnerRunSimulationBatchResult
{
    internal RunnerRunSimulationBatchResult(
        int seedCount,
        int failedRunCount,
        int firstFailedSeed,
        int totalPatternCount,
        int totalRowCount,
        int uniquePatternCount,
        int[] tierPatternCounts,
        int[] obstacleCounts,
        float minimumActionInterval,
        float minimumLaneChangeTimeMargin)
    {
        SeedCount = seedCount;
        FailedRunCount = failedRunCount;
        FirstFailedSeed = firstFailedSeed;
        TotalPatternCount = totalPatternCount;
        TotalRowCount = totalRowCount;
        UniquePatternCount = uniquePatternCount;
        TierPatternCounts = tierPatternCounts;
        ObstacleCounts = obstacleCounts;
        MinimumActionInterval = minimumActionInterval;
        MinimumLaneChangeTimeMargin = minimumLaneChangeTimeMargin;
    }

    private int[] TierPatternCounts { get; }
    private int[] ObstacleCounts { get; }

    public int SeedCount { get; }
    public int FailedRunCount { get; }
    public int FirstFailedSeed { get; }
    public int TotalPatternCount { get; }
    public int TotalRowCount { get; }
    public int UniquePatternCount { get; }
    public float MinimumActionInterval { get; }
    public float MinimumLaneChangeTimeMargin { get; }

    public int PatternCountForTier(int tier)
    {
        return tier >= 0 && tier < TierPatternCounts.Length ? TierPatternCounts[tier] : 0;
    }

    public int ObstacleCount(RunnerObstacleKind kind)
    {
        int index = (int)kind;
        return index >= 0 && index < ObstacleCounts.Length ? ObstacleCounts[index] : 0;
    }
}

public static class RunnerRunSimulator
{
    private const int CenterLane = 1;
    private const int AllLanesMask = 0b111;

    public static RunnerRunSimulationResult Simulate(int seed, float targetDistance)
    {
        if (targetDistance < 0f || float.IsNaN(targetDistance) || float.IsInfinity(targetDistance))
        {
            throw new ArgumentOutOfRangeException(nameof(targetDistance));
        }

        RunnerPatternSequence sequence = new RunnerPatternSequence(seed);
        List<RunnerPlacedObstacle> placedObstacles = new List<RunnerPlacedObstacle>();
        List<string> patternIds = new List<string>();
        StringBuilder fingerprint = new StringBuilder();
        int[] tierPatternCounts = new int[3];
        int[] obstacleCounts = new int[3];

        AddTutorialObstacles(placedObstacles, obstacleCounts, targetDistance);

        float nextPatternZ = RunnerRunTuning.FirstRandomPatternZ;
        while (nextPatternZ <= targetDistance + 0.001f)
        {
            float generationDistance = RunnerRunTuning.GenerationDistanceForPattern(nextPatternZ);
            int tier = RunnerRunTuning.TierForDistance(generationDistance);
            RunnerPatternDefinition pattern = sequence.Next(tier);
            tierPatternCounts[tier]++;
            patternIds.Add(pattern.Id);
            AppendFingerprint(fingerprint, pattern, tier, nextPatternZ);

            AddPatternObstacles(
                placedObstacles,
                obstacleCounts,
                pattern,
                nextPatternZ,
                targetDistance);

            nextPatternZ += pattern.Length + sequence.NextSpacing();
        }

        IReadOnlyList<RunnerObstacleRow> rows;
        bool layoutValid = RunnerObstacleRowBuilder.TryBuildRows(placedObstacles, out rows);
        IReadOnlyList<int> lanePath = Array.Empty<int>();
        float failureZ = layoutValid && rows.Count == 0 ? float.NaN : 0f;
        bool isSurvivable = layoutValid && RunnerSurvivalSolver.TryFindPathUsingRunTiming(
            rows,
            CenterLane,
            0f,
            out lanePath,
            out failureZ);

        float minimumActionInterval;
        float minimumLaneChangeTimeMargin;
        CalculatePathMetrics(
            rows,
            lanePath,
            out minimumActionInterval,
            out minimumLaneChangeTimeMargin);

        return new RunnerRunSimulationResult(
            seed,
            targetDistance,
            isSurvivable,
            failureZ,
            patternIds,
            lanePath,
            fingerprint.ToString(),
            tierPatternCounts,
            obstacleCounts,
            rows.Count,
            minimumActionInterval,
            minimumLaneChangeTimeMargin);
    }

    public static RunnerRunSimulationBatchResult SimulateBatch(
        int firstSeed,
        int seedCount,
        float targetDistance)
    {
        if (seedCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seedCount));
        }

        int failedRunCount = 0;
        int firstFailedSeed = -1;
        int totalPatternCount = 0;
        int totalRowCount = 0;
        int[] tierPatternCounts = new int[3];
        int[] obstacleCounts = new int[3];
        HashSet<string> uniquePatternIds = new HashSet<string>();
        float minimumActionInterval = float.PositiveInfinity;
        float minimumLaneChangeTimeMargin = float.PositiveInfinity;

        for (int seedOffset = 0; seedOffset < seedCount; seedOffset++)
        {
            int seed = unchecked(firstSeed + seedOffset);
            RunnerRunSimulationResult result = Simulate(seed, targetDistance);
            if (!result.IsSurvivable)
            {
                failedRunCount++;
                if (firstFailedSeed < 0)
                {
                    firstFailedSeed = seed;
                }
            }

            totalPatternCount += result.PatternCount;
            totalRowCount += result.RowCount;
            for (int tier = 0; tier < tierPatternCounts.Length; tier++)
            {
                tierPatternCounts[tier] += result.PatternCountForTier(tier);
            }

            for (int kind = 0; kind < obstacleCounts.Length; kind++)
            {
                obstacleCounts[kind] += result.ObstacleCount((RunnerObstacleKind)kind);
            }

            for (int patternIndex = 0; patternIndex < result.PatternIds.Count; patternIndex++)
            {
                uniquePatternIds.Add(result.PatternIds[patternIndex]);
            }

            minimumActionInterval = Math.Min(minimumActionInterval, result.MinimumActionInterval);
            minimumLaneChangeTimeMargin = Math.Min(
                minimumLaneChangeTimeMargin,
                result.MinimumLaneChangeTimeMargin);
        }

        return new RunnerRunSimulationBatchResult(
            seedCount,
            failedRunCount,
            firstFailedSeed,
            totalPatternCount,
            totalRowCount,
            uniquePatternIds.Count,
            tierPatternCounts,
            obstacleCounts,
            minimumActionInterval,
            minimumLaneChangeTimeMargin);
    }

    private static void AddTutorialObstacles(
        List<RunnerPlacedObstacle> obstacles,
        int[] obstacleCounts,
        float targetDistance)
    {
        if (RunnerRunTuning.TutorialLaneChangeZ <= targetDistance)
        {
            AddObstacleMask(
                obstacles,
                obstacleCounts,
                RunnerObstacleKind.Blocker,
                1 << CenterLane,
                RunnerRunTuning.TutorialLaneChangeZ);
        }

        if (RunnerRunTuning.TutorialJumpZ <= targetDistance)
        {
            AddObstacleMask(
                obstacles,
                obstacleCounts,
                RunnerObstacleKind.Hurdle,
                AllLanesMask,
                RunnerRunTuning.TutorialJumpZ);
        }

        if (RunnerRunTuning.TutorialSlideZ <= targetDistance)
        {
            AddObstacleMask(
                obstacles,
                obstacleCounts,
                RunnerObstacleKind.Overhead,
                AllLanesMask,
                RunnerRunTuning.TutorialSlideZ);
        }
    }

    private static void AddPatternObstacles(
        List<RunnerPlacedObstacle> obstacles,
        int[] obstacleCounts,
        RunnerPatternDefinition pattern,
        float startZ,
        float targetDistance)
    {
        for (int elementIndex = 0; elementIndex < pattern.Elements.Count; elementIndex++)
        {
            RunnerPatternElement element = pattern.Elements[elementIndex];
            float z = startZ + element.ZOffset;
            if (z <= targetDistance + 0.001f)
            {
                AddObstacleMask(obstacles, obstacleCounts, element.Kind, element.LaneMask, z);
            }
        }
    }

    private static void AddObstacleMask(
        List<RunnerPlacedObstacle> obstacles,
        int[] obstacleCounts,
        RunnerObstacleKind kind,
        int laneMask,
        float z)
    {
        for (int lane = 0; lane < 3; lane++)
        {
            if ((laneMask & (1 << lane)) == 0)
            {
                continue;
            }

            obstacles.Add(new RunnerPlacedObstacle(z, lane, kind));
            obstacleCounts[(int)kind]++;
        }
    }

    private static void AppendFingerprint(
        StringBuilder fingerprint,
        RunnerPatternDefinition pattern,
        int tier,
        float startZ)
    {
        fingerprint.Append(pattern.Id);
        fingerprint.Append(':');
        fingerprint.Append(tier);
        fingerprint.Append('@');
        fingerprint.Append(startZ.ToString("F3", CultureInfo.InvariantCulture));
        fingerprint.Append('|');
    }

    private static void CalculatePathMetrics(
        IReadOnlyList<RunnerObstacleRow> rows,
        IReadOnlyList<int> lanePath,
        out float minimumActionInterval,
        out float minimumLaneChangeTimeMargin)
    {
        minimumActionInterval = float.PositiveInfinity;
        minimumLaneChangeTimeMargin = float.PositiveInfinity;
        if (rows == null || lanePath == null || rows.Count != lanePath.Count)
        {
            return;
        }

        int previousLane = CenterLane;
        float previousTime = RunnerRunTuning.TimeAtDistance(0f);
        float previousRequiredActionTime = float.NegativeInfinity;

        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            RunnerObstacleRow row = rows[rowIndex];
            int lane = lanePath[rowIndex];
            float rowTime = RunnerRunTuning.TimeAtDistance(row.Z);
            int laneDelta = Math.Abs(lane - previousLane);
            if (laneDelta > 0)
            {
                float requiredTime = laneDelta * RunnerMotor.DefaultLaneWidth / RunnerMotor.LaneMoveSpeed;
                minimumLaneChangeTimeMargin = Math.Min(
                    minimumLaneChangeTimeMargin,
                    rowTime - previousTime - requiredTime);
            }

            RunnerObstacleKind? obstacle = row.ObstacleInLane(lane);
            if (obstacle.HasValue &&
                RunnerPatternCatalog.RequiredAction(obstacle.Value) != RunnerRequiredAction.None)
            {
                if (!float.IsNegativeInfinity(previousRequiredActionTime))
                {
                    minimumActionInterval = Math.Min(
                        minimumActionInterval,
                        rowTime - previousRequiredActionTime);
                }

                previousRequiredActionTime = rowTime;
            }

            previousLane = lane;
            previousTime = rowTime;
        }
    }
}

internal readonly struct RunnerPlacedObstacle
{
    public RunnerPlacedObstacle(float z, int lane, RunnerObstacleKind kind)
    {
        Z = z;
        Lane = lane;
        Kind = kind;
    }

    public float Z { get; }
    public int Lane { get; }
    public RunnerObstacleKind Kind { get; }
}

internal static class RunnerObstacleRowBuilder
{
    private const int AllLanesMask = 0b111;

    public static bool TryBuildPatternRows(
        RunnerPatternDefinition pattern,
        out IReadOnlyList<RunnerObstacleRow> rows)
    {
        rows = Array.Empty<RunnerObstacleRow>();
        if (pattern == null ||
            string.IsNullOrEmpty(pattern.Id) ||
            pattern.Elements == null ||
            pattern.Elements.Count == 0)
        {
            return false;
        }

        List<RunnerPlacedObstacle> placed = new List<RunnerPlacedObstacle>();
        for (int elementIndex = 0; elementIndex < pattern.Elements.Count; elementIndex++)
        {
            RunnerPatternElement element = pattern.Elements[elementIndex];
            if (element.LaneMask <= 0 ||
                (element.LaneMask & ~AllLanesMask) != 0 ||
                element.ZOffset < 0f ||
                float.IsNaN(element.ZOffset) ||
                float.IsInfinity(element.ZOffset) ||
                !Enum.IsDefined(typeof(RunnerObstacleKind), element.Kind))
            {
                return false;
            }

            for (int lane = 0; lane < 3; lane++)
            {
                if ((element.LaneMask & (1 << lane)) != 0)
                {
                    placed.Add(new RunnerPlacedObstacle(element.ZOffset, lane, element.Kind));
                }
            }
        }

        return TryBuildRows(placed, out rows);
    }

    public static bool TryBuildRows(
        IReadOnlyList<RunnerPlacedObstacle> placedObstacles,
        out IReadOnlyList<RunnerObstacleRow> rows)
    {
        rows = Array.Empty<RunnerObstacleRow>();
        if (placedObstacles == null)
        {
            return false;
        }

        if (placedObstacles.Count == 0)
        {
            return true;
        }

        List<RunnerPlacedObstacle> sorted = new List<RunnerPlacedObstacle>(placedObstacles.Count);
        for (int index = 0; index < placedObstacles.Count; index++)
        {
            RunnerPlacedObstacle obstacle = placedObstacles[index];
            if (obstacle.Lane < 0 ||
                obstacle.Lane > 2 ||
                obstacle.Z < 0f ||
                float.IsNaN(obstacle.Z) ||
                float.IsInfinity(obstacle.Z) ||
                !Enum.IsDefined(typeof(RunnerObstacleKind), obstacle.Kind))
            {
                return false;
            }

            sorted.Add(obstacle);
        }

        sorted.Sort(ComparePlacedObstacles);
        List<RunnerObstacleRow> builtRows = new List<RunnerObstacleRow>();
        int obstacleIndex = 0;
        while (obstacleIndex < sorted.Count)
        {
            float rowZ = sorted[obstacleIndex].Z;
            RunnerObstacleKind?[] laneObstacles = new RunnerObstacleKind?[3];

            while (obstacleIndex < sorted.Count &&
                   Math.Abs(sorted[obstacleIndex].Z - rowZ) < 0.001f)
            {
                RunnerPlacedObstacle obstacle = sorted[obstacleIndex];
                if (laneObstacles[obstacle.Lane].HasValue)
                {
                    return false;
                }

                laneObstacles[obstacle.Lane] = obstacle.Kind;
                obstacleIndex++;
            }

            builtRows.Add(new RunnerObstacleRow(
                rowZ,
                laneObstacles[0],
                laneObstacles[1],
                laneObstacles[2]));
        }

        rows = builtRows;
        return true;
    }

    private static int ComparePlacedObstacles(RunnerPlacedObstacle first, RunnerPlacedObstacle second)
    {
        int zComparison = first.Z.CompareTo(second.Z);
        return zComparison != 0 ? zComparison : first.Lane.CompareTo(second.Lane);
    }
}
