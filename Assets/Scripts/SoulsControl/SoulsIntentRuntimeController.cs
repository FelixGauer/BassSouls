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
        else
        {
            currentState = SoulsControlState.Default;
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

        SoulsControlState stateBeforeIntent = currentState;

        if (intent == SoulsIntent.ToggleControlState)
        {
            ToggleControlState(sourceNote, sourceFrequency);
            return;
        }

        if (intent == SoulsIntent.ReturnToDefault)
        {
            ReturnToDefault(sourceNote, sourceFrequency);
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
                $"State: {stateBeforeIntent} | " +
                $"Note: {noteName} | " +
                $"Frequency: {sourceFrequency:F2} Hz | " +
                $"Intent: {intent}"
            );
        }
    }

    public void SetState(SoulsControlState newState)
    {
        SoulsControlState previousState = currentState;
        currentState = newState;

        if (printDebug)
        {
            Debug.Log(
                $"SOULS CONTROL STATE CHANGED | " +
                $"{previousState} → {currentState}"
            );
        }
    }

    private void ToggleControlState(BassNoteDefinition sourceNote, float sourceFrequency)
    {
        SoulsControlState previousState = currentState;

        if (currentState == SoulsControlState.Default)
        {
            currentState = SoulsControlState.LockedOn;
        }
        else
        {
            currentState = SoulsControlState.Default;
        }

        ClearMomentaryOutputs();

        if (printDebug)
        {
            string noteName = sourceNote != null ? sourceNote.noteName : "None";

            Debug.Log(
                $"SOULS CONTROL STATE TOGGLED | " +
                $"Note: {noteName} | " +
                $"Frequency: {sourceFrequency:F2} Hz | " +
                $"{previousState} → {currentState}"
            );
        }
    }

    private void ReturnToDefault(BassNoteDefinition sourceNote, float sourceFrequency)
    {
        SoulsControlState previousState = currentState;
        currentState = SoulsControlState.Default;

        ClearMomentaryOutputs();

        if (printDebug)
        {
            string noteName = sourceNote != null ? sourceNote.noteName : "None";

            Debug.Log(
                $"SOULS CONTROL STATE RESET | " +
                $"Note: {noteName} | " +
                $"Frequency: {sourceFrequency:F2} Hz | " +
                $"{previousState} → {currentState}"
            );
        }
    }

    private void ApplyCurrentIntentStateToGamepad()
    {
        float now = Time.time;

        float leftStickX = 0f;
        float leftStickY = 0f;

        float rightStickX = 0f;
        float rightStickY = 0f;

        // Character movement: left stick.
        if (now < moveForwardUntil)
        {
            leftStickY = profile.movementStickStrength;
        }

        if (now < moveBackwardUntil)
        {
            leftStickY = -profile.movementStickStrength;
        }

        if (now < moveLeftUntil)
        {
            leftStickX = -profile.movementStickStrength;
        }

        if (now < moveRightUntil)
        {
            leftStickX = profile.movementStickStrength;
        }

        // Camera turning: right stick.
        if (now < turnLeftUntil)
        {
            rightStickX = -profile.cameraStickStrength;
        }

        if (now < turnRightUntil)
        {
            rightStickX = profile.cameraStickStrength;
        }

        bool lightAttackPressed = now < lightAttackUntil;
        bool rollPressed = now < rollUntil;

        VirtualGamepadOutput.State.SetLeftStick(leftStickX, leftStickY);
        VirtualGamepadOutput.State.SetRightStick(rightStickX, rightStickY);

        VirtualGamepadOutput.State.SetButton(GamepadButton.RightShoulder, lightAttackPressed);
        VirtualGamepadOutput.State.SetButton(GamepadButton.B, rollPressed);
    }

    private void ClearMomentaryOutputs()
    {
        moveForwardUntil = 0f;
        moveBackwardUntil = 0f;
        moveLeftUntil = 0f;
        moveRightUntil = 0f;

        turnLeftUntil = 0f;
        turnRightUntil = 0f;

        lightAttackUntil = 0f;
        rollUntil = 0f;

        VirtualGamepadOutput.State.SetLeftStick(0f, 0f);
        VirtualGamepadOutput.State.SetRightStick(0f, 0f);

        VirtualGamepadOutput.State.SetButton(GamepadButton.RightShoulder, false);
        VirtualGamepadOutput.State.SetButton(GamepadButton.B, false);
    }

    private void OnDisable()
    {
        ClearMomentaryOutputs();
    }
}