using UnityEditor;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    [CustomPropertyDrawer(typeof(AudioData))]
    public class AudioDataDrawer : PropertyDrawer
    {
        private const float LineHeight = 18f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            DrawFields(position, property);
            EditorGUI.EndProperty();
        }

        private void DrawFields(Rect position, SerializedProperty property)
        {
            var idProp = property.FindPropertyRelative("audioId");
            var clipProp = property.FindPropertyRelative("audioClip");

            float spacing = 5f;
            float idWidth = position.width * 0.35f;
            float clipWidth = position.width - idWidth - spacing;

            // Audio Id
            var idRect = new Rect(position.x, position.y, idWidth, LineHeight);
            EditorGUI.PropertyField(idRect, idProp, GUIContent.none);

            // Audio Clip
            var clipRect = new Rect(position.x + idWidth + spacing, position.y, clipWidth, LineHeight);
            EditorGUI.PropertyField(clipRect, clipProp, GUIContent.none);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return LineHeight;
        }
    }
}
