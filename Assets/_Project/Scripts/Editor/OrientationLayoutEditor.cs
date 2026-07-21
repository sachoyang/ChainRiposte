using ChainRiposte.Game.UI;
using UnityEditor;
using UnityEngine;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// <see cref="OrientationLayout"/>의 작업 흐름을 버튼으로 제공한다 (GDD §9.3).
    /// <b>씬 뷰에서 원하는 대로 배치 → "현재 배치를 ○○ 프리셋으로 저장"</b>이 기본 사용법이고,
    /// 미리보기 버튼으로 반대 방향 배치를 즉시 확인할 수 있다.
    /// </summary>
    [CustomEditor(typeof(OrientationLayout))]
    [CanEditMultipleObjects]
    public sealed class OrientationLayoutEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "씬 뷰에서 배치한 뒤 아래 '저장' 버튼을 누르세요.\n" +
                "Game 뷰 해상도를 세로/가로로 바꿔가며 잡으면 정확합니다.",
                MessageType.Info);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("현재 배치를 프리셋으로 저장", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("세로(Portrait)로 저장"))
                    Capture(ScreenLayout.Portrait);
                if (GUILayout.Button("가로(Landscape)로 저장"))
                    Capture(ScreenLayout.Landscape);
            }

            EditorGUILayout.LabelField("프리셋 미리보기 (씬에 즉시 적용)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("세로 미리보기"))
                    Preview(ScreenLayout.Portrait);
                if (GUILayout.Button("가로 미리보기"))
                    Preview(ScreenLayout.Landscape);
            }

            EditorGUILayout.Space(6f);
            DrawDefaultInspector();
        }

        private void Capture(ScreenLayout layout)
        {
            foreach (Object obj in targets)
            {
                var layoutComponent = (OrientationLayout)obj;
                Undo.RecordObject(layoutComponent, "Capture Orientation Preset");
                layoutComponent.CaptureEditorOnly(layout);
                EditorUtility.SetDirty(layoutComponent);
            }
        }

        private void Preview(ScreenLayout layout)
        {
            foreach (Object obj in targets)
            {
                var layoutComponent = (OrientationLayout)obj;
                Undo.RecordObject(layoutComponent.transform, "Preview Orientation Preset");
                layoutComponent.PreviewEditorOnly(layout);
            }
        }
    }
}
