using ChainRiposte.Game.Config;
using UnityEditor;
using UnityEngine;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// StageDataSO 커스텀 인스펙터 — 보드 마스킹(O/X/W)을 문자열 대신
    /// <b>클릭해서 칠하는 그리드</b>로 시각화·편집한다 (B1: 사용자가 직접 요청했던 항목).
    /// 브러시(O=활성/X=구멍/W=벽)를 고르고 셀을 클릭(드래그)하면 칠해진다.
    /// 나머지 필드는 기본 인스펙터 그대로.
    /// </summary>
    [CustomEditor(typeof(StageDataSO))]
    public sealed class StageDataSOEditor : UnityEditor.Editor
    {
        private const float CellSize = 30f;
        private const float CellGap = 2f;

        private static readonly char[] BrushChars = { 'O', 'X', 'W' };
        private static readonly string[] BrushLabels = { "O 활성", "X 구멍", "W 벽" };

        private static readonly Color ActiveColor = new(0.30f, 0.55f, 0.35f);
        private static readonly Color HoleColor = new(0.16f, 0.15f, 0.18f);
        private static readonly Color WallColor = new(0.45f, 0.33f, 0.20f);
        private static readonly Color InvalidColor = Color.magenta;

        private int _brush; // BrushChars 인덱스
        private bool _painting; // 드래그 페인트 중

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty rows = serializedObject.FindProperty("boardRows");
            DrawBoardEditor(rows);

            EditorGUILayout.Space(12f);
            DrawPropertiesExcluding(serializedObject, "m_Script", "boardRows");

            serializedObject.ApplyModifiedProperties();
        }

        // ── 보드 그리드 에디터 ──

        private void DrawBoardEditor(SerializedProperty rows)
        {
            EditorGUILayout.LabelField("보드 형태 (클릭/드래그로 칠하기)", EditorStyles.boldLabel);

            int height = rows.arraySize;
            int width = height > 0 ? SafeRow(rows, 0).Length : 0;

            // 크기 조절
            EditorGUILayout.BeginHorizontal();
            int newWidth = EditorGUILayout.IntField("가로 x 세로", Mathf.Max(1, width), GUILayout.ExpandWidth(true));
            int newHeight = EditorGUILayout.IntField(Mathf.Max(1, height), GUILayout.Width(64f));
            EditorGUILayout.EndHorizontal();
            newWidth = Mathf.Clamp(newWidth, 1, 20);
            newHeight = Mathf.Clamp(newHeight, 1, 20);
            if (newWidth != width || newHeight != height)
            {
                Resize(rows, width, height, newWidth, newHeight);
                width = newWidth;
                height = newHeight;
            }

            // 브러시 선택 + 일괄 채우기
            EditorGUILayout.BeginHorizontal();
            _brush = GUILayout.Toolbar(_brush, BrushLabels);
            if (GUILayout.Button("전체 채움", GUILayout.Width(72f)))
            {
                for (int r = 0; r < height; r++)
                    rows.GetArrayElementAtIndex(r).stringValue = new string(BrushChars[_brush], width);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4f);

            // 그리드 — 인스펙터 표기 순서 그대로(첫 행 = 보드 최상단)
            Rect gridRect = GUILayoutUtility.GetRect(
                width * (CellSize + CellGap), height * (CellSize + CellGap));
            gridRect.x += Mathf.Max(0f, (gridRect.width - width * (CellSize + CellGap)) * 0.5f);

            Event evt = Event.current;
            if (evt.type == EventType.MouseUp)
                _painting = false;

            for (int r = 0; r < height; r++)
            {
                SerializedProperty rowProp = rows.GetArrayElementAtIndex(r);
                string row = rowProp.stringValue ?? string.Empty;
                if (row.Length != width)
                {
                    row = row.Length > width ? row[..width] : row.PadRight(width, 'O');
                    rowProp.stringValue = row;
                }

                for (int c = 0; c < width; c++)
                {
                    var cell = new Rect(
                        gridRect.x + c * (CellSize + CellGap),
                        gridRect.y + r * (CellSize + CellGap),
                        CellSize, CellSize);

                    char ch = char.ToUpperInvariant(row[c]);
                    EditorGUI.DrawRect(cell, ColorOf(ch));
                    if (ch == 'W')
                        GUI.Label(cell, "W", CenteredLabel);

                    bool clickHere = evt.type == EventType.MouseDown && cell.Contains(evt.mousePosition);
                    bool dragHere = _painting && evt.type == EventType.MouseDrag && cell.Contains(evt.mousePosition);
                    if (clickHere || dragHere)
                    {
                        if (clickHere)
                            _painting = true;
                        char brush = BrushChars[_brush];
                        if (row[c] != brush)
                        {
                            char[] chars = row.ToCharArray();
                            chars[c] = brush;
                            row = new string(chars);
                            rowProp.stringValue = row;
                        }
                        evt.Use();
                    }
                }
            }

            // 범례 + 요약
            EditorGUILayout.Space(2f);
            int active = 0, walls = 0;
            for (int r = 0; r < height; r++)
                foreach (char ch in SafeRow(rows, r))
                {
                    char u = char.ToUpperInvariant(ch);
                    if (u == 'O') active++;
                    else if (u == 'W') { active++; walls++; }
                }
            EditorGUILayout.HelpBox(
                $"{width}x{height} — 활성 {active}칸 (벽 {walls}). 첫 행이 보드 최상단.", MessageType.None);
        }

        private static string SafeRow(SerializedProperty rows, int index) =>
            rows.GetArrayElementAtIndex(index).stringValue ?? string.Empty;

        /// <summary>기존 셀을 보존하며 크기를 바꾼다. 새 칸은 O.</summary>
        private static void Resize(SerializedProperty rows, int oldWidth, int oldHeight, int newWidth, int newHeight)
        {
            var oldRows = new string[oldHeight];
            for (int r = 0; r < oldHeight; r++)
                oldRows[r] = SafeRow(rows, r);

            rows.arraySize = newHeight;
            for (int r = 0; r < newHeight; r++)
            {
                string source = r < oldHeight ? oldRows[r] : string.Empty;
                string resized = source.Length >= newWidth
                    ? source[..newWidth]
                    : source.PadRight(newWidth, 'O');
                rows.GetArrayElementAtIndex(r).stringValue = resized;
            }
        }

        private static Color ColorOf(char ch) => ch switch
        {
            'O' => ActiveColor,
            'X' => HoleColor,
            'W' => WallColor,
            _ => InvalidColor,
        };

        private static GUIStyle _centeredLabel;
        private static GUIStyle CenteredLabel
        {
            get
            {
                _centeredLabel ??= new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.95f, 0.92f, 0.85f) },
                };
                return _centeredLabel;
            }
        }
    }
}
