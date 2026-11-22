using System.Collections;
using UnityEngine;

public class BassToGamepadMinimalMapper : MonoBehaviour
{
    [Header("Subscription")]
    [SerializeField] private bool autoFindBassActionMapper = true;
    [SerializeField] private BassActionMapper bassActionMapper;

    [Header("Minimal Mapping")]
    [SerializeField] private BassActionVerb forwardVerb = BassActionVerb.MoveNorth;
    [SerializeField] private BassActionVerb leftVerb = BassActionVerb.MoveWest;
    [SerializeField] private BassActionVerb rightVerb = BassActionVerb.MoveEast;
    [SerializeField] private BassActionVerb attackVerb = BassActionVerb.RotateRight;

    [Header("Timing")]
    [SerializeField] private float movementHoldSeconds = 1.0f;
    [SerializeField] private float attackHoldSeconds = 0.5f;

    [Header("Strength")]
    [SerializeField] private float stickStrength = 1.0f;

    [Header("Debug")]
    [SerializeField] private bool printDebug = true;

    private bool isSubscribed;

    private float forwardUntil;
    private float leftUntil;
    private float rightUntil;
    private float attackUntil;

    private void OnEnable()
    {
        StartCoroutine(SubscribeWhenReady());
    }

    private void Update()
    {
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

        VirtualGamepadOutput.State.SetLeftStick(leftStickX, leftStickY);
        VirtualGamepadOutput.State.SetButton(GamepadButton.RightShoulder, attackPressed);
    }

    private void OnDisable()
    {
        Unsubscribe();

        VirtualGamepadOutput.State.SetLeftStick(0f, 0f);
        VirtualGamepadOutput.State.SetButton(GamepadButton.RightShoulder, false);
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
                    Debug.Log("BassToGamepadMinimalMapper subscribed to BassActionMapper.");
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
                $"BassToGamepadMinimalMapper | " +
                $"Bass action: {actionVerb} | " +
                $"Frequency: {signal.frequency:F2} Hz"
            );
        }
    }
}