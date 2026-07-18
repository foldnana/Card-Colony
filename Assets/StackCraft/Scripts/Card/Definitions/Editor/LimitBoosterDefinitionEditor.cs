using UnityEditor;

namespace CryingSnow.StackCraft
{
    [CustomEditor(typeof(LimitBoosterDefinition))]
    public class LimitBoosterDefinitionEditor : CardDefinitionEditor
    {
        private SerializedProperty boostAmountProp;

        protected override void OnEnable()
        {
            base.OnEnable();
            boostAmountProp = serializedObject.FindProperty("boostAmount");
        }

        protected override void DrawDerivedSection()
        {
            EditorGUILayout.LabelField("Limit Booster Settings", EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(boostAmountProp);
            }
        }
    }
}
