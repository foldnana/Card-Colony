#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CryingSnow.StackCraft.NarrativeEditor
{
    [CustomEditor(typeof(NarrativeDefinition))]
    public sealed class NarrativeDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            if (!GUILayout.Button("验证剧情资源"))
                return;

            var registry = new GameplayInteractionRegistry();
            registry.Register(new ValidationGameplayInteractionHandler(
                "narrative.validation", "validated"));
            NarrativeValidationReport report =
                NarrativeValidator.ValidateInteractions(
                (NarrativeDefinition)target,
                registry);
            foreach (string error in report.Errors)
                Debug.LogError($"[Narrative] {error}", target);
            foreach (string warning in report.Warnings)
                Debug.LogWarning($"[Narrative] {warning}", target);
            if (report.IsValid)
                Debug.Log($"[Narrative] {target.name} 验证通过。", target);
        }

        [MenuItem("Tools/Card Colony/Narrative/Validate All")]
        public static void ValidateAll()
        {
            NarrativeDefinition[] definitions = AssetDatabase
                .FindAssets("t:NarrativeDefinition")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<NarrativeDefinition>)
                .Where(value => value != null)
                .ToArray();
            NarrativeValidationReport report =
                NarrativeValidator.ValidateAll(definitions);
            var registry = new GameplayInteractionRegistry();
            registry.Register(new ValidationGameplayInteractionHandler(
                "narrative.validation", "validated"));
            foreach (NarrativeDefinition definition in definitions)
            {
                NarrativeValidationReport interactionReport =
                    NarrativeValidator.ValidateInteractions(
                        definition,
                        registry);
                foreach (string error in interactionReport.Errors.Where(
                             value => value.Contains("未注册") ||
                                      value.Contains("Schema")))
                    report.AddError($"[{definition.name}] {error}");
            }
            foreach (string error in report.Errors)
                Debug.LogError($"[Narrative] {error}");
            foreach (string warning in report.Warnings)
                Debug.LogWarning($"[Narrative] {warning}");
            Debug.Log(
                $"[Narrative] 已验证 {definitions.Length} 个资源，" +
                $"发现 {report.Errors.Count} 个错误。");
        }
    }
}
#endif
