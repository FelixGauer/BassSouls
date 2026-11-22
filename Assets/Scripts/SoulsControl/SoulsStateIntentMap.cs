using System;
using System.Collections.Generic;

[Serializable]
public class SoulsStateIntentMap
{
    public SoulsControlState state = SoulsControlState.Default;

    public List<NoteIntentBinding> bindings = new List<NoteIntentBinding>();

    public SoulsIntent GetIntentForMidiNumber(int midiNumber)
    {
        if (bindings == null)
        {
            return SoulsIntent.None;
        }

        for (int i = 0; i < bindings.Count; i++)
        {
            NoteIntentBinding binding = bindings[i];

            if (binding == null)
            {
                continue;
            }

            if (binding.midiNumber == midiNumber)
            {
                return binding.intent;
            }
        }

        return SoulsIntent.None;
    }

    public NoteIntentBinding GetOrCreateBinding(int midiNumber, string noteName)
    {
        if (bindings == null)
        {
            bindings = new List<NoteIntentBinding>();
        }

        for (int i = 0; i < bindings.Count; i++)
        {
            NoteIntentBinding binding = bindings[i];

            if (binding == null)
            {
                continue;
            }

            if (binding.midiNumber == midiNumber)
            {
                binding.noteName = noteName;
                return binding;
            }
        }

        NoteIntentBinding newBinding = new NoteIntentBinding
        {
            midiNumber = midiNumber,
            noteName = noteName,
            intent = SoulsIntent.None
        };

        bindings.Add(newBinding);
        return newBinding;
    }

    public void RemoveDuplicateBindingsAndSyncNames(List<BassNoteDefinition> noteDefinitions)
    {
        if (bindings == null)
        {
            bindings = new List<NoteIntentBinding>();
        }

        Dictionary<int, NoteIntentBinding> uniqueBindingsByMidi =
            new Dictionary<int, NoteIntentBinding>();

        for (int i = 0; i < bindings.Count; i++)
        {
            NoteIntentBinding binding = bindings[i];

            if (binding == null)
            {
                continue;
            }

            string correctNoteName = GetNoteNameForMidi(noteDefinitions, binding.midiNumber);

            if (!string.IsNullOrEmpty(correctNoteName))
            {
                binding.noteName = correctNoteName;
            }

            if (!uniqueBindingsByMidi.TryGetValue(binding.midiNumber, out NoteIntentBinding existingBinding))
            {
                uniqueBindingsByMidi.Add(binding.midiNumber, binding);
                continue;
            }

            // Preserve a non-None intent if one duplicate has it.
            if (existingBinding.intent == SoulsIntent.None && binding.intent != SoulsIntent.None)
            {
                existingBinding.intent = binding.intent;
            }

            if (!string.IsNullOrEmpty(binding.noteName))
            {
                existingBinding.noteName = binding.noteName;
            }
        }

        bindings.Clear();

        if (noteDefinitions != null)
        {
            for (int i = 0; i < noteDefinitions.Count; i++)
            {
                BassNoteDefinition note = noteDefinitions[i];

                if (note == null)
                {
                    continue;
                }

                if (uniqueBindingsByMidi.TryGetValue(note.midiNumber, out NoteIntentBinding existingBinding))
                {
                    existingBinding.noteName = note.noteName;
                    bindings.Add(existingBinding);
                }
                else
                {
                    bindings.Add(new NoteIntentBinding
                    {
                        midiNumber = note.midiNumber,
                        noteName = note.noteName,
                        intent = SoulsIntent.None
                    });
                }
            }
        }
    }

    private string GetNoteNameForMidi(List<BassNoteDefinition> noteDefinitions, int midiNumber)
    {
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

            if (note.midiNumber == midiNumber)
            {
                return note.noteName;
            }
        }

        return null;
    }
}