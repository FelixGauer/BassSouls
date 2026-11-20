using System;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(AudioSource))]
public class BassInputHandler : MonoBehaviour
{
    public static BassInputHandler Instance;

    [Header("Debug Bass Input")]
    public bool debugMode = true;

    [Tooltip("If true, debug mode skips microphone/RMS/YIN processing entirely.")]
    public bool debugModeBypassesMic = true;

    [Tooltip("Minimum time between debug bass signals.")]
    public float debugSignalCooldownSeconds = 0.12f;

    [Header("Debug Frequencies")]
    public float debugMoveNorthFrequency = 98f;
    public float debugMoveSouthFrequency = 41f;
    public float debugMoveWestFrequency = 55f;
    public float debugMoveEastFrequency = 73f;
    public float debugRotateLeftFrequency = 130f;
    public float debugRotateRightFrequency = 150f;

    [Header("Readout")]
    [Range(0f, 1f)] public float smoothedLevel;
    public float rawRms;
    public float gain = 10f;
    public float smoothing = 12f;
    public float currentFrequency;

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
    public bool printDebugBassInput = true;

    public event Action<BassSignal> BassSignalConfirmed;

    /// <summary>
    /// Temporary legacy/simple event.
    /// </summary>
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

    private float lastDebugSignalTime = -999f;

    private enum BassInputState
    {
        Idle,
        Candidate,
        Active
    }

    private BassInputState inputState = BassInputState.Idle;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        micHandler = GetComponent<MicrophoneHandler>();
        pitchDetector = GetComponent<PitchDetector>();
    }

    private void Update()
    {
        if (debugMode)
        {
            ProcessDebugBassInput();

            if (debugModeBypassesMic)
            {
                return;
            }
        }

        ProcessRealBassInput();
    }

    private void ProcessDebugBassInput()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        if (Time.time - lastDebugSignalTime < debugSignalCooldownSeconds)
        {
            return;
        }

        // Debug bass movement signals.
        if (keyboard.wKey.wasPressedThisFrame)
        {
            EmitDebugBassSignal(debugMoveNorthFrequency, "Debug Move North / W");
            return;
        }

        if (keyboard.sKey.wasPressedThisFrame)
        {
            EmitDebugBassSignal(debugMoveSouthFrequency, "Debug Move South / S");
            return;
        }

        if (keyboard.aKey.wasPressedThisFrame)
        {
            EmitDebugBassSignal(debugMoveWestFrequency, "Debug Move West / A");
            return;
        }

        if (keyboard.dKey.wasPressedThisFrame)
        {
            EmitDebugBassSignal(debugMoveEastFrequency, "Debug Move East / D");
            return;
        }

        // Debug bass rotation signals.
        if (keyboard.qKey.wasPressedThisFrame)
        {
            EmitDebugBassSignal(debugRotateLeftFrequency, "Debug Rotate Left / Q");
            return;
        }

        if (keyboard.eKey.wasPressedThisFrame)
        {
            EmitDebugBassSignal(debugRotateRightFrequency, "Debug Rotate Right / E");
            return;
        }
    }

    private void EmitDebugBassSignal(float frequency, string debugLabel)
    {
        lastDebugSignalTime = Time.time;

        currentFrequency = frequency;

        // Fake readout values for inspector visibility.
        rawRms = 0.1f;
        smoothedLevel = 0.75f;

        BassSignal signal = new BassSignal(frequency);

        if (printDebugBassInput)
        {
            Debug.Log(
                $"BASS DEBUG SIGNAL | {debugLabel} | Frequency: {frequency:F2} Hz"
            );
        }

        BassSignalConfirmed?.Invoke(signal);
        StringWasPlucked?.Invoke();
    }

    private void ProcessRealBassInput()
    {
        if (micHandler == null)
        {
            return;
        }

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

            bool yinPitchFound = false;
            float frequency = 0f;
            float confidence = 0f;

            if (pitchDetector != null)
            {
                yinPitchFound = pitchDetector.TryGetFrequencyData(
                    out frequency,
                    out confidence
                );
            }

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
                $"BASS PLUCK CONFIRMED | " +
                $"Reason: ENOUGH_VALID_YIN_FRAMES | " +
                $"Checks: {candidateYinChecks} | " +
                $"Valid Frames: {candidateValidPitchFrames}/{requiredValidPitchFrames} | " +
                $"Raw RMS Peak: {candidatePeakRawRms:F6} | " +
                $"Smoothed RMS Peak: {candidatePeakSmoothedLevel:F6} | " +
                $"Best YIN Confidence: {candidateBestConfidence:F3} | " +
                $"Confirmed Frequency: {candidateBestFrequency:F2} Hz"
            );
        }

        currentFrequency = candidateBestFrequency;

        BassSignal bassSignal = new BassSignal(candidateBestFrequency);

        BassSignalConfirmed?.Invoke(bassSignal);
        StringWasPlucked?.Invoke();

        BeginActiveState(
            candidateBestFrequency,
            candidatePeakRawRms,
            candidatePeakSmoothedLevel
        );
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
                $"BASS ACTIVE STARTED | " +
                $"Frequency: {activeFrequency:F2} Hz | " +
                $"Peak Raw RMS: {activePeakRawRms:F6} | " +
                $"Peak Smoothed Level: {activePeakSmoothedLevel:F6} | " +
                $"Min Active Seconds: {minActiveSeconds:F3} | " +
                $"Release Smoothed Threshold: {releaseSmoothedLevelThreshold:F6} | " +
                $"Required Quiet Seconds: {releaseRequiredQuietSeconds:F3}"
            );
        }
    }

    private void ProcessActiveState()
    {
        float activeAge = Time.time - activeStartTime;

        bool minimumActiveTimePassed = activeAge >= minActiveSeconds;
        bool signalIsQuiet = smoothedLevel <= releaseSmoothedLevelThreshold;

        if (!minimumActiveTimePassed)
        {
            activeQuietStartTime = -1f;
            return;
        }

        if (!signalIsQuiet)
        {
            activeQuietStartTime = -1f;
            return;
        }

        if (activeQuietStartTime < 0f)
        {
            activeQuietStartTime = Time.time;

            if (printActiveStateDebug)
            {
                Debug.Log(
                    $"BASS RELEASE QUIET WINDOW STARTED | " +
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
                $"BASS ACTIVE RELEASED | " +
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
            string lastYinPitchFoundText = "N/A";
            string lastYinConfidenceText = "N/A";

            if (pitchDetector != null)
            {
                lastYinPitchFoundText = pitchDetector.lastYinPitchFound.ToString();
                lastYinConfidenceText = pitchDetector.lastYinConfidence.ToString("F3");
            }

            Debug.Log(
                $"BASS PLUCK CANDIDATE REJECTED | " +
                $"Reason: {reason} | " +
                $"Checks: {candidateYinChecks} | " +
                $"Valid Frames: {candidateValidPitchFrames}/{requiredValidPitchFrames} | " +
                $"Raw RMS Peak: {candidatePeakRawRms:F6} | " +
                $"Smoothed RMS Peak: {candidatePeakSmoothedLevel:F6} | " +
                $"Last YIN Pitch Found: {lastYinPitchFoundText} | " +
                $"Last YIN Confidence: {lastYinConfidenceText} | " +
                $"Best YIN Confidence: {candidateBestConfidence:F3} | " +
                $"Best Frequency: {candidateBestFrequency:F2} Hz"
            );
        }

        inputState = BassInputState.Idle;
    }
}