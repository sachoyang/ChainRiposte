using System.Collections.Generic;
using ChainRiposte.Game.Config;
using ChainRiposte.Game.Map;
using UnityEditor;
using UnityEngine;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// 월드맵 노드를 <b>씬 뷰에서 직접 찍어 배치</b>하는 툴. 배경 그림 위에서 길을 그리는 작업이라
    /// 숫자로 입력하는 것보다 눈으로 찍는 게 맞다.
    ///
    /// <list type="bullet">
    /// <item>노드를 끌면 <b>경로선이 즉시 다시 그려진다</b> — 런타임과 같은 곡선으로.</item>
    /// <item>「노드 찍기」를 켜면 씬을 클릭할 때마다 마지막 노드를 복제해 그 자리에 새 노드가 생긴다
    /// (라벨·잠금 배지 같은 꾸밈이 그대로 따라온다).</item>
    /// </list>
    ///
    /// 스테이지 에셋은 순서대로(1-1 → 2-3) 자동으로 물려 준다. 없으면 비워 두고 경고만 남긴다.
    /// </summary>
    [CustomEditor(typeof(StageSelectController))]
    public sealed class StageSelectControllerEditor : UnityEditor.Editor
    {
        private static readonly string[] StageAssetNames =
        {
            "Stage_1_1", "Stage_1_2", "Stage_1_3", "Stage_2_1", "Stage_2_2", "Stage_2_3",
        };

        private const string DataFolder = "Assets/_Project/Data";

        private static readonly Color PathColor = new(0.95f, 0.83f, 0.35f, 0.9f);

        private readonly List<Vector3> _preview = new();
        private bool _placing;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var controller = (StageSelectController)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("길 그리기", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "노드를 씬 뷰에서 끌면 경로선이 따라옵니다.\n" +
                "「노드 찍기」를 켜고 씬을 클릭하면 그 자리에 노드가 하나 생깁니다.",
                MessageType.None);

            using (new EditorGUI.DisabledScope(NodeCount(out _) == 0))
            {
                bool placing = GUILayout.Toggle(_placing, _placing ? "노드 찍기 — 켜짐 (씬을 클릭)" : "노드 찍기", "Button");
                if (placing != _placing)
                {
                    _placing = placing;
                    SceneView.RepaintAll();
                }
            }

            if (GUILayout.Button("경로선 다시 그리기"))
            {
                controller.RefreshPathLineEditorOnly();
                EditorUtility.SetDirty(controller);
            }

            if (GUILayout.Button("마지막 노드 지우기"))
                RemoveLastNode(controller);
        }

        private void OnSceneGUI()
        {
            var controller = (StageSelectController)target;
            SerializedProperty nodes = NodesProperty();
            if (nodes == null)
                return;

            DrawPathPreview(nodes);
            DrawNodeHandles(controller, nodes);

            if (_placing)
                HandlePlacement(controller, nodes);
        }

        // ─────────────────────────────────────────────────────────────

        /// <summary>런타임과 같은 곡선을 미리 그려 준다 — 저장 전에 모양을 확인할 수 있게.</summary>
        private void DrawPathPreview(SerializedProperty nodes)
        {
            var positions = new List<Vector3>();
            for (int i = 0; i < nodes.arraySize; i++)
            {
                var node = nodes.GetArrayElementAtIndex(i).objectReferenceValue as MapNode;
                if (node != null)
                    positions.Add(node.Position);
            }

            if (positions.Count < 2)
                return;

            MapPath.Build(positions, serializedObject.FindProperty("pathSmoothing").intValue, _preview);
            Handles.color = PathColor;
            Handles.DrawAAPolyLine(4f, _preview.ToArray());
        }

        private void DrawNodeHandles(StageSelectController controller, SerializedProperty nodes)
        {
            for (int i = 0; i < nodes.arraySize; i++)
            {
                var node = nodes.GetArrayElementAtIndex(i).objectReferenceValue as MapNode;
                if (node == null)
                    continue;

                Handles.color = Color.white;
                Handles.Label(node.Position + Vector3.up * 0.6f, $"{i / 3 + 1}-{i % 3 + 1}");

                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.FreeMoveHandle(
                    node.Position, HandleUtility.GetHandleSize(node.Position) * 0.18f,
                    Vector3.zero, Handles.CircleHandleCap);

                if (!EditorGUI.EndChangeCheck())
                    continue;

                Undo.RecordObject(node.transform, "Move Map Node");
                node.transform.position = new Vector3(moved.x, moved.y, node.transform.position.z);
                controller.RefreshPathLineEditorOnly();
                EditorUtility.SetDirty(controller);
            }
        }

        /// <summary>씬 클릭 → 그 자리에 노드 하나. 마지막 노드를 복제하므로 꾸밈이 그대로 따라온다.</summary>
        private void HandlePlacement(StageSelectController controller, SerializedProperty nodes)
        {
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

            Event current = Event.current;
            if (current.type != EventType.MouseDown || current.button != 0 || current.alt)
                return;

            int count = NodeCount(out MapNode template);
            if (template == null)
            {
                Debug.LogWarning("[StageSelect] 복제할 노드가 없습니다. Build StageSelect Layout 으로 먼저 하나 만드세요.");
                return;
            }

            Vector3 point = ScenePoint(current.mousePosition, template.transform.position.z);

            GameObject created = Object.Instantiate(template.gameObject, template.transform.parent);
            created.name = $"Node_{count / 3 + 1}-{count % 3 + 1}";
            created.transform.position = point;
            Undo.RegisterCreatedObjectUndo(created, "Add Map Node");

            var node = created.GetComponent<MapNode>();
            AssignStage(node, count);

            nodes.InsertArrayElementAtIndex(nodes.arraySize);
            nodes.GetArrayElementAtIndex(nodes.arraySize - 1).objectReferenceValue = node;
            serializedObject.ApplyModifiedProperties();

            controller.RefreshPathLineEditorOnly();
            EditorUtility.SetDirty(controller);
            current.Use();
        }

        private void RemoveLastNode(StageSelectController controller)
        {
            SerializedProperty nodes = NodesProperty();
            if (nodes == null || nodes.arraySize == 0)
                return;

            int last = nodes.arraySize - 1;
            var node = nodes.GetArrayElementAtIndex(last).objectReferenceValue as MapNode;

            nodes.DeleteArrayElementAtIndex(last);
            serializedObject.ApplyModifiedProperties();

            if (node != null)
                Undo.DestroyObjectImmediate(node.gameObject);

            controller.RefreshPathLineEditorOnly();
            EditorUtility.SetDirty(controller);
        }

        // ─────────────────────────────────────────────────────────────

        /// <summary>마우스 위치를 맵 평면(노드와 같은 z) 위의 한 점으로 바꾼다.</summary>
        private static Vector3 ScenePoint(Vector2 mousePosition, float z)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            var plane = new Plane(Vector3.forward, new Vector3(0f, 0f, z));
            return plane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : Vector3.zero;
        }

        private static void AssignStage(MapNode node, int index)
        {
            if (node == null || index >= StageAssetNames.Length)
                return;

            var stage = AssetDatabase.LoadAssetAtPath<StageDataSO>($"{DataFolder}/{StageAssetNames[index]}.asset");
            if (stage == null)
            {
                Debug.LogWarning($"[StageSelect] {StageAssetNames[index]}.asset 을 찾지 못해 스테이지를 비워 뒀습니다. 인스펙터에서 직접 지정하세요.", node);
                return;
            }

            node.SetStageEditorOnly(stage);
        }

        private SerializedProperty NodesProperty() => serializedObject.FindProperty("nodes");

        private int NodeCount(out MapNode last)
        {
            last = null;
            SerializedProperty nodes = NodesProperty();
            if (nodes == null)
                return 0;

            for (int i = nodes.arraySize - 1; i >= 0; i--)
            {
                if (nodes.GetArrayElementAtIndex(i).objectReferenceValue is MapNode node)
                {
                    last = node;
                    break;
                }
            }

            return nodes.arraySize;
        }
    }
}
