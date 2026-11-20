using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simple top-down-style controller for a standard 3D Unity scene.
/// 
/// Direct keyboard controls:
/// Arrow Up    = +Z
/// Arrow Down  = -Z
/// Arrow Left  = -X
/// Arrow Right = +X
/// N           = rotate left around Y
/// M           = rotate right around Y
/// 
/// Bass/external controls:
/// Other scripts can call TriggerBassAction(...)
/// to create short movement or rotation pulses.
/// </summary>
public class TopDown3DCharacterController : MonoBehaviour
{
    [Header("Keyboard Movement")]
    [SerializeField] private bool enableKeyboardInput = true;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 180f;

    [Header("Bass Action Pulses")]
    [SerializeField] private bool enableBassActions = true;

    [Tooltip("How long a bass movement pulse lasts in seconds.")]
    [SerializeField] private float bassMovePulseDuration = 0.15f;

    [Tooltip("How long a bass rotation pulse lasts in seconds.")]
    [SerializeField] private float bassRotationPulseDuration = 0.15f;

    [Tooltip("Movement speed multiplier for bass pulses.")]
    [SerializeField] private float bassMoveSpeedMultiplier = 1f;

    [Tooltip("Rotation speed multiplier for bass pulses.")]
    [SerializeField] private float bassRotationSpeedMultiplier = 1f;

    [Header("Debug")]
    [SerializeField] private bool printBassActionDebug = true;

    private Vector3 keyboardMoveInput;
    private float keyboardRotationInput;

    private Vector3 bassMoveInput;
    private float bassRotationInput;

    private float bassMovePulseTimer;
    private float bassRotationPulseTimer;

    private void Update()
    {
        ReadKeyboardInput();
        UpdateBassPulseTimers();
        ApplyMovement();
        ApplyRotation();
    }

    private void ReadKeyboardInput()
    {
        keyboardMoveInput = Vector3.zero;
        keyboardRotationInput = 0f;

        if (!enableKeyboardInput)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        // Arrow-key movement in X/Z plane.
        if (keyboard.upArrowKey.isPressed)
            keyboardMoveInput.z += 1f;

        if (keyboard.downArrowKey.isPressed)
            keyboardMoveInput.z -= 1f;

        if (keyboard.leftArrowKey.isPressed)
            keyboardMoveInput.x -= 1f;

        if (keyboard.rightArrowKey.isPressed)
            keyboardMoveInput.x += 1f;

        // N/M direct rotation around Y.
        if (keyboard.nKey.isPressed)
            keyboardRotationInput -= 1f;

        if (keyboard.mKey.isPressed)
            keyboardRotationInput += 1f;

        keyboardMoveInput = Vector3.ClampMagnitude(keyboardMoveInput, 1f);
    }

    private void UpdateBassPulseTimers()
    {
        if (bassMovePulseTimer > 0f)
        {
            bassMovePulseTimer -= Time.deltaTime;

            if (bassMovePulseTimer <= 0f)
            {
                bassMovePulseTimer = 0f;
                bassMoveInput = Vector3.zero;
            }
        }

        if (bassRotationPulseTimer > 0f)
        {
            bassRotationPulseTimer -= Time.deltaTime;

            if (bassRotationPulseTimer <= 0f)
            {
                bassRotationPulseTimer = 0f;
                bassRotationInput = 0f;
            }
        }
    }

    private void ApplyMovement()
    {
        Vector3 finalMoveInput = Vector3.zero;

        finalMoveInput += keyboardMoveInput;

        if (enableBassActions)
        {
            finalMoveInput += bassMoveInput * bassMoveSpeedMultiplier;
        }

        finalMoveInput = Vector3.ClampMagnitude(finalMoveInput, 1f);

        transform.position += finalMoveInput * moveSpeed * Time.deltaTime;
    }

    private void ApplyRotation()
    {
        float finalRotationInput = keyboardRotationInput;

        if (enableBassActions)
        {
            finalRotationInput += bassRotationInput * bassRotationSpeedMultiplier;
        }

        float rotationAmount = finalRotationInput * rotationSpeed * Time.deltaTime;

        transform.Rotate(0f, rotationAmount, 0f, Space.World);
    }

    public void TriggerBassAction(BassActionVerb actionVerb)
    {
        if (!enableBassActions)
        {
            return;
        }

        switch (actionVerb)
        {
            case BassActionVerb.MoveNorth:
                TriggerBassMove(Vector3.forward);
                break;

            case BassActionVerb.MoveSouth:
                TriggerBassMove(Vector3.back);
                break;

            case BassActionVerb.MoveWest:
                TriggerBassMove(Vector3.left);
                break;

            case BassActionVerb.MoveEast:
                TriggerBassMove(Vector3.right);
                break;

            case BassActionVerb.RotateLeft:
                TriggerBassRotation(-1f);
                break;

            case BassActionVerb.RotateRight:
                TriggerBassRotation(1f);
                break;

            case BassActionVerb.Action1:
                if (printBassActionDebug)
                    Debug.Log("TopDown3DCharacterController received Action1. No behavior assigned yet.");
                break;

            case BassActionVerb.Action2:
                if (printBassActionDebug)
                    Debug.Log("TopDown3DCharacterController received Action2. No behavior assigned yet.");
                break;

            case BassActionVerb.None:
            default:
                break;
        }

        if (printBassActionDebug)
        {
            Debug.Log($"Character Controller received bass action: {actionVerb}");
        }
    }

    private void TriggerBassMove(Vector3 direction)
    {
        bassMoveInput = direction.normalized;
        bassMovePulseTimer = bassMovePulseDuration;
    }

    private void TriggerBassRotation(float direction)
    {
        bassRotationInput = Mathf.Sign(direction);
        bassRotationPulseTimer = bassRotationPulseDuration;
    }
}