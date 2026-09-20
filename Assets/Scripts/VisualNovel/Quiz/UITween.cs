using System.Collections;
using TMPro;
using UnityEngine;

namespace VisualNovel.Quiz
{
    /// <summary>Способ появления панели или объекта задания.</summary>
    public enum UIAppearance
    {
        Pop,
        SlideFromBottom,
        SlideFromTop,
        SlideFromLeft,
        SlideFromRight,
        Fade
    }

    /// <summary>
    /// Небольшой набор UI-анимаций на корутинах: появление с отскоком,
    /// уход, тряска, выезд сбоку. Время — unscaled, чтобы работало и на паузе.
    /// </summary>
    public static class UITween
    {
        /// <summary>Кривая «пружинки» с перелётом за 1 и возвратом.</summary>
        public static readonly AnimationCurve Overshoot = new AnimationCurve(
            new Keyframe(0f, 0f, 2f, 2f),
            new Keyframe(0.6f, 1.08f),
            new Keyframe(1f, 1f, 0f, 0f));

        public static readonly AnimationCurve EaseOut = new AnimationCurve(
            new Keyframe(0f, 0f, 2f, 2f),
            new Keyframe(1f, 1f, 0f, 0f));

        /// <summary>Появление: прозрачность 0→1 и масштаб from→1 с отскоком.</summary>
        public static IEnumerator PopIn(RectTransform rt, CanvasGroup group,
            float duration = 0.25f, float fromScale = 0.8f, AnimationCurve curve = null)
        {
            if (rt == null) yield break;
            curve ??= Overshoot;

            rt.gameObject.SetActive(true);
            Vector3 target = Vector3.one;
            rt.localScale = target * fromScale;
            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = true;
            }

            yield return Run(duration, t =>
            {
                float e = curve.Evaluate(t);
                rt.localScale = Vector3.LerpUnclamped(target * fromScale, target, e);
                if (group != null) group.alpha = Mathf.Clamp01(t * 2f);
            });

            rt.localScale = target;
            if (group != null) group.alpha = 1f;
        }

        /// <summary>Уход: прозрачность 1→0 и лёгкое сжатие. Объект выключается.</summary>
        public static IEnumerator PopOut(RectTransform rt, CanvasGroup group,
            float duration = 0.18f, float toScale = 0.9f, bool deactivate = true)
        {
            if (rt == null) yield break;

            Vector3 from = rt.localScale;
            if (group != null) group.blocksRaycasts = false;

            yield return Run(duration, t =>
            {
                float e = EaseOut.Evaluate(t);
                rt.localScale = Vector3.LerpUnclamped(from, from * toScale, e);
                if (group != null) group.alpha = 1f - e;
            });

            if (group != null) group.alpha = 0f;
            rt.localScale = from;
            if (deactivate) rt.gameObject.SetActive(false);
        }

        /// <summary>Выезд из смещения offset в собственную позицию.</summary>
        public static IEnumerator SlideIn(RectTransform rt, CanvasGroup group, Vector2 offset,
            float duration = 0.3f, AnimationCurve curve = null)
        {
            if (rt == null) yield break;
            curve ??= Overshoot;

            rt.gameObject.SetActive(true);
            Vector2 target = rt.anchoredPosition;
            Vector2 start = target + offset;
            rt.anchoredPosition = start;
            if (group != null) group.alpha = 0f;

            yield return Run(duration, t =>
            {
                float e = curve.Evaluate(t);
                rt.anchoredPosition = Vector2.LerpUnclamped(start, target, e);
                if (group != null) group.alpha = Mathf.Clamp01(t * 2f);
            });

            rt.anchoredPosition = target;
            if (group != null) group.alpha = 1f;
        }

        /// <summary>Тряска вокруг текущей позиции — для неправильного ответа.</summary>
        public static IEnumerator Shake(RectTransform rt, float strength = 14f, float duration = 0.35f)
        {
            if (rt == null) yield break;

            Vector2 origin = rt.anchoredPosition;
            yield return Run(duration, t =>
            {
                float damp = 1f - t;
                float x = Mathf.Sin(t * Mathf.PI * 10f) * strength * damp;
                rt.anchoredPosition = origin + new Vector2(x, 0f);
            });

            rt.anchoredPosition = origin;
        }

        /// <summary>Пульс масштаба — подсветить только что добавленную карточку.</summary>
        public static IEnumerator Pulse(RectTransform rt, float amount = 1.06f, float duration = 0.25f)
        {
            if (rt == null) yield break;

            Vector3 origin = rt.localScale;
            yield return Run(duration, t =>
            {
                float e = Mathf.Sin(t * Mathf.PI);
                rt.localScale = origin * Mathf.LerpUnclamped(1f, amount, e);
            });

            rt.localScale = origin;
        }

        /// <summary>Плавное изменение прозрачности без выключения объекта.</summary>
        public static IEnumerator Fade(CanvasGroup group, float to, float duration = 0.2f)
        {
            if (group == null) yield break;

            float from = group.alpha;
            yield return Run(duration, t => group.alpha = Mathf.LerpUnclamped(from, to, EaseOut.Evaluate(t)));
            group.alpha = to;
        }

        /// <summary>Появление выбранным способом. Объект включается сам.</summary>
        public static IEnumerator Appear(RectTransform rt, CanvasGroup group, UIAppearance appearance,
            float duration = 0.3f, float slideDistance = 180f, float popFromScale = 0.85f)
        {
            if (rt == null) yield break;

            switch (appearance)
            {
                case UIAppearance.Pop:
                    yield return PopIn(rt, group, duration, popFromScale);
                    break;

                case UIAppearance.Fade:
                    rt.gameObject.SetActive(true);
                    if (group != null) group.alpha = 0f;
                    yield return Fade(group, 1f, duration);
                    break;

                default:
                    yield return SlideIn(rt, group, Offset(appearance, slideDistance), duration);
                    break;
            }
        }

