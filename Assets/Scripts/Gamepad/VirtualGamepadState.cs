using System.Collections.Generic;
using UnityEngine;

public class VirtualGamepadState
{
    private readonly Dictionary<GamepadButton, bool> buttonStates =
        new Dictionary<GamepadButton, bool>();

    public float LeftStickX { get; private set; }
    public float LeftStickY { get; private set; }

    public float RightStickX { get; private set; }
    public float RightStickY { get; private set; }

    public float LeftTrigger { get; private set; }
    public float RightTrigger { get; private set; }

    public void SetLeftStick(float x, float y)
    {
        LeftStickX = Mathf.Clamp(x, -1f, 1f);
        LeftStickY = Mathf.Clamp(y, -1f, 1f);
    }

    public void SetRightStick(float x, float y)
    {
        RightStickX = Mathf.Clamp(x, -1f, 1f);
        RightStickY = Mathf.Clamp(y, -1f, 1f);
    }

    public void SetLeftTrigger(float value)
    {
        LeftTrigger = Mathf.Clamp01(value);
    }

    public void SetRightTrigger(float value)
    {
        RightTrigger = Mathf.Clamp01(value);
    }

    public void SetButton(GamepadButton button, bool pressed)
    {
        buttonStates[button] = pressed;
    }

    public bool GetButton(GamepadButton button)
    {
        return buttonStates.TryGetValue(button, out bool pressed) && pressed;
    }

    public void Reset()
    {
        LeftStickX = 0f;
        LeftStickY = 0f;

        RightStickX = 0f;
        RightStickY = 0f;

        LeftTrigger = 0f;
        RightTrigger = 0f;

        buttonStates.Clear();
    }
}