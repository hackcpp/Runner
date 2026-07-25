using UnityEngine;

public sealed class ProceduralRunnerSfx
{
    private const int SampleRate = 22050;

    private readonly AudioSource source;
    private readonly AudioClip jumpClip;
    private readonly AudioClip slideClip;
    private readonly AudioClip clearClip;
    private readonly AudioClip crashClip;

    private ProceduralRunnerSfx(GameObject host)
    {
        source = host.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = 0.55f;
        source.priority = 24;

        jumpClip = CreateTone("Runner Jump", 0.16f, 280f, 620f, 0.36f, false);
        slideClip = CreateTone("Runner Slide", 0.2f, 190f, 90f, 0.3f, true);
        clearClip = CreateTone("Runner Clear", 0.18f, 540f, 880f, 0.34f, false);
        crashClip = CreateTone("Runner Crash", 0.34f, 150f, 48f, 0.48f, true);
    }

    public static ProceduralRunnerSfx AttachTo(GameObject host)
    {
        return new ProceduralRunnerSfx(host);
    }

    public void PlayJump()
    {
        source.pitch = 1f;
        source.PlayOneShot(jumpClip);
    }

    public void PlaySlide()
    {
        source.pitch = 1f;
        source.PlayOneShot(slideClip);
    }

    public void PlayClear(int multiplier)
    {
        source.pitch = Mathf.Lerp(1f, 1.18f, Mathf.InverseLerp(1f, RunnerComboTracker.MaximumMultiplier, multiplier));
        source.PlayOneShot(clearClip);
    }

    public void PlayCrash()
    {
        source.pitch = 1f;
        source.PlayOneShot(crashClip);
    }

    private static AudioClip CreateTone(
        string clipName,
        float duration,
        float startFrequency,
        float endFrequency,
        float amplitude,
        bool addNoise)
    {
        int sampleCount = Mathf.CeilToInt(duration * SampleRate);
        float[] samples = new float[sampleCount];
        float phase = 0f;

        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            float progress = sampleIndex / (float)sampleCount;
            float frequency = Mathf.Lerp(startFrequency, endFrequency, progress);
            phase += 2f * Mathf.PI * frequency / SampleRate;
            float envelope = Mathf.Pow(1f - progress, 1.6f);
            float tone = Mathf.Sin(phase) + Mathf.Sin(phase * 2.01f) * 0.18f;
            float noise = addNoise ? Noise(sampleIndex + clipName.Length * 97) * 0.3f : 0f;
            samples[sampleIndex] = Mathf.Clamp((tone + noise) * envelope * amplitude, -0.9f, 0.9f);
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
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