        /// <summary>Уход тем же способом, каким объект появлялся.</summary>
        public static IEnumerator Disappear(RectTransform rt, CanvasGroup group, UIAppearance appearance,
            float duration = 0.2f, float slideDistance = 180f, bool deactivate = true)
        {
            if (rt == null) yield break;

            if (appearance == UIAppearance.Pop || appearance == UIAppearance.Fade)
            {
                yield return PopOut(rt, group, duration, appearance == UIAppearance.Fade ? 1f : 0.9f, deactivate);
                yield break;
            }

            Vector2 origin = rt.anchoredPosition;
            Vector2 away = origin + Offset(appearance, slideDistance);
            if (group != null) group.blocksRaycasts = false;

            yield return Run(duration, t =>
            {
                float e = EaseOut.Evaluate(t);
                rt.anchoredPosition = Vector2.LerpUnclamped(origin, away, e);
                if (group != null) group.alpha = 1f - e;
            });

            rt.anchoredPosition = origin;
            if (group != null) group.alpha = 0f;
            if (deactivate) rt.gameObject.SetActive(false);
        }

        /// <summary>Смещение, из которого выезжает панель при выбранном способе.</summary>
        public static Vector2 Offset(UIAppearance appearance, float distance) => appearance switch
        {
            UIAppearance.SlideFromBottom => new Vector2(0f, -distance),
            UIAppearance.SlideFromTop => new Vector2(0f, distance),
            UIAppearance.SlideFromLeft => new Vector2(-distance, 0f),
            UIAppearance.SlideFromRight => new Vector2(distance, 0f),
            _ => Vector2.zero
        };

        /// <summary>Прогон нормализованного времени 0→1 с шагом по unscaled-времени.</summary>
        public static IEnumerator Run(float duration, System.Action<float> step)
        {
            if (duration <= 0f)
            {
                step(1f);
                yield break;
            }

            float time = 0f;
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                step(Mathf.Clamp01(time / duration));
                yield return null;
            }

            step(1f);
        }

        /// <summary>
        /// Печать текста по буквам. Текст уже стоит в поле (или передан в content) —
        /// буквы просто постепенно проявляются, разметка не ломается.
        /// </summary>
        public static IEnumerator Type(TMP_Text text, float charactersPerSecond, string content = null)
        {
            if (text == null) yield break;

            string full = Plain(content ?? text.text);
            text.maxVisibleCharacters = int.MaxValue;

            if (string.IsNullOrEmpty(full) || charactersPerSecond <= 0f)
            {
                text.text = full;
                yield break;
            }

            // Печатаем, дописывая невидимый хвост тегом <alpha=#00>: разметка на месте,
            // блок не скачет, и это не зависит от настроек TMP.
            var stops = VisibleStops(full);
            if (stops.Count == 0 || !text.richText)
            {
                text.text = full;
                yield break;
            }

            text.text = Hidden(full, 0, stops);

            float shown = 0f;
            int last = 0;
            while (last < stops.Count)
            {
                shown += charactersPerSecond * Time.unscaledDeltaTime;
                int visible = Mathf.Clamp(Mathf.FloorToInt(shown), 0, stops.Count);
                if (visible != last)
                {
                    last = visible;
                    text.text = Hidden(full, visible, stops);
                }

                yield return null;
            }

            text.text = full;
        }

        const string HiddenTag = "<alpha=#00>";

        /// <summary>
        /// Спрятать текст целиком, не меняя разметку, — чтобы он не мигнул
        /// перед началом печати. Печать потом снимет этот тег.
        /// </summary>
        public static void SetHidden(TMP_Text text)
        {
            if (text == null || string.IsNullOrEmpty(text.text)) return;

            if (!text.richText)
            {
                text.maxVisibleCharacters = 0;
                return;
            }

            if (!text.text.StartsWith(HiddenTag)) text.text = HiddenTag + text.text;
        }

        /// <summary>Текст без служебных тегов скрытия — где бы они ни стояли.</summary>
        public static string Plain(string text)
        {
            if (string.IsNullOrEmpty(text) || !text.Contains(HiddenTag)) return text;
            return text.Replace(HiddenTag, string.Empty);
        }

        /// <summary>true, если текст сейчас кто-то печатает (в нём стоит тег скрытия).</summary>
        public static bool IsBeingTyped(string text) =>
            !string.IsNullOrEmpty(text) && text.Contains(HiddenTag);

        /// <summary>Строка, в которой видны только первые visible символов.</summary>
        static string Hidden(string full, int visible, System.Collections.Generic.List<int> stops)
        {
            if (visible >= stops.Count) return full;
            int cut = visible <= 0 ? 0 : stops[visible - 1];
            return full.Insert(cut, HiddenTag);
        }

        /// <summary>Позиции в строке после каждого видимого символа: теги вида &lt;b&gt; пропускаем.</summary>
        static System.Collections.Generic.List<int> VisibleStops(string full)
        {
            var stops = new System.Collections.Generic.List<int>(full.Length);
            for (int i = 0; i < full.Length; i++)
            {
                if (full[i] == '<')
                {
                    int close = full.IndexOf('>', i);
                    if (close > i)
                    {
                        i = close;
                        continue;
                    }
                }

                stops.Add(i + 1);
            }

            return stops;
        }

        /// <summary>Пауза в unscaled-времени.</summary>
        public static IEnumerator Wait(float seconds)
        {
            float time = 0f;
            while (time < seconds)
            {
                time += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}
