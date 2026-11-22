using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BassSoulsControlProfile))]
public class BassSoulsControlProfileEditor : Editor
{
    private SoulsControlState selectedState = SoulsControlState.Default;

    private int firstVisibleFret = 10;
    private const int VisibleFretCount = 5;

    private const int CellWidth = 105;
    private const int CellHeight = 72;
    private const int StringLabelWidth = 55;

    private struct BassStringInfo
    {
        public string stringName;
        public int openMidiNumber;

        public BassStringInfo(string stringName, int openMidiNumber)
        {
            this.stringName = stringName;
            this.openMidiNumber = openMidiNumber;
        }
    }

    // Standard 4-string bass tuning:
    // E1 A1 D2 G2
    //
    // Display order is your POV:
    // E string closest, G string furthest.
    private static readonly BassStringInfo[] BassStrings =
    {
        new BassStringInfo("E", 28),
        new BassStringInfo("A", 33),
        new BassStringInfo("D", 38),
        new BassStringInfo("G", 43)
    };

    public override void OnInspectorGUI()
    {
        BassSoulsControlProfile profile = (BassSoulsControlProfile)target;

        EditorGUILayout.LabelField("Bass Souls Control Profile", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Fretboard authoring view. Standard view is frets 10–14, matching the intended hand position.",
            MessageType.Info
        );

        EditorGUILayout.Space(5);

        DrawTopButtons(profile);

        EditorGUILayout.Space(10);

        DrawRuntimeDefaults(profile);

        EditorGUILayout.Space(10);

        DrawFretboardSettings();

        EditorGUILayout.Space(10);

        DrawStateSelector();

        EditorGUILayout.Space(10);

        DrawFretboard(profile);

        EditorGUILayout.Space(10);

        DrawLegend();

        EditorGUILayout.Space(10);

        DrawFallbackNoteTable(profile);
    }

    private void DrawTopButtons(BassSoulsControlProfile profile)
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Load Notes E2-C4"))
        {
            Undo.RecordObject(profile, "Load Notes E2-C4");
            profile.LoadHardcodedNotes();
            EditorUtility.SetDirty(profile);
        }

        if (GUILayout.Button("Ensure State Maps"))
        {
            Undo.RecordObject(profile, "Ensure State Maps");
            profile.EnsureStateMapsExist();
            EditorUtility.SetDirty(profile);
        }

