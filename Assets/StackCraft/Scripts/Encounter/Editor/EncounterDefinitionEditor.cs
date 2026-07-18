using UnityEngine;
using UnityEditor;

namespace CryingSnow.StackCraft
{
    [CustomEditor(typeof(EncounterDefinition))]
    public class EncounterDefinitionEditor : Editor
    {
        SerializedProperty idProp;
        SerializedProperty messageProp;
        SerializedProperty cardProp;
        SerializedProperty countProp;
        SerializedProperty oneTimeProp;
        SerializedProperty typeProp;
        SerializedProperty dayValueProp;
        SerializedProperty maxDayProp;
        SerializedProperty priorityProp;
        SerializedProperty chanceProp;
        SerializedProperty limitProp;

        private void OnEnable()
        {
            idProp = serializedObject.FindProperty("id");
            messageProp = serializedObject.FindProperty("notificationMessage");
            cardProp = serializedObject.FindProperty("cardToSpawn");
            countProp = serializedObject.FindProperty("count");
            oneTimeProp = serializedObject.FindProperty("oneTimeOnly");
            typeProp = serializedObject.FindProperty("type");
            dayValueProp = serializedObject.FindProperty("dayValue");
            maxDayProp = serializedObject.FindProperty("maxDayValue");
            priorityProp = serializedObject.FindProperty("priority");
            chanceProp = serializedObject.FindProperty("chance");
            limitProp = serializedObject.FindProperty("maxCardsOnBoardLimit");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
            GUI.enabled = false;
            EditorGUILayout.PropertyField(idProp);
            GUI.enabled = true;
            EditorGUILayout.PropertyField(messageProp);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Spawn Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(cardProp);
            EditorGUILayout.PropertyField(countProp);
            EditorGUILayout.PropertyField(oneTimeProp);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Conditions", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(typeProp);

            EncounterType type = (EncounterType)typeProp.enumValueIndex;

            switch (type)
            {
                case EncounterType.SpecificDay:
                    EditorGUILayout.PropertyField(dayValueProp, new GUIContent("On Day"));
                    break;
                case EncounterType.Recurring:
                    EditorGUILayout.PropertyField(dayValueProp, new GUIContent("Every X Days"));
                    break;
                case EncounterType.MinimumDay:
                    EditorGUILayout.PropertyField(dayValueProp, new GUIContent("Starting From Day"));
                    break;
                case EncounterType.Range:
                    EditorGUILayout.PropertyField(dayValueProp, new GUIContent("From Day"));
                    EditorGUILayout.PropertyField(maxDayProp, new GUIContent("Until Day"));
                    break;
            }

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Constraints", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(priorityProp);
            EditorGUILayout.PropertyField(chanceProp);
            EditorGUILayout.PropertyField(limitProp);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
