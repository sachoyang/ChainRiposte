using UnityEditor;
using UnityEngine;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// 보드 마스킹(O/X/W) 그리드를 그리는 <b>공용 GUI</b>.
    ///
    /// <para><b>왜 따로 뺐나 — 에디터가 둘이기 때문이다.</b> 같은 그리드를 인스펙터
    /// (<see cref="StageDataSOEditor"/>)와 큰 창(<see cref="StageBoardWindow"/>)이 나눠 쓴다.
    /// 그리는 코드를 양쪽에 복붙하면 브러시를 하나 늘릴 때 두 곳을 고쳐야 하고,
    /// 반드시 한쪽을 잊는다. <b>호스트는 자리만 내주고 그리는 일은 여기 하나가 맡는다.</b></para>
    ///
    /// <para><b>좌표 → 저장 위치.</b> 저장은 행마다 문자열 하나(<c>string[] boardRows</c>)다.
    /// 다차원 배열을 1차원으로 눕힐 때 쓰는 계산(<c>index = row * width + x</c>)과 뜻이 같고,
    /// 다만 그 번호로 곧장 찾아가는 대신 <c>boardRows[row][x]</c>로 두 번에 나눠 찾는다
    /// — 문자열이 곱셈을 대신 들고 있는 것뿐이다. 번호 자체가 필요한 곳(드래그 중복 방지)에서는
    /// <see cref="ToIndex"/>로 직접 계산한다.</para>
    ///
    /// <para>⚠ 여기서 말하는 <c>row</c>는 <b>인스펙터 표기 순서</b>(첫 행 = 보드 최상단)이고,
    /// 게임의 <c>y</c>(0이 바닥)와 <b>뒤집혀 있다</b>. 뒤집는 일은 오직
    /// <c>StageDataSO.ParseBoardRows</c> 한 곳에서만 한다 — 두 곳에서 뒤집으면 원래대로 돌아온다.</para>
    /// </summary>
    internal static class BoardGridGUI
    {
        public const float DefaultCellSize = 30f;

        private const float CellGap = 2f;
        private const int MinSide = 1;
        private const int MaxSide = 20;

        private static readonly char[] BrushChars = { 'O', 'X', 'W' };
        private static readonly string[] BrushLabels = { "O 활성", "X 구멍", "W 벽" };

        private static readonly Color ActiveColor = new(0.30f, 0.55f, 0.35f);
        private static readonly Color HoleColor = new(0.16f, 0.15f, 0.18f);
        private static readonly Color WallColor = new(0.45f, 0.33f, 0.20f);
        private static readonly Color InvalidColor = Color.magenta;

        /// <summary>
        /// 한 호스트(인스펙터 창 하나 / 에디터 창 하나)의 편집 상태.
        ///
        /// <para><b>static으로 두지 않는 이유</b>: 인스펙터와 창을 동시에 열어 두면 하나뿐인 상태를
        /// 둘이 번갈아 덮어쓴다 — 창에서 드래그하는 중에 인스펙터가 그려지면서 페인팅이 끊긴다.
        /// 호스트가 자기 것을 하나씩 들고 넘긴다.</para>
        /// </summary>
        public sealed class PaintState
        {
            /// <summary>고른 브러시 (<see cref="BrushChars"/> 인덱스).</summary>
            public int Brush;

            /// <summary>왼쪽 버튼을 누른 채 끌고 있는 중인가.</summary>
            internal bool Painting;

            /// <summary>
            /// 이번 드래그에서 마지막으로 칠한 칸 번호. 같은 칸에 드래그 이벤트가 여러 번 와도
            /// 한 번만 처리한다 — 안 막으면 한 칸에 Undo 기록이 수십 개 쌓인다.
            /// </summary>
            internal int LastPaintedIndex = -1;
        }

        /// <summary>(x, row)를 1차원 칸 번호로. 드래그 중복 방지에만 쓴다.</summary>
        public static int ToIndex(int x, int row, int width) => row * width + x;

        /// <summary>
        /// 보드 편집 UI 한 벌(크기 조절 + 브러시 + 그리드 + 요약)을 그린다.
        /// </summary>
        /// <param name="rows"><c>StageDataSO.boardRows</c>의 SerializedProperty (문자열 배열).</param>
        /// <param name="state">호스트가 들고 있는 편집 상태.</param>
        /// <param name="cellSize">칸 한 변의 픽셀 크기.</param>
        /// <returns>이번 프레임에 보드가 바뀌었으면 true — 호스트가 Repaint 할지 정한다.</returns>
        public static bool Draw(SerializedProperty rows, PaintState state, float cellSize = DefaultCellSize)
        {
            bool changed = false;

            int height = rows.arraySize;
            int width = height > 0 ? SafeRow(rows, 0).Length : 0;

            // ── 크기 조절 ──
            EditorGUILayout.BeginHorizontal();
            int newWidth = EditorGUILayout.IntField("가로 x 세로", Mathf.Max(MinSide, width), GUILayout.ExpandWidth(true));
            int newHeight = EditorGUILayout.IntField(Mathf.Max(MinSide, height), GUILayout.Width(64f));
            EditorGUILayout.EndHorizontal();
            newWidth = Mathf.Clamp(newWidth, MinSide, MaxSide);
            newHeight = Mathf.Clamp(newHeight, MinSide, MaxSide);
            if (newWidth != width || newHeight != height)
            {
                Resize(rows, width, height, newWidth, newHeight);
                width = newWidth;
                height = newHeight;
                changed = true;
            }

            // ── 브러시 + 일괄 채우기 ──
            EditorGUILayout.BeginHorizontal();
            state.Brush = GUILayout.Toolbar(state.Brush, BrushLabels);
            if (GUILayout.Button("전체 채움", GUILayout.Width(72f)))
            {
                for (int r = 0; r < height; r++)
                    rows.GetArrayElementAtIndex(r).stringValue = new string(BrushChars[state.Brush], width);
                changed = true;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4f);

            changed |= DrawGrid(rows, state, width, height, cellSize);

            // ── 범례 + 요약 ──
            EditorGUILayout.Space(2f);
            CountCells(rows, height, out int active, out int walls);
            EditorGUILayout.HelpBox(
                $"{width}x{height} — 활성 {active}칸 (벽 {walls}). 첫 행이 보드 최상단.", MessageType.None);

            return changed;
        }

        // ── 그리드 ──

        private static bool DrawGrid(
            SerializedProperty rows, PaintState state, int width, int height, float cellSize)
        {
            bool changed = false;
            float step = cellSize + CellGap;

            Rect gridRect = GUILayoutUtility.GetRect(width * step, height * step);
            gridRect.x += Mathf.Max(0f, (gridRect.width - width * step) * 0.5f);

            Event evt = Event.current;

            // ⚠ type이 아니라 rawType이다. 그리드 밖(다른 창·인스펙터 바깥)에서 버튼을 놓으면
            // MouseUp이 type으로는 안 온다 — 그러면 Painting이 켜진 채 남아서, 다음에 마우스를
            // 그냥 지나가기만 해도 칸이 칠해진다. rawType은 그 경우에도 온다.
            if (evt.rawType == EventType.MouseUp && evt.button == 0)
            {
                state.Painting = false;
                state.LastPaintedIndex = -1;
            }

            for (int r = 0; r < height; r++)
            {
                SerializedProperty rowProp = rows.GetArrayElementAtIndex(r);
                string row = NormalizeRow(rowProp, width);

                for (int c = 0; c < width; c++)
                {
                    var cell = new Rect(
                        gridRect.x + c * step,
                        gridRect.y + r * step,
                        cellSize, cellSize);

                    char ch = char.ToUpperInvariant(row[c]);
                    EditorGUI.DrawRect(cell, ColorOf(ch));
                    if (ch == 'W')
                        GUI.Label(cell, "W", CenteredLabel);

                    if (TryPaint(evt, cell, state, rowProp, ref row, c, r, width))
                        changed = true;
                }
            }

            return changed;
        }

        /// <summary>
        /// 이 칸에 대한 마우스 입력을 처리한다. 클릭으로 칠하기를 시작하고,
        /// 누른 채 옮기면 계속 칠한다.
        /// </summary>
        private static bool TryPaint(
            Event evt, Rect cell, PaintState state, SerializedProperty rowProp,
            ref string row, int x, int r, int width)
        {
            bool leftButton = evt.button == 0;
            bool inside = cell.Contains(evt.mousePosition);
            bool clickHere = leftButton && inside && evt.type == EventType.MouseDown;
            bool dragHere = leftButton && inside && state.Painting && evt.type == EventType.MouseDrag;

            if (!clickHere && !dragHere)
                return false;

            if (clickHere)
            {
                state.Painting = true;
                state.LastPaintedIndex = -1;
            }

            evt.Use();

            // 같은 칸을 한 드래그에서 두 번 칠하지 않는다.
            int index = ToIndex(x, r, width);
            if (state.LastPaintedIndex == index)
                return false;
            state.LastPaintedIndex = index;

            char brush = BrushChars[state.Brush];
            if (row[x] == brush)
                return false;

            char[] chars = row.ToCharArray();
            chars[x] = brush;
            row = new string(chars);
            rowProp.stringValue = row;
            return true;
        }

        // ── 저장 형태 손질 ──

        private static string SafeRow(SerializedProperty rows, int index) =>
            rows.GetArrayElementAtIndex(index).stringValue ?? string.Empty;

        /// <summary>행 길이를 width에 맞춘다. 모자라면 O로 채우고 넘치면 자른다.</summary>
        private static string NormalizeRow(SerializedProperty rowProp, int width)
        {
            string row = rowProp.stringValue ?? string.Empty;
            if (row.Length == width)
                return row;

            row = row.Length > width ? row[..width] : row.PadRight(width, 'O');
            rowProp.stringValue = row;
            return row;
        }

        /// <summary>기존 칸을 보존하며 크기를 바꾼다. 새 칸은 O.</summary>
        private static void Resize(
            SerializedProperty rows, int oldWidth, int oldHeight, int newWidth, int newHeight)
        {
            var oldRows = new string[oldHeight];
            for (int r = 0; r < oldHeight; r++)
                oldRows[r] = SafeRow(rows, r);

            rows.arraySize = newHeight;
            for (int r = 0; r < newHeight; r++)
            {
                string source = r < oldHeight ? oldRows[r] : string.Empty;
                rows.GetArrayElementAtIndex(r).stringValue = source.Length >= newWidth
                    ? source[..newWidth]
                    : source.PadRight(newWidth, 'O');
            }
        }

        private static void CountCells(SerializedProperty rows, int height, out int active, out int walls)
        {
            active = 0;
            walls = 0;
            for (int r = 0; r < height; r++)
                foreach (char ch in SafeRow(rows, r))
                {
                    switch (char.ToUpperInvariant(ch))
                    {
                        case 'O':
                            active++;
                            break;
                        case 'W':
                            active++;
                            walls++;
                            break;
                    }
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
