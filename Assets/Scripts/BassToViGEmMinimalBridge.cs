using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using System.Collections;
using UnityEngine;

public class BassToViGEmMinimalBridge : MonoBehaviour
{
    [Header("Subscription")]
    [SerializeField] private bool autoFindBassActionMapper = true;
    [SerializeField] private BassActionMapper bassActionMapper;

    [Header("Minimal Action Mapping")]
    [SerializeField] private BassActionVerb forwardVerb = BassActionVerb.MoveNorth;
    [SerializeField] private BassActionVerb leftVerb = BassActionVerb.MoveWest;
    [SerializeField] private BassActionVerb rightVerb = BassActionVerb.MoveEast;

    [Tooltip("Temporary attack verb. Pick any existing BassActionVerb for now.")]
    [SerializeField] private BassActionVerb attackVerb = BassActionVerb.RotateRight;

    [Header("Output")]
    [SerializeField] private float stickStrength = 1.0f;
    [SerializeField] private float movementHoldSeconds = 0.20f;
    [SerializeField] private float attackHoldSeconds = 0.12f;

    [Header("Debug")]
    [SerializeField] private bool printDebug = true;

    private ViGEmClient client;
    private IXbox360Controller controller;

    private bool isSubscribed;

    private float forwardUntil;
    private float leftUntil;
    private float rightUntil;
    private float attackUntil;

    private void OnEnable()
    {
        ConnectController();
        StartCoroutine(SubscribeWhenReady());
    }

    private void Awake()
    {
        Application.runInBackground = true;
    }

    private void Update()
    {
        if (controller == null)
        {
            return;
        }

        float now = Time.time;

        float leftStickX = 0f;
        float leftStickY = 0f;

        if (now < forwardUntil)
        {
            leftStickY = stickStrength;
        }

        if (now < leftUntil)
        {
            leftStickX = -stickStrength;
        }

        if (now < rightUntil)
        {
            leftStickX = stickStrength;
        }

        bool attackPressed = now < attackUntil;

        SetLeftStick(leftStickX, leftStickY);
        controller.SetButtonState(Xbox360Button.RightShoulder, attackPressed);

        controller.SubmitReport();
    }

    private void OnDisable()
    {
        Unsubscribe();
        ShutdownController();
    }

    private void OnApplicationQuit()
    {
        Unsubscribe();
        ShutdownController();
    }

    private IEnumerator SubscribeWhenReady()
    {
        while (enabled && !isSubscribed)
        {
            if (bassActionMapper == null && autoFindBassActionMapper)
            {
                bassActionMapper = FindFirstObjectByType<BassActionMapper>();
            }

            if (bassActionMapper != null)
            {
                bassActionMapper.BassActionMapped += OnBassActionMapped;
                isSubscribed = true;

                if (printDebug)
                {
                    Debug.Log("BassToViGEmMinimalBridge subscribed to BassActionMapper.");
                }

                yield break;
            }

            yield return null;
        }
    }

    private void Unsubscribe()
    {
        if (bassActionMapper != null && isSubscribed)
        {
            bassActionMapper.BassActionMapped -= OnBassActionMapped;
        }

        isSubscribed = false;
    }

    private void OnBassActionMapped(BassActionVerb actionVerb, BassSignal signal)
    {
        float now = Time.time;

        if (actionVerb == forwardVerb)
        {
            forwardUntil = now + movementHoldSeconds;
        }
        else if (actionVerb == leftVerb)
        {
            leftUntil = now + movementHoldSeconds;
        }
        else if (actionVerb == rightVerb)
        {
            rightUntil = now + movementHoldSeconds;
        }
        else if (actionVerb == attackVerb)
        {
            attackUntil = now + attackHoldSeconds;
        }

        if (printDebug)
        {
            Debug.Log(
                $"BassToViGEmMinimalBridge | " +
                $"Bass action: {actionVerb} | " +
                $"Frequency: {signal.frequency:F2} Hz"
            );
        }
    }

    private void ConnectController()
    {
        client = new ViGEmClient();
        controller = client.CreateXbox360Controller();

        controller.AutoSubmitReport = false;
        controller.Connect();

        ResetController();
        controller.SubmitReport();

        if (printDebug)
        {
            Debug.Log("BassToViGEmMinimalBridge connected virtual Xbox 360 controller.");
        }
    }

    private void ShutdownController()
    {
        if (controller != null)
        {
            ResetController();
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
            Debug.Log("BassToViGEmMinimalBridge disconnected virtual Xbox 360 controller.");
        }
    }

    private void SetLeftStick(float x, float y)
    {
        x = Mathf.Clamp(x, -1f, 1f);
        y = Mathf.Clamp(y, -1f, 1f);

        controller.SetAxisValue(Xbox360Axis.LeftThumbX, FloatToShortAxis(x));
        controller.SetAxisValue(Xbox360Axis.LeftThumbY, FloatToShortAxis(y));
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
}