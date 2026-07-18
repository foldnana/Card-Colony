using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace CryingSnow.StackCraft
{
    [CustomEditor(typeof(CardDefinition))]
    public class CardDefinitionEditor : Editor
    {
        private SerializedProperty idProp;
        private SerializedProperty displayNameProp;
        private SerializedProperty descriptionProp;
        private SerializedProperty artTextureProp;

        private SerializedProperty categoryProp;
        private SerializedProperty factionProp;
        private SerializedProperty combatTypeProp;

        private SerializedProperty lootProp;

        private SerializedProperty isAggressiveProp;
        private SerializedProperty aggroRadiusProp;
        private SerializedProperty attackRadiusProp;

        private SerializedProperty produceCardProp;
        private SerializedProperty produceIntervalProp;

        private SerializedProperty isSellableProp;
        private SerializedProperty sellPriceProp;

        private SerializedProperty hasDurabilityProp;
        private SerializedProperty usesProp;

        private SerializedProperty nutritionProp;

        private SerializedProperty maxHealthProp;
        private SerializedProperty attackProp;
        private SerializedProperty defenseProp;
        private SerializedProperty attackSpeedProp;
        private SerializedProperty accuracyProp;
        private SerializedProperty dodgeProp;
        private SerializedProperty criticalChanceProp;
        private SerializedProperty criticalMultiplierProp;

        private SerializedProperty equipmentSlotProp;
        private SerializedProperty statModifiersProp;
        private SerializedProperty classChangeResultProp;

        private ReorderableList statModifiersList;

        protected virtual void OnEnable()
        {
            idProp = serializedObject.FindProperty("id");
            displayNameProp = serializedObject.FindProperty("displayName");
            descriptionProp = serializedObject.FindProperty("description");
            artTextureProp = serializedObject.FindProperty("artTexture");

            categoryProp = serializedObject.FindProperty("category");
            factionProp = serializedObject.FindProperty("faction");
            combatTypeProp = serializedObject.FindProperty("combatType");

            lootProp = serializedObject.FindProperty("loot");

            isAggressiveProp = serializedObject.FindProperty("isAggressive");
            aggroRadiusProp = serializedObject.FindProperty("aggroRadius");
            attackRadiusProp = serializedObject.FindProperty("attackRadius");

            produceCardProp = serializedObject.FindProperty("produceCard");
            produceIntervalProp = serializedObject.FindProperty("produceInterval");

            isSellableProp = serializedObject.FindProperty("isSellable");
            sellPriceProp = serializedObject.FindProperty("sellPrice");

            hasDurabilityProp = serializedObject.FindProperty("hasDurability");
            usesProp = serializedObject.FindProperty("uses");

            nutritionProp = serializedObject.FindProperty("nutrition");

            maxHealthProp = serializedObject.FindProperty("maxHealth");
            attackProp = serializedObject.FindProperty("attack");
            defenseProp = serializedObject.FindProperty("defense");
            attackSpeedProp = serializedObject.FindProperty("attackSpeed");
            accuracyProp = serializedObject.FindProperty("accuracy");
            dodgeProp = serializedObject.FindProperty("dodge");
            criticalChanceProp = serializedObject.FindProperty("criticalChance");
            criticalMultiplierProp = serializedObject.FindProperty("criticalMultiplier");

            equipmentSlotProp = serializedObject.FindProperty("equipmentSlot");
            statModifiersProp = serializedObject.FindProperty("statModifiers");
            classChangeResultProp = serializedObject.FindProperty("classChangeResult");

            statModifiersList = new ReorderableList(
                serializedObject,
                statModifiersProp,
                draggable: true,
                displayHeader: true,
                displayAddButton: true,
                displayRemoveButton: true
            );

            statModifiersList.drawHeaderCallback = (Rect rect) =>
            {
                EditorGUI.LabelField(rect, "Stat Modifiers");
            };

            statModifiersList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                SerializedProperty element = statModifiersList.serializedProperty.GetArrayElementAtIndex(index);

                SerializedProperty statProp = element.FindPropertyRelative("Stat");
                SerializedProperty valueProp = element.FindPropertyRelative("value");

                // Add a little vertical padding
                rect.y += 2;
                rect.height = EditorGUIUtility.singleLineHeight;

                // Define rectangles for Stat (60%) and Value (40%)
                float padding = 5f;
                float statWidth = rect.width * 0.6f;
                float valueWidth = rect.width - statWidth - padding;

                Rect statRect = new Rect(rect.x, rect.y, statWidth, rect.height);
                Rect valueRect = new Rect(rect.x + statWidth + padding, rect.y, valueWidth, rect.height);

                EditorGUI.PropertyField(statRect, statProp, GUIContent.none);
                EditorGUI.PropertyField(valueRect, valueProp, GUIContent.none);
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            CardCategory category = (CardCategory)categoryProp.enumValueIndex;

            DrawIdentificationSection(category);
            DrawClassificationSection(category);
            DrawLootSection(category, lootProp);
            DrawAggressiveMobSection(category);
            DrawPassiveMobSection(category);
            DrawTradingSection();
            DrawCraftingSection(category);
            DrawFoodSection(category);
            DrawStatsSection(category);
            DrawEquipmentSection(category);

            DrawDerivedSection();

            serializedObject.ApplyModifiedProperties();
        }

        protected virtual void DrawDerivedSection() { }

        private void DrawIdentificationSection(CardCategory category)
        {
            EditorGUILayout.LabelField("Identification", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                bool isRecipe = category == CardCategory.Recipe;

                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.PropertyField(idProp);
                EditorGUI.EndDisabledGroup();

                EditorGUI.BeginDisabledGroup(isRecipe);
                EditorGUILayout.PropertyField(displayNameProp);
                EditorGUILayout.PropertyField(descriptionProp);
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.Space();

                if (isRecipe)
                {
                    EditorGUILayout.HelpBox(
                        "Do NOT create Recipe cards manually.\n" +
                        "(e.g. Recipe: Plank, Recipe: House)." +
                        "\nThey are generated automatically at runtime.",
                        MessageType.Warning
                    );
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.HelpBox(
                        "Art Texture (Card Art)\nMake sure the Alpha Source is set correctly in the Import Settings.",
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
            }

            EditorGUILayout.Space(6);
        }

        private void DrawClassificationSection(CardCategory category)
        {
            EditorGUILayout.LabelField("Classification", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(categoryProp);

                if (category == CardCategory.Character || category == CardCategory.Mob)
                {
                    EditorGUILayout.PropertyField(factionProp);
                    EditorGUILayout.PropertyField(combatTypeProp);
                }
            }

            EditorGUILayout.Space(6);
        }

        private void DrawLootSection(CardCategory category, SerializedProperty listProp)
        {
            if (category is not (CardCategory.Mob or CardCategory.Character or CardCategory.Area))
                return;

            EditorGUILayout.LabelField(listProp.displayName, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            int indexToDelete = -1;

            for (int i = 0; i < listProp.arraySize; i++)
            {
                SerializedProperty entryProp = listProp.GetArrayElementAtIndex(i);
                SerializedProperty cardProp = entryProp.FindPropertyRelative("Card");
                SerializedProperty weightProp = entryProp.FindPropertyRelative("Weight");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(cardProp, GUIContent.none);
                EditorGUILayout.PropertyField(weightProp, GUIContent.none, GUILayout.Width(80));
                GUILayout.Label("%", GUILayout.Width(20));

                if (GUILayout.Button("-", GUILayout.Width(20)))
                {
                    indexToDelete = i; // Mark for deletion
                }
                EditorGUILayout.EndHorizontal();
            }

            // Perform deletion outside the loop
            if (indexToDelete > -1)
            {
                listProp.DeleteArrayElementAtIndex(indexToDelete);
            }

            if (GUILayout.Button("+ Add Loot Entry"))
            {
                listProp.InsertArrayElementAtIndex(listProp.arraySize);
                SerializedProperty newEntry = listProp.GetArrayElementAtIndex(listProp.arraySize - 1);
                newEntry.FindPropertyRelative("Card").objectReferenceValue = null;
                newEntry.FindPropertyRelative("Weight").intValue = 1;
            }

            if (listProp.arraySize > 0)
            {
                if (GUILayout.Button("Normalize Weights (to 100%)"))
                {
                    int totalWeight = 0;
                    for (int i = 0; i < listProp.arraySize; i++)
                    {
                        totalWeight += listProp.GetArrayElementAtIndex(i).FindPropertyRelative("Weight").intValue;
                    }

                    if (totalWeight > 0)
                    {
                        int totalPercentage = 0;
                        for (int i = 0; i < listProp.arraySize; i++)
                        {
                            SerializedProperty weightProp = listProp.GetArrayElementAtIndex(i).FindPropertyRelative("Weight");
                            // Calculate new percentage and round it
                            int newWeight = Mathf.RoundToInt((float)weightProp.intValue / totalWeight * 100f);
                            weightProp.intValue = newWeight;
                            totalPercentage += newWeight;
                        }

                        // Adjust for rounding errors (e.g., 33+33+33 = 99)
                        int diff = 100 - totalPercentage;
                        if (diff != 0)
                        {
                            // Add/subtract the difference from the first element
                            listProp.GetArrayElementAtIndex(0).FindPropertyRelative("Weight").intValue += diff;
                        }

                        // Ensure no weight is less than 1 after normalization
                        for (int i = 0; i < listProp.arraySize; i++)
                        {
                            SerializedProperty weightProp = listProp.GetArrayElementAtIndex(i).FindPropertyRelative("Weight");
                            if (weightProp.intValue < 1) weightProp.intValue = 1;
                        }
                    }
                }
            }

            EditorGUI.indentLevel--;
        }

        private void DrawAggressiveMobSection(CardCategory category)
        {
            if (category != CardCategory.Mob) return;

            CardFaction faction = (CardFaction)factionProp.enumValueIndex;
            if (faction != CardFaction.Mob) return;

            EditorGUILayout.LabelField("Aggressive Mob", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(isAggressiveProp);
                if (isAggressiveProp.boolValue)
                {
                    EditorGUILayout.PropertyField(aggroRadiusProp);
                    EditorGUILayout.PropertyField(attackRadiusProp);
                }
            }

            EditorGUILayout.Space(6);
        }

        private void DrawPassiveMobSection(CardCategory category)
        {
            if (category is not CardCategory.Mob || isAggressiveProp.boolValue)
                return;

            EditorGUILayout.LabelField("Passive Mob", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(produceCardProp);

                if (produceCardProp.objectReferenceValue != null)
                    EditorGUILayout.PropertyField(produceIntervalProp);
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
                {
                    EditorGUILayout.PropertyField(sellPriceProp);
                }
            }

            EditorGUILayout.Space(6);
        }

        private void DrawCraftingSection(CardCategory category)
        {
            if (category == CardCategory.Recipe) return;

            EditorGUILayout.LabelField("Crafting", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(hasDurabilityProp);

                if (hasDurabilityProp.boolValue)
                {
                    EditorGUI.indentLevel++;

                    EditorGUILayout.PropertyField(usesProp);

                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.Space(6);
        }

        private void DrawFoodSection(CardCategory category)
        {
            if (category != CardCategory.Consumable) return;

            EditorGUILayout.LabelField("Food", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(nutritionProp);
            }

            EditorGUILayout.Space(6);
        }

        private void DrawStatsSection(CardCategory category)
        {
            bool showCombatStats = category == CardCategory.Character || category == CardCategory.Mob;
            if (!showCombatStats) return;

            EditorGUILayout.LabelField("Stats", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                if (showCombatStats)
                {
                    EditorGUILayout.PropertyField(maxHealthProp);
                    EditorGUILayout.PropertyField(attackProp);
                    EditorGUILayout.PropertyField(defenseProp);
                    EditorGUILayout.IntSlider(attackSpeedProp, 30, 300);
                    EditorGUILayout.IntSlider(accuracyProp, 0, 100);
                    EditorGUILayout.IntSlider(dodgeProp, 0, 100);
                    EditorGUILayout.IntSlider(criticalChanceProp, 0, 100);
                    EditorGUILayout.IntSlider(criticalMultiplierProp, 100, 300);
                }
            }

            EditorGUILayout.Space(6);
        }

        private void DrawEquipmentSection(CardCategory category)
        {
            if (category != CardCategory.Equipment) return;

            EditorGUILayout.LabelField("Equipment", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(equipmentSlotProp);
                statModifiersList.DoLayoutList();

                if (equipmentSlotProp.intValue == (int)EquipmentSlot.Weapon)
                {
                    EditorGUILayout.PropertyField(classChangeResultProp);
                }
            }

            EditorGUILayout.Space(6);
        }
    }
}
