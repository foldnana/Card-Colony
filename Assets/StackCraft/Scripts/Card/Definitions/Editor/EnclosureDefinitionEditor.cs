using UnityEditor;

namespace CryingSnow.StackCraft
{
    [CustomEditor(typeof(EnclosureDefinition))]
    public class EnclosureDefinitionEditor : CardDefinitionEditor
    {
        private SerializedProperty capacityProp;

        protected override void OnEnable()
        {
            base.OnEnable();
            capacityProp = serializedObject.FindProperty("capacity");
        }

        protected override void DrawDerivedSection()
        {
            EditorGUILayout.LabelField("Enclosure Settings", EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(capacityProp);
            }
        }
    }
}
