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

    private float debugMoveNorthFrequency = 98f;
    private float debugMoveSouthFrequency = 41f;
    private float debugMoveWestFrequency = 55f;
    private float debugMoveEastFrequency = 73f;
    private float debugRotateLeftFrequency = 130f;
    private float debugRotateRightFrequency = 150f;

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

    [Header("Initial Started Attack")]
    public float rmsDeltaThreshold = 0.01f;

    [Header("Release / Muted")]
    public float minActiveSeconds = 0.15f;
    public float releaseSmoothedLevelThreshold = 0.035f;
    public float releaseRequiredQuietSeconds = 0.12f;

    [Header("Continuation Readiness")]
    [Tooltip("Minimum time after Started/Continued before another pluck may count as Continued.")]
    public float continuedListenDelaySeconds = 0.25f;

    [Tooltip("Smoothed level must fall to or below this value before Continued detection can be armed.")]
    public float continuedReadySmoothedLevelThreshold = 0.35f;

    [Tooltip("Print when continuation listening becomes armed.")]
    public bool printContinuationReadinessDebug = true;

    [Header("Continuation Attack Threshold")]


    [Tooltip("Minimum smoothed RMS level required to consider a Continued attack.")]
    public float continuedAttackMinimumSmoothedLevel = 0.12f;

    [Tooltip("Print when a possible Continued attack is detected.")]
    public bool printContinuationAttackDebug = true;

    [Header("Active State")]
    public bool printActiveStateDebug = true;

    [Header("Debug Printing")]
    public bool printRmsCandidateDebug = true;
    public bool printDebugBassInput = true;
    public bool printBassSignalEmissionDebug = true;

    [Header("Legacy Event Compatibility")]
    [Tooltip("If false, BassSignalConfirmed only receives Started/Continued signals. BassSignalEmitted still receives all signal types.")]
    public bool sendMutedSignalsToLegacyConfirmedEvent = false;

    public event Action<BassSignal> BassSignalEmitted;
    public event Action<BassSignal> BassSignalConfirmed;
    public event Action StringWasPlucked;

    private MicrophoneHandler micHandler;
    private PitchDetector pitchDetector;

    private float lastRms;

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

    private bool continuationListeningArmed;

    private float lastDebugSignalTime = -999f;

    private enum BassInputState
    {
        Idle,
        CandidateFromIdle,
        Active,
        CandidateFromActive
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

        rawRms = 0.1f;
        smoothedLevel = 0.75f;

        BassSignal signal = new BassSignal(
            frequency,
            BassSignalType.Started
        );

        if (printDebugBassInput)
        {
            Debug.Log(
                $"BASS DEBUG SIGNAL | {debugLabel} | " +
                $"Frequency: {frequency:F2} Hz | " +
                $"Type: {signal.signalType}"
            );
        }

        EmitBassSignal(signal);
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
                    BeginRmsCandidate(BassInputState.CandidateFromIdle);
                }
                break;

            case BassInputState.CandidateFromIdle:
            case BassInputState.CandidateFromActive:
                ProcessCandidateValidation();
                break;

            case BassInputState.Active:
                ProcessActiveState(delta);
                break;
        }

        lastRms = smoothedLevel;
    }

    private void BeginRmsCandidate(BassInputState candidateState)
    {
        inputState = candidateState;

        candidateStartTime = Time.time;
        candidateLastYinCheckTime = -999f;

        candidatePeakSmoothedLevel = smoothedLevel;
        candidatePeakRawRms = rawRms;

        candidateYinChecks = 0;
        candidateValidPitchFrames = 0;

        candidateBestFrequency = 0f;
        candidateBestConfidence = 0f;

        activeQuietStartTime = -1f;

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
        bool candidateWasFromActive = inputState == BassInputState.CandidateFromActive;

        BassSignalType signalType = candidateWasFromActive
            ? BassSignalType.Continued
            : BassSignalType.Started;

        if (printRmsCandidateDebug)
        {
            Debug.Log(
                $"BASS PLUCK CONFIRMED | " +
                $"Reason: ENOUGH_VALID_YIN_FRAMES | " +
                $"Source: {(candidateWasFromActive ? "Active" : "Idle")} | " +
                $"Checks: {candidateYinChecks} | " +
                $"Valid Frames: {candidateValidPitchFrames}/{requiredValidPitchFrames} | " +
                $"Raw RMS Peak: {candidatePeakRawRms:F6} | " +
                $"Smoothed RMS Peak: {candidatePeakSmoothedLevel:F6} | " +
                $"Best YIN Confidence: {candidateBestConfidence:F3} | " +
                $"Confirmed Frequency: {candidateBestFrequency:F2} Hz | " +
                $"Type: {signalType}"
            );
        }

        currentFrequency = candidateBestFrequency;

        BassSignal bassSignal = new BassSignal(
            candidateBestFrequency,
            signalType
        );

        EmitBassSignal(bassSignal);
        StringWasPlucked?.Invoke();

        BeginActiveState(
            candidateBestFrequency,
            candidatePeakRawRms,
            candidatePeakSmoothedLevel,
            signalType
        );
    }

    private void BeginActiveState(
        float frequency,
        float peakRawRms,
        float peakSmoothedLevel,
        BassSignalType sourceSignalType
    )
    {
        activeStartTime = Time.time;
        activeFrequency = frequency;
        activePeakRawRms = peakRawRms;
        activePeakSmoothedLevel = peakSmoothedLevel;

        activeQuietStartTime = -1f;
        continuationListeningArmed = false;

        inputState = BassInputState.Active;

        if (printActiveStateDebug)
        {
            Debug.Log(
                $"BASS ACTIVE STARTED | " +
                $"Source Signal: {sourceSignalType} | " +
                $"Frequency: {activeFrequency:F2} Hz | " +
                $"Peak Raw RMS: {activePeakRawRms:F6} | " +
                $"Peak Smoothed Level: {activePeakSmoothedLevel:F6} | " +
                $"Min Active Seconds: {minActiveSeconds:F3} | " +
                $"Release Smoothed Threshold: {releaseSmoothedLevelThreshold:F6} | " +
                $"Required Quiet Seconds: {releaseRequiredQuietSeconds:F3} | " +
                $"Continuation Delay: {continuedListenDelaySeconds:F3} | " +
                $"Continuation Ready Threshold: {continuedReadySmoothedLevelThreshold:F6}"
            );
        }
    }

    private void ProcessActiveState(float delta)
    {
        float activeAge = Time.time - activeStartTime;

        UpdateContinuationReadiness(activeAge);

        if (TryBeginContinuationCandidate(delta, activeAge))
        {
            return;
        }

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

        EmitMutedSignal(activeAge, quietDuration);

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
        continuationListeningArmed = false;
        inputState = BassInputState.Idle;
    }

    private void UpdateContinuationReadiness(float activeAge)
    {
        if (continuationListeningArmed)
        {
            return;
        }

        bool delayPassed = activeAge >= continuedListenDelaySeconds;
        bool levelHasFallenEnough = smoothedLevel <= continuedReadySmoothedLevelThreshold;

        if (!delayPassed || !levelHasFallenEnough)
        {
            return;
        }

        continuationListeningArmed = true;

        if (printContinuationReadinessDebug)
        {
            Debug.Log(
                $"BASS CONTINUATION LISTENING ARMED | " +
                $"Active Age: {activeAge:F3}s | " +
                $"Smoothed Level: {smoothedLevel:F6} | " +
                $"Raw RMS: {rawRms:F6} | " +
                $"Required Delay: {continuedListenDelaySeconds:F3}s | " +
                $"Ready Threshold: {continuedReadySmoothedLevelThreshold:F6} | " +
                $"Active Frequency: {activeFrequency:F2} Hz"
            );
        }
    }

    private bool TryBeginContinuationCandidate(float delta, float activeAge)
    {
        if (!continuationListeningArmed)
        {
            return false;
        }

        bool levelHighEnough = smoothedLevel >= continuedAttackMinimumSmoothedLevel;

        if (!levelHighEnough)
        {
            return false;
        }

        if (printContinuationAttackDebug)
        {
            Debug.Log(
                $"BASS CONTINUATION ATTACK DETECTED | " +
                $"Active Age: {activeAge:F3}s | " +
                $"Delta Observed: {delta:F6} | " +
                $"Delta Gate: Disabled | " +
                $"Smoothed Level: {smoothedLevel:F6} | " +
                $"Required Min Level: {continuedAttackMinimumSmoothedLevel:F6} | " +
                $"Raw RMS: {rawRms:F6} | " +
                $"Previous Active Frequency: {activeFrequency:F2} Hz | " +
                $"Entering: CandidateFromActive"
            );
        }

        BeginRmsCandidate(BassInputState.CandidateFromActive);
        return true;
    }

    private void EmitMutedSignal(float activeAge, float quietDuration)
    {
        BassSignal mutedSignal = new BassSignal(
            activeFrequency,
            BassSignalType.Muted
        );

        if (printBassSignalEmissionDebug)
        {
            Debug.Log(
                $"BASS MUTED SIGNAL EMITTED | " +
                $"Frequency: {activeFrequency:F2} Hz | " +
                $"Type: {mutedSignal.signalType} | " +
                $"Active Age: {activeAge:F3}s | " +
                $"Quiet Duration: {quietDuration:F3}s | " +
                $"Smoothed Level: {smoothedLevel:F6} | " +
                $"Raw RMS: {rawRms:F6}"
            );
        }

        EmitBassSignal(mutedSignal);
    }

    private void RejectCandidate(string reason)
    {
        bool candidateWasFromActive = inputState == BassInputState.CandidateFromActive;

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
                $"Source: {(candidateWasFromActive ? "Active" : "Idle")} | " +
                $"Checks: {candidateYinChecks} | " +
                $"Valid Frames: {candidateValidPitchFrames}/{requiredValidPitchFrames} | " +
                $"Raw RMS Peak: {candidatePeakRawRms:F6} | " +
                $"Smoothed RMS Peak: {candidatePeakSmoothedLevel:F6} | " +
                $"Last YIN Pitch Found: {lastYinPitchFoundText} | " +
                $"Last YIN Confidence: {lastYinConfidenceText} | " +
                $"Best YIN Confidence: {candidateBestConfidence:F3} | " +
                $"Best Frequency: {candidateBestFrequency:F2} Hz | " +
                $"Returning To: {(candidateWasFromActive ? "Active" : "Idle")}"
            );
        }

        if (candidateWasFromActive)
        {
            inputState = BassInputState.Active;
            activeQuietStartTime = -1f;
        }
        else
        {
            inputState = BassInputState.Idle;
        }
    }

    private void EmitBassSignal(BassSignal signal)
    {
        BassSignalEmitted?.Invoke(signal);

        bool shouldForwardToLegacyConfirmed =
            signal.signalType == BassSignalType.Started ||
            signal.signalType == BassSignalType.Continued ||
            sendMutedSignalsToLegacyConfirmedEvent;

        if (shouldForwardToLegacyConfirmed)
        {
            BassSignalConfirmed?.Invoke(signal);
        }
    }
}