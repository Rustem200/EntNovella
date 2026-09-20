using UnityEditor;
using UnityEngine;

namespace VisualNovel.EditorTools
{
    /// <summary>
    /// Инспектор режиссёра: навигация по этапам, показ этапа на сцене,
    /// ручная и авто-запись правок сцены в выбранный этап.
    /// </summary>
    [CustomEditor(typeof(NovelDirector))]
    public class NovelDirectorEditor : UnityEditor.Editor
    {
        NovelDirector director;

        void OnEnable() => director = (NovelDirector)target;

        // Нужно, чтобы статус «есть незаписанные правки» обновлялся живьём.
        public override bool RequiresConstantRepaint() => !Application.isPlaying;

        public override void OnInspectorGUI()
        {
            // Этап, добавленный кнопкой «+» списка, заполняем со сцены сразу же.
            if (!Application.isPlaying && director.CaptureNewStages() > 0)
            {
                EditorUtility.SetDirty(director);
                serializedObject.Update();
            }

            DrawDefaultInspector();

            EditorGUILayout.Space(8);
            DrawStageTools();
        }

        void DrawStageTools()
        {
            var stages = director.StagesEditable;
            int count = stages.Count;
            int index = Mathf.Clamp(director.EditorStageIndex, -1, count - 1);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Редактор этапов", EditorStyles.boldLabel);

            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "В Play Mode правки этапов не записываются. Выйди из Play Mode.",
                    MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            if (count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Этапов пока нет. Разложи элементы на сцене как надо и нажми «＋ Добавить этап со сцены».",
                    MessageType.Info);
                if (GUILayout.Button("＋ Добавить этап со сцены", GUILayout.Height(24)))
                    AddStage();
                EditorGUILayout.EndVertical();
                return;
            }

            // --- навигация ---
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(index <= 0))
            {
                if (GUILayout.Button("◀", GUILayout.Width(32))) SelectStage(index - 1);
            }

            string title = index < 0
                ? "— этап не выбран —"
                : $"{index + 1} / {count}   «{stages[index].label}»";
            EditorGUILayout.LabelField(title, EditorStyles.centeredGreyMiniLabel);

            using (new EditorGUI.DisabledScope(index >= count - 1))
            {
                if (GUILayout.Button("▶", GUILayout.Width(32))) SelectStage(index + 1);
            }

            EditorGUILayout.EndHorizontal();

            // Важно: слайдер меняет этап только когда его тронул человек.
            // Иначе перерисовка инспектора сама накладывала бы этап на сцену.
            EditorGUI.BeginChangeCheck();
            int picked = EditorGUILayout.IntSlider("Этап на сцене", Mathf.Max(index, 0) + 1, 1, count) - 1;
            if (EditorGUI.EndChangeCheck() && picked != index) SelectStage(picked);

            // --- авто-запись ---
            EditorGUI.BeginChangeCheck();
            bool auto = EditorGUILayout.ToggleLeft(
                "Авто-запись правок сцены в выбранный этап", director.EditorAutoRecord);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(director, "Novel auto record");
                director.EditorAutoRecord = auto;
                EditorUtility.SetDirty(director);
            }

            // --- статус синхронизации ---
            if (index >= 0)
            {
                bool synced = director.MatchesScene(stages[index]);
                EditorGUILayout.LabelField(
                    synced
                        ? "✔ Этап совпадает со сценой"
                        : auto
                            ? "● Пишу правки сцены в этап…"
                            : "✎ На сцене есть незаписанные правки",
                    EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(4);

            // --- действия ---
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Показать на сцене")) SelectStage(index);
            using (new EditorGUI.DisabledScope(index < 0))
            {
                if (GUILayout.Button("Записать сцену в этап")) CaptureStage(index);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("＋ Добавить со сцены")) AddStage();
            using (new EditorGUI.DisabledScope(index < 0))
            {
                if (GUILayout.Button("Дублировать")) DuplicateStage(index);
                if (GUILayout.Button("Удалить")) RemoveStage(index);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        void SelectStage(int index)
        {
            var stages = director.StagesEditable;
            if (index < 0 || index >= stages.Count) return;

            var targets = director.CollectSceneTargets();
            Undo.RecordObjects(targets, "Показать этап новеллы");
            Undo.RecordObject(director, "Показать этап новеллы");

            director.EditorStageIndex = index;
            director.ApplyStage(stages[index]);

            foreach (var o in targets) EditorUtility.SetDirty(o);
            EditorUtility.SetDirty(director);
            NovelStageRecorder.Sync(director);
        }

        void CaptureStage(int index)
        {
            var stages = director.StagesEditable;
            if (index < 0 || index >= stages.Count) return;

            Undo.RecordObject(director, "Записать этап новеллы");
            director.CaptureInto(stages[index]);
            EditorUtility.SetDirty(director);
        }

        void AddStage()
        {
            Undo.RecordObject(director, "Добавить этап новеллы");
            int index = director.AddStageFromScene();
            director.EditorStageIndex = index;
            EditorUtility.SetDirty(director);
        }

        void DuplicateStage(int index)
        {
            var stages = director.StagesEditable;
            Undo.RecordObject(director, "Дублировать этап новеллы");
            var copy = stages[index].Clone();
            copy.label += " (копия)";
            stages.Insert(index + 1, copy);
            director.EditorStageIndex = index + 1;
            EditorUtility.SetDirty(director);
        }

        void RemoveStage(int index)
        {
            var stages = director.StagesEditable;
            if (!EditorUtility.DisplayDialog(
                    "Удалить этап?", $"Удалить этап «{stages[index].label}»?", "Удалить", "Отмена"))
                return;

            Undo.RecordObject(director, "Удалить этап новеллы");
            stages.RemoveAt(index);
            director.EditorStageIndex = Mathf.Clamp(index, -1, stages.Count - 1);
            EditorUtility.SetDirty(director);
        }
    }
}
