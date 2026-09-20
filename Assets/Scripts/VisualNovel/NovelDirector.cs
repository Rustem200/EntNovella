using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace VisualNovel
{
    /// <summary>
    /// Ядро визуальной новеллы. Висит на Canvas.
    /// В полях — ссылки на элементы сцены, в списке <see cref="stages"/> — этапы.
    /// Клик (или пробел/Enter) показывает следующий этап.
    /// </summary>
    [AddComponentMenu("Visual Novel/Novel Director")]
    [DisallowMultipleComponent]
    public class NovelDirector : MonoBehaviour
    {
        [Header("Элементы сцены")]
        [SerializeField] Image background;
        [SerializeField] Image textPanel;
        [SerializeField] Image characterOne;
        [SerializeField] Image characterTwo;
        [SerializeField] TMP_Text text;
        [Tooltip("Необязательно: отдельная подпись с именем говорящего.")]
        [SerializeField] TMP_Text speakerLabel;

        [Header("Персонажи (один раз на всю новеллу)")]
        [Tooltip("Справочник персонажей: имена по картинкам и спрайты панели текста.")]
        [SerializeField] CharacterCatalog catalog;

        [Tooltip("Поле, куда пишется имя персонажа из справочника.")]
        [SerializeField] TMP_Text characterNameText;

        [Tooltip("Скрывать поле имени, когда персонаж не опознан.")]
        [SerializeField] bool hideNameWhenUnknown = true;

        [Header("Переход при смене фона")]
        [Tooltip("Затемнять экран, когда на следующем кадре другой задний фон.")]
        [SerializeField] bool fadeOnBackgroundChange = true;

        [Tooltip("Чёрная картинка поверх всего экрана. Пусто — затемняется сам фон.")]
        [SerializeField] Image fadeOverlay;

        [Tooltip("Длительность затемнения в одну сторону, с.")]
        [SerializeField] float fadeDuration = 0.35f;

        [Header("Этапы")]
        [SerializeField] List<NovelStage> stages = new List<NovelStage>();

        [Header("Воспроизведение")]
        [SerializeField] bool playOnStart = true;
        [SerializeField] bool advanceOnClick = true;
        [SerializeField] bool advanceOnKey = true;
        [Tooltip("Печатать реплики побуквенно. Настройка общая на всю новеллу.")]
        [SerializeField] bool typewriter = true;

        [Tooltip("Символов в секунду для побуквенной печати.")]
        [SerializeField] float charactersPerSecond = 40f;
        [Tooltip("Зациклить: после последнего этапа вернуться к первому.")]
        [SerializeField] bool loop;

        [Header("Отладка")]
        [SerializeField] bool logStages;

        // Служебные поля редактора: какой этап сейчас «разложен» на сцене и включена ли авто-запись.
        [SerializeField, HideInInspector] int editorStageIndex = -1;
        [SerializeField, HideInInspector] bool editorAutoRecord = true;

        /// <summary>Сработало при показе этапа: (индекс, этап).</summary>
        public event Action<int, NovelStage> StageShown;

        /// <summary>Сработало после последнего этапа (если loop выключен).</summary>
        public event Action Finished;

        int currentIndex = -1;
        Coroutine typing;
        string typingFull;
        Vector2 baseNamePosition;
        Quiz.TypewriterText writer;
        Coroutine transition;
        bool isTyping;
        float autoAdvanceTimer;
        bool finished;

        public IReadOnlyList<NovelStage> Stages => stages;
        public int CurrentIndex => currentIndex;
        public bool IsTyping => isTyping;
        public NovelStage CurrentStage =>
            currentIndex >= 0 && currentIndex < stages.Count ? stages[currentIndex] : null;

        void Awake()
        {
            // Исходное место имени: от него считаем подъём вместе с панелью.
            if (characterNameText != null)
                baseNamePosition = characterNameText.rectTransform.anchoredPosition;

            if (text != null) writer = text.GetComponent<Quiz.TypewriterText>();
        }

        void Start()
        {
            if (playOnStart) Play();
        }

        /// <summary>
        /// Пока true — клики и клавиши не переключают кадры и не работает автопереход.
        /// Этим пользуется система заданий, пока висит вопрос.
        /// </summary>
        public bool InputBlocked { get; set; }

        void Update()
        {
            if (finished || currentIndex < 0 || InputBlocked) return;

            if (WasAdvancePressed())
            {
                // Недопечатанная реплика не держит: клик всегда листает кадр.
                Next();
                return;
            }

            var stage = CurrentStage;
            if (stage != null && stage.autoAdvanceDelay > 0f && !isTyping)
            {
                autoAdvanceTimer += Time.deltaTime;
                if (autoAdvanceTimer >= stage.autoAdvanceDelay) Next();
            }
        }

        /// <summary>Начать с первого этапа.</summary>
        public void Play()
        {
            finished = false;
            if (stages.Count == 0)
            {
                Debug.LogWarning($"[{nameof(NovelDirector)}] Список этапов пуст.", this);
                return;
            }

            ShowStage(0);
        }

        /// <summary>Следующий этап. Если он последний — событие Finished (или цикл).</summary>
        public void Next()
        {
            int next = currentIndex + 1;
            if (next >= stages.Count)
            {
                if (loop && stages.Count > 0)
                {
                    ShowStage(0);
                    return;
                }

                finished = true;
                Finished?.Invoke();
                return;
            }

            ShowStage(next);
        }

        /// <summary>Предыдущий этап (для отладки/меню «назад»).</summary>
        public void Previous()
        {
            if (currentIndex > 0) ShowStage(currentIndex - 1);
        }

        /// <summary>Показать этап по индексу.</summary>
        public void ShowStage(int index)
        {
            if (index < 0 || index >= stages.Count)
            {
                Debug.LogWarning($"[{nameof(NovelDirector)}] Нет этапа с индексом {index}.", this);
                return;
            }

            currentIndex = index;
            finished = false;
            autoAdvanceTimer = 0f;

            var stage = stages[index];

            // Другой задний фон — показываем кадр через затемнение.
            if (NeedsFade(stage))
            {
                if (transition != null) StopCoroutine(transition);
                transition = StartCoroutine(ShowWithFade(index, stage));
                return;
            }

            ApplyAndAnnounce(index, stage);
        }

        void ApplyAndAnnounce(int index, NovelStage stage)
        {
            bool animate = Application.isPlaying && typewriter && charactersPerSecond > 0f;
            ApplyStage(stage, !animate);

            if (logStages)
                Debug.Log($"[{nameof(NovelDirector)}] Этап {index}: {stage.label}", this);

            StageShown?.Invoke(index, stage);
        }

        bool NeedsFade(NovelStage stage) =>
            fadeOnBackgroundChange
            && Application.isPlaying
            && fadeDuration > 0f
            && background != null
            && stage.captured
            && stage.background.sprite != background.sprite;

        IEnumerator ShowWithFade(int index, NovelStage stage)
        {
            yield return FadeToBlack();
            ApplyAndAnnounce(index, stage);
            yield return FadeFromBlack();
            transition = null;
        }

        IEnumerator FadeToBlack()
        {
            if (fadeOverlay != null)
            {
                fadeOverlay.gameObject.SetActive(true);
                var color = fadeOverlay.color;
                float from = color.a;
                yield return Quiz.UITween.Run(fadeDuration * (1f - from), t =>
                    fadeOverlay.color = new Color(color.r, color.g, color.b, Mathf.Lerp(from, 1f, t)));
                yield break;
            }

            // Оверлея нет — гасим сам фон до чёрного, прозрачность не трогаем.
            var start = background.color;
            var dark = new Color(0f, 0f, 0f, start.a);
            yield return Quiz.UITween.Run(fadeDuration, t => background.color = Color.Lerp(start, dark, t));
        }

        IEnumerator FadeFromBlack()
        {
            if (fadeOverlay != null)
            {
                var color = fadeOverlay.color;
                float from = color.a;
                yield return Quiz.UITween.Run(fadeDuration * from, t =>
                    fadeOverlay.color = new Color(color.r, color.g, color.b, Mathf.Lerp(from, 0f, t)));
                fadeOverlay.gameObject.SetActive(false);
                yield break;
            }

            // Кадр уже наложен, значит в background.color лежит нужный цвет — из чёрного в него.
            var target = background.color;
            var dark = new Color(0f, 0f, 0f, target.a);
            background.color = dark;
            yield return Quiz.UITween.Run(fadeDuration, t => background.color = Color.Lerp(dark, target, t));
            background.color = target;
        }

        /// <summary>
        /// Наложить данные этапа на элементы сцены.
        /// instant = false — текст печатается побуквенно (только в Play Mode).
        /// </summary>
        public void ApplyStage(NovelStage stage, bool instant = true)
        {
            if (stage == null) return;

            // Незаполненный этап накладывать нельзя: он обнулил бы спрайты и размеры.
            if (!stage.captured)
            {
                Debug.LogWarning(
                    $"[{nameof(NovelDirector)}] Этап «{stage.label}» ещё не заполнен со сцены — пропускаю.",
                    this);
                return;
            }

            stage.background.ApplyTo(background);
            stage.textPanel.ApplyTo(textPanel);
            stage.characterOne.ApplyTo(characterOne);
            stage.characterTwo.ApplyTo(characterTwo);
            stage.text.ApplyTo(text);

            if (speakerLabel != null)
            {
                speakerLabel.text = stage.text.speaker ?? string.Empty;
                speakerLabel.gameObject.SetActive(!string.IsNullOrEmpty(stage.text.speaker));
            }
            else if (text != null)
            {
                text.text = ComposeText(stage);
            }

            ApplyCatalog();

            // Порядок строгий: сначала обрываем печать прошлой реплики,
            // потом ставим в поле полный текст этого кадра, и только потом печатаем его.
            StopTyping();
            if (text == null) return;

            string full = Quiz.UITween.Plain(text.text);
            text.text = full;

            // Если на тексте висит TypewriterText, печатает он — иначе два механизма
            // писали бы в одно поле и затирали друг друга.
            if (writer != null && writer.isActiveAndEnabled)
            {
                if (instant) writer.Show(full);
                else writer.Play(full);
                return;
            }

            if (!instant) typing = StartCoroutine(TypeRoutine(full));
        }

        /// <summary>
        /// Имя персонажа по картинке в CharacterOne и спрайт панели текста:
        /// один — когда персонаж виден полностью, другой — когда нет.
        /// </summary>
        void ApplyCatalog()
        {
            if (catalog == null) return;

            bool visible = characterOne != null
                           && characterOne.gameObject.activeSelf
                           && characterOne.sprite != null
                           && characterOne.color.a >= 0.999f;

            if (characterNameText != null)
            {
                string name = visible ? catalog.ResolveName(characterOne.sprite) : string.Empty;
                characterNameText.text = name;
                if (hideNameWhenUnknown)
                    characterNameText.gameObject.SetActive(!string.IsNullOrEmpty(name));
            }

            if (textPanel == null) return;

            var sprite = catalog.ResolvePanel(visible);
            if (sprite != null) textPanel.sprite = sprite;

            // Высоту множим только в игре: в редакторе кадр хранит свой размер,
            // иначе авто-запись записала бы увеличенный и он бы рос с каждым показом.
            if (!Application.isPlaying) return;

            var panelRect = textPanel.rectTransform;
            float baseHeight = panelRect.rect.height;
            float grown = baseHeight * catalog.ResolvePanelHeight(visible);
            float delta = grown - baseHeight;

            // Текст-реплика лежит внутри панели: запоминаем его размер и место,
            // чтобы растянулась только картинка панели.
            RectTransform textRect = text != null ? text.rectTransform : null;
            Vector3 textWorld = Vector3.zero;
            Vector2 textSize = Vector2.zero;
            if (textRect != null)
            {
                textWorld = textRect.position;
                textSize = textRect.rect.size;
            }

            panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, panelRect.sizeDelta.y + delta);

            if (textRect != null)
            {
                textRect.sizeDelta += textSize - textRect.rect.size;
                textRect.position = textWorld;
            }

            // Высота имени: прирост панели плюс сдвиг, заданный в справочнике.
            if (characterNameText != null)
                characterNameText.rectTransform.anchoredPosition =
                    baseNamePosition + new Vector2(0f, catalog.ResolveNameOffset(visible, delta));
        }

        /// <summary>Скопировать текущее состояние элементов сцены в этап.</summary>
        public void CaptureInto(NovelStage stage)
        {
            if (stage == null) return;

            stage.background.CopyFrom(background);
            stage.textPanel.CopyFrom(textPanel);
            stage.characterOne.CopyFrom(characterOne);
            stage.characterTwo.CopyFrom(characterTwo);

            string speaker = stage.text.speaker;
            stage.text.CopyFrom(text);
            if (speakerLabel != null)
            {
                stage.text.speaker = speakerLabel.text;
            }
            else
            {
                stage.text.speaker = speaker;
                stage.text.content = StripSpeaker(stage, stage.text.content);
            }

            stage.captured = true;
        }

        /// <summary>
        /// Заполнить со сцены все этапы, которые ещё ни разу не заполнялись
        /// (например, добавленные кнопкой «+» самого списка). Возвращает их число.
        /// </summary>
        public int CaptureNewStages()
        {
            int filled = 0;
            for (int i = 0; i < stages.Count; i++)
            {
                if (stages[i] == null || stages[i].captured) continue;
                CaptureInto(stages[i]);
                if (string.IsNullOrEmpty(stages[i].label) || stages[i].label == "Новый этап")
                    stages[i].label = $"Этап {i + 1}";
                filled++;
            }

            return filled;
        }

        /// <summary>true, если элементы сцены в точности соответствуют этапу.</summary>
        public bool MatchesScene(NovelStage stage)
        {
            if (stage == null) return true;
            return stage.background.Matches(background)
                   && stage.textPanel.Matches(textPanel)
                   && stage.characterOne.Matches(characterOne)
                   && stage.characterTwo.Matches(characterTwo)
                   && stage.text.MatchesStyle(text)
                   && (text == null || ComposeText(stage) == text.text)
                   && (speakerLabel == null || (stage.text.speaker ?? string.Empty) == speakerLabel.text);
        }

        /// <summary>Текст, который реально попадает в TMP: с именем говорящего,
        /// если для имени нет отдельной подписи.</summary>
        string ComposeText(NovelStage stage)
        {
            string content = stage.text.content ?? string.Empty;
            if (speakerLabel != null || string.IsNullOrEmpty(stage.text.speaker)) return content;
            return $"{stage.text.speaker}\n{content}";
        }

        string StripSpeaker(NovelStage stage, string sceneText)
        {
            if (string.IsNullOrEmpty(stage.text.speaker) || sceneText == null) return sceneText;
            string prefix = stage.text.speaker + "\n";
            return sceneText.StartsWith(prefix) ? sceneText.Substring(prefix.Length) : sceneText;
        }

        /// <summary>Новый этап со снимком текущей сцены. Возвращает его индекс.</summary>
        public int AddStageFromScene(string label = null)
        {
            var stage = new NovelStage { label = label ?? $"Этап {stages.Count + 1}" };
            CaptureInto(stage);
            stages.Add(stage);
            return stages.Count - 1;
        }

        /// <summary>
        /// Все объекты сцены, которые меняет <see cref="ApplyStage"/>.
        /// Нужно редактору, чтобы зарегистрировать их в Undo.
        /// </summary>
        public UnityEngine.Object[] CollectSceneTargets()
        {
            var list = new List<UnityEngine.Object>();
            Collect(background);
            Collect(textPanel);
            Collect(characterOne);
            Collect(characterTwo);
            Collect(text);
            Collect(speakerLabel);
            return list.ToArray();

            void Collect(Component c)
            {
                if (c == null) return;
                list.Add(c);
                list.Add(c.gameObject);
                var rt = c.transform as RectTransform;
                if (rt != null) list.Add(rt);
            }
        }

        /// <summary>Список этапов для правки из инспектора.</summary>
        public List<NovelStage> StagesEditable => stages;

        /// <summary>Индекс этапа, который сейчас «разложен» на сцене в редакторе.</summary>
        public int EditorStageIndex
        {
            get => editorStageIndex;
            set => editorStageIndex = value;
        }

        /// <summary>Писать ли правки сцены в выбранный этап автоматически.</summary>
        public bool EditorAutoRecord
        {
            get => editorAutoRecord;
            set => editorAutoRecord = value;
        }

        /// <summary>Дописать текущую реплику целиком, не дожидаясь печати.</summary>
        public void FinishTyping()
        {
            if (!isTyping) return;

            string full = typingFull;
            StopTyping();
            if (text != null && full != null) text.text = full;
        }

        /// <summary>
        /// Просто оборвать печать, ничего не подставляя в поле:
        /// текст следующего кадра уже стоит там и затирать его нельзя.
        /// Дописать реплику целиком умеет FinishTyping.
        /// </summary>
        void StopTyping()
        {
            if (typing != null)
            {
                StopCoroutine(typing);
                typing = null;
            }

            isTyping = false;
            typingFull = null;
        }

        IEnumerator TypeRoutine(string full)
        {
            isTyping = true;
            typingFull = Quiz.UITween.Plain(full);

            yield return Quiz.UITween.Type(text, charactersPerSecond, typingFull);

            isTyping = false;
            typing = null;
            typingFull = null;
        }

        bool WasAdvancePressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (advanceOnClick)
            {
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return true;
                var touch = Touchscreen.current;
                if (touch != null && touch.primaryTouch.press.wasPressedThisFrame) return true;
            }

            if (advanceOnKey && Keyboard.current != null)
            {
                if (Keyboard.current.spaceKey.wasPressedThisFrame) return true;
                if (Keyboard.current.enterKey.wasPressedThisFrame) return true;
                if (Keyboard.current.numpadEnterKey.wasPressedThisFrame) return true;
            }

            return false;
#else
            if (advanceOnClick && Input.GetMouseButtonDown(0)) return true;
            if (advanceOnKey && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
                return true;
            return false;
#endif
        }
    }
}
