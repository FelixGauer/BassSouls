using UnityEngine;

public class MicrophoneHandler : MonoBehaviour
{
    public enum MicrophoneSelectionMode
    {
        PreferredSubstring,
        DeviceIndex,
        FirstAvailable,
        Disabled
    }

    private AudioClip micClip;
    private int micClipChannels;
    private float[] micInterleavedBuffer;
    private float[] micMonoBuffer;

    [Header("Mic Clip Polling")]
    public bool useMicClipPolling = true;
    public int micReadLagSamples = 1024;

    [Header("Device Selection")]
    public MicrophoneSelectionMode selectionMode = MicrophoneSelectionMode.PreferredSubstring;

    [Tooltip("Used when Selection Mode is PreferredSubstring.")]
    public string preferredDeviceContains = "Focusrite";

    [Tooltip("Used when Selection Mode is DeviceIndex. Check Console logs for available indices.")]
    public int selectedDeviceIndex = 0;

    [Tooltip("If preferred/index selection fails, use the first available microphone instead.")]
    public bool allowFallbackToFirstAvailable = false;

    [Header("Device Settings")]
    public int sampleRate = 48000;
    public int clipLengthSeconds = 1;

    [Header("Status")]
    [SerializeField] private string selectedDevice;
    [SerializeField] private string microphoneStatus = "Not started";

    [HideInInspector] public bool usingMic;
    [HideInInspector] public float audioThreadRms;
    [HideInInspector] public AudioSource audioSource;

    public bool HasValidMic => usingMic && micClip != null && !string.IsNullOrEmpty(selectedDevice);
    public string SelectedDevice => selectedDevice;
    public string MicrophoneStatus => microphoneStatus;

    private PitchDetector pitchDetector;

    private void Awake()
    {
        pitchDetector = GetComponent<PitchDetector>();
        audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        StartMicrophone();
    }

