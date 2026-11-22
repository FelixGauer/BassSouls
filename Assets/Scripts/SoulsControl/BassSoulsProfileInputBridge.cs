using System.Collections;
using UnityEngine;

public class BassSoulsProfileInputBridge : MonoBehaviour
{
    [Header("Profile")]
    [SerializeField] private BassSoulsControlProfile profile;

    [Header("Subscription")]
    [SerializeField] private bool autoFindBassInputHandler = true;
    [SerializeField] private BassInputHandler bassInputHandler;

    [Header("Runtime Controller")]
    [SerializeField] private SoulsIntentRuntimeController soulsIntentRuntimeController;

    [Header("Debug")]
    [SerializeField] private bool printDebug = true;
    [SerializeField] private bool printUnmatchedFrequencies = true;

    private bool isSubscribed;

    private void OnEnable()
    {
        StartCoroutine(SubscribeWhenReady());
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private IEnumerator SubscribeWhenReady()
    {
        while (enabled && !isSubscribed)
        {
            if (bassInputHandler == null && autoFindBassInputHandler)
            {
                bassInputHandler = BassInputHandler.Instance;
            }

            if (soulsIntentRuntimeController == null)
            {
                soulsIntentRuntimeController = FindFirstObjectByType<SoulsIntentRuntimeController>();
            }

            if (bassInputHandler != null && soulsIntentRuntimeController != null)
            {
                bassInputHandler.BassSignalConfirmed += OnBassSignalConfirmed;
                isSubscribed = true;

                if (printDebug)
                {
                    Debug.Log(
                 $"BassSoulsProfileInputBridge subscribed to BassInputHandler. " +
                 $"Profile: {(profile != null ? profile.name : "None")}");
                }

                yield break;
            }

            yield return null;
        }
    }

    private void Unsubscribe()
    {
        if (bassInputHandler != null && isSubscribed)
        {
            bassInputHandler.BassSignalConfirmed -= OnBassSignalConfirmed;
        }

        isSubscribed = false;
    }

    private void OnBassSignalConfirmed(BassSignal signal)
    {
        if (profile == null || soulsIntentRuntimeController == null)
        {
            return;
        }

        SoulsControlState currentState = soulsIntentRuntimeController.CurrentState;

        bool resolved = profile.TryResolveFrequency(
            signal.frequency,
            currentState,
            out BassNoteDefinition matchedNote,
            out SoulsIntent matchedIntent
        );

        if (!resolved)
        {
            if (printUnmatchedFrequencies)
            {
                BassNoteDefinition closestNote = profile.FindClosestNote(signal.frequency);

                if (closestNote != null)
                {
                    Debug.Log(
                        $"BASS DS1 INPUT | " +
                        $"Frequency: {signal.frequency:F2} Hz | " +
                        $"No note matched | " +
                        $"Closest: {closestNote.noteName} / " +
                        $"{closestNote.centerFrequency:F2} Hz"
                    );
                }
                else
                {
                    Debug.Log(
                        $"BASS DS1 INPUT | " +
                        $"Frequency: {signal.frequency:F2} Hz | " +
                        $"No note matched"
                    );
                }
            }

            return;
        }

        if (printDebug)
        {
            Debug.Log(
                $"BASS DS1 INPUT | " +
                $"Frequency: {signal.frequency:F2} Hz | " +
                $"Matched Note: {matchedNote.noteName} | " +
                $"Range: {matchedNote.minFrequency:F2}-{matchedNote.maxFrequency:F2} Hz | " +
                $"State: {currentState} | " +
                $"Intent: {matchedIntent}"
            );
        }

        if (matchedIntent == SoulsIntent.None)
        {
            return;
        }

        soulsIntentRuntimeController.ReceiveIntent(
            matchedIntent,
            matchedNote,
            signal.frequency
        );
    }
}