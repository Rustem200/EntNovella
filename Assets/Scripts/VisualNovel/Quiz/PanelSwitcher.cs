using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VisualNovel.Quiz
{
    /// <summary>
    /// Кнопки и объекты: нажатие на кнопку открывает объект с тем же номером,
    /// остальные закрываются. Номера берутся из порядка в списках.
    /// </summary>
    [AddComponentMenu("Visual Novel/Panel Switcher")]
    public class PanelSwitcher : MonoBehaviour
    {
        [SerializeField] List<Button> buttons = new List<Button>();
        [SerializeField] List<GameObject> panels = new List<GameObject>();

        [Tooltip("Что открыто на старте. -1 — всё закрыто.")]
        [SerializeField] int startIndex = -1;

        [Tooltip("Повторное нажатие на ту же кнопку закрывает объект.")]
        [SerializeField] bool closeOnSecondClick;

        [Header("Анимация")]
        [SerializeField] UIAppearance appearance = UIAppearance.Pop;
        [SerializeField] float showDuration = 0.25f;
        [SerializeField] float hideDuration = 0.15f;
        [Tooltip("Дальность выезда, если выбран Slide.")]
        [SerializeField] float slideDistance = 120f;

        Coroutine switching;

        /// <summary>Номер открытого объекта, -1 — все закрыты.</summary>
        public int Current { get; private set; } = -1;

        void Awake()
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                if (buttons[i] == null) continue;
                int index = i;
                buttons[i].onClick.AddListener(() => Toggle(index));
            }
        }

        void Start()
        {
            // Стартовое состояние ставим без анимации.
            for (int i = 0; i < panels.Count; i++)
                if (panels[i] != null) panels[i].SetActive(i == startIndex);

            Current = startIndex;
        }

        /// <summary>Открыть объект по номеру, остальные закрыть.</summary>
        public void Open(int index)
        {
            if (index == Current) return;

            if (switching != null) StopCoroutine(switching);
            switching = StartCoroutine(SwitchRoutine(index));
        }

        /// <summary>Закрыть всё.</summary>
        public void CloseAll() => Open(-1);

        /// <summary>Нажатие по кнопке: открыть или, если уже открыт, закрыть.</summary>
        public void Toggle(int index)
        {
            if (closeOnSecondClick && index == Current) Open(-1);
            else Open(index);
        }

        IEnumerator SwitchRoutine(int index)
        {
            Current = index;

            // Сначала убираем всё лишнее, потом показываем нужный объект.
            for (int i = 0; i < panels.Count; i++)
            {
                var panel = panels[i];
                if (panel == null || i == index || !panel.activeSelf) continue;
                StartCoroutine(UITween.Disappear(panel.transform as RectTransform, GroupOf(panel),
                    appearance, hideDuration, slideDistance));
            }

            if (index >= 0 && index < panels.Count && panels[index] != null)
            {
                var panel = panels[index];
                yield return UITween.Appear(panel.transform as RectTransform, GroupOf(panel),
                    appearance, showDuration, slideDistance);
            }

            switching = null;
        }

        static CanvasGroup GroupOf(GameObject go)
        {
            var group = go.GetComponent<CanvasGroup>();
            if (group == null) group = go.AddComponent<CanvasGroup>();
            return group;
        }
    }
}
