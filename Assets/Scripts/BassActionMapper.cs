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
            label = "Debug South / S",
            minFrequency = 35f,
            maxFrequency = 49f,
            actionVerb = BassActionVerb.MoveSouth
        },
        new FrequencyActionBand
        {
            label = "Debug West / A",
            minFrequency = 49f,
            maxFrequency = 65f,
            actionVerb = BassActionVerb.MoveWest
        },
        new FrequencyActionBand
        {
            label = "Debug East / D",
            minFrequency = 65f,
            maxFrequency = 85f,
            actionVerb = BassActionVerb.MoveEast
        },
        new FrequencyActionBand
        {
            label = "Debug North / W",
            minFrequency = 85f,
            maxFrequency = 120f,
            actionVerb = BassActionVerb.MoveNorth
        },
        new FrequencyActionBand
        {
            label = "Debug Rotate Left / Q",
            minFrequency = 120f,
            maxFrequency = 140f,
            actionVerb = BassActionVerb.RotateLeft
        },
        new FrequencyActionBand
        {
            label = "Debug Rotate Right / E",
            minFrequency = 140f,
            maxFrequency = 170f,
            actionVerb = BassActionVerb.RotateRight
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
        FrequencyActionBand matchedBand = GetMatchingBand(signal.frequency);

        if (matchedBand == null)
        {
            if (printUnmappedFrequencies)
            {
                Debug.Log(
                    $"BassActionMapper | Unmapped frequency: {signal.frequency:F2} Hz"
                );
            }

            return;
        }

        BassActionVerb actionVerb = matchedBand.actionVerb;

        if (actionVerb == BassActionVerb.None)
        {
            if (printUnmappedFrequencies)
            {
                Debug.Log(
                    $"BassActionMapper | Frequency: {signal.frequency:F2} Hz matched band '{matchedBand.label}', but action is None."
                );
            }

            return;
        }

        if (printDebug)
        {
            Debug.Log(
                $"BassActionMapper | " +
                $"Frequency: {signal.frequency:F2} Hz | " +
                $"Band: {matchedBand.label} | " +
                $"Action: {actionVerb}"
            );
        }

        BassActionMapped?.Invoke(actionVerb, signal);
    }

    private FrequencyActionBand GetMatchingBand(float frequency)
    {
        if (frequencyBands == null)
        {
            return null;
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
                return band;
            }
        }

        return null;
    }
}