using UnityEngine;

public enum RunnerMusicState
{
    Menu,
    RunningLow,
    RunningHigh,
    GameOver
}

public readonly struct RunnerMusicClipMetrics
{
    public RunnerMusicClipMetrics(
        int sampleCount,
        float duration,
        float peak,
        float dcOffset,
        float loopSeamDelta,
        bool allSamplesFinite)
    {
        SampleCount = sampleCount;
        Duration = duration;
        Peak = peak;
        DcOffset = dcOffset;
        LoopSeamDelta = loopSeamDelta;
        AllSamplesFinite = allSamplesFinite;
    }

    public int SampleCount { get; }
    public float Duration { get; }
    public float Peak { get; }
    public float DcOffset { get; }
    public float LoopSeamDelta { get; }
    public bool AllSamplesFinite { get; }
}

public sealed class ProceduralRunnerMusic
{
    public const int LayerCount = 3;
    public const int TotalBeats = 64;
    public const float BeatsPerMinute = 126f;
    public const float CrossfadeDuration = 0.72f;
    public const float DuckGain = 0.7079f;
    public const float DuckAttackDuration = 0.04f;
    public const float DuckReleaseDuration = 0.32f;

    private const int SampleRate = 44100;
    private const float DuckHoldDuration = 0.06f;

    private static readonly int[] ChordRoots =
    {
        48, 44, 51, 46, 48, 44, 53, 46,
        48, 55, 51, 46, 44, 53, 46, 55
    };

    private static readonly bool[] MinorChords =
    {
        true, false, false, false, true, false, true, false,
        true, false, false, false, false, true, false, false
    };

    private static readonly int[] MinorArpeggio = { 0, 7, 12, 15, 12, 7, 3, 7 };
    private static readonly int[] MajorArpeggio = { 0, 7, 12, 16, 12, 7, 4, 7 };
    private static readonly float[] LayerBaseGains = { 0.34f, 0.3f, 0.2f };

    private readonly AudioSource[] sources = new AudioSource[LayerCount];
    private readonly AudioClip[] clips = new AudioClip[LayerCount];
    private readonly RunnerMusicClipMetrics[] metrics = new RunnerMusicClipMetrics[LayerCount];
    private readonly float[] currentWeights = new float[LayerCount];
    private readonly float[] targetWeights = new float[LayerCount];

    private RunnerMusicState state;
    private float duckLevel = 1f;
    private float duckHoldRemaining;
    private bool duckAttacking;

    private ProceduralRunnerMusic(GameObject host)
    {
        CreateLayer(0, host, "Rooftop Pulse - Atmosphere", CreateAtmosphereSample);
        CreateLayer(1, host, "Rooftop Pulse - Rhythm", CreateRhythmSample);
        CreateLayer(2, host, "Rooftop Pulse - Drive", CreateDriveSample);

        state = RunnerMusicState.Menu;
        ApplyTargetWeights(state, currentWeights);
        ApplyTargetWeights(state, targetWeights);
        UpdateVolumes();

        double startTime = AudioSettings.dspTime + 0.05d;
        for (int index = 0; index < sources.Length; index++)
        {
            sources[index].PlayScheduled(startTime);
        }
    }

    private delegate float LayerSampleGenerator(int sampleIndex, float time, float beatDuration);

    public RunnerMusicState State => state;
    public int SourceCount => sources.Length;
    public int ClipCount => clips.Length;
    public float CurrentDuckGain => duckLevel;

    public static ProceduralRunnerMusic AttachTo(GameObject host)
    {
        return new ProceduralRunnerMusic(host);
    }

    public AudioSource GetSource(int layerIndex)
    {
        return sources[layerIndex];
    }

    public AudioClip GetClip(int layerIndex)
    {
        return clips[layerIndex];
    }

    public RunnerMusicClipMetrics GetMetrics(int layerIndex)
    {
        return metrics[layerIndex];
    }

    public float GetCurrentWeight(int layerIndex)
    {
        return currentWeights[layerIndex];
    }

    public float GetTargetWeight(int layerIndex)
    {
        return targetWeights[layerIndex];
    }

    public void SetState(RunnerMusicState nextState)
    {
        if (state == nextState)
        {
            return;
        }

        state = nextState;
        ApplyTargetWeights(state, targetWeights);
    }

    public void TriggerDuck()
    {
        duckAttacking = true;
        duckHoldRemaining = DuckHoldDuration;
    }

    public void Tick(float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        float weightStep = deltaTime / CrossfadeDuration;
        for (int index = 0; index < currentWeights.Length; index++)
        {
            currentWeights[index] = Mathf.MoveTowards(
                currentWeights[index],
                targetWeights[index],
                weightStep);
        }

        if (duckAttacking)
        {
            duckLevel = Mathf.MoveTowards(
                duckLevel,
                DuckGain,
                (1f - DuckGain) * deltaTime / DuckAttackDuration);

            if (duckLevel <= DuckGain + 0.0001f)
            {
                duckLevel = DuckGain;
                duckAttacking = false;
            }
        }
        else if (duckHoldRemaining > 0f)
        {
            duckHoldRemaining = Mathf.Max(0f, duckHoldRemaining - deltaTime);
        }
        else
        {
            duckLevel = Mathf.MoveTowards(
                duckLevel,
                1f,
                (1f - DuckGain) * deltaTime / DuckReleaseDuration);
        }

        UpdateVolumes();
    }

