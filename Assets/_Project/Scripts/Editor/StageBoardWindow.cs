using ChainRiposte.Game.Config;
using UnityEditor;
using UnityEngine;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// 보드 형태를 <b>큰 창에서</b> 편집한다. 인스펙터(<see cref="StageDataSOEditor"/>)와
    /// <b>같은 그리드</b>(<see cref="BoardGridGUI"/>)이고 같은 데이터를 본다 — 한쪽에서 칠하면
    /// 다른 쪽도 바뀐다.
    ///
    /// <para><b>왜 창이 따로 필요한가</b>: 인스펙터는 폭이 좁아서 20x20 보드가 잘리고,
    /// 그 아래 스테이지 필드가 수십 개라 칠하는 동안 계속 스크롤을 잃는다. 판을 짜는 일은
    /// 그것만 하는 화면에서 해야 한다.</para>
    ///
    /// <para><b>편집은 SerializedObject를 거친다</b>(에셋을 직접 고치지 않는다) —
    /// Undo·에셋 더티 표시·인스펙터와의 동기화를 유니티가 알아서 맡는다.</para>
    /// </summary>
    public sealed class StageBoardWindow : EditorWindow
    {
        private const float MinCellSize = 16f;
        private const float MaxCellSize = 64f;

        [SerializeField] private StageDataSO stage;
        [SerializeField] private float cellSize = BoardGridGUI.DefaultCellSize;
        [SerializeField] private bool followSelection = true;

        private SerializedObject _serialized;

        /// <summary>이 창 몫의 편집 상태 — 인스펙터와 나눠 쓰지 않는다.</summary>
        private readonly BoardGridGUI.PaintState _paint = new();

        private Vector2 _scroll;

        [MenuItem("Tools/ChainRiposte/Stage/Board Editor")]
        public static void Open()
        {
            StageBoardWindow window = GetWindow<StageBoardWindow>("Stage Board");
            window.minSize = new Vector2(320f, 360f);
            window.TryFollowSelection();
        }

        private void OnEnable() => Selection.selectionChanged += OnSelectionChanged;

        private void OnDisable() => Selection.selectionChanged -= OnSelectionChanged;

        private void OnSelectionChanged()
        {
            if (!followSelection || !TryFollowSelection())
                return;

            Repaint();
        }

        /// <summary>Project 창에서 스테이지를 고르면 따라간다. 고른 것이 스테이지가 아니면 그대로 둔다.</summary>
        private bool TryFollowSelection()
        {
            var selected = Selection.activeObject as StageDataSO;
            if (selected == null || selected == stage)
                return false;

            stage = selected;
            _serialized = null;
            return true;
        }

        private void OnGUI()
        {
            HandleAssetDragAndDrop();
            DrawToolbar();

            if (stage == null)
            {
                EditorGUILayout.HelpBox(
                    "편집할 Stage 에셋을 위 칸에 넣거나, 이 창에 끌어다 놓거나, "
                    + "Project 창에서 고르세요 (「선택 따라가기」가 켜져 있을 때).",
                    MessageType.Info);
                return;
            }

            // 에셋이 바뀌었거나 외부(인스펙터)에서 고쳐졌을 수 있으므로 매번 최신값을 읽는다.
            _serialized ??= new SerializedObject(stage);
            _serialized.Update();

            SerializedProperty rows = _serialized.FindProperty("boardRows");
            if (rows == null)
            {
                EditorGUILayout.HelpBox(
                    "이 에셋에서 boardRows 필드를 찾지 못했습니다.", MessageType.Error);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            bool changed = BoardGridGUI.Draw(rows, _paint, cellSize);
            EditorGUILayout.EndScrollView();

            // ApplyModifiedProperties가 Undo 기록과 에셋 더티 표시를 같이 해 준다.
            if (_serialized.ApplyModifiedProperties() || changed)
                Repaint();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            var picked = (StageDataSO)EditorGUILayout.ObjectField(
                stage, typeof(StageDataSO), false, GUILayout.MinWidth(120f));
            if (picked != stage)
            {
                stage = picked;
                _serialized = null;
            }

            followSelection = GUILayout.Toggle(
                followSelection, "선택 따라가기", EditorStyles.toolbarButton, GUILayout.Width(96f));

            GUILayout.Label("칸 크기", GUILayout.Width(48f));
            cellSize = GUILayout.HorizontalSlider(
                cellSize, MinCellSize, MaxCellSize, GUILayout.Width(96f));

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>Project 창의 Stage 에셋을 이 창에 끌어다 놓으면 연결한다.</summary>
        private void HandleAssetDragAndDrop()
        {
            Event evt = Event.current;
            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
                return;

            foreach (Object dragged in DragAndDrop.objectReferences)
            {
                if (dragged is not StageDataSO draggedStage)
                    continue;

                DragAndDrop.visualMode = DragAndDropVisualMode.Link;

                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    stage = draggedStage;
                    _serialized = null;
                    Repaint();
                }

                evt.Use();
                break;
            }
        }
    }
}
