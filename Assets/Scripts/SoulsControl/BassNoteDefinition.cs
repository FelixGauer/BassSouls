using System;
using UnityEngine;

[Serializable]
public class BassNoteDefinition
{
    public string noteName;

    public int midiNumber;

    [Min(0f)] public float centerFrequency;
    [Min(0f)] public float minFrequency;
    [Min(0f)] public float maxFrequency;

    public bool enabled = true;

    public bool MatchesFrequency(float frequency)
    {
        if (!enabled)
        {
            return false;
        }

        return frequency >= minFrequency && frequency <= maxFrequency;
    }

    public float GetDistanceToCenter(float frequency)
    {
        return Mathf.Abs(frequency - centerFrequency);
    }
}