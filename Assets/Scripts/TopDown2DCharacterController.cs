using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simple top-down-style controller for a standard 3D Unity scene.
/// 
/// Assumptions:
/// - Character exists in X/Z space.
/// - Y is vertical.
/// - W/S move along Z.
/// - A/D move along X.
/// - Q/E rotate around the Y axis.
/// - No Rigidbody or physics required.
/// 
/// Controls:
/// W = move forward / +Z
/// S = move backward / -Z
/// A = move left / -X
/// D = move right / +X
/// Q = rotate counter-clockwise around Y
/// E = rotate clockwise around Y
/// </summary>
public class TopDown3DCharacterController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 180f;

    private Vector3 moveInput;
    private float rotationInput;

    private void Update()
    {
        ReadInput();
        Move();
        Rotate();
    }

    private void ReadInput()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        moveInput = Vector3.zero;
        rotationInput = 0f;

        // WASD movement in X/Z plane
        if (keyboard.wKey.isPressed)
            moveInput.z -= 1f;

        if (keyboard.sKey.isPressed)
            moveInput.z += 1f;

        if (keyboard.aKey.isPressed)
            moveInput.x += 1f;

        if (keyboard.dKey.isPressed)
            moveInput.x -= 1f;

        // Q/E rotation around Y axis
        if (keyboard.qKey.isPressed)
            rotationInput -= 1f;

        if (keyboard.eKey.isPressed)
            rotationInput += 1f;

        // Prevent faster diagonal movement
        moveInput = Vector3.ClampMagnitude(moveInput, 1f);
    }

    private void Move()
    {
        transform.position += moveInput * moveSpeed * Time.deltaTime;
    }

    private void Rotate()
    {
        float rotationAmount = rotationInput * rotationSpeed * Time.deltaTime;
        transform.Rotate(0f, rotationAmount, 0f, Space.World);
    }
}