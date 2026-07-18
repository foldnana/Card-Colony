using UnityEditor;

namespace CryingSnow.StackCraft
{
    [CustomEditor(typeof(ResearchDefinition))]
    public class ResearchDefinitionEditor : CardDefinitionEditor
    {
        protected override void DrawDerivedSection()
        {
            EditorGUILayout.HelpBox(
                "This is a research card, no further configurations required.",
                MessageType.Info
            );
        }
    }
}
