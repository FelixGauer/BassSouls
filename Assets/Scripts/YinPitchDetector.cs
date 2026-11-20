using UnityEngine;

public class YinPitchDetector
{
    private readonly float[] yinBuffer;

    public YinPitchDetector(int maxBufferSize)
    {
        yinBuffer = new float[(maxBufferSize / 2) + 1];
    }

    public bool TryGetPitch(
        float[] audioBuffer,
        int sampleRate,
        float minFrequency,
        float maxFrequency,
        float threshold,
        float minRms,
        out float frequency,
        out float confidence)
    {
        frequency = 0f;
        confidence = 0f;

        int bufferSize = audioBuffer.Length;
        int halfBufferSize = bufferSize / 2;

        if (halfBufferSize >= yinBuffer.Length)
        {
            Debug.LogError("YIN buffer is too small.");
            return false;
        }

        // Basic RMS gate for pitch analysis only.
        float sumSquares = 0f;
        float mean = 0f;

        for (int i = 0; i < bufferSize; i++)
        {
            mean += audioBuffer[i];
        }

        mean /= bufferSize;

        for (int i = 0; i < bufferSize; i++)
        {
            float centered = audioBuffer[i] - mean;
            sumSquares += centered * centered;
        }

        float rms = Mathf.Sqrt(sumSquares / bufferSize);

        if (rms < minRms)
        {
            return false;
        }

        int tauMin = Mathf.FloorToInt(sampleRate / maxFrequency);
        int tauMax = Mathf.CeilToInt(sampleRate / minFrequency);

        tauMin = Mathf.Clamp(tauMin, 2, halfBufferSize - 1);
        tauMax = Mathf.Clamp(tauMax, tauMin + 1, halfBufferSize - 1);

        // Step 1: difference function.
        for (int tau = 0; tau <= tauMax; tau++)
        {
            yinBuffer[tau] = 0f;
        }

        for (int tau = 1; tau <= tauMax; tau++)
        {
            float difference = 0f;

            for (int i = 0; i < halfBufferSize; i++)
            {
                float delta = audioBuffer[i] - audioBuffer[i + tau];
                difference += delta * delta;
            }

            yinBuffer[tau] = difference;
        }

        // Step 2: cumulative mean normalized difference.
        yinBuffer[0] = 1f;
        float runningSum = 0f;

        for (int tau = 1; tau <= tauMax; tau++)
        {
            runningSum += yinBuffer[tau];

            if (runningSum > 0f)
            {
                yinBuffer[tau] *= tau / runningSum;
            }
            else
            {
                yinBuffer[tau] = 1f;
            }
        }

        // Step 3: absolute threshold.
        int tauEstimate = -1;

        for (int tau = tauMin; tau <= tauMax; tau++)
        {
            if (yinBuffer[tau] < threshold)
            {
                // Walk to local minimum.
                while (tau + 1 <= tauMax && yinBuffer[tau + 1] < yinBuffer[tau])
                {
                    tau++;
                }

                tauEstimate = tau;
                break;
            }
        }

        if (tauEstimate == -1)
        {
            // Optional fallback: choose best minimum in the allowed range.
            float bestValue = 1f;
            int bestTau = -1;

            for (int tau = tauMin; tau <= tauMax; tau++)
            {
                if (yinBuffer[tau] < bestValue)
                {
                    bestValue = yinBuffer[tau];
                    bestTau = tau;
                }
            }

            if (bestTau == -1 || bestValue > 0.3f)
            {
                return false;
            }

            tauEstimate = bestTau;
        }

        // Step 4: parabolic interpolation around the minimum.
        float betterTau = tauEstimate;

        if (tauEstimate > tauMin && tauEstimate < tauMax)
        {
            float left = yinBuffer[tauEstimate - 1];
            float center = yinBuffer[tauEstimate];
            float right = yinBuffer[tauEstimate + 1];

            float denominator = left - 2f * center + right;

            if (Mathf.Abs(denominator) > 0.000001f)
            {
                betterTau += 0.5f * (left - right) / denominator;
            }
        }

        frequency = sampleRate / betterTau;
        confidence = 1f - yinBuffer[tauEstimate];

        return frequency >= minFrequency && frequency <= maxFrequency;
    }
}