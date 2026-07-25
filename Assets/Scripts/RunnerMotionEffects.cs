using UnityEngine;

public sealed class RunnerMotionEffects : MonoBehaviour
{
    private ParticleSystem dustParticles;
    private ParticleSystem sparkParticles;
    private ParticleSystem trailParticles;
    private RunnerMotor motor;
    private float sparkTimer;
    private float trailTimer;
    private int sequence;

    public ParticleSystem DustParticles => dustParticles;
    public ParticleSystem SparkParticles => sparkParticles;
    public ParticleSystem TrailParticles => trailParticles;
    public int FootstepEmissionCount { get; private set; }
    public int LandingEmissionCount { get; private set; }
    public int SlideEmissionCount { get; private set; }
    public int TrailEmissionCount { get; private set; }

    public void Configure(
        RunnerMotor configuredMotor,
        Material dustMaterial,
        Material sparkMaterial,
        Material trailMaterial)
    {
        motor = configuredMotor;
        dustParticles = CreateParticleSystem("Runner Dust Particles", dustMaterial, 72);
        sparkParticles = CreateParticleSystem("Runner Slide Sparks", sparkMaterial, 64);
        trailParticles = CreateParticleSystem("Runner Speed Trail", trailMaterial, 96);
        ResetForRun();
    }

    public void ResetForRun()
    {
        sparkTimer = 0f;
        trailTimer = 0f;
        sequence = 0;
        FootstepEmissionCount = 0;
        LandingEmissionCount = 0;
        SlideEmissionCount = 0;
        TrailEmissionCount = 0;

        Clear(dustParticles);
        Clear(sparkParticles);
        Clear(trailParticles);
    }

    public void Tick(float speed, bool isRunning, float deltaTime)
    {
        if (motor == null || deltaTime <= 0f)
        {
            return;
        }

        if (motor.LandedThisFrame)
        {
            EmitLanding();
        }

        if (!isRunning)
        {
            return;
        }

        if (motor.State == RunnerActionState.Sliding)
        {
            sparkTimer -= deltaTime;
            if (sparkTimer <= 0f)
            {
                EmitSlideSparks(4);
                sparkTimer = 0.055f;
            }
        }
        else
        {
            sparkTimer = 0f;
        }

        float speedAmount = Mathf.InverseLerp(9f, RunnerPatternCatalog.MaximumRunnerSpeed, speed);
        trailTimer -= deltaTime;
        if (speedAmount > 0.08f && trailTimer <= 0f)
        {
            EmitTrail(Mathf.Lerp(0.08f, 0.045f, speedAmount));
        }
    }

    public void EmitFootstep(int footSide)
    {
        Vector3 basePosition = transform.position + new Vector3(footSide * 0.2f, 0.06f, -0.2f);
        EmitDust(basePosition, 2, 0.22f, 0.06f);
        FootstepEmissionCount++;
    }

    public void EmitLanding()
    {
        EmitDust(transform.position + new Vector3(0f, 0.06f, 0f), 12, 0.46f, 0.1f);
        LandingEmissionCount++;
    }

    private void EmitSlideSparks(int count)
    {
        for (int index = 0; index < count; index++)
        {
            float offset = SignedWave(sequence++) * 0.34f;
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
            {
                position = transform.position + new Vector3(offset, 0.1f, -0.28f),
                velocity = new Vector3(offset * 2f, 0.7f + Mathf.Abs(offset), -3.2f),
                startLifetime = 0.2f + Mathf.Abs(offset) * 0.15f,
                startSize = 0.045f,
                startColor = new Color(1f, 0.66f, 0.12f, 1f)
            };
            sparkParticles.Emit(emit, 1);
        }

        SlideEmissionCount += count;
    }

    private void EmitTrail(float interval)
    {
        float offset = SignedWave(sequence++) * 0.22f;
        ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
        {
            position = transform.position + new Vector3(offset, 0.9f, -0.5f),
            velocity = new Vector3(0f, 0.12f, -1.8f),
            startLifetime = 0.34f,
            startSize = 0.075f,
            startColor = new Color(0.22f, 0.88f, 0.94f, 0.5f)
        };
        trailParticles.Emit(emit, 1);
        TrailEmissionCount++;
        trailTimer = interval;
    }

    private void EmitDust(Vector3 basePosition, int count, float radius, float height)
    {
        for (int index = 0; index < count; index++)
        {
            float angle = (sequence++ * 2.399963f) % (Mathf.PI * 2f);
            float amount = 0.35f + Mathf.Abs(SignedWave(sequence)) * 0.65f;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
            {
                position = basePosition + direction * radius * 0.24f,
                velocity = direction * radius * 2.4f + Vector3.up * height * 5f,
                startLifetime = 0.28f + amount * 0.18f,
                startSize = 0.09f + amount * 0.08f,
                startColor = new Color(0.75f, 0.68f, 0.54f, 0.62f)
            };
            dustParticles.Emit(emit, 1);
        }
    }

    private ParticleSystem CreateParticleSystem(string objectName, Material material, int maximumParticles)
    {
        GameObject particleObject = new GameObject(objectName);
        particleObject.transform.SetParent(transform, false);
        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = false;
        main.maxParticles = maximumParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = false;

        ParticleSystemRenderer particleRenderer = particleObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sharedMaterial = material;
        return particles;
    }

    private static void Clear(ParticleSystem particles)
    {
        if (particles != null)
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private static float SignedWave(int value)
    {
        return Mathf.Sin(value * 12.9898f + 78.233f);
    }
}
