using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VisualNovel.Quiz
{
    /// <summary>
    /// Карточка журнала — вешается на твой префаб карточки (иконка + текст).
    /// </summary>
    [AddComponentMenu("Visual Novel/Quiz Journal Card")]
    public class QuizJournalCard : MonoBehaviour
    {
        [SerializeField] Image icon;
        [SerializeField] TMP_Text label;

        /// <summary>Текст карточки — нужен журналу для печати по буквам.</summary>
        public TMP_Text Label => label;

        /// <summary>Категория карточки — по ней работают кнопки-фильтры журнала.</summary>
        public string Category { get; private set; }

        public RectTransform Rect => (RectTransform)transform;

        public CanvasGroup Group
        {
            get
            {
                if (group == null)
                {
                    group = GetComponent<CanvasGroup>();
                    if (group == null) group = gameObject.AddComponent<CanvasGroup>();
                }

                return group;
            }
        }

        CanvasGroup group;

        public void Fill(string text, Sprite sprite, string category)
        {
            Category = category;
            if (label != null) label.text = text;
            if (icon != null)
            {
                if (sprite != null) icon.sprite = sprite;
                icon.gameObject.SetActive(icon.sprite != null);
            }
        }
    }
}
