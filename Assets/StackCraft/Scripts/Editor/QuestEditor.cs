using UnityEditor;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    [CustomEditor(typeof(Quest))]
    public class QuestEditor : Editor
    {
        SerializedProperty idProp;
        SerializedProperty titleProp;
        SerializedProperty descriptionProp;

        SerializedProperty typeProp;
        SerializedProperty targetCardProp;
        SerializedProperty targetRecipeProp;
        SerializedProperty targetAmountProp;
        SerializedProperty targetPaceProp;

        SerializedProperty prereqProp;
        SerializedProperty unlockProp;

        void OnEnable()
        {
            idProp = serializedObject.FindProperty("id");
            titleProp = serializedObject.FindProperty("title");
            descriptionProp = serializedObject.FindProperty("description");

            typeProp = serializedObject.FindProperty("type");
            targetCardProp = serializedObject.FindProperty("targetCard");
            targetRecipeProp = serializedObject.FindProperty("targetRecipe");
            targetAmountProp = serializedObject.FindProperty("targetAmount");
            targetPaceProp = serializedObject.FindProperty("targetPace");

            prereqProp = serializedObject.FindProperty("prerequisiteQuests");
            unlockProp = serializedObject.FindProperty("questsToUnlock");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Info", EditorStyles.boldLabel);

            GUI.enabled = false;
            EditorGUILayout.PropertyField(idProp);
            GUI.enabled = true;

            EditorGUILayout.PropertyField(titleProp);
            EditorGUILayout.PropertyField(descriptionProp);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Requirements", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(typeProp);

            QuestType questType = (QuestType)typeProp.enumValueIndex;

            EditorGUI.indentLevel++;

            bool forceAmountOne =
                questType == QuestType.Discover ||
                questType == QuestType.Equip ||
                questType == QuestType.Time;

            switch (questType)
            {
                case QuestType.Have:
                case QuestType.Obtain:
                case QuestType.Defeat:
                case QuestType.Craft:
                case QuestType.Sell:
                case QuestType.Buy:
                    EditorGUILayout.PropertyField(targetCardProp);
                    EditorGUILayout.PropertyField(targetAmountProp);
                    break;

                case QuestType.Discover:
                    EditorGUILayout.PropertyField(targetRecipeProp);
                    targetAmountProp.intValue = 1;
                    GUI.enabled = false;
                    EditorGUILayout.PropertyField(targetAmountProp);
                    GUI.enabled = true;
                    break;

                case QuestType.Equip:
                case QuestType.Explore:
                    EditorGUILayout.PropertyField(targetCardProp);
                    targetAmountProp.intValue = 1;
                    GUI.enabled = false;
                    EditorGUILayout.PropertyField(targetAmountProp);
                    GUI.enabled = true;
                    break;

                case QuestType.Time:
                    EditorGUILayout.PropertyField(targetPaceProp);
                    targetAmountProp.intValue = 1;
                    GUI.enabled = false;
                    EditorGUILayout.PropertyField(targetAmountProp);
                    GUI.enabled = true;
                    break;

                case QuestType.Day:
                    EditorGUILayout.PropertyField(targetAmountProp);
                    break;

                case QuestType.Food:
                case QuestType.Coins:
                case QuestType.Capacity:
                    EditorGUILayout.PropertyField(targetAmountProp);
                    break;
            }

            EditorGUI.indentLevel--;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Flow", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(prereqProp, true);
            EditorGUILayout.PropertyField(unlockProp, true);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
