using UnityEditor;

namespace CryingSnow.StackCraft
{
    [CustomEditor(typeof(GrowerDefinition))]
    public class GrowerDefinitionEditor : CardDefinitionEditor
    {
        protected override void DrawDerivedSection()
        {
            EditorGUILayout.HelpBox(
                "This is a grower card, no further configurations required.",
                MessageType.Info
            );
        }
    }
}
