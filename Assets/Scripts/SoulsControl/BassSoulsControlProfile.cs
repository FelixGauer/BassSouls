using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "BassSoulsControlProfile",
    menuName = "Bass Souls Controller/Control Profile"
)]
public class BassSoulsControlProfile : ScriptableObject
{
    [Header("Generated Notes")]
    public List<BassNoteDefinition> noteDefinitions = new List<BassNoteDefinition>();

    [Header("State Intent Maps")]
    public List<SoulsStateIntentMap> stateMaps = new List<SoulsStateIntentMap>();

    [Header("Runtime Defaults")]
    public SoulsControlState initialState = SoulsControlState.Default;

    [Min(0f)] public float movementHoldSeconds = 1.0f;
    [Min(0f)] public float attackHoldSeconds = 0.5f;

    [Header("Stick Strengths")]
    [Range(0f, 1f)] public float movementStickStrength = 1.0f;
    [Range(0f, 1f)] public float cameraStickStrength = 0.45f;

    private struct HardcodedNote
    {
        public string noteName;
        public int midiNumber;
        public float centerFrequency;

        public HardcodedNote(string noteName, int midiNumber, float centerFrequency)
        {
            this.noteName = noteName;
            this.midiNumber = midiNumber;
            this.centerFrequency = centerFrequency;
        }
    }

    private static readonly HardcodedNote[] HardcodedNotes =
    {
        new HardcodedNote("E2",  40, 82.41f),
        new HardcodedNote("F2",  41, 87.31f),
        new HardcodedNote("F#2", 42, 92.50f),
        new HardcodedNote("G2",  43, 98.00f),
        new HardcodedNote("G#2", 44, 103.83f),
        new HardcodedNote("A2",  45, 110.00f),
        new HardcodedNote("A#2", 46, 116.54f),
        new HardcodedNote("B2",  47, 123.47f),

        new HardcodedNote("C3",  48, 130.81f),
        new HardcodedNote("C#3", 49, 138.59f),
        new HardcodedNote("D3",  50, 146.83f),
        new HardcodedNote("D#3", 51, 155.56f),
        new HardcodedNote("E3",  52, 164.81f),
        new HardcodedNote("F3",  53, 174.61f),
        new HardcodedNote("F#3", 54, 185.00f),
        new HardcodedNote("G3",  55, 196.00f),
        new HardcodedNote("G#3", 56, 207.65f),
        new HardcodedNote("A3",  57, 220.00f),
        new HardcodedNote("A#3", 58, 233.08f),
        new HardcodedNote("B3",  59, 246.94f),

        new HardcodedNote("C4",  60, 261.63f)
    };

    [ContextMenu("Load Hardcoded Notes E2-C4")]
    public void LoadHardcodedNotes()
    {
        noteDefinitions.Clear();

        for (int i = 0; i < HardcodedNotes.Length; i++)
        {
            HardcodedNote currentNote = HardcodedNotes[i];

            float minFrequency;
            float maxFrequency;

            if (i == 0)
            {
                float virtualPreviousCenter = GetVirtualAdjacentFrequency(
                    currentNote.centerFrequency,
                    -1
                );

                minFrequency = GetGeometricMidpoint(
                    virtualPreviousCenter,
                    currentNote.centerFrequency
                );
            }
            else
            {
                minFrequency = GetGeometricMidpoint(
                    HardcodedNotes[i - 1].centerFrequency,
                    currentNote.centerFrequency
                );
            }

            if (i == HardcodedNotes.Length - 1)
            {
                float virtualNextCenter = GetVirtualAdjacentFrequency(
                    currentNote.centerFrequency,
                    1
                );

                maxFrequency = GetGeometricMidpoint(
                    currentNote.centerFrequency,
                    virtualNextCenter
                );
            }
            else
            {
                maxFrequency = GetGeometricMidpoint(
                    currentNote.centerFrequency,
                    HardcodedNotes[i + 1].centerFrequency
                );
            }

            BassNoteDefinition noteDefinition = new BassNoteDefinition
            {
                noteName = currentNote.noteName,
                midiNumber = currentNote.midiNumber,
                centerFrequency = currentNote.centerFrequency,
                minFrequency = minFrequency,
                maxFrequency = maxFrequency,
                enabled = true
            };

            noteDefinitions.Add(noteDefinition);
        }

        EnsureStateMapsExist();
        SyncStateMapBindingsToNotes();
    }

