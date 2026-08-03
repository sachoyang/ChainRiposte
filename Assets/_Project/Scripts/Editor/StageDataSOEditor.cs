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
    ///
    /// <para>그리드를 그리는 일은 <see cref="BoardGridGUI"/>가 맡는다 —
    /// 같은 그리드를 <see cref="StageBoardWindow"/>도 쓰기 때문이다.
    /// 이 클래스는 <b>자리를 내주고 상태를 하나 들고 있을 뿐</b>이다.</para>
    /// </summary>
    [CustomEditor(typeof(StageDataSO))]
    public sealed class StageDataSOEditor : UnityEditor.Editor
    {
        /// <summary>이 인스펙터 몫의 편집 상태 — 보드 창과 나눠 쓰지 않는다.</summary>
        private readonly BoardGridGUI.PaintState _paint = new();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("보드 형태 (클릭/드래그로 칠하기)", EditorStyles.boldLabel);
            if (GUILayout.Button("큰 창에서 편집", GUILayout.Width(100f)))
                StageBoardWindow.Open();
            EditorGUILayout.EndHorizontal();

            SerializedProperty rows = serializedObject.FindProperty("boardRows");
            if (BoardGridGUI.Draw(rows, _paint))
                Repaint();

            EditorGUILayout.Space(12f);
            DrawPropertiesExcluding(serializedObject, "m_Script", "boardRows");

            serializedObject.ApplyModifiedProperties();
        }
    }
}
