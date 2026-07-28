using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public enum RunnerTouchGesture
{
    None,
    SwipeLeft,
    SwipeRight,
    SwipeUp,
    SwipeDown
}

public sealed class RunnerTouchInput : MonoBehaviour
{
    public const float MinimumSwipePixels = 36f;
    public const float MinimumSwipeInches = 0.11f;
    public const float MaximumSwipeDuration = 1.1f;
    public const float AxisDominance = 1.1f;

    private RunnerMotor motor;
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
    private int activeFingerId = -1;
    private Vector2 touchStartPosition;
    private float touchStartTime;
    private bool ignoreActiveTouch;

    public bool IsTrackingTouch => activeFingerId >= 0;

    public static RunnerTouchInput AttachTo(GameObject host, RunnerMotor configuredMotor)
    {
        RunnerTouchInput touchInput = host.GetComponent<RunnerTouchInput>();
        if (touchInput == null)
        {
            touchInput = host.AddComponent<RunnerTouchInput>();
        }

        touchInput.motor = configuredMotor;
        touchInput.ResetTracking();
        return touchInput;
    }

    public void Tick(bool gameplayEnabled)
    {
        if (!gameplayEnabled)
        {
            ResetTracking();
            return;
        }

        if (!IsTrackingTouch)
        {
            BeginFirstAvailableTouch();
        }

        if (!IsTrackingTouch)
        {
            return;
        }

        for (int touchIndex = 0; touchIndex < Input.touchCount; touchIndex++)
        {
            Touch touch = Input.GetTouch(touchIndex);
            if (touch.fingerId != activeFingerId)
            {
                continue;
            }

            if (touch.phase == TouchPhase.Canceled)
            {
                ResetTracking();
                return;
            }

            bool gesturePhase = touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Ended;
            if (gesturePhase && !ignoreActiveTouch && TrySubmitGesture(
                    touch.position - touchStartPosition,
                    Time.unscaledTime - touchStartTime,
                    Screen.dpi))
            {
                ResetTracking();
                return;
            }

            if (touch.phase == TouchPhase.Ended)
            {
                ResetTracking();
                return;
            }
        }
    }

    public bool TrySubmitGesture(Vector2 delta, float duration, float screenDpi)
    {
        return SubmitGesture(ClassifyGesture(delta, duration, screenDpi));
    }

    public bool SubmitGesture(RunnerTouchGesture gesture)
    {
        if (motor == null || gesture == RunnerTouchGesture.None)
        {
            return false;
        }

        switch (gesture)
        {
            case RunnerTouchGesture.SwipeLeft:
                motor.RequestLaneChange(-1);
                break;
            case RunnerTouchGesture.SwipeRight:
                motor.RequestLaneChange(1);
                break;
            case RunnerTouchGesture.SwipeUp:
                motor.RequestJump();
                break;
            case RunnerTouchGesture.SwipeDown:
                motor.RequestSlide();
                break;
        }

        return true;
    }

    public static RunnerTouchGesture ClassifyGesture(Vector2 delta, float duration, float screenDpi)
    {
        if (duration < 0f || duration > MaximumSwipeDuration)
        {
            return RunnerTouchGesture.None;
        }

        float dpiThreshold = screenDpi > 0f ? screenDpi * MinimumSwipeInches : 0f;
        float threshold = Mathf.Max(MinimumSwipePixels, dpiThreshold);
        float horizontalDistance = Mathf.Abs(delta.x);
        float verticalDistance = Mathf.Abs(delta.y);

        if (horizontalDistance < threshold && verticalDistance < threshold)
        {
            return RunnerTouchGesture.None;
        }

        if (horizontalDistance >= verticalDistance * AxisDominance)
        {
            return delta.x < 0f ? RunnerTouchGesture.SwipeLeft : RunnerTouchGesture.SwipeRight;
        }

        if (verticalDistance >= horizontalDistance * AxisDominance)
        {
            return delta.y < 0f ? RunnerTouchGesture.SwipeDown : RunnerTouchGesture.SwipeUp;
        }

        return RunnerTouchGesture.None;
    }

    private void BeginFirstAvailableTouch()
    {
        for (int touchIndex = 0; touchIndex < Input.touchCount; touchIndex++)
        {
            Touch touch = Input.GetTouch(touchIndex);
            if (touch.phase != TouchPhase.Began)
            {
                continue;
            }

            activeFingerId = touch.fingerId;
            touchStartPosition = touch.position;
            touchStartTime = Time.unscaledTime;
            ignoreActiveTouch = IsTouchOverUi(touch.position);
            return;
        }
    }

    private bool IsTouchOverUi(Vector2 position)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return false;
        }

        PointerEventData pointer = new PointerEventData(eventSystem)
        {
            position = position
        };
        uiRaycastResults.Clear();
        eventSystem.RaycastAll(pointer, uiRaycastResults);
        return uiRaycastResults.Count > 0;
    }

    private void ResetTracking()
    {
        activeFingerId = -1;
        touchStartPosition = Vector2.zero;
        touchStartTime = 0f;
        ignoreActiveTouch = false;
    }
}
