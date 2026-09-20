using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VisualNovel.Quiz
{
    /// <summary>
    /// Вешается на Content журнала. Раскладывает карточки сверху вниз
    /// с одинаковым отступом и подгоняет высоту Content, чтобы ScrollRect
    /// правильно скроллился. Размеры самих карточек не меняются. Layout Group не нужен.
    /// </summary>
    [AddComponentMenu("Visual Novel/Journal Card Layout")]
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class JournalCardLayout : MonoBehaviour
    {
        [Tooltip("Расстояние между карточками, px.")]
        [SerializeField] float spacing = 8f;

        [Tooltip("Отступы от краёв Content: слева, справа, сверху, снизу.")]
        [SerializeField] RectOffset padding = new RectOffset(8, 8, 8, 8);

        [Tooltip("Подгонять высоту Content под карточки.")]
        [SerializeField] bool fitContentHeight = true;

        [Header("Анимация")]
        [Tooltip("Сдвигать карточки к новым местам плавно, а не мгновенно.")]
        [SerializeField] bool animateMove = true;

        [SerializeField] float moveDuration = 0.2f;

        RectTransform self;
        Coroutine moving;
        int lastSignature;
        bool rebuilding;

        RectTransform Self => self != null ? self : self = (RectTransform)transform;

        void OnEnable() => Rebuild();

        void OnTransformChildrenChanged() => Rebuild();

        void OnRectTransformDimensionsChange() => Rebuild();

        void LateUpdate()
        {
            // Карточки включаются и выключаются фильтрами, текст печатается —
            // перестраиваем, только когда состав или размеры реально поменялись.
            int signature = Signature();
            if (signature == lastSignature) return;
            lastSignature = signature;
            Rebuild();
        }

        /// <summary>Создать карточку из префаба в этом Content и разложить заново.</summary>
        public T Spawn<T>(T prefab) where T : Component
        {
            if (prefab == null) return null;

            var instance = Instantiate(prefab, Self);
            instance.gameObject.SetActive(true);
            Rebuild();
            return instance;
        }

        /// <summary>Разложить карточки сверху вниз.</summary>
        public void Rebuild()
        {
            // Подгонка высоты Content сама дёргает OnRectTransformDimensionsChange — не зацикливаемся.
            if (!isActiveAndEnabled || rebuilding) return;
            rebuilding = true;
            try
            {
                RebuildInternal();
            }
            finally
            {
                rebuilding = false;
            }
        }

        void RebuildInternal()
        {
            var targets = new List<KeyValuePair<RectTransform, Vector2>>();
            float y = padding.top;

            for (int i = 0; i < Self.childCount; i++)
            {
                var child = Self.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeSelf) continue;

                // Размер карточки не трогаем: запоминаем его, меняем якоря и возвращаем как было.
                Vector2 size = child.rect.size;
                child.anchorMin = new Vector2(0.5f, 1f);
                child.anchorMax = new Vector2(0.5f, 1f);
                child.pivot = new Vector2(0.5f, 1f);
                child.sizeDelta = size;

                float x = (padding.left - padding.right) * 0.5f;
                targets.Add(new KeyValuePair<RectTransform, Vector2>(child, new Vector2(x, -y)));

                y += size.y + spacing;
            }

            if (targets.Count > 0) y -= spacing;
            y += padding.bottom;

            if (fitContentHeight) Self.sizeDelta = new Vector2(Self.sizeDelta.x, y);

            bool smooth = animateMove && Application.isPlaying && moveDuration > 0f;
            if (!smooth)
            {
                foreach (var pair in targets) pair.Key.anchoredPosition = pair.Value;
                return;
            }

            if (moving != null) StopCoroutine(moving);
            moving = StartCoroutine(MoveRoutine(targets));
        }

        IEnumerator MoveRoutine(List<KeyValuePair<RectTransform, Vector2>> targets)
        {
            var from = new List<Vector2>(targets.Count);
            foreach (var pair in targets) from.Add(pair.Key.anchoredPosition);

            yield return UITween.Run(moveDuration, t =>
            {
                float e = UITween.EaseOut.Evaluate(t);
                for (int i = 0; i < targets.Count; i++)
                {
                    var rt = targets[i].Key;
                    if (rt == null) continue;
                    rt.anchoredPosition = Vector2.LerpUnclamped(from[i], targets[i].Value, e);
                }
            });

            moving = null;
        }

        /// <summary>Слепок состава и размеров — чтобы зря не пересчитывать каждый кадр.</summary>
        int Signature()
        {
            int hash = 17;
            for (int i = 0; i < Self.childCount; i++)
            {
                var child = Self.GetChild(i) as RectTransform;
                if (child == null) continue;

                hash = hash * 31 + child.GetInstanceID();
                hash = hash * 31 + (child.gameObject.activeSelf ? 1 : 0);
                hash = hash * 31 + Mathf.RoundToInt(child.rect.height * 4f);
            }

            return hash;
        }
    }
}
