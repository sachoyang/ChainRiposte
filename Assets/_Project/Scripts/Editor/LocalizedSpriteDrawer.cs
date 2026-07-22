using ChainRiposte.Game.Localization;
using UnityEditor;
using UnityEngine;

namespace ChainRiposte.Editor
{
    /// <summary>
    /// <see cref="LocalizedSprite"/>를 인스펙터에서 한 줄(언어 | 스프라이트)로 보여 준다.
    /// 이 파일은 반드시 에디터 어셈블리(ChainRiposte.Editor)에 있어야 한다 — 런타임 코드에 UnityEditor가 섞이면 빌드가 깨진다.
    /// </summary>
    [CustomPropertyDrawer(typeof(LocalizedSprite))]
    public sealed class LocalizedSpriteDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            position.width /= 2f;
            EditorGUI.PropertyField(position, property.FindPropertyRelative("language"), GUIContent.none);
            position.x += position.width;
            EditorGUI.PropertyField(position, property.FindPropertyRelative("sprite"), GUIContent.none);

            EditorGUI.EndProperty();
        }
    }
}
