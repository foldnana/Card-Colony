using UnityEditor;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    [CustomEditor(typeof(StackingRulesMatrix))]
    public class StackingRulesMatrixEditor : Editor
    {
        private StackingRulesMatrix matrix;

        private static class Styles
        {
            public static readonly GUIStyle rightLabel = new GUIStyle("RightLabel");

            public static readonly GUIStyle ruleIcon;

            static Styles()
            {
                ruleIcon = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 16,
                    fixedWidth = 20,
                    fixedHeight = 20,
                };
            }
        }

        private static readonly GUIContent[] ruleIcons =
        {
            new GUIContent("", "No stacking allowed"),
            new GUIContent("✓", "Category-wide stacking"),
            new GUIContent("≡", "Same-definition stacking")
        };

        private void OnEnable()
        {
            matrix = (StackingRulesMatrix)target;
        }

        public override void OnInspectorGUI()
        {
            var categories = System.Enum.GetValues(typeof(CardCategory));
            int size = categories.Length;

            const int checkboxSize = 20;
            int labelSize = 110;
            const int indent = 10;

            // ===== Find Widest Label =====
            foreach (var cat in categories)
            {
                var textDimensions = GUI.skin.label.CalcSize(new GUIContent(cat.ToString()));
                if (labelSize < textDimensions.x)
                    labelSize = (int)textDimensions.x;
            }

            // ===== Draw Top Labels (Vertical) =====
            Rect topRect = GUILayoutUtility.GetRect(labelSize + indent + size * checkboxSize, labelSize);
            for (int x = 0; x < size; x++)
            {
                string cat = categories.GetValue(x).ToString();
                Vector2 pos = new Vector2(topRect.x + labelSize + indent + x * checkboxSize, topRect.y + labelSize);
                GUIUtility.RotateAroundPivot(-90, pos);

                var headerRect = new Rect(pos.x, pos.y, labelSize, checkboxSize);
                GUI.Label(headerRect, cat);

                GUI.matrix = Matrix4x4.identity;
            }

            // ===== Draw Rows =====
            for (int y = 0; y < size; y++)
            {
                Rect rowRect = GUILayoutUtility.GetRect(indent + labelSize + size * checkboxSize, checkboxSize);

                // Row Label
                var rowLabelRect = new Rect(rowRect.x + indent, rowRect.y, labelSize, checkboxSize);
                GUI.Label(rowLabelRect, categories.GetValue(y).ToString(), Styles.rightLabel);

                // Row Toggles
                for (int x = 0; x < size; x++)
                {
                    var current = matrix.GetRule((CardCategory)y, (CardCategory)x);
                    var cellRect = new Rect(rowRect.x + labelSize + indent + x * checkboxSize, rowRect.y, checkboxSize, checkboxSize);

                    if (GUI.Button(cellRect, ruleIcons[(int)current], Styles.ruleIcon))
                    {
                        Undo.RecordObject(matrix, "Change stacking rule");
                        var next = (StackingRule)(((int)current + 1) % System.Enum.GetValues(typeof(StackingRule)).Length);
                        matrix.SetRule((CardCategory)y, (CardCategory)x, next);
                        EditorUtility.SetDirty(matrix);
                    }
                }
            }

            EditorGUILayout.Space(8);

            EditorGUILayout.LabelField("Set All Rules:");
            // ===== Buttons =====
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("None"))
                SetAll(StackingRule.None);
            if (GUILayout.Button("Category-Wide"))
                SetAll(StackingRule.CategoryWide);
            if (GUILayout.Button("Same Definition"))
                SetAll(StackingRule.SameDefinition);
            GUILayout.EndHorizontal();
        }

        private void SetAll(StackingRule rule)
        {
            var categories = System.Enum.GetValues(typeof(CardCategory));
            int size = categories.Length;

            Undo.RecordObject(matrix, "Set all stacking rules");
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    matrix.SetRule((CardCategory)y, (CardCategory)x, rule);
                }
            }
            EditorUtility.SetDirty(matrix);
        }
    }
}