    [ContextMenu("Print Microphone Devices")]
    public void PrintMicrophoneDevices()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("No microphone devices found.");
            return;
        }

        Debug.Log("Available microphone devices:");

        for (int i = 0; i < Microphone.devices.Length; i++)
        {
            string device = Microphone.devices[i];

            Microphone.GetDeviceCaps(device, out int minFreq, out int maxFreq);

            Debug.Log(
                $"[{i}] {device} | minFreq: {minFreq} | maxFreq: {maxFreq}"
            );
        }
    }

    [ContextMenu("Restart Microphone")]
    public void RestartMicrophone()
    {
        StopMicrophone();
        StartMicrophone();
    }

    public void StartMicrophone()
    {
        usingMic = false;
        microphoneStatus = "Starting microphone...";
        selectedDevice = null;

        if (selectionMode == MicrophoneSelectionMode.Disabled)
        {
            microphoneStatus = "Microphone disabled by selection mode.";
            Debug.LogWarning(microphoneStatus);
            ClearAudioSource();
            return;
        }

        if (Microphone.devices.Length == 0)
        {
            microphoneStatus = "No microphone devices found. Running without microphone.";
            Debug.LogWarning(microphoneStatus);
            ClearAudioSource();
            return;
        }

        PrintMicrophoneDevices();

        selectedDevice = PickDevice();

        if (string.IsNullOrEmpty(selectedDevice))
        {
            microphoneStatus =
                "No suitable microphone device selected. Running without microphone.";

            Debug.LogWarning(
                $"{microphoneStatus} " +
                $"Selection Mode: {selectionMode}, " +
                $"Preferred Contains: '{preferredDeviceContains}', " +
                $"Selected Index: {selectedDeviceIndex}, " +
                $"Fallback To First Available: {allowFallbackToFirstAvailable}"
            );

            ClearAudioSource();
            return;
        }

        Debug.Log($"Selected microphone device: {selectedDevice}");

        micClip = Microphone.Start(
            selectedDevice,
            loop: true,
            lengthSec: clipLengthSeconds,
            frequency: sampleRate
        );

        if (micClip == null)
        {
            microphoneStatus = $"Microphone.Start failed for device: {selectedDevice}";
            Debug.LogWarning(microphoneStatus);

            usingMic = false;
            ClearAudioSource();
            return;
        }

        float waitStartTime = Time.realtimeSinceStartup;
        float maxWaitSeconds = 2f;

        while (Microphone.GetPosition(selectedDevice) <= 0)
        {
            if (Time.realtimeSinceStartup - waitStartTime > maxWaitSeconds)
            {
                microphoneStatus =
                    $"Timed out waiting for microphone to start: {selectedDevice}";

                Debug.LogWarning(microphoneStatus);

                StopMicrophone();
                ClearAudioSource();
                return;
            }
        }

        micClipChannels = micClip.channels;

        int pitchBufferSize = 8192;

        if (pitchDetector != null)
        {
            pitchBufferSize = pitchDetector.pitchBufferSize;
        }

        micMonoBuffer = new float[pitchBufferSize];
        micInterleavedBuffer = new float[pitchBufferSize * micClipChannels];

        Debug.Log(
            $"Mic clip started | " +
            $"Device: {selectedDevice} | " +
            $"samples: {micClip.samples} | " +
            $"channels: {micClipChannels} | " +
            $"frequency: {micClip.frequency}"
        );

        // Do not play the mic clip through Unity.
        ClearAudioSource();

        usingMic = true;
        microphoneStatus = $"Using microphone: {selectedDevice}";
    }

    public void StopMicrophone()
    {
        if (!string.IsNullOrEmpty(selectedDevice))
        {
            if (Microphone.IsRecording(selectedDevice))
            {
                Microphone.End(selectedDevice);
            }
        }

        micClip = null;
        micInterleavedBuffer = null;
        micMonoBuffer = null;
        usingMic = false;

        microphoneStatus = "Microphone stopped.";
    }

    private string PickDevice()
    {
        switch (selectionMode)
        {
            case MicrophoneSelectionMode.PreferredSubstring:
                {
                    string preferred = PickPreferredSubstringDevice();

                    if (!string.IsNullOrEmpty(preferred))
                    {
                        return preferred;
                    }

                    break;
                }

            case MicrophoneSelectionMode.DeviceIndex:
                {
                    string indexed = PickIndexedDevice();

                    if (!string.IsNullOrEmpty(indexed))
                    {
                        return indexed;
                    }

                    break;
                }

            case MicrophoneSelectionMode.FirstAvailable:
                {
                    return Microphone.devices[0];
                }

            case MicrophoneSelectionMode.Disabled:
            default:
                return null;
        }

        if (allowFallbackToFirstAvailable && Microphone.devices.Length > 0)
        {
            Debug.LogWarning(
                $"Microphone selection failed. Falling back to first available device: {Microphone.devices[0]}"
            );

            return Microphone.devices[0];
        }

        return null;
    }

    private string PickPreferredSubstringDevice()
    {
        if (string.IsNullOrWhiteSpace(preferredDeviceContains))
        {
            return null;
        }

        string preferredLower = preferredDeviceContains.ToLowerInvariant();

        foreach (string device in Microphone.devices)
        {
            if (device.ToLowerInvariant().Contains(preferredLower))
            {
                return device;
            }
        }

        return null;
    }

    private string PickIndexedDevice()
    {
        if (Microphone.devices.Length == 0)
        {
            return null;
        }

        if (selectedDeviceIndex < 0 || selectedDeviceIndex >= Microphone.devices.Length)
        {
            Debug.LogWarning(
                $"Selected microphone index {selectedDeviceIndex} is out of range. " +
                $"Available range: 0 to {Microphone.devices.Length - 1}"
            );

            return null;
        }

        return Microphone.devices[selectedDeviceIndex];
    }

    private void ClearAudioSource()
    {
        if (audioSource == null)
        {
            return;
        }

        audioSource.clip = null;
        audioSource.Stop();
    }

    public bool TryReadRecentMicWindow(float[] monoTarget)
    {
        if (monoTarget == null || monoTarget.Length == 0)
        {
            return false;
        }

        if (!HasValidMic)
        {
            return false;
        }

        int micPosition = Microphone.GetPosition(selectedDevice);

        if (micPosition <= 0)
        {
            return false;
        }

        int windowSamples = monoTarget.Length;
        int clipSamples = micClip.samples;

        if (windowSamples + micReadLagSamples >= clipSamples)
        {
            Debug.LogWarning(
                $"Mic read window too large. " +
                $"windowSamples: {windowSamples}, " +
                $"lag: {micReadLagSamples}, " +
                $"clipSamples: {clipSamples}"
            );

            return false;
        }

        int requiredInterleavedLength = windowSamples * micClipChannels;

        if (micInterleavedBuffer == null || micInterleavedBuffer.Length < requiredInterleavedLength)
        {
            micInterleavedBuffer = new float[requiredInterleavedLength];
        }

        int startSample = micPosition - micReadLagSamples - windowSamples;

        while (startSample < 0)
        {
            startSample += clipSamples;
        }

        bool gotData = micClip.GetData(micInterleavedBuffer, startSample);

        if (!gotData)
        {
            Debug.LogWarning("micClip.GetData failed.");
            return false;
        }

        if (micClipChannels == 1)
        {
            for (int i = 0; i < windowSamples; i++)
            {
                monoTarget[i] = micInterleavedBuffer[i];
            }
        }
        else
        {
            for (int i = 0; i < windowSamples; i++)
            {
                int baseIndex = i * micClipChannels;

                float sum = 0f;

                for (int ch = 0; ch < micClipChannels; ch++)
                {
                    sum += micInterleavedBuffer[baseIndex + ch];
                }

                monoTarget[i] = sum / micClipChannels;
            }
        }

        return true;
    }

    public bool TryGetMicClipRms(out float rms)
    {
        rms = 0f;

        if (micMonoBuffer == null)
        {
            int pitchBufferSize = 8192;

            if (pitchDetector != null)
            {
                pitchBufferSize = pitchDetector.pitchBufferSize;
            }

            micMonoBuffer = new float[pitchBufferSize];
        }

        if (!TryReadRecentMicWindow(micMonoBuffer))
        {
            return false;
        }

        double sum = 0.0;

        for (int i = 0; i < micMonoBuffer.Length; i++)
        {
            float sample = micMonoBuffer[i];
            sum += sample * sample;
        }

        rms = Mathf.Sqrt((float)(sum / micMonoBuffer.Length));
        return true;
    }
}