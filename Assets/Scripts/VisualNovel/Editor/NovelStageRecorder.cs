using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VisualNovel.EditorTools
{
    /// <summary>
    /// Фоновый наблюдатель редактора: пока выбран этап и включена авто-запись,
    /// любое изменение элементов на сцене (спрайт, размер, позиция, масштаб,
    /// цвет, текст, включён/выключен) сразу пишется в поля именно этого этапа.
    /// Остальные этапы не трогаются.
    /// </summary>
    [InitializeOnLoad]
    public static class NovelStageRecorder
    {
        const double PollInterval = 0.08; // с

        static double nextPoll;
        static readonly List<NovelDirector> Directors = new List<NovelDirector>();
        static bool directorsDirty = true;

        static NovelStageRecorder()
        {
            EditorApplication.update += Poll;
            EditorApplication.hierarchyChanged += () => directorsDirty = true;
            EditorApplication.playModeStateChanged += _ => directorsDirty = true;
        }

        /// <summary>Сбросить кэш после программного показа этапа.</summary>
        public static void Sync(NovelDirector director) => nextPoll = EditorApplication.timeSinceStartup + PollInterval;

        static void Poll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                return;

            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + PollInterval;

            if (directorsDirty) RefreshDirectors();

            for (int i = 0; i < Directors.Count; i++)
            {
                var director = Directors[i];
                if (director == null)
                {
                    directorsDirty = true;
                    continue;
                }

                RecordIfChanged(director);
            }
        }

        static void RefreshDirectors()
        {
            Directors.Clear();
            Directors.AddRange(Object.FindObjectsByType<NovelDirector>(
                FindObjectsInactive.Include, FindObjectsSortMode.None));
            directorsDirty = false;
        }

        static void RecordIfChanged(NovelDirector director)
        {
            // Новый этап (в том числе добавленный кнопкой «+» самого списка)
            // сразу забирает текущие настройки объектов со сцены, а не остаётся пустым.
            if (director.CaptureNewStages() > 0)
            {
                EditorUtility.SetDirty(director);
                MarkDirty(director);
            }

            if (!director.EditorAutoRecord) return;

            var stages = director.StagesEditable;
            int index = director.EditorStageIndex;
            if (index < 0 || index >= stages.Count) return;

            var stage = stages[index];
            if (director.MatchesScene(stage)) return;

            director.CaptureInto(stage);
            EditorUtility.SetDirty(director);
            MarkDirty(director);
        }

        static void MarkDirty(NovelDirector director)
        {
            var scene = director.gameObject.scene;
            if (scene.IsValid() && !scene.isDirty) EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
