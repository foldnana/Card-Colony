using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace CryingSnow.StackCraft
{
    [CustomEditor(typeof(QuestManager))]
    public class QuestManagerEditor : Editor
    {
        private SerializedProperty groupsProp;

        // Main List (QuestGroup)
        private ReorderableList groupsList;

        // Nested Lists per Group (Quest)
        private readonly Dictionary<int, ReorderableList> nestedQuestLists = new();

        // Foldout States
        private readonly Dictionary<int, bool> foldouts = new();

        private void OnEnable()
        {
            groupsProp = serializedObject.FindProperty("questGroups");

            groupsList = new ReorderableList(serializedObject, groupsProp, true, true, true, true);

            groupsList.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, "Quest Groups");
            };

            groupsList.elementHeightCallback = index =>
            {
                bool isOpen = foldouts.ContainsKey(index) && foldouts[index];

                if (!isOpen)
                    return EditorGUIUtility.singleLineHeight + 6;

                return CalculateExpandedHeight(index);
            };

            groupsList.onReorderCallbackWithDetails = (list, oldIndex, newIndex) =>
            {
                nestedQuestLists.Clear();
            };

            groupsList.drawElementCallback = DrawGroupElement;
        }

        private float CalculateExpandedHeight(int index)
        {
            SerializedProperty group = groupsProp.GetArrayElementAtIndex(index);
            SerializedProperty questsProp = group.FindPropertyRelative("Quests");

            float height = 6;
            height += EditorGUIUtility.singleLineHeight + 4;

            if (!nestedQuestLists.ContainsKey(index))
                CreateNestedList(index, questsProp);

            height += nestedQuestLists[index].GetHeight() + 20;

            return height;
        }

        private void DrawGroupElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            SerializedProperty group = groupsProp.GetArrayElementAtIndex(index);
            SerializedProperty groupNameProp = group.FindPropertyRelative("GroupName");
            SerializedProperty questsProp = group.FindPropertyRelative("Quests");

            if (!foldouts.ContainsKey(index))
                foldouts[index] = false;

            float handleWidth = 18f;

            Rect foldRect = new Rect(
                rect.x + handleWidth,
                rect.y + 2,
                rect.width - handleWidth,
                EditorGUIUtility.singleLineHeight
            );

            int questCount = group.FindPropertyRelative("Quests").arraySize;
            string headerLabel = $"{groupNameProp.stringValue} ({questCount})";

            foldouts[index] = EditorGUI.Foldout(foldRect, foldouts[index], headerLabel, true);

            if (!foldouts[index])
                return;

            EditorGUI.indentLevel++;

            Rect nameRect = new(rect.x, rect.y + 22, rect.width - 6, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(nameRect, groupNameProp);

            // Nested ReorderableList
            if (!nestedQuestLists.ContainsKey(index))
                CreateNestedList(index, questsProp);

            Rect listRect = new(
                rect.x,
                rect.y + 22 + EditorGUIUtility.singleLineHeight + 6,
                rect.width - 10,
                nestedQuestLists[index].GetHeight()
            );

            nestedQuestLists[index].DoList(listRect);

            EditorGUI.indentLevel--;
        }

        private void CreateNestedList(int index, SerializedProperty questsProp)
        {
            var list = new ReorderableList(serializedObject, questsProp, true, true, true, true);

            list.drawHeaderCallback = rect =>
            {
                string title = $"Quests ({questsProp.arraySize})";
                EditorGUI.LabelField(rect, title);

                HandleDragAndDrop(rect, questsProp);
            };

            list.elementHeight = EditorGUIUtility.singleLineHeight + 4;

            list.drawElementCallback = (rect, i, a, f) =>
            {
                SerializedProperty questProp = questsProp.GetArrayElementAtIndex(i);
                EditorGUI.PropertyField(rect, questProp, GUIContent.none);
            };

            nestedQuestLists[index] = list;
        }

        private void HandleDragAndDrop(Rect rect, SerializedProperty listProperty)
        {
            Event evt = Event.current;

            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                if (!rect.Contains(evt.mousePosition))
                    return;

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();

                    foreach (Object draggedObject in DragAndDrop.objectReferences)
                    {
                        // 1. Add a new element to the array
                        int index = listProperty.arraySize;
                        listProperty.InsertArrayElementAtIndex(index);

                        // 2. Get the new element
                        SerializedProperty element = listProperty.GetArrayElementAtIndex(index);

                        // 3. Assign the dragged object to it
                        if (element.propertyType == SerializedPropertyType.ObjectReference)
                        {
                            element.objectReferenceValue = draggedObject;
                        }
                    }
                }

                evt.Use();
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            groupsList.DoLayoutList();

            EditorGUILayout.BeginVertical("box");

            int totalQuests = 0;

            for (int i = 0; i < groupsProp.arraySize; i++)
            {
                SerializedProperty group = groupsProp.GetArrayElementAtIndex(i);
                SerializedProperty questsProp = group.FindPropertyRelative("Quests");
                totalQuests += questsProp.arraySize;
            }

            EditorGUILayout.LabelField(
                $"Total Quests: {totalQuests}",
                EditorStyles.boldLabel
            );

            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();
        }
    }
}
