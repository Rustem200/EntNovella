using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VisualNovel
{
    /// <summary>
    /// Снимок RectTransform: позиция, размер, масштаб, поворот.
    /// Якоря и пивот не трогаются — они остаются такими, как настроены в объекте.
    /// </summary>
    [Serializable]
    public class RectState
    {
        public bool active = true;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Vector3 localScale = Vector3.one;
        public Vector3 localEulerAngles = Vector3.zero;

        public void CopyFrom(RectTransform rt)
        {
            if (rt == null) return;
            active = rt.gameObject.activeSelf;
            anchoredPosition = rt.anchoredPosition;
            sizeDelta = rt.sizeDelta;
            localScale = rt.localScale;
            localEulerAngles = rt.localEulerAngles;
        }

        public void ApplyTo(RectTransform rt)
        {
            if (rt == null) return;
            if (rt.gameObject.activeSelf != active) rt.gameObject.SetActive(active);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = sizeDelta;
            rt.localScale = localScale;
            rt.localEulerAngles = localEulerAngles;
        }

        public RectState Clone() => (RectState)MemberwiseClone();

        public bool Matches(RectTransform rt)
        {
            if (rt == null) return true;
            return active == rt.gameObject.activeSelf
                   && NovelMath.Same(anchoredPosition, rt.anchoredPosition)
                   && NovelMath.Same(sizeDelta, rt.sizeDelta)
                   && NovelMath.Same(localScale, rt.localScale)
                   && NovelMath.Same(localEulerAngles, rt.localEulerAngles);
        }
    }

    /// <summary>Картинка этапа: спрайт + цвет + геометрия.</summary>
    [Serializable]
    public class ImageState
    {
        public Sprite sprite;
        public Color color = Color.white;
        public bool preserveAspect;
        public RectState rect = new RectState();

        public void CopyFrom(Image image)
        {
            if (image == null) return;
            sprite = image.sprite;
            color = image.color;
            preserveAspect = image.preserveAspect;
            rect.CopyFrom(image.rectTransform);
        }

        public void ApplyTo(Image image)
        {
            if (image == null) return;
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = preserveAspect;
            rect.ApplyTo(image.rectTransform);
        }

        public ImageState Clone()
        {
            var copy = (ImageState)MemberwiseClone();
            copy.rect = rect.Clone();
            return copy;
        }

        public bool Matches(Image image)
        {
            if (image == null) return true;
            return sprite == image.sprite
                   && NovelMath.Same(color, image.color)
                   && preserveAspect == image.preserveAspect
                   && rect.Matches(image.rectTransform);
        }
    }

    /// <summary>Текст этапа: реплика, имя говорящего (опционально), стиль и геометрия.</summary>
    [Serializable]
    public class TextState
    {
        public string speaker;
        [TextArea(3, 8)] public string content;
        public Color color = Color.white;
        public float fontSize = 36f;
        public TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft;
        public RectState rect = new RectState();

        public void CopyFrom(TMP_Text text)
        {
            if (text == null) return;
            content = text.text;
            color = text.color;
            fontSize = text.fontSize;
            alignment = text.alignment;
            rect.CopyFrom(text.rectTransform);
        }

        public void ApplyTo(TMP_Text text)
        {
            if (text == null) return;
            text.text = content;
            text.color = color;
            text.fontSize = fontSize;
            text.alignment = alignment;
            rect.ApplyTo(text.rectTransform);
        }

        public TextState Clone()
        {
            var copy = (TextState)MemberwiseClone();
            copy.rect = rect.Clone();
            return copy;
        }

        /// <summary>Совпадает ли всё, кроме самой реплики (её сравнивает режиссёр —
        /// на сцене может стоять текст с приклеенным именем говорящего).</summary>
        public bool MatchesStyle(TMP_Text text)
        {
            if (text == null) return true;
            return NovelMath.Same(color, text.color)
                   && Mathf.Abs(fontSize - text.fontSize) < 0.001f
                   && alignment == text.alignment
                   && rect.Matches(text.rectTransform);
        }
    }

    /// <summary>
    /// Один этап новеллы: что показано на экране после очередного клика.
    /// </summary>
    [Serializable]
    public class NovelStage
    {
        [Tooltip("Подпись этапа — только для удобства в инспекторе.")]
        public string label = "Новый этап";

        public ImageState background = new ImageState();
        public ImageState textPanel = new ImageState();
        public ImageState characterOne = new ImageState();
        public ImageState characterTwo = new ImageState();
        public TextState text = new TextState();

        [Header("Переход")]
        [Tooltip("Секунд до автоперехода. 0 — ждать клик.")]
        public float autoAdvanceDelay;

        /// <summary>
        /// false — этап только что создан и ещё ни разу не заполнялся со сцены.
        /// Такой этап редактор сразу заполняет текущими настройками объектов,
        /// а наложить его на сцену нельзя (иначе он обнулил бы элементы).
        /// </summary>
        [HideInInspector] public bool captured;

        /// <summary>Полная независимая копия этапа (для кнопки «Дублировать»).</summary>
        public NovelStage Clone() => new NovelStage
        {
            label = label,
            captured = captured,
            background = background.Clone(),
            textPanel = textPanel.Clone(),
            characterOne = characterOne.Clone(),
            characterTwo = characterTwo.Clone(),
            text = text.Clone(),
            autoAdvanceDelay = autoAdvanceDelay
        };
    }

    internal static class NovelMath
    {
        const float Eps = 0.0001f;

        public static bool Same(Vector2 a, Vector2 b) =>
            Mathf.Abs(a.x - b.x) < Eps && Mathf.Abs(a.y - b.y) < Eps;

        public static bool Same(Vector3 a, Vector3 b) =>
            Mathf.Abs(a.x - b.x) < Eps && Mathf.Abs(a.y - b.y) < Eps && Mathf.Abs(a.z - b.z) < Eps;

        public static bool Same(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < Eps && Mathf.Abs(a.g - b.g) < Eps &&
            Mathf.Abs(a.b - b.b) < Eps && Mathf.Abs(a.a - b.a) < Eps;
    }
}
