using UnityEngine;
using UnityEditor;

namespace CryingSnow.StackCraft
{
    [CustomEditor(typeof(PackDefinition))]
    public class PackDefinitionEditor : Editor
    {
        // Base Properties
        private SerializedProperty idProp;
        private SerializedProperty displayNameProp;
        private SerializedProperty descriptionProp;
        private SerializedProperty artTextureProp;
        private SerializedProperty isSellableProp;
        private SerializedProperty sellPriceProp;

        // Pack Specific
        private SerializedProperty buyPriceProp;
        private SerializedProperty minQuestsProp;
        private SerializedProperty slotsProp;

        private void OnEnable()
        {
            // Base Properties
            idProp = serializedObject.FindProperty("id");
            displayNameProp = serializedObject.FindProperty("displayName");
            descriptionProp = serializedObject.FindProperty("description");
            artTextureProp = serializedObject.FindProperty("artTexture");
            isSellableProp = serializedObject.FindProperty("isSellable");
            sellPriceProp = serializedObject.FindProperty("sellPrice");

            // Pack Specific
            buyPriceProp = serializedObject.FindProperty("buyPrice");
            minQuestsProp = serializedObject.FindProperty("minQuests");
            slotsProp = serializedObject.FindProperty("slots");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawIdentificationSection();
            DrawTradingSection();
            DrawVendorSection();
            DrawPackContentSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawIdentificationSection()
        {
            EditorGUILayout.LabelField("Identification", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.PropertyField(idProp);
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.PropertyField(displayNameProp);
                EditorGUILayout.PropertyField(descriptionProp);

                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.HelpBox(
                    "Art Texture (Pack Art)\nMake sure the Alpha Source is set correctly in the Import Settings.",
                    MessageType.Info,
                    true
                );

                artTextureProp.objectReferenceValue = EditorGUILayout.ObjectField(
                    GUIContent.none,
                    artTextureProp.objectReferenceValue,
                    typeof(Texture2D),
                    false,
                    GUILayout.Width(80f)
                );

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(6);
        }

        private void DrawTradingSection()
        {
            EditorGUILayout.LabelField("Trading", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(isSellableProp);
                if (isSellableProp.boolValue)
                    EditorGUILayout.PropertyField(sellPriceProp);
            }

            EditorGUILayout.Space(6);
        }

        private void DrawVendorSection()
        {
            EditorGUILayout.LabelField("Vendor", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(buyPriceProp);
                EditorGUILayout.PropertyField(minQuestsProp);
            }

            EditorGUILayout.Space(6);
        }

        private void DrawPackContentSection()
        {
            EditorGUILayout.LabelField("Pack Content", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                for (int i = 0; i < slotsProp.arraySize; i++)
                {
                    SerializedProperty slotProp = slotsProp.GetArrayElementAtIndex(i);
                    SerializedProperty entriesProp = slotProp.FindPropertyRelative("Entries");

                    SerializedProperty recipeChanceProp = slotProp.FindPropertyRelative("RecipeChance");
                    SerializedProperty possibleRecipesProp = slotProp.FindPropertyRelative("PossibleRecipes");

                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"Slot {i + 1}", EditorStyles.boldLabel);
                    if (GUILayout.Button("X", GUILayout.Width(20)))
                    {
                        slotsProp.DeleteArrayElementAtIndex(i);
                        break;
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUI.indentLevel++;

                    EditorGUILayout.LabelField("Weighted Entries", EditorStyles.boldLabel);
                    for (int j = 0; j < entriesProp.arraySize; j++)
                    {
                        SerializedProperty entryProp = entriesProp.GetArrayElementAtIndex(j);
                        SerializedProperty cardProp = entryProp.FindPropertyRelative("Card");
                        SerializedProperty weightProp = entryProp.FindPropertyRelative("Weight");

                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PropertyField(cardProp, GUIContent.none);
                        EditorGUILayout.PropertyField(weightProp, GUIContent.none, GUILayout.Width(60));
                        GUILayout.Label("%", GUILayout.Width(14));

                        if (GUILayout.Button("-", GUILayout.Width(20)))
                        {
                            entriesProp.DeleteArrayElementAtIndex(j);
                            break;
                        }
                        EditorGUILayout.EndHorizontal();
                    }

                    if (GUILayout.Button("+ Add Entry"))
                        entriesProp.InsertArrayElementAtIndex(entriesProp.arraySize);

                    if (entriesProp.arraySize > 0)
                    {
                        if (GUILayout.Button("Normalize (100%)"))
                        {
                            NormalizeWeights(entriesProp);
                        }
                    }

                    EditorGUILayout.Space(5);

                    EditorGUILayout.LabelField("Recipe Card Override", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(recipeChanceProp);
                    EditorGUILayout.PropertyField(possibleRecipesProp, true);

                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.Space(5);
                }

                if (GUILayout.Button("+ Add Slot"))
                    slotsProp.InsertArrayElementAtIndex(slotsProp.arraySize);

                EditorGUILayout.Space(5);
            }
        }

        private void NormalizeWeights(SerializedProperty listProp)
        {
            int totalWeight = 0;
            for (int k = 0; k < listProp.arraySize; k++)
            {
                totalWeight += listProp.GetArrayElementAtIndex(k).FindPropertyRelative("Weight").intValue;
            }

            if (totalWeight > 0)
            {
                int totalPercentage = 0;
                for (int k = 0; k < listProp.arraySize; k++)
                {
                    SerializedProperty weightProp = listProp.GetArrayElementAtIndex(k).FindPropertyRelative("Weight");
                    // Calculate new percentage and round it.
                    int newWeight = Mathf.RoundToInt((float)weightProp.intValue / totalWeight * 100f);
                    weightProp.intValue = newWeight;
                    totalPercentage += newWeight;
                }

                // Adjust for rounding errors (e.g., 33+33+33 = 99).
                int diff = 100 - totalPercentage;
                if (diff != 0)
                {
                    // Add/subtract the difference from the first element.
                    listProp.GetArrayElementAtIndex(0).FindPropertyRelative("Weight").intValue += diff;
                }

                // Ensure no weight is less than 1 after normalization.
                for (int k = 0; k < listProp.arraySize; k++)
                {
                    SerializedProperty weightProp = listProp.GetArrayElementAtIndex(k).FindPropertyRelative("Weight");
                    if (weightProp.intValue < 1) weightProp.intValue = 1;
                }
            }
        }
    }
}
