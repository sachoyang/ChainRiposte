using System;
using ChainRiposte.Game.Config;
using UnityEditor;
using UnityEngine;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// 보스 채보 에디터 (GDD §5.2).
    /// 패턴마다 박자 타임라인을 띄우고 클릭으로 노트를 찍는다 — 배열 인덱스를 손으로 만지지 않게 하는 것이 목적.
    ///
    /// 노트 하나는 <b>타격 시점(박)</b>과 <b>예비동작 길이(박)</b>만 갖는다.
    /// 연속기는 별도 타입이 아니라 노트를 촘촘히 찍으면 된다.
    /// </summary>
    [CustomEditor(typeof(BossDataSO))]
    public sealed class BossDataSOEditor : UnityEditor.Editor
    {
        private const float TimelineHeight = 74f;
        private const float NoteHitWidth = 9f;

        private static readonly Color GridColor = new(1f, 1f, 1f, 0.10f);
        private static readonly Color BeatColor = new(1f, 1f, 1f, 0.28f);
        private static readonly Color TrackColor = new(0.12f, 0.11f, 0.15f);
        private static readonly Color TelegraphColor = new(0.55f, 0.16f, 0.18f, 0.65f);
        private static readonly Color NoteColor = new(0.98f, 0.96f, 0.92f);
        private static readonly Color SelectedColor = new(0.98f, 0.78f, 0.25f);
        private static readonly Color LeadInColor = new(0.9f, 0.5f, 0.1f, 0.25f);

        private static readonly float[] SnapValues = { 1f, 0.5f, 0.25f };
        private static readonly string[] SnapLabels = { "1박", "1/2박", "1/4박" };

        private int _snapIndex = 2;
        private int _selectedPattern = -1;
        private int _selectedNote = -1;
        private BossChartPreview _preview;

        private void OnEnable()
        {
            _preview = new BossChartPreview(Repaint);
            EditorApplication.update += _preview.Tick;
        }

        private void OnDisable()
        {
            if (_preview == null)
                return;

            // 인스펙터를 떠나도 소리가 계속 나면 안 된다 — 다른 에셋을 고르는 순간 멈춘다.
            EditorApplication.update -= _preview.Tick;
            _preview.Stop();
            _preview = null;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawPropertiesExcluding(serializedObject, "m_Script", "patterns", "phases");

            EditorGUILayout.Space(8f);
            DrawPatterns();
            EditorGUILayout.Space(8f);
            DrawPhases();

            serializedObject.ApplyModifiedProperties();
        }

        // ── 패턴 ──────────────────────────────────────────────────────

        private void DrawPatterns()
        {
            SerializedProperty patterns = serializedObject.FindProperty("patterns");

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("채보 (패턴)", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                _snapIndex = GUILayout.Toolbar(_snapIndex, SnapLabels, GUILayout.Width(180f));
                if (GUILayout.Button("+ 패턴", GUILayout.Width(70f)))
                    AddPattern(patterns);
            }

            if (patterns.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "패턴이 없습니다. '+ 패턴'으로 하나 만들고 타임라인을 클릭해 노트를 찍으세요.\n" +
                    "노트가 없는 박은 플레이어의 공격 기회입니다.", MessageType.Info);
                return;
            }

            for (int i = 0; i < patterns.arraySize; i++)
                DrawPattern(patterns, i);
        }

        private void DrawPattern(SerializedProperty patterns, int index)
        {
            SerializedProperty pattern = patterns.GetArrayElementAtIndex(index);
            SerializedProperty name = pattern.FindPropertyRelative("name");
            SerializedProperty bpm = pattern.FindPropertyRelative("bpm");
            SerializedProperty lengthBeats = pattern.FindPropertyRelative("lengthBeats");
            SerializedProperty speed = pattern.FindPropertyRelative("speedMultiplier");
            SerializedProperty notes = pattern.FindPropertyRelative("notes");

            using var box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"[{index}]", GUILayout.Width(28f));
                EditorGUILayout.PropertyField(name, GUIContent.none);
                if (GUILayout.Button("복제", GUILayout.Width(46f)))
                {
                    patterns.InsertArrayElementAtIndex(index);
                    return;
                }

                if (GUILayout.Button("삭제", GUILayout.Width(46f)))
                {
                    patterns.DeleteArrayElementAtIndex(index);
                    _selectedPattern = -1;
                    return;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(bpm, new GUIContent("BPM"), GUILayout.MinWidth(90f));
                EditorGUILayout.PropertyField(lengthBeats, new GUIContent("길이(박)"), GUILayout.MinWidth(90f));
                EditorGUILayout.PropertyField(speed, new GUIContent("배율"), GUILayout.MinWidth(80f));
            }

            float secondsPerBeat = 60f / Mathf.Max(0.01f, bpm.floatValue * speed.floatValue);
            EditorGUILayout.LabelField(
                $"→ 한 박 {secondsPerBeat:0.###}초 · 패턴 전체 {lengthBeats.floatValue * secondsPerBeat:0.##}초 · 노트 {notes.arraySize}개",
                EditorStyles.miniLabel);

            DrawTimeline(index, lengthBeats.floatValue, notes, secondsPerBeat);

            BossChartPreview.PatternTiming timing = BuildTiming(notes, secondsPerBeat, lengthBeats.floatValue);
            _preview.DrawTransport(index, timing);
            _preview.DrawStage(index, timing);

            DrawSelectedNote(index, notes);
        }

        /// <summary>
        /// 인스펙터의 박 단위 값을 초로 옮긴다. 노트의 개별 배율은 <b>예비동작만</b> 줄인다 —
        /// 타격 시점은 패턴의 박이 정하고 배율은 "얼마나 일찍 보이나"만 바꾸는 것이 채보의 규칙이다.
        /// </summary>
        private static BossChartPreview.PatternTiming BuildTiming(
            SerializedProperty notes, float secondsPerBeat, float lengthBeats)
        {
            var built = new BossChartPreview.PatternTiming.Note[notes.arraySize];
            for (int i = 0; i < notes.arraySize; i++)
            {
                SerializedProperty note = notes.GetArrayElementAtIndex(i);
                float beat = note.FindPropertyRelative("beat").floatValue;
                float telegraphBeats = note.FindPropertyRelative("telegraphBeats").floatValue
                                       / Mathf.Max(0.01f, note.FindPropertyRelative("speedMultiplier").floatValue);

                built[i] = new BossChartPreview.PatternTiming.Note(
                    beat * secondsPerBeat, telegraphBeats * secondsPerBeat);
            }

            // 미리보기는 노트를 시간 순으로 훑는다 — 인스펙터에서 찍은 순서는 뒤죽박죽일 수 있다.
            Array.Sort(built, (a, b) => a.HitSeconds.CompareTo(b.HitSeconds));
            return new BossChartPreview.PatternTiming(built, secondsPerBeat, lengthBeats);
        }

        private void DrawTimeline(
            int patternIndex, float lengthBeats, SerializedProperty notes, float secondsPerBeat)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, TimelineHeight);
            EditorGUI.DrawRect(rect, TrackColor);

            float snap = SnapValues[_snapIndex];
            float perBeat = rect.width / Mathf.Max(0.01f, lengthBeats);

            // 눈금 — 정박은 진하게, 스냅 단위는 연하게
            for (float beat = 0f; beat <= lengthBeats + 0.001f; beat += snap)
            {
                float x = rect.x + beat * perBeat;
                bool onBeat = Mathf.Abs(beat - Mathf.Round(beat)) < 0.001f;
                EditorGUI.DrawRect(new Rect(x, rect.y, 1f, rect.height), onBeat ? BeatColor : GridColor);
                if (onBeat && beat < lengthBeats)
                    GUI.Label(new Rect(x + 3f, rect.y, 24f, 14f), $"{beat + 1:0}", EditorStyles.miniLabel);
            }

            // 노트 — 예비동작을 왼쪽으로 뻗는 막대로 그려 '언제부터 보이는지'가 눈에 들어오게 한다
            for (int i = 0; i < notes.arraySize; i++)
            {
                SerializedProperty note = notes.GetArrayElementAtIndex(i);
                float beat = note.FindPropertyRelative("beat").floatValue;
                float telegraph = note.FindPropertyRelative("telegraphBeats").floatValue
                                  / Mathf.Max(0.01f, note.FindPropertyRelative("speedMultiplier").floatValue);

                float hitX = rect.x + beat * perBeat;
                float startX = rect.x + (beat - telegraph) * perBeat;
                bool selected = patternIndex == _selectedPattern && i == _selectedNote;

                // 패턴 시작보다 앞서 감기 시작하는 부분 — 리드인으로 자동 처리되지만 눈에 보이게 표시
                if (startX < rect.x)
                {
                    EditorGUI.DrawRect(new Rect(rect.x, rect.y + 20f, hitX - rect.x, 22f), LeadInColor);
                    startX = rect.x;
                }

                EditorGUI.DrawRect(new Rect(startX, rect.y + 24f, Mathf.Max(1f, hitX - startX), 14f), TelegraphColor);
                EditorGUI.DrawRect(
                    new Rect(hitX - NoteHitWidth * 0.5f, rect.y + 14f, NoteHitWidth, 36f),
                    selected ? SelectedColor : NoteColor);
            }

            _preview.DrawPlayhead(patternIndex, rect, lengthBeats, secondsPerBeat);

            HandleTimelineInput(rect, patternIndex, lengthBeats, perBeat, snap, notes);
        }

        private void HandleTimelineInput(
            Rect rect, int patternIndex, float lengthBeats, float perBeat, float snap, SerializedProperty notes)
        {
            Event evt = Event.current;
            if (!rect.Contains(evt.mousePosition))
                return;

            if (evt.type != EventType.MouseDown && evt.type != EventType.MouseDrag)
                return;

            float rawBeat = (evt.mousePosition.x - rect.x) / perBeat;
            float snapped = Mathf.Clamp(Mathf.Round(rawBeat / snap) * snap, 0f, lengthBeats);

            if (evt.type == EventType.MouseDown)
            {
                int hit = FindNoteNear(notes, rawBeat, NoteHitWidth * 0.5f / perBeat);

                if (evt.button == 1) // 우클릭 = 삭제
                {
                    if (hit >= 0)
                    {
                        notes.DeleteArrayElementAtIndex(hit);
                        _selectedNote = -1;
                    }

                    evt.Use();
                    return;
                }

                _selectedPattern = patternIndex;
                _selectedNote = hit >= 0 ? hit : AddNote(notes, snapped);
                evt.Use();
                return;
            }

            // 드래그 = 선택한 노트를 옮긴다
            if (patternIndex == _selectedPattern && _selectedNote >= 0 && _selectedNote < notes.arraySize)
            {
                notes.GetArrayElementAtIndex(_selectedNote).FindPropertyRelative("beat").floatValue = snapped;
                evt.Use();
            }
        }

        private void DrawSelectedNote(int patternIndex, SerializedProperty notes)
        {
            if (patternIndex != _selectedPattern || _selectedNote < 0 || _selectedNote >= notes.arraySize)
            {
                EditorGUILayout.LabelField(
                    "타임라인 클릭 = 노트 추가/선택 · 드래그 = 이동 · 우클릭 = 삭제", EditorStyles.miniLabel);
                return;
            }

            SerializedProperty note = notes.GetArrayElementAtIndex(_selectedNote);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    $"노트 {note.FindPropertyRelative("beat").floatValue:0.##}박", GUILayout.Width(80f));
                EditorGUILayout.PropertyField(
                    note.FindPropertyRelative("telegraphBeats"), new GUIContent("예비(박)"), GUILayout.MinWidth(90f));
                EditorGUILayout.PropertyField(
                    note.FindPropertyRelative("speedMultiplier"), new GUIContent("배율"), GUILayout.MinWidth(70f));
                EditorGUILayout.PropertyField(
                    note.FindPropertyRelative("damage"), new GUIContent("피해"), GUILayout.MinWidth(70f));
                if (GUILayout.Button("삭제", GUILayout.Width(46f)))
                {
                    notes.DeleteArrayElementAtIndex(_selectedNote);
                    _selectedNote = -1;
                }
            }
        }

        private static int FindNoteNear(SerializedProperty notes, float beat, float tolerance)
        {
            for (int i = 0; i < notes.arraySize; i++)
            {
                float existing = notes.GetArrayElementAtIndex(i).FindPropertyRelative("beat").floatValue;
                if (Mathf.Abs(existing - beat) <= tolerance)
                    return i;
            }

            return -1;
        }

        private static int AddNote(SerializedProperty notes, float beat)
        {
            int index = notes.arraySize;
            notes.InsertArrayElementAtIndex(index);
            SerializedProperty note = notes.GetArrayElementAtIndex(index);
            note.FindPropertyRelative("beat").floatValue = beat;
            // 1박은 준비 시간이 짧아 원이 갑자기 튀어나온다 — 읽을 수 있는 기본값으로 둔다
            note.FindPropertyRelative("telegraphBeats").floatValue = 1.5f;
            note.FindPropertyRelative("speedMultiplier").floatValue = 1f;
            note.FindPropertyRelative("damage").floatValue = 12f;
            return index;
        }

        private static void AddPattern(SerializedProperty patterns)
        {
            int index = patterns.arraySize;
            patterns.InsertArrayElementAtIndex(index);
            SerializedProperty pattern = patterns.GetArrayElementAtIndex(index);
            pattern.FindPropertyRelative("name").stringValue = $"Pattern {index}";
            pattern.FindPropertyRelative("bpm").floatValue = 120f;
            pattern.FindPropertyRelative("lengthBeats").floatValue = 8f;
            pattern.FindPropertyRelative("speedMultiplier").floatValue = 1f;
            pattern.FindPropertyRelative("notes").ClearArray();
        }

        // ── 페이즈 ────────────────────────────────────────────────────

        private void DrawPhases()
        {
            SerializedProperty phases = serializedObject.FindProperty("phases");
            SerializedProperty patterns = serializedObject.FindProperty("patterns");

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("HP 페이즈", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("+ 페이즈", GUILayout.Width(80f)))
                {
                    int index = phases.arraySize;
                    phases.InsertArrayElementAtIndex(index);
                    phases.GetArrayElementAtIndex(index).FindPropertyRelative("hpRatioAtOrBelow").floatValue = 1f;
                }
            }

            if (phases.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "페이즈가 없으면 모든 패턴을 균등하게 쓰는 단일 페이즈로 동작합니다.\n" +
                    "체력이 깎일수록 험한 패턴을 쓰게 하려면 페이즈를 추가하세요.", MessageType.Info);
                return;
            }

            for (int i = 0; i < phases.arraySize; i++)
            {
                SerializedProperty phase = phases.GetArrayElementAtIndex(i);
                SerializedProperty threshold = phase.FindPropertyRelative("hpRatioAtOrBelow");
                SerializedProperty entries = phase.FindPropertyRelative("patterns");

                using var box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(threshold, new GUIContent("HP 비율 이하"));
                    if (GUILayout.Button("+ 패턴", GUILayout.Width(70f)))
                    {
                        int index = entries.arraySize;
                        entries.InsertArrayElementAtIndex(index);
                        entries.GetArrayElementAtIndex(index).FindPropertyRelative("weight").floatValue = 1f;
                    }

                    if (GUILayout.Button("삭제", GUILayout.Width(46f)))
                    {
                        phases.DeleteArrayElementAtIndex(i);
                        return;
                    }
                }

                for (int j = 0; j < entries.arraySize; j++)
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(j);
                    SerializedProperty patternIndex = entry.FindPropertyRelative("patternIndex");

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        patternIndex.intValue = EditorGUILayout.Popup(
                            patternIndex.intValue, PatternNames(patterns), GUILayout.MinWidth(140f));
                        EditorGUILayout.PropertyField(
                            entry.FindPropertyRelative("weight"), new GUIContent("가중치"), GUILayout.MinWidth(90f));
                        if (GUILayout.Button("x", GUILayout.Width(24f)))
                        {
                            entries.DeleteArrayElementAtIndex(j);
                            break;
                        }
                    }

                    if (patternIndex.intValue >= patterns.arraySize)
                        EditorGUILayout.HelpBox("없는 패턴을 가리킵니다 — 무시됩니다.", MessageType.Warning);
                }
            }
        }

        private static string[] PatternNames(SerializedProperty patterns)
        {
            var names = new string[Mathf.Max(1, patterns.arraySize)];
            if (patterns.arraySize == 0)
            {
                names[0] = "(패턴 없음)";
                return names;
            }

            for (int i = 0; i < patterns.arraySize; i++)
            {
                string name = patterns.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
                names[i] = $"[{i}] {(string.IsNullOrWhiteSpace(name) ? "Pattern" : name)}";
            }

            return names;
        }
    }
}
