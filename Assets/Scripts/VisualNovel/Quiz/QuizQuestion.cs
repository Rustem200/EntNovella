using System;
using UnityEngine;
using UnityEngine.UI;

namespace VisualNovel.Quiz
{
    /// <summary>Вопрос: готовый объект со сцены и номер правильной кнопки внутри него.</summary>
    [Serializable]
    public class QuizQuestion
    {
        [Tooltip("Объект вопроса: панель с текстом и кнопками вариантов.")]
        public GameObject questionObject;

        [Tooltip("Номера правильных кнопок внутри объекта, считая с нуля (сверху вниз по иерархии).")]
        public int[] correctButtons = { 0 };

        [Tooltip("Нужно нажать все правильные кнопки. Выключено — засчитывается любая из них.")]
        public bool requireAllCorrect = true;

        [Tooltip("Префаб панельки правильного ответа для этого вопроса. " +
                 "Пусто — возьмётся общий из контроллера.")]
        public QuizResultPanel correctPanel;

        [Tooltip("Искать кнопки внутри объекта самому — по иерархии сверху вниз. " +
                 "Выключено — порядок берётся из списка ниже.")]
        public bool autoFindButtons = true;

        [Tooltip("Кнопки вариантов в нужном порядке: номер правильной считается по этому списку.")]
        public Button[] buttons = new Button[0];
    }

    /// <summary>Задание: кадр новеллы и вопросы, которые на нём показываются по очереди.</summary>
    [Serializable]
    public class QuizTask
    {
        [Tooltip("Номер кадра новеллы, на котором включается задание.")]
        public int stageIndex = -1;

        public QuizQuestion[] questions = new QuizQuestion[0];

        [HideInInspector] public bool completed;
    }
}
