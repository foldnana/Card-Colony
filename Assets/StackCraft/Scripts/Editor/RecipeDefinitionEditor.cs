using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    [CustomEditor(typeof(RecipeDefinition), true)]
    public class RecipeDefinitionEditor : Editor
    {
        private SerializedProperty idProp;
        private SerializedProperty categoryProp;
        private SerializedProperty displayNameProp;
        private SerializedProperty requiredIngredientsProp;
        private SerializedProperty resultingCardProp;
        private SerializedProperty isContinuousProp;
        private SerializedProperty allowExcessIngredientsProp;
        private SerializedProperty craftingDurationProp;
        private SerializedProperty randomWeightProp;

        // Caching for conflict detection
        private List<RecipeDefinition> allOtherRecipes;
        private List<RecipeDefinition> conflictingRecipes;
        private bool isDirty = true; // Force check on first draw

        private void OnEnable()
        {
            idProp = serializedObject.FindProperty("id");
            categoryProp = serializedObject.FindProperty("category");
            displayNameProp = serializedObject.FindProperty("displayName");
            requiredIngredientsProp = serializedObject.FindProperty("requiredIngredients");
            resultingCardProp = serializedObject.FindProperty("resultingCard");
            isContinuousProp = serializedObject.FindProperty("isContinuous");
            allowExcessIngredientsProp = serializedObject.FindProperty("allowExcessIngredients");
            craftingDurationProp = serializedObject.FindProperty("craftingDuration");
            randomWeightProp = serializedObject.FindProperty("randomWeight");

            // Cache all recipes in the project to avoid expensive FindAssets calls every frame
            FetchAllRecipes();
        }

        private void FetchAllRecipes()
        {
            allOtherRecipes = new List<RecipeDefinition>();
            RecipeDefinition currentTarget = (RecipeDefinition)target;

            string[] guids = AssetDatabase.FindAssets("t:RecipeDefinition");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                RecipeDefinition r = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(path);

                // Don't compare with self
                if (r != null && r != currentTarget)
                {
                    allOtherRecipes.Add(r);
                }
            }
            isDirty = true;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            RecipeDefinition recipe = (RecipeDefinition)target;

            // Check for changes to trigger a re-scan of conflicts
            EditorGUI.BeginChangeCheck();

            DrawIdentificationSection();
            DrawIngredientsSection();
            DrawCraftingResultSection(recipe);
            DrawCraftingSection();

            if (EditorGUI.EndChangeCheck())
            {
                isDirty = true;
            }

            // Only recalculate logic when data changes
            if (isDirty)
            {
                FindConflicts(recipe);
                isDirty = false;
            }

            DrawConflictInfoSection(recipe);

            serializedObject.ApplyModifiedProperties();
        }

        private void FindConflicts(RecipeDefinition currentRecipe)
        {
            conflictingRecipes = new List<RecipeDefinition>();

            // 1. Summarize current ingredients into a Dictionary<Card, Count>
            var currentIngredients = GetIngredientSummary(currentRecipe.RequiredIngredients);

            foreach (var otherRecipe in allOtherRecipes)
            {
                // Simple optimization: Must have same number of unique cards
                var otherIngredients = GetIngredientSummary(otherRecipe.RequiredIngredients);

                if (AreIngredientsIdentical(currentIngredients, otherIngredients))
                {
                    conflictingRecipes.Add(otherRecipe);
                }
            }
        }

        private Dictionary<CardDefinition, int> GetIngredientSummary(List<RecipeDefinition.Ingredient> list)
        {
            var summary = new Dictionary<CardDefinition, int>();
            if (list == null) return summary;

            foreach (var item in list)
            {
                if (item.card == null) continue;
                if (summary.ContainsKey(item.card))
                    summary[item.card] += item.count;
                else
                    summary[item.card] = item.count;
            }
            return summary;
        }

        private bool AreIngredientsIdentical(Dictionary<CardDefinition, int> a, Dictionary<CardDefinition, int> b)
        {
            if (a.Count != b.Count) return false;

            foreach (var kvp in a)
            {
                if (!b.TryGetValue(kvp.Key, out int otherCount)) return false;
                if (otherCount != kvp.Value) return false;
            }

            return true;
        }

        private void DrawIdentificationSection()
        {
            EditorGUILayout.LabelField("Identification", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.PropertyField(idProp);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.PropertyField(categoryProp);
                EditorGUILayout.PropertyField(displayNameProp);
            }
            EditorGUILayout.Space(6);
        }

        private void DrawIngredientsSection()
        {
            EditorGUILayout.LabelField("Ingredients", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                for (int i = 0; i < requiredIngredientsProp.arraySize; i++)
                {
                    SerializedProperty ingredientProp = requiredIngredientsProp.GetArrayElementAtIndex(i);
                    SerializedProperty cardProp = ingredientProp.FindPropertyRelative("card");
                    SerializedProperty countProp = ingredientProp.FindPropertyRelative("count");
                    SerializedProperty consumptionProp = ingredientProp.FindPropertyRelative("consumptionMode");

                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.PropertyField(cardProp, GUIContent.none);

                    Rect countRect = EditorGUILayout.GetControlRect(GUILayout.Width(50));
                    Rect labelRect = new Rect(countRect.x, countRect.y, 10, countRect.height);
                    Rect fieldRect = new Rect(labelRect.xMax + 2, countRect.y, countRect.width - 12, countRect.height);

                    EditorGUI.PrefixLabel(labelRect, GUIUtility.GetControlID(FocusType.Passive), new GUIContent("x"));
                    EditorGUI.PropertyField(fieldRect, countProp, GUIContent.none);
                    if (countProp.intValue < 1) countProp.intValue = 1;

                    EditorGUILayout.PropertyField(consumptionProp, GUIContent.none, GUILayout.Width(90));

                    if (GUILayout.Button("-", GUILayout.Width(22)))
                    {
                        requiredIngredientsProp.DeleteArrayElementAtIndex(i);
                        break;
                    }

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                }

                if (GUILayout.Button("+ Add Ingredient"))
                    requiredIngredientsProp.InsertArrayElementAtIndex(requiredIngredientsProp.arraySize);
            }
            EditorGUILayout.Space(6);
        }

        private void DrawCraftingResultSection(RecipeDefinition recipe)
        {
            EditorGUILayout.LabelField("Crafting Result", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                if (recipe is ExplorationRecipe)
                {
                    EditorGUILayout.HelpBox("Exploration Recipe: Result handled by Area card Loot table.", MessageType.Info);
                    if (resultingCardProp.objectReferenceValue != null) resultingCardProp.objectReferenceValue = null;
                }
                else if (recipe is GrowthRecipe)
                {
                    EditorGUILayout.HelpBox("Growth Recipe: Ensure Result matches the Seed ingredient.", MessageType.Info);
                    EditorGUILayout.PropertyField(resultingCardProp);
                }
                else if (recipe is ResearchRecipe)
                {
                    EditorGUILayout.HelpBox("Research Recipe: Result is generated dynamically.", MessageType.Info);
                    if (resultingCardProp.objectReferenceValue != null) resultingCardProp.objectReferenceValue = null;
                }
                else if (recipe is TravelRecipe)
                {
                    EditorGUILayout.HelpBox(
                        "Travel Recipe: This recipe triggers scene travel rather than producing a card.\n\n" +
                        "Travel behavior:\n" +
                        "The system moves to the next scene in 'Target Scenes'.\n" +
                        "If the current scene is the last in the list, it loops back to the first.",
                        MessageType.Info
                    );

                    if (resultingCardProp.objectReferenceValue != null) resultingCardProp.objectReferenceValue = null;
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("targetScenes"));
                }
                else
                {
                    EditorGUILayout.PropertyField(resultingCardProp);
                }
            }
            EditorGUILayout.Space(6);
        }

        private void DrawCraftingSection()
        {
            EditorGUILayout.LabelField("Crafting Info", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(isContinuousProp);
                EditorGUILayout.PropertyField(allowExcessIngredientsProp);
                EditorGUILayout.PropertyField(craftingDurationProp);
                EditorGUILayout.PropertyField(randomWeightProp);
            }
            EditorGUILayout.Space(6);
        }

        private void DrawConflictInfoSection(RecipeDefinition recipe)
        {
            if (conflictingRecipes == null || conflictingRecipes.Count == 0) return;

            // Calculate total weight for probability
            float totalWeight = recipe.RandomWeight;
            foreach (var r in conflictingRecipes) totalWeight += r.RandomWeight;

            // Header Style
            GUIStyle headerStyle = new GUIStyle(EditorStyles.helpBox);
            headerStyle.fontSize = 12;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.alignment = TextAnchor.MiddleLeft;
            headerStyle.padding = new RectOffset(10, 10, 10, 10);

            // Container
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Shared Ingredients Detected ({conflictingRecipes.Count + 1} Recipes)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("The following recipes require the EXACT same ingredients.", EditorStyles.miniLabel);
            EditorGUILayout.Space(5);

            // Table Header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Result", EditorStyles.boldLabel, GUILayout.Width(120));
            EditorGUILayout.LabelField("Weight", EditorStyles.boldLabel, GUILayout.Width(60));
            EditorGUILayout.LabelField("Chance", EditorStyles.boldLabel, GUILayout.Width(60));
            EditorGUILayout.LabelField("Action", EditorStyles.boldLabel, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            DrawConflictRow(recipe, recipe.RandomWeight, totalWeight, true);

            foreach (var conflict in conflictingRecipes)
            {
                DrawConflictRow(conflict, conflict.RandomWeight, totalWeight, false);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawConflictRow(RecipeDefinition r, float weight, float totalWeight, bool isSelf)
        {
            EditorGUILayout.BeginHorizontal(isSelf ? "box" : GUIStyle.none);

            // Name
            string name = r.ResultingCard != null ? r.ResultingCard.DisplayName : r.name;
            if (isSelf) name += " (This)";
            EditorGUILayout.LabelField(name, GUILayout.Width(120));

            // Weight
            EditorGUILayout.LabelField(weight.ToString("0.##"), GUILayout.Width(60));

            // Chance %
            float chance = totalWeight > 0 ? (weight / totalWeight) * 100f : 0f;
            EditorGUILayout.LabelField($"{chance:F1}%", GUILayout.Width(60));

            // Ping Button
            if (!isSelf)
            {
                if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(60)))
                {
                    EditorGUIUtility.PingObject(r);
                    Selection.activeObject = r;
                }
            }
            else
            {
                EditorGUILayout.LabelField("-", GUILayout.Width(60)); // Placeholder
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
