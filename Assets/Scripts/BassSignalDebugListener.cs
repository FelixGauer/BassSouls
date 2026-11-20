using UnityEngine;

public class BassSignalDebugListener : MonoBehaviour
{
    private void OnEnable()
    {
        if (BassInputHandler.Instance != null)
        {
            BassInputHandler.Instance.BassSignalConfirmed += OnBassSignalConfirmed;
        }
    }

    private void OnDisable()
    {
        if (BassInputHandler.Instance != null)
        {
            BassInputHandler.Instance.BassSignalConfirmed -= OnBassSignalConfirmed;
        }
    }

    private void OnBassSignalConfirmed(BassSignal signal)
    {
        Debug.Log($"Received BassSignal | Frequency: {signal.frequency:F2} Hz");
    }
}