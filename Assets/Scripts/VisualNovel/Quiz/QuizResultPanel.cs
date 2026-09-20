using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VisualNovel.Quiz
{
    /// <summary>
    /// Панелька результата — вешается на твой префаб (и для ошибки, и для правильного ответа).
    /// Визуал твой, здесь только подстановка данных и анимация появления/ухода.
    /// Любое поле можно оставить пустым.
    /// </summary>
    [AddComponentMenu("Visual Novel/Quiz Result Panel")]
    public class QuizResultPanel : MonoBehaviour
    {
        [Header("Элементы (необязательные)")]
        [Tooltip("Картинка: портрет или иконка результата.")]
        [SerializeField] Image image;

        [Tooltip("Подпись под картинкой, например «П. Столыпин».")]
        [SerializeField] TMP_Text caption;

        [Tooltip("Основной текст панели (подсказка при ошибке).")]
        [SerializeField] TMP_Text message;

        [Tooltip("Кнопка «Принял!» / «Закрыть». Пусто — панель закроется сама.")]
        [SerializeField] Button closeButton;

        [Header("Карточка в журнале")]
        [TextArea(2, 6)]
        [Tooltip("Текст карточки. Пусто — соберётся из текста вопроса и подписи правильной кнопки.")]
        [SerializeField] string journalText;

        [Tooltip("Иконка карточки, например портрет.")]
        [SerializeField] Sprite journalIcon;

        [Header("Анимация")]
        [SerializeField] UIAppearance appearance = UIAppearance.Pop;
        [SerializeField] float showDuration = 0.28f;
        [SerializeField] float hideDuration = 0.18f;
        [Tooltip("Насколько далеко панель выезжает, если выбран Slide.")]
        [SerializeField] float slideDistance = 180f;
        [Tooltip("Масштаб, с которого панель вырастает при Pop.")]
        [SerializeField] float popFromScale = 0.82f;

        [Header("Печать текста")]
        [Tooltip("Печатать текст панели по буквам после появления.")]
        [SerializeField] bool typeText = true;

        [Tooltip("Символов в секунду.")]
        [SerializeField] float charactersPerSecond = 60f;

        [Header("Закрытие")]
        [Tooltip("Закрыться самой через столько секунд. 0 — ждать кнопку.")]
        [SerializeField] float autoCloseDelay;

        [Tooltip("Уничтожать объект после закрытия (для панелей, созданных из префаба).")]
        [SerializeField] bool destroyOnClose = true;

        /// <summary>Текст карточки журнала, заданный на этой панели.</summary>
        public string JournalText => journalText;

        /// <summary>Иконка карточки журнала, заданная на этой панели.</summary>
        public Sprite JournalIcon => journalIcon;

        RectTransform rect;
        CanvasGroup group;
        Action closed;
        bool closing;

        void Awake() => Cache();

        void Cache()
        {
            if (rect == null) rect = (RectTransform)transform;
            if (group == null)
            {
                group = GetComponent<CanvasGroup>();
                if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            }
        }

        /// <summary>Заполнить панель данными. Любой аргумент может быть пустым.</summary>
        public void Fill(Sprite sprite, string captionText, string messageText)
        {
            if (image != null)
            {
                if (sprite != null) image.sprite = sprite;
                image.gameObject.SetActive(image.sprite != null);
            }

            if (caption != null && !string.IsNullOrEmpty(captionText)) caption.text = captionText;
            if (message != null && !string.IsNullOrEmpty(messageText)) message.text = messageText;
        }

        /// <summary>Показать с анимацией. onClosed вызовется после закрытия и ухода.</summary>
        public void Show(Action onClosed = null)
        {
            Cache();
            closed = onClosed;
            closing = false;

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(Close);
                closeButton.onClick.AddListener(Close);
            }

            StartCoroutine(ShowRoutine());
        }

        /// <summary>Закрыть панель (вызывается кнопкой или снаружи).</summary>
        public void Close()
        {
            if (closing) return;
            closing = true;
            StartCoroutine(HideRoutine());
        }

        IEnumerator ShowRoutine()
        {
            // Текст прячем до появления панели, иначе он мигнёт целиком.
            var typed = typeText ? ResolveTypedText() : null;
            UITween.SetHidden(typed);

            yield return UITween.Appear(rect, group, appearance, showDuration, slideDistance, popFromScale);

            if (typed != null) yield return UITween.Type(typed, charactersPerSecond);

            if (autoCloseDelay > 0f)
            {
                yield return UITween.Wait(autoCloseDelay);
                Close();
            }
            else if (closeButton == null)
            {
                // Ни кнопки, ни таймера — чтобы панель не висела навсегда, ждём секунду.
                yield return UITween.Wait(1f);
                Close();
            }
        }

        /// <summary>
        /// Что печатать: поле Message, иначе первый текст панели, не лежащий внутри кнопки
        /// (чтобы не печаталась надпись на «Принял!»).
        /// </summary>
        TMP_Text ResolveTypedText()
        {
            if (message != null) return message;

            foreach (var candidate in GetComponentsInChildren<TMP_Text>(true))
            {
                if (candidate.GetComponentInParent<Button>() != null) continue;
                if (string.IsNullOrEmpty(candidate.text)) continue;
                return candidate;
            }

            return null;
        }

        IEnumerator HideRoutine()
        {
            yield return UITween.Disappear(rect, group, appearance, hideDuration, slideDistance, !destroyOnClose);

            var callback = closed;
            closed = null;
            callback?.Invoke();

            if (destroyOnClose) Destroy(gameObject);
        }
    }
}
