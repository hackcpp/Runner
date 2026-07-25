using UnityEngine;

public sealed class RunnerVisualRig : MonoBehaviour
{
    private const float LandingPulseDuration = 0.14f;

    private Transform rigRoot;
    private Transform torso;
    private Transform head;
    private Transform leftArm;
    private Transform rightArm;
    private Transform leftLeg;
    private Transform rightLeg;
    private RunnerMotor motor;
    private float gaitPhase;
    private float stepTimer;
    private float landingPulseRemaining;
    private float idlePhase;
    private int nextFoot;

    public RunnerActionState PoseState { get; private set; } = RunnerActionState.Grounded;
    public bool FootstepThisFrame { get; private set; }
    public int FootstepSideThisFrame { get; private set; }
    public int TotalFootstepEvents { get; private set; }
    public float LandingPulse => landingPulseRemaining;

    public void Configure(
        RunnerMotor configuredMotor,
        Transform visualRoot,
        Material primaryMaterial,
        Material accentMaterial,
        Material darkMaterial)
    {
        motor = configuredMotor;
        BuildRig(visualRoot, primaryMaterial, accentMaterial, darkMaterial);
        ResetForRun();
    }

    public void ResetForRun()
    {
        gaitPhase = 0f;
        stepTimer = 0.12f;
        landingPulseRemaining = 0f;
        idlePhase = 0f;
        nextFoot = -1;
        FootstepThisFrame = false;
        FootstepSideThisFrame = 0;
        TotalFootstepEvents = 0;
        PoseState = RunnerActionState.Grounded;

        if (rigRoot == null)
        {
            return;
        }

        rigRoot.localPosition = Vector3.zero;
        rigRoot.localRotation = Quaternion.identity;
        torso.localRotation = Quaternion.Euler(6f, 0f, 0f);
        head.localRotation = Quaternion.identity;
        leftArm.localRotation = Quaternion.identity;
        rightArm.localRotation = Quaternion.identity;
        leftLeg.localRotation = Quaternion.identity;
        rightLeg.localRotation = Quaternion.identity;
    }

    public void Tick(float speed, bool isRunning, float deltaTime)
    {
        FootstepThisFrame = false;
        FootstepSideThisFrame = 0;

        if (motor == null || rigRoot == null || deltaTime <= 0f)
        {
            return;
        }

        PoseState = motor.State;
        if (motor.LandedThisFrame)
        {
            landingPulseRemaining = LandingPulseDuration;
        }

        float speedAmount = Mathf.InverseLerp(9f, RunnerPatternCatalog.MaximumRunnerSpeed, speed);
        float gait = 0f;
        if (isRunning && motor.State == RunnerActionState.Grounded)
        {
            float cadence = Mathf.Lerp(11.5f, 15.5f, speedAmount);
            gaitPhase += cadence * deltaTime;
            gait = Mathf.Sin(gaitPhase);
            TickFootsteps(speedAmount, deltaTime);
        }
        else
        {
            idlePhase += deltaTime * 2.2f;
            stepTimer = Mathf.Max(stepTimer, 0.08f);
        }

        ApplyPose(gait, speedAmount, isRunning, deltaTime);
        landingPulseRemaining = Mathf.Max(0f, landingPulseRemaining - deltaTime);
    }

    private void BuildRig(
        Transform visualRoot,
        Material primaryMaterial,
        Material accentMaterial,
        Material darkMaterial)
    {
        GameObject rootObject = new GameObject("Runner Character Rig");
        rigRoot = rootObject.transform;
        rigRoot.SetParent(visualRoot, false);

        CreatePart("Runner Hips", rigRoot, new Vector3(0f, 0.59f, 0f), new Vector3(0.58f, 0.28f, 0.36f), darkMaterial);

        torso = CreatePivot("Runner Torso Pivot", rigRoot, new Vector3(0f, 0.85f, 0f));
        CreatePart("Runner Torso", torso, new Vector3(0f, 0.28f, 0f), new Vector3(0.72f, 0.66f, 0.42f), primaryMaterial);
        CreatePart("Runner Chest Mark", torso, new Vector3(0f, 0.31f, 0.225f), new Vector3(0.34f, 0.22f, 0.04f), accentMaterial);
        CreatePart("Runner Back Pack", torso, new Vector3(0f, 0.25f, -0.29f), new Vector3(0.42f, 0.46f, 0.2f), darkMaterial);
        CreatePart("Runner Back Stripe", torso, new Vector3(0f, 0.28f, -0.405f), new Vector3(0.13f, 0.34f, 0.035f), accentMaterial);

        head = CreatePivot("Runner Head Pivot", rigRoot, new Vector3(0f, 1.55f, 0f));
        CreatePart("Runner Head", head, Vector3.zero, new Vector3(0.48f, 0.45f, 0.44f), darkMaterial);
        CreatePart("Runner Visor", head, new Vector3(0f, 0.035f, 0.245f), new Vector3(0.38f, 0.15f, 0.07f), accentMaterial);

        leftArm = CreateLimb("Runner Left Arm", rigRoot, new Vector3(-0.47f, 1.35f, 0f), primaryMaterial, true);
        rightArm = CreateLimb("Runner Right Arm", rigRoot, new Vector3(0.47f, 1.35f, 0f), primaryMaterial, true);
        leftLeg = CreateLimb("Runner Left Leg", rigRoot, new Vector3(-0.2f, 0.58f, 0f), primaryMaterial, false);
        rightLeg = CreateLimb("Runner Right Leg", rigRoot, new Vector3(0.2f, 0.58f, 0f), primaryMaterial, false);
    }

