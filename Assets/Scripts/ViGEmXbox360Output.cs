using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using UnityEngine;

public class ViGEmXbox360Output : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool printDebug = true;

    private ViGEmClient client;
    private IXbox360Controller controller;

    private void Awake()
    {
        Application.runInBackground = true;
    }

    private void OnEnable()
    {
        ConnectController();
    }

    private void Update()
    {
        if (controller == null)
        {
            return;
        }

        SendState(VirtualGamepadOutput.State);
        controller.SubmitReport();
    }

    private void OnDisable()
    {
        ShutdownController();
    }

    private void OnApplicationQuit()
    {
        ShutdownController();
    }

    private void ConnectController()
    {
        client = new ViGEmClient();
        controller = client.CreateXbox360Controller();

        controller.AutoSubmitReport = false;
        controller.Connect();

        VirtualGamepadOutput.State.Reset();
        SendState(VirtualGamepadOutput.State);
        controller.SubmitReport();

        if (printDebug)
        {
            Debug.Log("ViGEmXbox360Output connected virtual Xbox 360 controller.");
        }
    }

    private void ShutdownController()
    {
        if (controller != null)
        {
            VirtualGamepadOutput.State.Reset();
            SendState(VirtualGamepadOutput.State);
            controller.SubmitReport();

            controller.Disconnect();
            controller = null;
        }

        if (client != null)
        {
            client.Dispose();
            client = null;
        }

        if (printDebug)
        {
            Debug.Log("ViGEmXbox360Output disconnected virtual Xbox 360 controller.");
        }
    }

    private void SendState(VirtualGamepadState state)
    {
        controller.SetAxisValue(Xbox360Axis.LeftThumbX, FloatToShortAxis(state.LeftStickX));
        controller.SetAxisValue(Xbox360Axis.LeftThumbY, FloatToShortAxis(state.LeftStickY));

        controller.SetAxisValue(Xbox360Axis.RightThumbX, FloatToShortAxis(state.RightStickX));
        controller.SetAxisValue(Xbox360Axis.RightThumbY, FloatToShortAxis(state.RightStickY));

        controller.SetSliderValue(Xbox360Slider.LeftTrigger, FloatToByteSlider(state.LeftTrigger));
        controller.SetSliderValue(Xbox360Slider.RightTrigger, FloatToByteSlider(state.RightTrigger));

        controller.SetButtonState(Xbox360Button.A, state.GetButton(GamepadButton.A));
        controller.SetButtonState(Xbox360Button.B, state.GetButton(GamepadButton.B));
        controller.SetButtonState(Xbox360Button.X, state.GetButton(GamepadButton.X));
        controller.SetButtonState(Xbox360Button.Y, state.GetButton(GamepadButton.Y));

        controller.SetButtonState(Xbox360Button.LeftShoulder, state.GetButton(GamepadButton.LeftShoulder));
        controller.SetButtonState(Xbox360Button.RightShoulder, state.GetButton(GamepadButton.RightShoulder));

        controller.SetButtonState(Xbox360Button.Back, state.GetButton(GamepadButton.Back));
        controller.SetButtonState(Xbox360Button.Start, state.GetButton(GamepadButton.Start));

        controller.SetButtonState(Xbox360Button.LeftThumb, state.GetButton(GamepadButton.LeftThumb));
        controller.SetButtonState(Xbox360Button.RightThumb, state.GetButton(GamepadButton.RightThumb));

        controller.SetButtonState(Xbox360Button.Up, state.GetButton(GamepadButton.DPadUp));
        controller.SetButtonState(Xbox360Button.Down, state.GetButton(GamepadButton.DPadDown));
        controller.SetButtonState(Xbox360Button.Left, state.GetButton(GamepadButton.DPadLeft));
        controller.SetButtonState(Xbox360Button.Right, state.GetButton(GamepadButton.DPadRight));
    }

    private short FloatToShortAxis(float value)
    {
        value = Mathf.Clamp(value, -1f, 1f);

        if (value >= 0f)
        {
            return (short)(value * short.MaxValue);
        }

        return (short)(value * -short.MinValue);
    }

    private byte FloatToByteSlider(float value)
    {
        return (byte)(Mathf.Clamp01(value) * byte.MaxValue);
    }
}