        if (GUILayout.Button("Sync Bindings"))
        {
            Undo.RecordObject(profile, "Sync Bindings");
            profile.SyncStateMapBindingsToNotes();
            EditorUtility.SetDirty(profile);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawRuntimeDefaults(BassSoulsControlProfile profile)
    {
        EditorGUILayout.LabelField("Runtime Defaults", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        SoulsControlState initialState = (SoulsControlState)EditorGUILayout.EnumPopup(
            "Initial State",
            profile.initialState
        );

        float movementHoldSeconds = EditorGUILayout.FloatField(
            "Movement Hold Seconds",
            profile.movementHoldSeconds
        );

        float attackHoldSeconds = EditorGUILayout.FloatField(
            "Attack Hold Seconds",
            profile.attackHoldSeconds
        );

        float movementStickStrength = EditorGUILayout.Slider(
             "Movement Stick Strength",
            profile.movementStickStrength,
            0f,
            1f
        );

        float cameraStickStrength = EditorGUILayout.Slider(
            "Camera Stick Strength",
            profile.cameraStickStrength,
            0f,
            1f
        );

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(profile, "Edit Runtime Defaults");

            profile.initialState = initialState;
            profile.movementHoldSeconds = Mathf.Max(0f, movementHoldSeconds);
            profile.attackHoldSeconds = Mathf.Max(0f, attackHoldSeconds);
            profile.movementStickStrength = Mathf.Clamp01(movementStickStrength);
            profile.cameraStickStrength = Mathf.Clamp01(cameraStickStrength);

            EditorUtility.SetDirty(profile);
        }
    }

    private void DrawFretboardSettings()
    {
        EditorGUILayout.LabelField("Fretboard View", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        firstVisibleFret = EditorGUILayout.IntField(
            "First Fret",
            firstVisibleFret
        );

        EditorGUILayout.LabelField(
            "Visible Frets: 5",
            GUILayout.Width(120)
        );

        EditorGUILayout.EndHorizontal();

        firstVisibleFret = Mathf.Clamp(firstVisibleFret, 0, 20);

        EditorGUILayout.HelpBox(
            $"Showing frets {firstVisibleFret}–{firstVisibleFret + VisibleFretCount - 1}. " +
            "Default is 10–14 for the intended bass hand position.",
            MessageType.None
        );

        if (GUILayout.Button("Reset View To Frets 10–14"))
        {
            firstVisibleFret = 10;
        }
    }

    private void DrawStateSelector()
    {
        EditorGUILayout.LabelField("Intent Authoring", EditorStyles.boldLabel);

        selectedState = (SoulsControlState)EditorGUILayout.EnumPopup(
            "Editing State",
            selectedState
        );
    }

    private void DrawFretboard(BassSoulsControlProfile profile)
    {
        SoulsStateIntentMap stateMap = profile.GetStateMap(selectedState);

        if (stateMap == null)
        {
            EditorGUILayout.HelpBox(
                $"No state map exists for {selectedState}. Click 'Ensure State Maps'.",
                MessageType.Warning
            );

            return;
        }

        if (profile.noteDefinitions == null || profile.noteDefinitions.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No notes loaded. Click 'Load Notes E2-C4'.",
                MessageType.Warning
            );

            return;
        }

        EditorGUILayout.LabelField("Fretboard", EditorStyles.boldLabel);

        DrawFretNumbersRow();

        for (int stringIndex = BassStrings.Length - 1; stringIndex >= 0; stringIndex--)
        {
            // Furthest string first, closest E string last.
            DrawStringRow(profile, stateMap, BassStrings[stringIndex]);
        }

        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField(
            "Closest to you: E string",
            EditorStyles.miniBoldLabel
        );
    }

    private void DrawFretNumbersRow()
    {
        EditorGUILayout.BeginHorizontal();

        GUILayout.Space(StringLabelWidth);

        for (int fret = firstVisibleFret; fret < firstVisibleFret + VisibleFretCount; fret++)
        {
            GUILayout.Label(
                $"Fret {fret}",
                EditorStyles.boldLabel,
                GUILayout.Width(CellWidth)
            );
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawStringRow(
        BassSoulsControlProfile profile,
        SoulsStateIntentMap stateMap,
        BassStringInfo bassString
    )
    {
        EditorGUILayout.BeginHorizontal();

        GUILayout.Label(
            $"{bassString.stringName} string",
            EditorStyles.boldLabel,
            GUILayout.Width(StringLabelWidth),
            GUILayout.Height(CellHeight)
        );

        for (int fret = firstVisibleFret; fret < firstVisibleFret + VisibleFretCount; fret++)
        {
            int midiNumber = bassString.openMidiNumber + fret;
            BassNoteDefinition note = FindNoteByMidi(profile, midiNumber);

            DrawFretCell(profile, stateMap, bassString, fret, midiNumber, note);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawFretCell(
        BassSoulsControlProfile profile,
        SoulsStateIntentMap stateMap,
        BassStringInfo bassString,
        int fret,
        int midiNumber,
        BassNoteDefinition note
    )
    {
        Color previousBackgroundColor = GUI.backgroundColor;

        bool noteIsInProfile = note != null;
        SoulsIntent currentIntent = SoulsIntent.None;

        NoteIntentBinding binding = null;

        if (noteIsInProfile)
        {
            binding = stateMap.GetOrCreateBinding(
                note.midiNumber,
                note.noteName
            );

            currentIntent = binding.intent;
            GUI.backgroundColor = GetIntentColor(currentIntent);
        }
        else
        {
            GUI.backgroundColor = new Color(0.25f, 0.25f, 0.25f);
        }

        EditorGUILayout.BeginVertical(
            GUI.skin.box,
            GUILayout.Width(CellWidth),
            GUILayout.Height(CellHeight)
        );

        string noteLabel = noteIsInProfile ? note.noteName : GetNoteNameFromMidi(midiNumber);

        EditorGUILayout.LabelField(
            noteLabel,
            EditorStyles.boldLabel,
            GUILayout.Width(CellWidth - 10)
        );

        EditorGUILayout.LabelField(
            $"{bassString.stringName}{fret}",
            EditorStyles.miniLabel,
            GUILayout.Width(CellWidth - 10)
        );

        if (noteIsInProfile)
        {
            EditorGUI.BeginChangeCheck();

            SoulsIntent newIntent = (SoulsIntent)EditorGUILayout.EnumPopup(
                currentIntent,
                GUILayout.Width(CellWidth - 12)
            );

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(profile, "Assign Fret Intent");

                binding.intent = newIntent;
                binding.noteName = note.noteName;
                binding.midiNumber = note.midiNumber;

                EditorUtility.SetDirty(profile);
            }
        }
        else
        {
            EditorGUILayout.LabelField(
                "out",
                EditorStyles.centeredGreyMiniLabel,
                GUILayout.Width(CellWidth - 10)
            );
        }

        EditorGUILayout.EndVertical();

        GUI.backgroundColor = previousBackgroundColor;
    }

    private void DrawLegend()
    {
        EditorGUILayout.LabelField("Legend", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        DrawLegendSwatch("None", SoulsIntent.None);
        DrawLegendSwatch("Move", SoulsIntent.MoveForward);
        DrawLegendSwatch("Turn", SoulsIntent.TurnLeft);
        DrawLegendSwatch("Attack", SoulsIntent.LightAttack);
        DrawLegendSwatch("Roll", SoulsIntent.Roll);

        EditorGUILayout.EndHorizontal();
    }

    private void DrawLegendSwatch(string label, SoulsIntent intent)
    {
        Color previousBackgroundColor = GUI.backgroundColor;
        GUI.backgroundColor = GetIntentColor(intent);

        GUILayout.Box(label, GUILayout.Width(70), GUILayout.Height(22));

        GUI.backgroundColor = previousBackgroundColor;
    }

    private void DrawFallbackNoteTable(BassSoulsControlProfile profile)
    {
        EditorGUILayout.LabelField("Note List Reference", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Reference only. Main editing should happen on the fretboard above.",
            MessageType.None
        );

        SoulsStateIntentMap stateMap = profile.GetStateMap(selectedState);

        if (stateMap == null || profile.noteDefinitions == null)
        {
            return;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        for (int i = 0; i < profile.noteDefinitions.Count; i++)
        {
            BassNoteDefinition note = profile.noteDefinitions[i];

            if (note == null)
            {
                continue;
            }

            NoteIntentBinding binding = stateMap.GetOrCreateBinding(
                note.midiNumber,
                note.noteName
            );

            EditorGUILayout.BeginHorizontal();

            GUILayout.Label(note.noteName, GUILayout.Width(45));

            GUILayout.Label(
                $"{note.centerFrequency:F2} Hz",
                GUILayout.Width(75)
            );

            GUILayout.Label(
                $"{note.minFrequency:F2}-{note.maxFrequency:F2}",
                GUILayout.Width(130)
            );

            EditorGUI.BeginChangeCheck();

            SoulsIntent newIntent = (SoulsIntent)EditorGUILayout.EnumPopup(binding.intent);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(profile, "Change Note Intent");

                binding.intent = newIntent;
                binding.noteName = note.noteName;
                binding.midiNumber = note.midiNumber;

                EditorUtility.SetDirty(profile);
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    private BassNoteDefinition FindNoteByMidi(
        BassSoulsControlProfile profile,
        int midiNumber
    )
    {
        if (profile.noteDefinitions == null)
        {
            return null;
        }

        for (int i = 0; i < profile.noteDefinitions.Count; i++)
        {
            BassNoteDefinition note = profile.noteDefinitions[i];

            if (note == null)
            {
                continue;
            }

            if (note.midiNumber == midiNumber)
            {
                return note;
            }
        }

        return null;
    }

    private string GetNoteNameFromMidi(int midiNumber)
    {
        string[] noteNames =
        {
            "C",
            "C#",
            "D",
            "D#",
            "E",
            "F",
            "F#",
            "G",
            "G#",
            "A",
            "A#",
            "B"
        };

        int noteIndex = midiNumber % 12;
        int octave = (midiNumber / 12) - 1;

        return $"{noteNames[noteIndex]}{octave}";
    }

    private Color GetIntentColor(SoulsIntent intent)
    {
        switch (intent)
        {
            case SoulsIntent.MoveForward:
            case SoulsIntent.MoveBackward:
            case SoulsIntent.MoveLeft:
            case SoulsIntent.MoveRight:
                return new Color(0.35f, 0.65f, 0.35f);

            case SoulsIntent.TurnLeft:
            case SoulsIntent.TurnRight:
                return new Color(0.35f, 0.45f, 0.85f);

            case SoulsIntent.LightAttack:
                return new Color(0.85f, 0.35f, 0.35f);

            case SoulsIntent.Roll:
                return new Color(0.85f, 0.70f, 0.25f);

            case SoulsIntent.ToggleControlState:
                return new Color(0.65f, 0.35f, 0.85f);

            case SoulsIntent.ReturnToDefault:
                return new Color(0.25f, 0.75f, 0.75f);

            case SoulsIntent.None:
            default:
                return new Color(0.55f, 0.55f, 0.55f);
        }
    }
}