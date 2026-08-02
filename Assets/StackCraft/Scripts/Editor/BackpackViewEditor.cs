#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CryingSnow.StackCraft.EditorTools
{
    [CustomEditor(typeof(BackpackView))]
    public sealed class BackpackViewEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            if (Application.isPlaying)
                return;

            SerializedProperty tablePanel = serializedObject.FindProperty(
                "tablePanel");
            var panel = tablePanel?.objectReferenceValue as RectTransform;
            if (panel == null)
                return;

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("编辑器预览", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "背包外观已序列化进 UIRoot.prefab 的右侧信息栏。这里可以不进入播放模式直接显示或隐藏背包页面。",
                MessageType.Info);
            string label = panel.gameObject.activeSelf
                ? "隐藏背包页面预览"
                : "显示背包页面预览";
            if (!GUILayout.Button(label, GUILayout.Height(30f)))
                return;

            Undo.RecordObject(panel.gameObject, label);
            panel.gameObject.SetActive(!panel.gameObject.activeSelf);
            EditorUtility.SetDirty(panel.gameObject);
            SceneView.RepaintAll();
        }
    }
}
#endif
