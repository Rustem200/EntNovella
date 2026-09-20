using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace VisualNovel.Quiz
{
    /// <summary>
    /// Печать текста по буквам. Вешается на любой объект с TMP-текстом:
    /// текст, который уже стоит в поле, при включении проявляется побуквенно.
    /// </summary>
    [AddComponentMenu("Visual Novel/Typewriter Text")]
    public class TypewriterText : MonoBehaviour
    {
        [Tooltip("Текст. Пусто — возьмётся с этого же объекта.")]
        [SerializeField] TMP_Text target;

        [Tooltip("Символов в секунду.")]
        [SerializeField] float charactersPerSecond = 60f;

        [Tooltip("Пауза перед началом печати, с.")]
        [SerializeField] float startDelay;

        [Tooltip("Печатать сразу при включении объекта.")]
        [SerializeField] bool playOnEnable = true;

        [Tooltip("Печатать заново каждый раз, когда текст меняют извне — " +
                 "например, когда новелла показывает следующую реплику.")]
        [SerializeField] bool playOnTextChange = true;

        public UnityEvent Finished = new UnityEvent();

        Coroutine running;
        string fullText;

        /// <summary>Идёт печать.</summary>
        public bool IsTyping => running != null;

        void Reset() => target = GetComponent<TMP_Text>();

        void Awake()
        {
            // Текст может быть и на дочернем объекте — ищем там тоже.
            if (target == null) target = GetComponent<TMP_Text>();
            if (target == null) target = GetComponentInChildren<TMP_Text>(true);
            if (target == null)
                Debug.LogWarning($"[{nameof(TypewriterText)}] На «{name}» не найден TMP-текст.", this);
        }

        void OnEnable()
        {
            if (playOnEnable) Play();
        }

        void OnDisable() => running = null;

        void Update()
        {
            // Реплику ставит другой скрипт (режиссёр новеллы) — ловим это и печатаем заново.
            if (!playOnTextChange || target == null) return;

            // Текст печатает кто-то другой, а мы стоим — не мешаем.
            if (!IsTyping && UITween.IsBeingTyped(target.text)) return;

            // Пока печатаем сами, в поле наш же текст с тегом; если он стал другим —
            // значит реплику сменили снаружи, и печатать надо её, а не старую.
            string current = UITween.Plain(target.text);
            if (string.IsNullOrEmpty(current) || current == fullText) return;

            Play();
        }

        /// <summary>Оборвать печать и показать текст целиком.</summary>
        public void Show(string content)
        {
            if (target == null) return;

            if (running != null) StopCoroutine(running);
            running = null;
            fullText = UITween.Plain(content ?? target.text);
            target.text = fullText;
        }

        /// <summary>Напечатать текст, который уже стоит в поле.</summary>
        public void Play() => Play(null);

        /// <summary>Подставить текст и напечатать его.</summary>
        public void Play(string content)
        {
            if (target == null) return;

            if (running != null) StopCoroutine(running);
            running = StartCoroutine(Routine(content));
        }

        /// <summary>Показать текст целиком, не дожидаясь конца печати.</summary>
        public void Skip()
        {
            if (running != null) StopCoroutine(running);
            running = null;
            if (target != null && fullText != null) target.text = fullText;
            Finished.Invoke();
        }

        IEnumerator Routine(string content)
        {
            if (content != null) target.text = content;
            fullText = UITween.Plain(target.text);

            // Прячем текст сразу, чтобы он не мигнул целиком перед задержкой.
            UITween.SetHidden(target);
            if (startDelay > 0f) yield return UITween.Wait(startDelay);

            yield return UITween.Type(target, charactersPerSecond, fullText);

            running = null;
            Finished.Invoke();
        }
    }
}