    private void CreateLayer(
        int layerIndex,
        GameObject host,
        string clipName,
        LayerSampleGenerator generator)
    {
        float beatDuration = 60f / BeatsPerMinute;
        int sampleCount = Mathf.RoundToInt(TotalBeats * beatDuration * SampleRate);
        float[] samples = new float[sampleCount];

        for (int sampleIndex = 0; sampleIndex < samples.Length; sampleIndex++)
        {
            float time = sampleIndex / (float)SampleRate;
            samples[sampleIndex] = generator(sampleIndex, time, beatDuration);
        }

        metrics[layerIndex] = FinalizeSamples(samples);
        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        clips[layerIndex] = clip;

        AudioSource source = host.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.priority = 48 + layerIndex;
        sources[layerIndex] = source;
    }

    private static float CreateAtmosphereSample(int sampleIndex, float time, float beatDuration)
    {
        float beatPosition = time / beatDuration;
        int beatIndex = Mathf.FloorToInt(beatPosition);
        int barIndex = (beatIndex / 4) % ChordRoots.Length;
        float barPosition = beatPosition - Mathf.Floor(beatPosition / 4f) * 4f;
        float barEnvelope = Mathf.Sin(Mathf.PI * barPosition * 0.25f);
        int root = ChordRoots[barIndex];
        bool isMinor = MinorChords[barIndex];

        float pad = Sine(time, MidiToFrequency(root));
        pad += Sine(time, MidiToFrequency(root + (isMinor ? 3 : 4))) * 0.82f;
        pad += Sine(time, MidiToFrequency(root + 7)) * 0.68f;
        pad += Sine(time, MidiToFrequency(root + 12)) * 0.18f;
        pad *= barEnvelope * 0.055f;

        float halfBarDuration = beatDuration * 2f;
        float pulseLocal = time - Mathf.Floor(time / halfBarDuration) * halfBarDuration;
        float pulseEnvelope = NoteEnvelope(pulseLocal, halfBarDuration, 0.18f, 0.55f);
        float pulse = WarmTone(time, MidiToFrequency(root - 12)) * pulseEnvelope * 0.025f;
        return pad + pulse;
    }

    private static float CreateRhythmSample(int sampleIndex, float time, float beatDuration)
    {
        float beatPosition = time / beatDuration;
        int beatIndex = Mathf.FloorToInt(beatPosition);
        int barIndex = (beatIndex / 4) % ChordRoots.Length;
        float beatLocal = time - beatIndex * beatDuration;
        int root = ChordRoots[barIndex];

        int bassNote = root - 12 + ((beatIndex & 1) == 1 ? 7 : 0);
        float bassEnvelope = NoteEnvelope(beatLocal, beatDuration, 0.018f, 0.16f);
        float bass = WarmTone(time, MidiToFrequency(bassNote)) * bassEnvelope * 0.14f;

        float eighthDuration = beatDuration * 0.5f;
        float eighthLocal = time - Mathf.Floor(time / eighthDuration) * eighthDuration;
        float kick = CreateKick(beatLocal, beatIndex);
        float snare = CreateSnare(sampleIndex, beatIndex, beatLocal);
        float hiHat = CreateHiHat(sampleIndex, eighthLocal, eighthDuration, 0.032f);
        return bass + kick + snare + hiHat;
    }

    private static float CreateDriveSample(int sampleIndex, float time, float beatDuration)
    {
        float beatPosition = time / beatDuration;
        int beatIndex = Mathf.FloorToInt(beatPosition);
        int barIndex = (beatIndex / 4) % ChordRoots.Length;
        float beatLocal = time - beatIndex * beatDuration;
        int root = ChordRoots[barIndex];
        bool isMinor = MinorChords[barIndex];

        float eighthDuration = beatDuration * 0.5f;
        int eighthIndex = Mathf.FloorToInt(time / eighthDuration);
        float eighthLocal = time - eighthIndex * eighthDuration;
        int[] arpeggio = isMinor ? MinorArpeggio : MajorArpeggio;
        int phraseVariation = (barIndex / 4) % 2;
        int arpeggioStep = phraseVariation == 0
            ? eighthIndex % arpeggio.Length
            : arpeggio.Length - 1 - eighthIndex % arpeggio.Length;
        int note = root + 12 + arpeggio[arpeggioStep];
        float arpeggioEnvelope = NoteEnvelope(eighthLocal, eighthDuration, 0.012f, 0.075f);
        float arpeggioTone = BrightTone(time, MidiToFrequency(note)) * arpeggioEnvelope * 0.1f;

        float sixteenthDuration = beatDuration * 0.25f;
        float sixteenthLocal = time - Mathf.Floor(time / sixteenthDuration) * sixteenthDuration;
        float highHat = CreateHiHat(sampleIndex + 7919, sixteenthLocal, sixteenthDuration, 0.02f);
        float offbeatEnvelope = NoteEnvelope(beatLocal, beatDuration, 0.025f, 0.1f);
        float offbeat = (beatIndex & 1) == 1
            ? BrightTone(time, MidiToFrequency(root + 19)) * offbeatEnvelope * 0.025f
            : 0f;
        return arpeggioTone + highHat + offbeat;
    }

