using UnityEngine;

public class SoulsIntentRuntimeController : MonoBehaviour
{
    [Header("Profile")]
    [SerializeField] private BassSoulsControlProfile profile;

    [Header("Debug")]
    [SerializeField] private bool printDebug = true;

    private SoulsControlState currentState;

    private float moveForwardUntil;
    private float moveBackwardUntil;
    private float moveLeftUntil;
    private float moveRightUntil;

    private float turnLeftUntil;
    private float turnRightUntil;

    private float lightAttackUntil;
    private float rollUntil;

    public SoulsControlState CurrentState => currentState;

    private void Awake()
    {
        Application.runInBackground = true;

        if (profile != null)
        {
            currentState = profile.initialState;
        }
    }

    private void Update()
    {
        if (profile == null)
        {
            return;
        }

        ApplyCurrentIntentStateToGamepad();
    }

    public void ReceiveIntent(SoulsIntent intent, BassNoteDefinition sourceNote, float sourceFrequency)
    {
        if (profile == null)
        {
            return;
        }

        float now = Time.time;

        switch (intent)
        {
            case SoulsIntent.MoveForward:
                moveForwardUntil = now + profile.movementHoldSeconds;
                break;

            case SoulsIntent.MoveBackward:
                moveBackwardUntil = now + profile.movementHoldSeconds;
                break;

            case SoulsIntent.MoveLeft:
                moveLeftUntil = now + profile.movementHoldSeconds;
                break;

            case SoulsIntent.MoveRight:
                moveRightUntil = now + profile.movementHoldSeconds;
                break;

            case SoulsIntent.TurnLeft:
                turnLeftUntil = now + profile.movementHoldSeconds;
                break;

            case SoulsIntent.TurnRight:
                turnRightUntil = now + profile.movementHoldSeconds;
                break;

            case SoulsIntent.LightAttack:
                lightAttackUntil = now + profile.attackHoldSeconds;
                break;

            case SoulsIntent.Roll:
                rollUntil = now + profile.attackHoldSeconds;
                break;
        }

        if (printDebug)
        {
            string noteName = sourceNote != null ? sourceNote.noteName : "None";

            Debug.Log(
                $"SOULS INTENT RECEIVED | " +
                $"State: {currentState} | " +
                $"Note: {noteName} | " +
                $"Frequency: {sourceFrequency:F2} Hz | " +
                $"Intent: {intent}"
            );
        }
    }

    public void SetState(SoulsControlState newState)
    {
        currentState = newState;

        if (printDebug)
        {
            Debug.Log($"Souls control state changed to: {currentState}");
        }
    }

    private void ApplyCurrentIntentStateToGamepad()
    {
        float now = Time.time;

        float leftStickX = 0f;
        float leftStickY = 0f;

        if (now < moveForwardUntil)
        {
            leftStickY = profile.stickStrength;
        }

        if (now < moveBackwardUntil)
        {
            leftStickY = -profile.stickStrength;
        }

        if (now < moveLeftUntil)
        {
            leftStickX = -profile.stickStrength;
        }

        if (now < moveRightUntil)
        {
            leftStickX = profile.stickStrength;
        }

        if (now < turnLeftUntil)
        {
            leftStickX = -profile.stickStrength;
        }

        if (now < turnRightUntil)
        {
            leftStickX = profile.stickStrength;
        }

        bool lightAttackPressed = now < lightAttackUntil;
        bool rollPressed = now < rollUntil;

        VirtualGamepadOutput.State.SetLeftStick(leftStickX, leftStickY);
        VirtualGamepadOutput.State.SetButton(GamepadButton.RightShoulder, lightAttackPressed);
        VirtualGamepadOutput.State.SetButton(GamepadButton.B, rollPressed);
    }

    private void OnDisable()
    {
        VirtualGamepadOutput.State.SetLeftStick(0f, 0f);
        VirtualGamepadOutput.State.SetButton(GamepadButton.RightShoulder, false);
        VirtualGamepadOutput.State.SetButton(GamepadButton.B, false);
    }
}