using System;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class BassInputHandler : MonoBehaviour
{
    public static BassInputHandler Instance;

    [Header("Readout")]
    [Range(0f, 1f)] public float smoothedLevel; // used as wave rms parameter
    public float rawRms;
    public float gain = 10f;
    public float smoothing = 12f;
    public float currentFrequency; // also will be used for the wave mechanic

    [Header("Candidate Validation")]
    public float candidateMaxDurationSeconds = 0.12f;
    public float candidateYinCheckIntervalSeconds = 0.02f;
    public int requiredValidPitchFrames = 4;

    [Header("Release")]
    public float minActiveSeconds = 0.15f;
    public float releaseSmoothedLevelThreshold = 0.035f;
    public float releaseRequiredQuietSeconds = 0.12f;

    [Header("Active State")]
    public bool printActiveStateDebug = true;

    [Header("Debug Printing")]
    public bool printRmsCandidateDebug = true;

    [HideInInspector] public WaveSession waveSession; // holds wave class to keep its info in one place
    public event Action StringWasPlucked;


    private MicrophoneHandler micHandler;
    private PitchDetector pitchDetector;

    private float lastRms;
    private float rmsDeltaThreshold = 0.01f;

    private float candidateStartTime;
    private float candidateLastYinCheckTime;
    private float candidatePeakSmoothedLevel;
    private float candidatePeakRawRms;

    private int candidateYinChecks;
    private int candidateValidPitchFrames;

    private float candidateBestFrequency;
    private float candidateBestConfidence;

    private float activeStartTime;
    private float activeFrequency;
    private float activePeakRawRms;
    private float activePeakSmoothedLevel;

    private float activeQuietStartTime = -1f;

    private enum BassInputState
    {
        Idle,
        Candidate,
        Active
    }

    private BassInputState inputState = BassInputState.Idle;

    private void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);

        micHandler = GetComponent<MicrophoneHandler>();
        pitchDetector = GetComponent<PitchDetector>();
    }

    void Start()
    {

    }

    void Update()
    {
        //if (!micHandler.usingMic)
        //{
        //    micHandler.PlayBassWithControllers();
        //    return;
        //}

        if (micHandler.useMicClipPolling)
        {
            if (!micHandler.TryGetMicClipRms(out float micRms))
            {
                return;
            }

            rawRms = micRms;
        }
        else
        {
            rawRms = micHandler.audioThreadRms;
        }

        float target = Mathf.Clamp01(rawRms * gain);

        smoothedLevel = Mathf.Lerp(
            smoothedLevel,
            target,
            1f - Mathf.Exp(-smoothing * Time.deltaTime)
        );

        float delta = Mathf.Abs(smoothedLevel - lastRms);

        switch (inputState)
        {
            case BassInputState.Idle:
                if (delta > rmsDeltaThreshold)
                {
                    BeginRmsCandidate();
                }
                break;

            case BassInputState.Candidate:
                ProcessCandidateValidation();
                break;

            case BassInputState.Active:
                ProcessActiveState();
                break;
        }

        lastRms = smoothedLevel;

    }

    private void BeginRmsCandidate()
    {
        inputState = BassInputState.Candidate;

        candidateStartTime = Time.time;
        candidateLastYinCheckTime = -999f;

        candidatePeakSmoothedLevel = smoothedLevel;
        candidatePeakRawRms = rawRms;

        candidateYinChecks = 0;
        candidateValidPitchFrames = 0;

        candidateBestFrequency = 0f;
        candidateBestConfidence = 0f;

        ProcessCandidateValidation();
    }

    private void ProcessCandidateValidation()
    {
        candidatePeakSmoothedLevel = Mathf.Max(candidatePeakSmoothedLevel, smoothedLevel);
        candidatePeakRawRms = Mathf.Max(candidatePeakRawRms, rawRms);

        float candidateAge = Time.time - candidateStartTime;

        bool shouldRunYinCheck =
            Time.time - candidateLastYinCheckTime >= candidateYinCheckIntervalSeconds;

        if (shouldRunYinCheck)
        {
            candidateLastYinCheckTime = Time.time;
            candidateYinChecks++;

            bool yinPitchFound = pitchDetector.TryGetFrequencyData(
                out float frequency,
                out float confidence
            );

            if (yinPitchFound)
            {
                candidateValidPitchFrames++;

                if (confidence > candidateBestConfidence)
                {
                    candidateBestConfidence = confidence;
                    candidateBestFrequency = frequency;
                }
            }

            if (candidateValidPitchFrames >= Mathf.Max(1, requiredValidPitchFrames))
            {
                ConfirmPluckCandidate();
                return;
            }
        }

        if (candidateAge >= candidateMaxDurationSeconds)
        {
            RejectCandidate("TIMEOUT_OR_NOT_ENOUGH_VALID_YIN_FRAMES");
        }
    }

    private void ConfirmPluckCandidate()
    {
        if (printRmsCandidateDebug)
        {
            Debug.Log(
                $"PHASE 6 BASS PLUCK CONFIRMED | " +
                $"Reason: ENOUGH_VALID_YIN_FRAMES | " +
                $"Checks: {candidateYinChecks} | " +
                $"Valid Frames: {candidateValidPitchFrames}/{requiredValidPitchFrames} | " +
                $"Raw RMS Peak: {candidatePeakRawRms:F6} | " +
                $"Smoothed RMS Peak: {candidatePeakSmoothedLevel:F6} | " +
                $"Best YIN Confidence: {candidateBestConfidence:F3} | " +
                $"Confirmed Frequency: {candidateBestFrequency:F2} Hz"
            );
        }
        ;

        waveSession = new WaveSession
        {
            startTime = Time.time,
            duration = candidatePeakSmoothedLevel,
            waveFrequency = candidateBestFrequency
        };

        currentFrequency = candidateBestFrequency;

        BeginActiveState(
            candidateBestFrequency,
            candidatePeakRawRms,
            candidatePeakSmoothedLevel
        );

        StringWasPlucked?.Invoke();

    }

    private void BeginActiveState(
    float frequency,
    float peakRawRms,
    float peakSmoothedLevel
)
    {
        activeStartTime = Time.time;
        activeFrequency = frequency;
        activePeakRawRms = peakRawRms;
        activePeakSmoothedLevel = peakSmoothedLevel;

        activeQuietStartTime = -1f;

        inputState = BassInputState.Active;

        if (printActiveStateDebug)
        {
            Debug.Log(
                $"PHASE 8 ACTIVE STARTED | " +
                $"Frequency: {activeFrequency:F2} Hz | " +
                $"Peak Raw RMS: {activePeakRawRms:F6} | " +
                $"Peak Smoothed Level: {activePeakSmoothedLevel:F6} | " +
                $"WaveSession Active: {(waveSession != null && waveSession.IsActive)} | " +
                $"Min Active Seconds: {minActiveSeconds:F3} | " +
                $"Release Smoothed Threshold: {releaseSmoothedLevelThreshold:F6} | " +
                $"Required Quiet Seconds: {releaseRequiredQuietSeconds:F3}"
            );
        }
    }

    private void ProcessActiveState()
    {
        float activeAge = Time.time - activeStartTime;

        bool waveHasEnded = waveSession == null || !waveSession.IsActive;
        bool minimumActiveTimePassed = activeAge >= minActiveSeconds;

        bool signalIsQuiet = smoothedLevel <= releaseSmoothedLevelThreshold;

        // Do not even start counting quiet time until the wave has ended
        // and the note has been active for at least the minimum duration.
        if (!waveHasEnded || !minimumActiveTimePassed)
        {
            activeQuietStartTime = -1f;
            return;
        }

        // Signal is still too loud / alive.
        // Stay Active and keep blocking new candidates.
        if (!signalIsQuiet)
        {
            activeQuietStartTime = -1f;
            return;
        }

        // Signal has just become quiet enough.
        if (activeQuietStartTime < 0f)
        {
            activeQuietStartTime = Time.time;

            if (printActiveStateDebug)
            {
                Debug.Log(
                    $"PHASE 8 RELEASE QUIET WINDOW STARTED | " +
                    $"Active Age: {activeAge:F3}s | " +
                    $"Smoothed Level: {smoothedLevel:F6} | " +
                    $"Raw RMS: {rawRms:F6} | " +
                    $"Required Quiet Seconds: {releaseRequiredQuietSeconds:F3}"
                );
            }

            return;
        }

        float quietDuration = Time.time - activeQuietStartTime;

        if (quietDuration < releaseRequiredQuietSeconds)
        {
            return;
        }

        if (printActiveStateDebug)
        {
            Debug.Log(
                $"PHASE 8 ACTIVE RELEASED | " +
                $"Active Duration: {activeAge:F3}s | " +
                $"Quiet Duration: {quietDuration:F3}s | " +
                $"Frequency: {activeFrequency:F2} Hz | " +
                $"Smoothed Level: {smoothedLevel:F6} | " +
                $"Raw RMS: {rawRms:F6} | " +
                $"Returning To: Idle"
            );
        }

        activeQuietStartTime = -1f;

        inputState = BassInputState.Idle;
    }

    private void RejectCandidate(string reason)
    {
        if (printRmsCandidateDebug)
        {
            Debug.Log(
                $"PHASE 6 PLUCK CANDIDATE REJECTED | " +
                $"Reason: {reason} | " +
                $"Checks: {candidateYinChecks} | " +
                $"Valid Frames: {candidateValidPitchFrames}/{requiredValidPitchFrames} | " +
                $"Raw RMS Peak: {candidatePeakRawRms:F6} | " +
                $"Smoothed RMS Peak: {candidatePeakSmoothedLevel:F6} | " +
                $"Last YIN Pitch Found: {pitchDetector.lastYinPitchFound} | " +
                $"Last YIN Confidence: {pitchDetector.lastYinConfidence:F3} | " +
                $"Best YIN Confidence: {candidateBestConfidence:F3} | " +
                $"Best Frequency: {candidateBestFrequency:F2} Hz | " +
                $"Wave Spawned: False"
            );
        }
        ;

        inputState = BassInputState.Idle;
    }



    #region ReadSoundData

    //private void OnAudioFilterRead(float[] data, int channels)
    //{
    //    double sum = 0.0;
    //
    //    for (int i = 0; i < data.Length; i += channels)
    //    {
    //        float sample = data[i]; // left/mono channel
    //        sum += sample * sample;
    //    }
    //
    //    int sampleCount = data.Length / channels;
    //    float rms = sampleCount > 0 ? Mathf.Sqrt((float)(sum / sampleCount)) : 0f;
    //
    //    audioThreadRms = rms;
    //}




    #endregion

}