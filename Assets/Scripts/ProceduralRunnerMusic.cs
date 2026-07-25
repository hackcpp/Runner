using UnityEngine;

public static class ProceduralRunnerMusic
{
    private const int SampleRate = 44100;
    private const float BeatsPerMinute = 126f;
    private const int TotalBeats = 32;

    private static readonly int[] ChordRoots = { 48, 44, 51, 46, 48, 44, 53, 46 };
    private static readonly bool[] MinorChords = { true, false, false, false, true, false, true, false };
    private static readonly int[] MinorArpeggio = { 0, 7, 12, 15, 12, 7, 3, 7 };
    private static readonly int[] MajorArpeggio = { 0, 7, 12, 16, 12, 7, 4, 7 };

    public static AudioSource AttachTo(GameObject host)
    {
        AudioSource source = host.GetComponent<AudioSource>();
        if (source == null)
        {
            source = host.AddComponent<AudioSource>();
        }

        source.clip = CreateClip();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0.34f;
        source.priority = 32;
        source.Play();
        return source;
    }

    private static AudioClip CreateClip()
    {
        float beatDuration = 60f / BeatsPerMinute;
        float loopDuration = TotalBeats * beatDuration;
        int sampleCount = Mathf.CeilToInt(loopDuration * SampleRate);
        float[] samples = new float[sampleCount];

        // Layer a pad, bass, arpeggio and compact drum pattern into one seamless mono loop.
        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            float time = sampleIndex / (float)SampleRate;
            float beatPosition = time / beatDuration;
            int beatIndex = Mathf.FloorToInt(beatPosition);
            int barIndex = (beatIndex / 4) % ChordRoots.Length;
            float beatLocal = time - beatIndex * beatDuration;
            float barPosition = beatPosition - Mathf.Floor(beatPosition / 4f) * 4f;
            int root = ChordRoots[barIndex];
            bool isMinor = MinorChords[barIndex];

            float padEnvelope = Mathf.Sin(Mathf.PI * barPosition * 0.25f);
            float pad = Sine(time, MidiToFrequency(root));
            pad += Sine(time, MidiToFrequency(root + (isMinor ? 3 : 4)));
            pad += Sine(time, MidiToFrequency(root + 7));
            pad *= 0.035f * padEnvelope;

            int bassNote = root - 12 + ((beatIndex & 1) == 1 ? 7 : 0);
            float bassEnvelope = NoteEnvelope(beatLocal, beatDuration, 0.025f, 0.16f);
            float bass = WarmTone(time, MidiToFrequency(bassNote)) * bassEnvelope * 0.16f;

            float eighthDuration = beatDuration * 0.5f;
            int eighthIndex = Mathf.FloorToInt(time / eighthDuration);
            float eighthLocal = time - eighthIndex * eighthDuration;
            int[] arpeggio = isMinor ? MinorArpeggio : MajorArpeggio;
            int arpeggioNote = root + 12 + arpeggio[eighthIndex % arpeggio.Length];
            float arpeggioEnvelope = NoteEnvelope(eighthLocal, eighthDuration, 0.012f, 0.08f);
            float arpeggioTone = BrightTone(time, MidiToFrequency(arpeggioNote)) * arpeggioEnvelope * 0.1f;

            float kick = CreateKick(beatLocal);
            float snare = CreateSnare(sampleIndex, beatIndex, beatLocal);
            float hiHat = CreateHiHat(sampleIndex, eighthLocal);

            float loopFade = Mathf.Min(
                Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / 0.035f)),
                Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((loopDuration - time) / 0.035f)));

            samples[sampleIndex] = Mathf.Clamp(
                (pad + bass + arpeggioTone + kick + snare + hiHat) * loopFade,
                -0.92f,
                0.92f);
        }

        AudioClip clip = AudioClip.Create("Rooftop Pulse", sampleCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static float CreateKick(float localTime)
    {
        if (localTime >= 0.2f)
        {
            return 0f;
        }

        float phase = 2f * Mathf.PI * (76f * localTime - 92f * localTime * localTime);
        return Mathf.Sin(phase) * Mathf.Exp(-18f * localTime) * 0.28f;
    }

    private static float CreateSnare(int sampleIndex, int beatIndex, float localTime)
    {
        if ((beatIndex % 4 != 1 && beatIndex % 4 != 3) || localTime >= 0.16f)
        {
            return 0f;
        }

        float envelope = Mathf.Exp(-22f * localTime);
        float body = Mathf.Sin(2f * Mathf.PI * 185f * localTime) * 0.035f;
        return (Noise(sampleIndex) * 0.085f + body) * envelope;
    }

    private static float CreateHiHat(int sampleIndex, float localTime)
    {
        if (localTime >= 0.065f)
        {
            return 0f;
        }

        return Noise(sampleIndex * 3 + 17) * Mathf.Exp(-52f * localTime) * 0.035f;
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
