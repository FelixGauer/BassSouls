using System;
using System.Collections;
using UnityEngine;

public class BassActionMapper : MonoBehaviour
{
    [Serializable]
    public class FrequencyActionBand
    {
        public string label;

        [Min(0f)] public float minFrequency;
        [Min(0f)] public float maxFrequency;

        public BassActionVerb actionVerb;
    }

    [Header("Subscription")]
    [SerializeField] private bool autoFindBassInputHandler = true;
    [SerializeField] private BassInputHandler bassInputHandler;

    [Header("Frequency Mapping")]
    [SerializeField]
    private FrequencyActionBand[] frequencyBands =
    {
        new FrequencyActionBand
        {
            label = "E String / Low Range",
            minFrequency = 35f,
            maxFrequency = 49f,
            actionVerb = BassActionVerb.MoveSouth
        },
        new FrequencyActionBand
        {
            label = "A String / Low-Mid Range",
            minFrequency = 49f,
            maxFrequency = 65f,
            actionVerb = BassActionVerb.MoveWest
        },
        new FrequencyActionBand
        {
            label = "D String / Mid Range",
            minFrequency = 65f,
            maxFrequency = 85f,
            actionVerb = BassActionVerb.MoveEast
        },
        new FrequencyActionBand
        {
            label = "G String / High Range",
            minFrequency = 85f,
            maxFrequency = 120f,
            actionVerb = BassActionVerb.MoveNorth
        }
    };

    [Header("Debug")]
    [SerializeField] private bool printDebug = true;
    [SerializeField] private bool printUnmappedFrequencies = true;

    public event Action<BassActionVerb, BassSignal> BassActionMapped;

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

            if (bassInputHandler != null)
            {
                Subscribe();
                yield break;
            }

            yield return null;
        }
    }

    private void Subscribe()
    {
        if (bassInputHandler == null)
        {
            return;
        }

        if (isSubscribed)
        {
            return;
        }

        bassInputHandler.BassSignalConfirmed += OnBassSignalConfirmed;
        isSubscribed = true;

        if (printDebug)
        {
            Debug.Log("BassActionMapper subscribed to BassInputHandler.");
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
        BassActionVerb actionVerb = MapFrequencyToAction(signal.frequency);

        if (actionVerb == BassActionVerb.None)
        {
            if (printUnmappedFrequencies)
            {
                Debug.Log(
                    $"BassActionMapper | Unmapped frequency: {signal.frequency:F2} Hz"
                );
            }

            return;
        }

        if (printDebug)
        {
            Debug.Log(
                $"BassActionMapper | Frequency: {signal.frequency:F2} Hz → Action: {actionVerb}"
            );
        }

        BassActionMapped?.Invoke(actionVerb, signal);
    }

    private BassActionVerb MapFrequencyToAction(float frequency)
    {
        if (frequencyBands == null)
        {
            return BassActionVerb.None;
        }

        for (int i = 0; i < frequencyBands.Length; i++)
        {
            FrequencyActionBand band = frequencyBands[i];

            if (band == null)
            {
                continue;
            }

            if (frequency >= band.minFrequency && frequency < band.maxFrequency)
            {
                return band.actionVerb;
            }
        }

        return BassActionVerb.None;
    }
}