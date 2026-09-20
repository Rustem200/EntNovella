using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VisualNovel.Quiz;

namespace VisualNovel.EditorTools
{
    /// <summary>
    /// Рисует вопрос четырьмя понятными строками, а номера правильных кнопок —
    /// одним полем: «3» или «1, 2», если правильных несколько.
    /// </summary>
    [CustomPropertyDrawer(typeof(QuizQuestion))]
    public class QuizQuestionDrawer : PropertyDrawer
    {
        const float Gap = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            int rows = RequireAllVisible(property) ? 5 : 4;
            float height = rows * (EditorGUIUtility.singleLineHeight + Gap);

            var autoProp = property.FindPropertyRelative("autoFindButtons");
            if (autoProp != null && !autoProp.boolValue)
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("buttons"), true) + Gap;

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var objectProp = property.FindPropertyRelative("questionObject");
            var correctProp = property.FindPropertyRelative("correctButtons");
            var requireProp = property.FindPropertyRelative("requireAllCorrect");
            var panelProp = property.FindPropertyRelative("correctPanel");

            var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            EditorGUI.PropertyField(row, objectProp, new GUIContent("Объект вопроса"));
            row.y += row.height + Gap;

            // Номера правильных кнопок одной строкой.
            EditorGUI.BeginChangeCheck();
            string text = EditorGUI.TextField(row,
                new GUIContent("Правильные кнопки", "Номера кнопок с нуля. Несколько — через запятую: 1, 2"),
                Join(correctProp));
            if (EditorGUI.EndChangeCheck()) Fill(correctProp, text);
            row.y += row.height + Gap;

            if (RequireAllVisible(property))
            {
                EditorGUI.PropertyField(row, requireProp, new GUIContent("Нужно нажать все"));
                row.y += row.height + Gap;
            }

            EditorGUI.PropertyField(row, panelProp, new GUIContent("Панель верного ответа"));
            row.y += row.height + Gap;

            var autoProp = property.FindPropertyRelative("autoFindButtons");
            var buttonsProp = property.FindPropertyRelative("buttons");

            EditorGUI.PropertyField(row, autoProp,
                new GUIContent("Искать кнопки самому",
                    "Включено — кнопки берутся из объекта по иерархии сверху вниз. " +
                    "Выключено — порядок задаётся списком ниже."));
            row.y += row.height + Gap;

            if (!autoProp.boolValue)
            {
                row.height = EditorGUI.GetPropertyHeight(buttonsProp, true);
                EditorGUI.PropertyField(row, buttonsProp, new GUIContent("Кнопки вариантов"), true);
            }

            EditorGUI.EndProperty();
        }

        /// <summary>Галочка «нужно нажать все» нужна, только когда правильных больше одной.</summary>
        static bool RequireAllVisible(SerializedProperty property)
        {
            var correct = property.FindPropertyRelative("correctButtons");
            return correct != null && correct.arraySize > 1;
        }

        static string Join(SerializedProperty array)
        {
            if (array == null) return string.Empty;

            var parts = new List<string>(array.arraySize);
            for (int i = 0; i < array.arraySize; i++)
                parts.Add(array.GetArrayElementAtIndex(i).intValue.ToString());

            return string.Join(", ", parts);
        }

        static void Fill(SerializedProperty array, string text)
        {
            if (array == null) return;

            var values = new List<int>();
            foreach (string chunk in text.Split(',', ' ', ';'))
            {
                if (string.IsNullOrWhiteSpace(chunk)) continue;
                if (int.TryParse(chunk.Trim(), out int value)) values.Add(value);
            }

            array.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                array.GetArrayElementAtIndex(i).intValue = values[i];
        }
    }
}
