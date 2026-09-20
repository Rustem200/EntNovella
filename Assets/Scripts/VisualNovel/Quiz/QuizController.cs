using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace VisualNovel.Quiz
{
    /// <summary>
    /// Задания поверх новеллы: на указанном кадре по очереди показывает объекты вопросов.
    /// Кнопки внутри объекта находятся сами, правильная задаётся номером.
    /// Неверный ответ — панелька ошибки, верный — панелька и карточка в журнале.
    /// </summary>
    [AddComponentMenu("Visual Novel/Quiz Controller")]
    public class QuizController : MonoBehaviour
    {
        [Header("Связи")]
        [SerializeField] NovelDirector director;
        [SerializeField] QuizJournal journal;

        [Header("Панельки")]
        [SerializeField] QuizResultPanel wrongPanelPrefab;
        [SerializeField] QuizResultPanel correctPanelPrefab;
        [Tooltip("Куда создавать панельки. Пусто — в родителя объекта вопроса.")]
        [SerializeField] RectTransform panelsParent;

        [Header("Задания")]
        [SerializeField] List<QuizTask> tasks = new List<QuizTask>();

        [Header("Анимация")]
        [SerializeField] UIAppearance appearance = UIAppearance.Pop;
        [SerializeField] float showDuration = 0.3f;
        [SerializeField] float hideDuration = 0.2f;
        [Tooltip("Дальность выезда, если выбран Slide.")]
        [SerializeField] float slideDistance = 180f;

        [Header("События (для звуков)")]
        public UnityEvent CorrectAnswer = new UnityEvent();
        public UnityEvent WrongAnswer = new UnityEvent();

        QuizTask activeTask;

        /// <summary>Идёт задание.</summary>
        public bool IsRunning => activeTask != null;

        void Awake()
        {
            if (director == null) director = GetComponentInParent<NovelDirector>();
        }

        void OnEnable()
        {
            if (director != null) director.StageShown += OnStageShown;
        }

        void OnDisable()
        {
            if (director != null) director.StageShown -= OnStageShown;
        }

        void OnStageShown(int stageIndex, NovelStage stage)
        {
            if (IsRunning) return;

            foreach (var task in tasks)
            {
                if (task == null || task.completed || task.stageIndex != stageIndex) continue;
                if (task.questions.Length == 0) continue;

                activeTask = task;
                if (director != null) director.InputBlocked = true;
                StartCoroutine(RunTask(task));
                return;
            }
        }

        IEnumerator RunTask(QuizTask task)
        {
            foreach (var question in task.questions)
            {
                if (question?.questionObject == null) continue;
                yield return RunQuestion(question);
            }

            task.completed = true;
            activeTask = null;

            if (director != null)
            {
                director.InputBlocked = false;
                director.Next();
            }
        }

        IEnumerator RunQuestion(QuizQuestion question)
        {
            var go = question.questionObject;
            var rect = go.transform as RectTransform;
            var group = go.GetComponent<CanvasGroup>();
            if (group == null) group = go.AddComponent<CanvasGroup>();

            var buttons = ResolveButtons(question, go);
            if (buttons.Length == 0)
            {
                Debug.LogWarning(
                    question.autoFindButtons
                        ? $"[{nameof(QuizController)}] В объекте «{go.name}» нет кнопок."
                        : $"[{nameof(QuizController)}] У вопроса «{go.name}» пустой список кнопок.", this);
                yield break;
            }

            int chosen = -1;
            for (int i = 0; i < buttons.Length; i++)
            {
                int index = i;
                buttons[i].onClick.RemoveAllListeners();
                buttons[i].onClick.AddListener(() => chosen = index);
            }

            yield return UITween.Appear(rect, group, appearance, showDuration, slideDistance);

            // Уже нажатые правильные кнопки — для вопросов с несколькими ответами.
            var found = new List<int>();
            int needed = question.requireAllCorrect && question.correctButtons != null
                ? question.correctButtons.Length
                : 1;

            bool answered = false;
            while (!answered)
            {
                while (chosen < 0) yield return null;

                int picked = chosen;
                chosen = -1;
                SetInteractable(buttons, false);

                bool correct = IsCorrect(question, picked) && !found.Contains(picked);
                if (correct)
                {
                    found.Add(picked);
                    CorrectAnswer.Invoke();

                    var panel = question.correctPanel != null ? question.correctPanel : correctPanelPrefab;
                    yield return ShowPanel(panel, go, null);

                    answered = found.Count >= needed;
                    if (answered) AddJournalCard(go, buttons, question, panel);
                    else SetInteractable(buttons, true);
                }
                else
                {
                    WrongAnswer.Invoke();
                    if (picked >= 0 && picked < buttons.Length)
                        yield return UITween.Shake((RectTransform)buttons[picked].transform);

                    yield return ShowPanel(wrongPanelPrefab, go, null);
                    SetInteractable(buttons, true);
                }

                // Уже отгаданные кнопки нажимать повторно нельзя.
                foreach (int index in found)
                    if (index >= 0 && index < buttons.Length && buttons[index] != null)
                        buttons[index].interactable = false;
            }

            yield return UITween.Disappear(rect, group, appearance, hideDuration, slideDistance);
        }

        IEnumerator ShowPanel(QuizResultPanel prefab, GameObject questionObject, string text)
        {
            if (prefab == null) yield break;

            var parent = panelsParent != null ? panelsParent : questionObject.transform.parent as RectTransform;
            var panel = Instantiate(prefab, parent != null ? parent : questionObject.transform);
            panel.transform.SetAsLastSibling();
            if (!string.IsNullOrEmpty(text)) panel.Fill(null, null, text);

            bool closed = false;
            panel.Show(() => closed = true);
            while (!closed) yield return null;
        }

        /// <summary>Кнопки вопроса: найденные в объекте или заданные списком.</summary>
        static Button[] ResolveButtons(QuizQuestion question, GameObject go)
        {
            if (question.autoFindButtons) return go.GetComponentsInChildren<Button>(true);

            var list = new List<Button>(question.buttons.Length);
            foreach (var button in question.buttons)
                if (button != null) list.Add(button);

            return list.ToArray();
        }

        static bool IsCorrect(QuizQuestion question, int index)
        {
            if (question.correctButtons == null) return false;
            foreach (int correct in question.correctButtons)
                if (correct == index) return true;
            return false;
        }

        /// <summary>
        /// Карточка журнала: текст и иконка берутся с панели верного ответа,
        /// а если там пусто — текст собирается из вопроса и подписей правильных кнопок.
        /// </summary>
        void AddJournalCard(GameObject questionObject, Button[] buttons, QuizQuestion question,
            QuizResultPanel panel)
        {
            if (journal == null) return;

            if (panel != null && !string.IsNullOrWhiteSpace(panel.JournalText))
            {
                journal.AddCard(panel.JournalText, panel.JournalIcon, string.Empty);
                return;
            }

            var parts = new List<string>();
            foreach (int index in question.correctButtons)
            {
                if (index < 0 || index >= buttons.Length || buttons[index] == null) continue;
                string label = LabelOf(buttons[index].gameObject);
                if (!string.IsNullOrWhiteSpace(label)) parts.Add(label);
            }

            string answer = string.Join(", ", parts);

            string prompt = string.Empty;
            foreach (var label in questionObject.GetComponentsInChildren<TMP_Text>(true))
            {
                // Берём первый текст, который не лежит внутри кнопки, — это текст вопроса.
                if (label.GetComponentInParent<Button>() != null) continue;
                prompt = label.text;
                break;
            }

            string card = string.IsNullOrWhiteSpace(prompt) ? answer : $"{prompt.TrimEnd()} {answer}";
            journal.AddCard(card, panel != null ? panel.JournalIcon : null, string.Empty);
        }

        static string LabelOf(GameObject go)
        {
            var label = go.GetComponentInChildren<TMP_Text>(true);
            return label != null ? label.text : string.Empty;
        }

        static void SetInteractable(Button[] buttons, bool value)
        {
            foreach (var button in buttons)
                if (button != null) button.interactable = value;
        }
    }
}
