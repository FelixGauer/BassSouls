using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using UnityEngine;

public class ViGEmXbox360MinimalTest : MonoBehaviour
{
    [Header("Stick Settings")]
    [SerializeField] private float stickStrength = 1.0f;

    [Header("Debug")]
    [SerializeField] private bool printDebug = true;

    private ViGEmClient client;
    private IXbox360Controller controller;

    private void Start()
    {
        client = new ViGEmClient();
        controller = client.CreateXbox360Controller();

        controller.AutoSubmitReport = false;
        controller.Connect();

        ResetController();
        Submit();

        if (printDebug)
        {
            Debug.Log("ViGEm virtual Xbox 360 controller connected.");
        }
    }

    private void Update()
    {
        if (controller == null)
        {
            return;
        }

        float leftStickX = 0f;
        float leftStickY = 0f;

        if (Input.GetKey(KeyCode.W))
        {
            leftStickY = stickStrength;
        }

        if (Input.GetKey(KeyCode.A))
        {
            leftStickX = -stickStrength;
        }

        if (Input.GetKey(KeyCode.D))
        {
            leftStickX = stickStrength;
        }

        bool attackPressed = Input.GetKey(KeyCode.Space);

        SetLeftStick(leftStickX, leftStickY);
        controller.SetButtonState(Xbox360Button.RightShoulder, attackPressed);

        Submit();
    }

    private void OnApplicationQuit()
    {
        Shutdown();
    }

    private void OnDestroy()
    {
        Shutdown();
    }

    private void SetLeftStick(float x, float y)
    {
        x = Mathf.Clamp(x, -1f, 1f);
        y = Mathf.Clamp(y, -1f, 1f);

        short xValue = FloatToShortAxis(x);
        short yValue = FloatToShortAxis(y);

        controller.SetAxisValue(Xbox360Axis.LeftThumbX, xValue);
        controller.SetAxisValue(Xbox360Axis.LeftThumbY, yValue);
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

    private void ResetController()
    {
        SetLeftStick(0f, 0f);

        controller.SetAxisValue(Xbox360Axis.RightThumbX, 0);
        controller.SetAxisValue(Xbox360Axis.RightThumbY, 0);

        controller.SetSliderValue(Xbox360Slider.LeftTrigger, 0);
        controller.SetSliderValue(Xbox360Slider.RightTrigger, 0);

        controller.SetButtonState(Xbox360Button.A, false);
        controller.SetButtonState(Xbox360Button.B, false);
        controller.SetButtonState(Xbox360Button.X, false);
        controller.SetButtonState(Xbox360Button.Y, false);
        controller.SetButtonState(Xbox360Button.LeftShoulder, false);
        controller.SetButtonState(Xbox360Button.RightShoulder, false);
    }

    private void Submit()
    {
        controller.SubmitReport();
    }

    private void Shutdown()
    {
        if (controller != null)
        {
            ResetController();
            Submit();

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
            Debug.Log("ViGEm virtual Xbox 360 controller disconnected.");
        }
    }
}