    [ContextMenu("Ensure State Maps Exist")]
    public void EnsureStateMapsExist()
    {
        EnsureStateMapExists(SoulsControlState.Default);
        EnsureStateMapExists(SoulsControlState.LockedOn);
    }

    [ContextMenu("Sync State Bindings To Notes")]
    public void SyncStateMapBindingsToNotes()
    {
        if (stateMaps == null)
        {
            stateMaps = new List<SoulsStateIntentMap>();
        }

        for (int stateIndex = 0; stateIndex < stateMaps.Count; stateIndex++)
        {
            SoulsStateIntentMap stateMap = stateMaps[stateIndex];

            if (stateMap == null)
            {
                continue;
            }

            for (int noteIndex = 0; noteIndex < noteDefinitions.Count; noteIndex++)
            {
                BassNoteDefinition note = noteDefinitions[noteIndex];

                if (note == null)
                {
                    continue;
                }

                stateMap.GetOrCreateBinding(note.midiNumber, note.noteName);
            }
        }
    }

    public bool TryResolveFrequency(
        float frequency,
        SoulsControlState state,
        out BassNoteDefinition matchedNote,
        out SoulsIntent matchedIntent
    )
    {
        matchedNote = null;
        matchedIntent = SoulsIntent.None;

        BassNoteDefinition note = FindBestMatchingNote(frequency);

        if (note == null)
        {
            return false;
        }

        matchedNote = note;
        matchedIntent = GetIntentForNote(note.midiNumber, state);

        return true;
    }

    public BassNoteDefinition FindBestMatchingNote(float frequency)
    {
        BassNoteDefinition bestNote = null;
        float bestDistance = float.MaxValue;

        if (noteDefinitions == null)
        {
            return null;
        }

        for (int i = 0; i < noteDefinitions.Count; i++)
        {
            BassNoteDefinition note = noteDefinitions[i];

            if (note == null)
            {
                continue;
            }

            if (!note.MatchesFrequency(frequency))
            {
                continue;
            }

            float distance = note.GetDistanceToCenter(frequency);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestNote = note;
            }
        }

        return bestNote;
    }

    public BassNoteDefinition FindClosestNote(float frequency)
    {
        BassNoteDefinition closestNote = null;
        float closestDistance = float.MaxValue;

        if (noteDefinitions == null)
        {
            return null;
        }

        for (int i = 0; i < noteDefinitions.Count; i++)
        {
            BassNoteDefinition note = noteDefinitions[i];

            if (note == null)
            {
                continue;
            }

            float distance = note.GetDistanceToCenter(frequency);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestNote = note;
            }
        }

        return closestNote;
    }

    public SoulsIntent GetIntentForNote(int midiNumber, SoulsControlState state)
    {
        SoulsStateIntentMap stateMap = GetStateMap(state);

        if (stateMap == null)
        {
            return SoulsIntent.None;
        }

        return stateMap.GetIntentForMidiNumber(midiNumber);
    }

    public SoulsStateIntentMap GetStateMap(SoulsControlState state)
    {
        if (stateMaps == null)
        {
            return null;
        }

        for (int i = 0; i < stateMaps.Count; i++)
        {
            SoulsStateIntentMap stateMap = stateMaps[i];

            if (stateMap == null)
            {
                continue;
            }

            if (stateMap.state == state)
            {
                return stateMap;
            }
        }

        return null;
    }

    private void EnsureStateMapExists(SoulsControlState state)
    {
        if (stateMaps == null)
        {
            stateMaps = new List<SoulsStateIntentMap>();
        }

        for (int i = 0; i < stateMaps.Count; i++)
        {
            SoulsStateIntentMap existingMap = stateMaps[i];

            if (existingMap == null)
            {
                continue;
            }

            if (existingMap.state == state)
            {
                return;
            }
        }

        SoulsStateIntentMap newMap = new SoulsStateIntentMap
        {
            state = state,
            bindings = new List<NoteIntentBinding>()
        };

        stateMaps.Add(newMap);
    }

    private static float GetGeometricMidpoint(float lowerFrequency, float upperFrequency)
    {
        return Mathf.Sqrt(lowerFrequency * upperFrequency);
    }

    private static float GetVirtualAdjacentFrequency(float centerFrequency, int semitoneOffset)
    {
        return centerFrequency * Mathf.Pow(2f, semitoneOffset / 12f);
    }
}