    private Transform CreateLimb(
        string limbName,
        Transform parent,
        Vector3 pivotPosition,
        Material material,
        bool isArm)
    {
        Transform pivot = CreatePivot(limbName + " Pivot", parent, pivotPosition);
        float length = isArm ? 0.54f : 0.5f;
        float width = isArm ? 0.17f : 0.21f;
        CreatePart(limbName, pivot, new Vector3(0f, -length * 0.5f, 0f), new Vector3(width, length, width), material);

        if (isArm)
        {
            CreatePart(limbName + " Hand", pivot, new Vector3(0f, -0.59f, 0.045f), new Vector3(0.19f, 0.17f, 0.2f), material);
        }
        else
        {
            CreatePart(limbName + " Foot", pivot, new Vector3(0f, -0.53f, 0.12f), new Vector3(0.24f, 0.12f, 0.42f), material);
        }

        return pivot;
    }

    private static Transform CreatePivot(string objectName, Transform parent, Vector3 localPosition)
    {
        GameObject pivotObject = new GameObject(objectName);
        Transform pivot = pivotObject.transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = localPosition;
        return pivot;
    }

    private static void CreatePart(
        string objectName,
        Transform parent,
        Vector3 localPosition,
        Vector3 localScale,
        Material darkMaterial)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = objectName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;
        RunnerWorldPool.RemovePhysicsCollider(part);

        Renderer renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = darkMaterial;
    }

    private void TickFootsteps(float speedAmount, float deltaTime)
    {
        stepTimer -= deltaTime;
        if (stepTimer > 0f)
        {
            return;
        }

        float interval = Mathf.Lerp(0.3f, 0.22f, speedAmount);
        stepTimer += interval;
        nextFoot = -nextFoot;
        FootstepThisFrame = true;
        FootstepSideThisFrame = nextFoot;
        TotalFootstepEvents++;
    }

    private void ApplyPose(float gait, float speedAmount, bool isRunning, float deltaTime)
    {
        Vector3 rootPosition;
        Quaternion rootRotation;
        Quaternion torsoRotation;
        Quaternion headRotation;
        Quaternion leftArmRotation;
        Quaternion rightArmRotation;
        Quaternion leftLegRotation;
        Quaternion rightLegRotation;

        if (motor.State == RunnerActionState.Sliding)
        {
            rootPosition = new Vector3(0f, -0.2f, 0.18f);
            rootRotation = Quaternion.Euler(0f, 0f, -4f);
            torsoRotation = Quaternion.Euler(42f, 0f, 0f);
            headRotation = Quaternion.Euler(-18f, 0f, 0f);
            leftArmRotation = Quaternion.Euler(-68f, 0f, -12f);
            rightArmRotation = Quaternion.Euler(-82f, 0f, 12f);
            leftLegRotation = Quaternion.Euler(66f, 0f, -8f);
            rightLegRotation = Quaternion.Euler(35f, 0f, 8f);
        }
        else if (motor.State == RunnerActionState.Airborne)
        {
            rootPosition = new Vector3(0f, 0.04f, 0f);
            rootRotation = Quaternion.Euler(-3f, 0f, 0f);
            torsoRotation = Quaternion.Euler(-8f, 0f, 0f);
            headRotation = Quaternion.Euler(7f, 0f, 0f);
            leftArmRotation = Quaternion.Euler(-42f, 0f, -14f);
            rightArmRotation = Quaternion.Euler(-42f, 0f, 14f);
            leftLegRotation = Quaternion.Euler(24f, 0f, -7f);
            rightLegRotation = Quaternion.Euler(-18f, 0f, 7f);
        }
        else
        {
            float landingAmount = landingPulseRemaining / LandingPulseDuration;
            float bob = isRunning ? Mathf.Abs(gait) * 0.035f : Mathf.Sin(idlePhase) * 0.014f;
            rootPosition = new Vector3(0f, bob - landingAmount * 0.08f, 0f);
            rootRotation = Quaternion.Euler(0f, 0f, gait * 1.6f);
            torsoRotation = Quaternion.Euler(5f + speedAmount * 4f - landingAmount * 8f, 0f, -gait * 3f);
            headRotation = Quaternion.Euler(-gait * 1.5f, 0f, gait * 2f);
            leftArmRotation = Quaternion.Euler(gait * 46f, 0f, -4f);
            rightArmRotation = Quaternion.Euler(-gait * 46f, 0f, 4f);
            leftLegRotation = Quaternion.Euler(-gait * 34f + landingAmount * 10f, 0f, -2f);
            rightLegRotation = Quaternion.Euler(gait * 34f + landingAmount * 10f, 0f, 2f);
        }

        float blend = 1f - Mathf.Exp(-20f * deltaTime);
        rigRoot.localPosition = Vector3.Lerp(rigRoot.localPosition, rootPosition, blend);
        rigRoot.localRotation = Quaternion.Slerp(rigRoot.localRotation, rootRotation, blend);
        torso.localRotation = Quaternion.Slerp(torso.localRotation, torsoRotation, blend);
        head.localRotation = Quaternion.Slerp(head.localRotation, headRotation, blend);
        leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, leftArmRotation, blend);
        rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation, rightArmRotation, blend);
        leftLeg.localRotation = Quaternion.Slerp(leftLeg.localRotation, leftLegRotation, blend);
        rightLeg.localRotation = Quaternion.Slerp(rightLeg.localRotation, rightLegRotation, blend);
    }
}
