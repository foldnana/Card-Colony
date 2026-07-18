using UnityEditor;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    [CustomPropertyDrawer(typeof(CategoryEntry))]
    public class CategoryEntryDrawer : PropertyDrawer
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
            var categoryProp = property.FindPropertyRelative("category");
            var prefabProp = property.FindPropertyRelative("prefab");

            float spacing = 5f;
            float categoryWidth = position.width * 0.35f;
            float prefabWidth = position.width - categoryWidth - spacing;

            // Category
            var categoryRect = new Rect(position.x, position.y, categoryWidth, LineHeight);
            EditorGUI.PropertyField(categoryRect, categoryProp, GUIContent.none);

            // Prefab
            var prefabRect = new Rect(position.x + categoryWidth + spacing, position.y, prefabWidth, LineHeight);
            EditorGUI.PropertyField(prefabRect, prefabProp, GUIContent.none);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return LineHeight;
        }
    }
}