    private static RunnerMusicClipMetrics FinalizeSamples(float[] samples)
    {
        double sum = 0d;
        for (int index = 0; index < samples.Length; index++)
        {
            sum += samples[index];
        }

        float dcOffset = (float)(sum / samples.Length);
        float peak = 0f;
        for (int index = 0; index < samples.Length; index++)
        {
            samples[index] -= dcOffset;
            peak = Mathf.Max(peak, Mathf.Abs(samples[index]));
        }

        if (peak > 0.9f)
        {
            float scale = 0.9f / peak;
            for (int index = 0; index < samples.Length; index++)
            {
                samples[index] *= scale;
            }
        }

        double correctedSum = 0d;
        peak = 0f;
        bool allFinite = true;
        for (int index = 0; index < samples.Length; index++)
        {
            float sample = samples[index];
            correctedSum += sample;
            peak = Mathf.Max(peak, Mathf.Abs(sample));
            allFinite &= !float.IsNaN(sample) && !float.IsInfinity(sample);
        }

        return new RunnerMusicClipMetrics(
            samples.Length,
            samples.Length / (float)SampleRate,
            peak,
            (float)(correctedSum / samples.Length),
            Mathf.Abs(samples[0] - samples[samples.Length - 1]),
            allFinite);
    }

    private static void ApplyTargetWeights(RunnerMusicState musicState, float[] weights)
    {
        switch (musicState)
        {
            case RunnerMusicState.RunningLow:
                weights[0] = 0.82f;
                weights[1] = 0.72f;
                weights[2] = 0.18f;
                break;
            case RunnerMusicState.RunningHigh:
                weights[0] = 0.7f;
                weights[1] = 1f;
                weights[2] = 0.9f;
                break;
            case RunnerMusicState.GameOver:
                weights[0] = 0.55f;
                weights[1] = 0.05f;
                weights[2] = 0f;
                break;
            default:
                weights[0] = 1f;
                weights[1] = 0f;
                weights[2] = 0f;
                break;
        }
    }

    private void UpdateVolumes()
    {
        for (int index = 0; index < sources.Length; index++)
        {
            sources[index].volume = LayerBaseGains[index] * currentWeights[index] * duckLevel;
        }
    }

    private static float CreateKick(float localTime, int beatIndex)
    {
        if (localTime >= 0.2f || beatIndex % 4 == 3)
        {
            return 0f;
        }

        float attack = Mathf.Clamp01(localTime / 0.004f);
        float phase = 2f * Mathf.PI * (76f * localTime - 92f * localTime * localTime);
        return Mathf.Sin(phase) * attack * Mathf.Exp(-18f * localTime) * 0.24f;
    }

    private static float CreateSnare(int sampleIndex, int beatIndex, float localTime)
    {
        if ((beatIndex % 4 != 1 && beatIndex % 4 != 3) || localTime >= 0.16f)
        {
            return 0f;
        }

        float attack = Mathf.Clamp01(localTime / 0.003f);
        float envelope = attack * Mathf.Exp(-22f * localTime);
        float body = Mathf.Sin(2f * Mathf.PI * 185f * localTime) * 0.035f;
        return (Noise(sampleIndex) * 0.075f + body) * envelope;
    }

    private static float CreateHiHat(
        int sampleIndex,
        float localTime,
        float noteDuration,
        float amplitude)
    {
        if (localTime >= Mathf.Min(0.065f, noteDuration * 0.45f))
        {
            return 0f;
        }

        float attack = Mathf.Clamp01(localTime / 0.0025f);
        return Noise(sampleIndex * 3 + 17) * attack * Mathf.Exp(-52f * localTime) * amplitude;
    }

    private static float NoteEnvelope(float localTime, float duration, float attack, float release)
    {
        float attackAmount = Mathf.Clamp01(localTime / attack);
        float releaseAmount = Mathf.Clamp01((duration - localTime) / release);
        return attackAmount * releaseAmount;
    }

    private static float WarmTone(float time, float frequency)
    {
        return Sine(time, frequency) + Sine(time, frequency * 2f) * 0.22f;
    }

    private static float BrightTone(float time, float frequency)
    {
        return Sine(time, frequency) + Sine(time, frequency * 2f) * 0.3f + Sine(time, frequency * 3f) * 0.12f;
    }

    private static float Sine(float time, float frequency)
    {
        return Mathf.Sin(2f * Mathf.PI * frequency * time);
    }

    private static float MidiToFrequency(int note)
    {
        return 440f * Mathf.Pow(2f, (note - 69) / 12f);
    }

    private static float Noise(int value)
    {
        uint bits = (uint)value;
        bits ^= bits << 13;
        bits ^= bits >> 17;
        bits ^= bits << 5;
        return (bits & 0x00ffffff) / 8388607.5f - 1f;
    }
}
