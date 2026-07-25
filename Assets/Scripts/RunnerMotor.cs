using UnityEngine;

public sealed class RunnerMotor : MonoBehaviour
{
    public const float JumpVelocity = 8.8f;
    public const float Gravity = -24f;
    public const float JumpBufferDuration = 0.12f;
    public const float SlideDuration = 0.65f;
    public const float StandingBodyHeight = 1.82f;
    public const float SlidingBodyHeight = 0.76f;

    private const float LaneMoveSpeed = 13.5f;

    private Transform visualRoot;
    private Transform body;
    private Transform shadow;
    private float laneWidth;
    private float verticalVelocity;
    private float jumpBufferRemaining;
    private float slideRemaining;
    private float landingFeedbackRemaining;
    private bool jumpRequested;
    private bool slideRequested;

    public RunnerActionState State { get; private set; } = RunnerActionState.Grounded;
    public int Lane { get; private set; } = 1;
    public float FeetHeight => transform.position.y;
    public float BodyHeight => State == RunnerActionState.Sliding ? SlidingBodyHeight : StandingBodyHeight;
    public bool JumpStartedThisFrame { get; private set; }
    public bool SlideStartedThisFrame { get; private set; }
    public bool LandedThisFrame { get; private set; }

    public void Configure(Transform runnerVisualRoot, Transform runnerBody, Transform runnerShadow, float configuredLaneWidth)
    {
        visualRoot = runnerVisualRoot;
        body = runnerBody;
        shadow = runnerShadow;
        laneWidth = configuredLaneWidth;
        ResetForRun();
    }

    public void ResetForRun()
    {
        State = RunnerActionState.Grounded;
        Lane = 1;
        verticalVelocity = 0f;
        jumpBufferRemaining = 0f;
        slideRemaining = 0f;
        landingFeedbackRemaining = 0f;
        jumpRequested = false;
        slideRequested = false;
        JumpStartedThisFrame = false;
        SlideStartedThisFrame = false;
        LandedThisFrame = false;

        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        ResetVisuals();
    }

    public void RequestJump()
    {
        jumpRequested = true;
        jumpBufferRemaining = JumpBufferDuration;
    }

    public void RequestSlide()
    {
        slideRequested = true;
    }

    public void Tick(float forwardSpeed)
    {
        ReadInput();
        Tick(forwardSpeed, Time.deltaTime);
    }

    public void Tick(float forwardSpeed, float deltaTime)
    {
        JumpStartedThisFrame = false;
        SlideStartedThisFrame = false;
        LandedThisFrame = false;

        if (deltaTime <= 0f)
        {
            return;
        }

        if (jumpBufferRemaining > 0f)
        {
            jumpBufferRemaining -= deltaTime;
        }

        if (jumpRequested)
        {
            jumpBufferRemaining = State == RunnerActionState.Grounded ? JumpBufferDuration : 0f;
            jumpRequested = false;
        }

        if (jumpBufferRemaining > 0f && State == RunnerActionState.Grounded)
        {
            State = RunnerActionState.Airborne;
            verticalVelocity = JumpVelocity;
            jumpBufferRemaining = 0f;
            JumpStartedThisFrame = true;
        }

        if (slideRequested)
        {
            if (State == RunnerActionState.Grounded)
            {
                State = RunnerActionState.Sliding;
                slideRemaining = SlideDuration;
                SlideStartedThisFrame = true;
            }

            slideRequested = false;
        }

        if (State == RunnerActionState.Sliding)
        {
            slideRemaining -= deltaTime;
            if (slideRemaining <= 0f)
            {
                State = RunnerActionState.Grounded;
            }
        }

        Vector3 position = transform.position;
        position.z += forwardSpeed * deltaTime;
        position.x = Mathf.MoveTowards(position.x, LaneX(Lane), LaneMoveSpeed * deltaTime);

        if (State == RunnerActionState.Airborne)
        {
            verticalVelocity += Gravity * deltaTime;
            position.y += verticalVelocity * deltaTime;

            if (position.y <= 0f && verticalVelocity < 0f)
            {
                position.y = 0f;
                verticalVelocity = 0f;
                State = RunnerActionState.Grounded;
                LandedThisFrame = true;
                landingFeedbackRemaining = 0.12f;
            }
        }
        else
        {
            position.y = 0f;
        }

        transform.position = position;

        float tilt = Mathf.Clamp((LaneX(Lane) - position.x) * 8f, -12f, 12f);
        transform.rotation = Quaternion.Euler(0f, 0f, tilt);

        landingFeedbackRemaining = Mathf.Max(0f, landingFeedbackRemaining - deltaTime);
        UpdateVisuals(deltaTime);
    }

    public void FreezeVisuals()
    {
        jumpBufferRemaining = 0f;
        jumpRequested = false;
        slideRequested = false;
        UpdateVisuals(Time.deltaTime);
    }

    private void ReadInput()
    {
        bool moveLeft = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow);
        bool moveRight = Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow);

        if (moveLeft)
        {
            Lane = Mathf.Max(0, Lane - 1);
        }
        else if (moveRight)
        {
            Lane = Mathf.Min(2, Lane + 1);
        }

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            RequestJump();
        }

        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            RequestSlide();
        }
    }

    private void UpdateVisuals(float deltaTime)
    {
        if (visualRoot == null)
        {
            return;
        }

        Vector3 targetScale = Vector3.one;
        Vector3 targetPosition = Vector3.zero;
        Quaternion targetRotation = Quaternion.identity;

        if (State == RunnerActionState.Sliding)
        {
            targetScale = new Vector3(1.08f, 0.54f, 1.22f);
            targetPosition = new Vector3(0f, 0.1f, 0.2f);
            targetRotation = Quaternion.Euler(68f, 0f, 0f);
        }
        else if (State == RunnerActionState.Airborne)
        {
            float stretch = Mathf.Clamp(verticalVelocity * 0.018f, -0.08f, 0.1f);
            targetScale = new Vector3(1f - stretch * 0.45f, 1f + stretch, 1f - stretch * 0.45f);
        }
        else if (landingFeedbackRemaining > 0f)
        {
            float amount = landingFeedbackRemaining / 0.12f;
            targetScale = new Vector3(1f + amount * 0.16f, 1f - amount * 0.18f, 1f + amount * 0.16f);
        }

        float blend = 1f - Mathf.Exp(-18f * deltaTime);
        visualRoot.localScale = Vector3.Lerp(visualRoot.localScale, targetScale, blend);
        visualRoot.localPosition = Vector3.Lerp(visualRoot.localPosition, targetPosition, blend);
        visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation, targetRotation, blend);

        if (body != null && State != RunnerActionState.Sliding)
        {
            body.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 14f) * 3f, 0f, 0f);
        }

        if (shadow != null)
        {
            Vector3 worldPosition = transform.position;
            shadow.position = new Vector3(worldPosition.x, 0.03f, worldPosition.z);
            float shadowScale = Mathf.Lerp(0.54f, 0.82f, 1f - Mathf.Clamp01(FeetHeight / 1.8f));
            shadow.localScale = new Vector3(shadowScale, 0.025f, shadowScale);
        }
    }

    private void ResetVisuals()
    {
        if (visualRoot != null)
        {
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = Vector3.one;
        }

        if (body != null)
        {
            body.localRotation = Quaternion.identity;
        }

        if (shadow != null)
        {
            shadow.localPosition = new Vector3(0f, 0.03f, 0f);
            shadow.localScale = new Vector3(0.82f, 0.025f, 0.82f);
        }
    }

    private float LaneX(int lane)
    {
        return (lane - 1) * laneWidth;
    }
}
