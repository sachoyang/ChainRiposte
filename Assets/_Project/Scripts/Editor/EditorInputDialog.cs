using UnityEditor;
using UnityEngine;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// 한 줄 입력을 받는 모달 창. Unity에 기본 제공되는 것이 없어 직접 둔다.
    /// 취소하면 null을 돌려준다.
    /// </summary>
    public sealed class EditorInputDialog : EditorWindow
    {
        private string _message;
        private string _value;
        private bool _confirmed;
        private bool _closed;
        private bool _focusRequested;

        public static string Show(string title, string message, string initialValue = "")
        {
            var window = CreateInstance<EditorInputDialog>();
            window.titleContent = new GUIContent(title);
            window._message = message;
            window._value = initialValue ?? string.Empty;
            window.position = new Rect(Screen.width / 2f, Screen.height / 2f, 520f, 150f);
            window.ShowModalUtility();

            return window._confirmed ? window._value : null;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(_message, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();

            GUI.SetNextControlName("InputField");
            _value = EditorGUILayout.TextField(_value);
            if (!_focusRequested)
            {
                EditorGUI.FocusTextInControl("InputField");
                _focusRequested = true;
            }

            // 엔터/ESC로도 닫히게 — 붙여넣고 엔터가 자연스럽다
            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode is KeyCode.Return or KeyCode.KeypadEnter)
                    Close(confirmed: true);
                else if (Event.current.keyCode == KeyCode.Escape)
                    Close(confirmed: false);
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("취소", GUILayout.Width(90f)))
                    Close(confirmed: false);
                if (GUILayout.Button("확인", GUILayout.Width(90f)))
                    Close(confirmed: true);
            }
        }

        private void Close(bool confirmed)
        {
            if (_closed)
                return;

            _closed = true;
            _confirmed = confirmed;
            base.Close();
        }
    }
}
