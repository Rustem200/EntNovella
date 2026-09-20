using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VisualNovel.Quiz
{
    /// <summary>
    /// Журнал «Вопросы ЕНТ»: карточки за правильные ответы и кнопки-фильтры по категориям.
    /// Префаб карточки, кнопки и разметка — твои; здесь появление карточки и переключение фильтра.
    /// </summary>
    [AddComponentMenu("Visual Novel/Quiz Journal")]
    public class QuizJournal : MonoBehaviour
    {
        [Serializable]
        public class CategoryFilter
        {
            [Tooltip("Категория, как она написана в вопросе. Пусто — кнопка «все».")]
            public string category;

            public Button button;
        }

        [Header("Карточки")]
        [Tooltip("Префаб карточки с компонентом Quiz Journal Card.")]
        [SerializeField] QuizJournalCard cardPrefab;

        [Tooltip("Контейнер, куда складываются карточки (обычно с Vertical Layout Group).")]
        [SerializeField] RectTransform cardsContainer;

        [Tooltip("Добавлять новые карточки сверху списка.")]
        [SerializeField] bool newestFirst;

        [Header("Фильтры")]
        [SerializeField] List<CategoryFilter> filters = new List<CategoryFilter>();

        [Tooltip("Категория, выбранная при старте. Пусто — показывать всё.")]
        [SerializeField] string startCategory = string.Empty;

        [Header("Анимация")]
        [SerializeField] float cardAppearDuration = 0.3f;
        [Tooltip("Смещение, из которого выезжает карточка. (0,0) — просто отскок. " +
                 "Если контейнер с Layout Group, оставь (0,0): позицию там задаёт разметка.")]
        [SerializeField] Vector2 cardSlideFrom = Vector2.zero;
        [Tooltip("Подсветить пульсом карточку после появления.")]
        [SerializeField] bool pulseNewCard = true;

        [Tooltip("Печатать текст новой карточки по буквам.")]
        [SerializeField] bool typeCardText = true;

        [Tooltip("Символов в секунду при печати карточки.")]
        [SerializeField] float charactersPerSecond = 70f;
        [SerializeField] float filterFadeDuration = 0.15f;

        readonly List<QuizJournalCard> cards = new List<QuizJournalCard>();
        string current;

        /// <summary>Все карточки журнала.</summary>
        public IReadOnlyList<QuizJournalCard> Cards => cards;

        void Awake()
        {
            current = startCategory;
            foreach (var filter in filters)
            {
                if (filter?.button == null) continue;
                string category = filter.category;
                filter.button.onClick.AddListener(() => ShowCategory(category));
            }
        }

        void Start() => ApplyFilter(false);

        /// <summary>Добавить карточку.</summary>
        public QuizJournalCard AddCard(string text, Sprite icon, string category)
        {
            if (cardPrefab == null || cardsContainer == null)
            {
                Debug.LogWarning($"[{nameof(QuizJournal)}] Не заданы префаб карточки или контейнер.", this);
                return null;
            }

            var card = Instantiate(cardPrefab, cardsContainer);
            card.gameObject.SetActive(true);
            card.Fill(text, icon, category);
            if (newestFirst) card.transform.SetAsFirstSibling();
            else card.transform.SetAsLastSibling();

            cards.Add(card);

            bool visible = Matches(card);
            card.gameObject.SetActive(visible);
            if (visible) StartCoroutine(AppearRoutine(card));

            return card;
        }

        /// <summary>Переключить фильтр по категории. Пустая строка — показать всё.</summary>
        public void ShowCategory(string category)
        {
            current = category;
            ApplyFilter(true);
        }

        void ApplyFilter(bool animate)
        {
            foreach (var card in cards)
            {
                if (card == null) continue;
                bool visible = Matches(card);
                card.gameObject.SetActive(visible);
                if (!visible) continue;

                if (animate) StartCoroutine(UITween.Fade(SetAlpha(card, 0f), 1f, filterFadeDuration));
                else card.Group.alpha = 1f;
            }
        }

        static CanvasGroup SetAlpha(QuizJournalCard card, float alpha)
        {
            var group = card.Group;
            group.alpha = alpha;
            return group;
        }

        bool Matches(QuizJournalCard card) =>
            string.IsNullOrEmpty(current) || card.Category == current;

        IEnumerator AppearRoutine(QuizJournalCard card)
        {
            var rect = card.Rect;
            var group = card.Group;

            // Layout Group ставит карточку на место только после пересчёта —
            // ждём кадр, иначе выезд пойдёт из старой позиции.
            group.alpha = 0f;
            if (typeCardText) UITween.SetHidden(card.Label);
            yield return null;

            if (cardSlideFrom == Vector2.zero)
                yield return UITween.PopIn(rect, group, cardAppearDuration);
            else
                yield return UITween.SlideIn(rect, group, cardSlideFrom, cardAppearDuration);

            if (pulseNewCard) yield return UITween.Pulse(rect);
            if (typeCardText && card.Label != null)
                yield return UITween.Type(card.Label, charactersPerSecond);
        }
    }
}
