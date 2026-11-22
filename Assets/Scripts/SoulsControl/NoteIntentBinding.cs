using System;

[Serializable]
public class NoteIntentBinding
{
    public int midiNumber;
    public string noteName;

    public SoulsIntent intent = SoulsIntent.None;
}