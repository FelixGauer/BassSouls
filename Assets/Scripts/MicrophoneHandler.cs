using System;
using UnityEngine;

public class MicrophoneHandler : MonoBehaviour
{
    private AudioClip micClip;
    private int micClipChannels;
    private float[] micInterleavedBuffer;
    private float[] micMonoBuffer;

    [Header("Mic Clip Polling")]
    public bool useMicClipPolling = true;
    public int micReadLagSamples = 1024;

    [Header("Device")]
    public string preferredDeviceContains = "Focusrite";
    public int sampleRate = 48000;
    public int clipLengthSeconds = 1;

    private string selectedDevice;
    [HideInInspector] public bool usingMic;
    [HideInInspector] public float audioThreadRms;

    private PitchDetector pitchDetector;
    [HideInInspector] public AudioSource audioSource;

    private void Awake()
    {
        pitchDetector = GetComponent<PitchDetector>();
        audioSource = GetComponent<AudioSource>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        StartMicrophone();
    }


    void StartMicrophone()
    {
        if (Microphone.devices.Length == 0)
        {
            usingMic = false;
            return;
        }

        Debug.Log("Available microphone devices:");

        foreach (string device in Microphone.devices)
        {
            Microphone.GetDeviceCaps(device, out int minFreq, out int maxFreq);
            Debug.Log($"Mic: {device} | minFreq: {minFreq} | maxFreq: {maxFreq}");
        }

        selectedDevice = PickDevice();

        if (string.IsNullOrEmpty(selectedDevice))
        {
            Debug.LogError("No microphone device found. Check OS audio input permissions and routing.");
            usingMic = false;
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
            Debug.LogError("Microphone.Start failed.");
            usingMic = false;
            return;
        }

        while (Microphone.GetPosition(selectedDevice) <= 0)
        {
        }

        micClipChannels = micClip.channels;

        micMonoBuffer = new float[pitchDetector.pitchBufferSize];
        micInterleavedBuffer = new float[pitchDetector.pitchBufferSize * micClipChannels];

        Debug.Log(
            $"Mic clip started | samples: {micClip.samples} | channels: {micClipChannels} | frequency: {micClip.frequency}"
        );

        // Do not play the mic clip through Unity for this test.
        audioSource.clip = null;
        audioSource.Stop();

        usingMic = true;
    }

    private string PickDevice()
    {
        if (Microphone.devices.Length == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(preferredDeviceContains))
        {
            foreach (string device in Microphone.devices)
            {
                if (device.ToLower().Contains(preferredDeviceContains.ToLower()))
                    return device;
            }
        }

        // Fallback: first available input device.
        // return Microphone.devices[0];

        return null; // to not select laptop mic automatically
    }

    public bool TryReadRecentMicWindow(float[] monoTarget)
    {
        if (micClip == null || string.IsNullOrEmpty(selectedDevice))
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
                $"Mic read window too large. windowSamples: {windowSamples}, lag: {micReadLagSamples}, clipSamples: {clipSamples}"
            );

            return false;
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
