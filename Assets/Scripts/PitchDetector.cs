using System;
using UnityEngine;

public class PitchDetector : MonoBehaviour
{

    [Header("YIN Pitch Detection")]
    public int pitchBufferSize = 8192;
    public float minPitchFrequency = 28f;
    public float maxPitchFrequency = 450f;
    public float yinThreshold = 0.12f;
    public float pitchMinRms = 0.005f;
    public float pitchConfidenceThreshold = 0.75f;

    private float[] pitchBuffer;
    private YinPitchDetector yinDetector;

    private MicrophoneHandler micHandler;
    private BassInputHandler bassHandler;

    [HideInInspector] public bool lastYinPitchFound;
    [HideInInspector] public float lastYinConfidence;

    private void Awake()
    {
        pitchBuffer = new float[pitchBufferSize];
        yinDetector = new YinPitchDetector(pitchBufferSize);

        micHandler = GetComponent<MicrophoneHandler>();
        bassHandler = GetComponent<BassInputHandler>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public bool TryGetFrequencyData(out float frequency, out float confidence)
    {
        frequency = 0f;
        confidence = 0f;

        lastYinPitchFound = false;
        lastYinConfidence = 0f;

        bool gotMicData;

        if (micHandler.useMicClipPolling)
        {
            gotMicData = micHandler.TryReadRecentMicWindow(pitchBuffer);
        }
        else
        {
            micHandler.audioSource.GetOutputData(pitchBuffer, 0);
            gotMicData = true;
        }

        if (!gotMicData)
        {
            return false;
        }

        bool foundPitch = yinDetector.TryGetPitch(
            pitchBuffer,
            micHandler.sampleRate,
            minPitchFrequency,
            maxPitchFrequency,
            yinThreshold,
            pitchMinRms,
            out float detectedFrequency,
            out float detectedConfidence
        );

        confidence = detectedConfidence;
        lastYinConfidence = detectedConfidence;

        if (!foundPitch || detectedConfidence < pitchConfidenceThreshold)
        {
            return false;
        }

        frequency = detectedFrequency;

        lastYinPitchFound = true;

        bassHandler.currentFrequency = detectedFrequency;

        return true;
    }
}
