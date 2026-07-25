using UnityEngine;

public sealed class RunnerCameraRig : MonoBehaviour
{
    private const float BaseFieldOfView = 58f;
    private const float MaximumSpeedFieldOfView = 64f;

    private Camera controlledCamera;
    private Vector3 followOffset;
    private float fieldOfViewPulse;
    private float impactRemaining;
    private float impactDuration;
    private float roll;

    public static RunnerCameraRig AttachTo(Camera targetCamera, Vector3 cameraOffset)
    {
        RunnerCameraRig rig = targetCamera.GetComponent<RunnerCameraRig>();
        if (rig == null)
        {
            rig = targetCamera.gameObject.AddComponent<RunnerCameraRig>();
        }

        rig.controlledCamera = targetCamera;
        rig.followOffset = cameraOffset;
        rig.ResetFeedback();
        return rig;
    }

    public void ResetFeedback()
    {
        fieldOfViewPulse = 0f;
        impactRemaining = 0f;
        impactDuration = 0f;
        roll = 0f;
    }

    public void PulseFieldOfView(float amount)
    {
        fieldOfViewPulse = Mathf.Max(fieldOfViewPulse, amount);
    }

    public void TriggerImpact(float duration, float fovPulse)
    {
        if (duration > impactRemaining)
        {
            impactRemaining = duration;
            impactDuration = duration;
        }

        PulseFieldOfView(fovPulse);
    }

    public void Tick(
        Vector3 targetPosition,
        float targetLaneX,
        float speedAmount,
        bool isPlaying,
        bool snap = false)
    {
        if (controlledCamera == null)
        {
            return;
        }

        Vector3 desiredPosition = targetPosition + followOffset;
        if (impactRemaining > 0f && !snap)
        {
            float shake = impactDuration <= 0f ? 0f : impactRemaining / impactDuration;
            desiredPosition += new Vector3(
                Mathf.Sin(Time.unscaledTime * 82f) * 0.16f * shake,
                Mathf.Cos(Time.unscaledTime * 67f) * 0.1f * shake,
                0f);
        }

        float positionBlend = snap ? 1f : 1f - Mathf.Exp(-8f * Time.deltaTime);
        controlledCamera.transform.position = Vector3.Lerp(
            controlledCamera.transform.position,
            desiredPosition,
            positionBlend);
        controlledCamera.transform.LookAt(targetPosition + new Vector3(0f, 1.1f, 8.5f));

        float laneOffset = targetLaneX - targetPosition.x;
        float targetRoll = isPlaying ? Mathf.Clamp(-laneOffset * 2.1f, -4.5f, 4.5f) : 0f;
        float rollBlend = snap ? 1f : 1f - Mathf.Exp(-10f * Time.deltaTime);
        roll = Mathf.Lerp(roll, targetRoll, rollBlend);
        controlledCamera.transform.rotation *= Quaternion.Euler(0f, 0f, roll);

        float targetFieldOfView = Mathf.Lerp(
            BaseFieldOfView,
            MaximumSpeedFieldOfView,
            Mathf.Clamp01(speedAmount)) + fieldOfViewPulse;
        float fieldOfViewBlend = snap ? 1f : 1f - Mathf.Exp(-4f * Time.deltaTime);
        controlledCamera.fieldOfView = Mathf.Lerp(
            controlledCamera.fieldOfView,
            targetFieldOfView,
            fieldOfViewBlend);

        fieldOfViewPulse = Mathf.MoveTowards(fieldOfViewPulse, 0f, Time.deltaTime * 4.5f);
        impactRemaining = Mathf.Max(0f, impactRemaining - Time.deltaTime);
    }
}
