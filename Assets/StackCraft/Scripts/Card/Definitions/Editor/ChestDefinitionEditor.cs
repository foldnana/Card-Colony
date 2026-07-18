using UnityEditor;

namespace CryingSnow.StackCraft
{
    [CustomEditor(typeof(ChestDefinition))]
    public class ChestDefinitionEditor : CardDefinitionEditor
    {
        private SerializedProperty capacityProp;

        protected override void OnEnable()
        {
            base.OnEnable();
            capacityProp = serializedObject.FindProperty("capacity");
        }

        protected override void DrawDerivedSection()
        {
            EditorGUILayout.LabelField("Chest Settings", EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(capacityProp);
            }
        }
    }
}
