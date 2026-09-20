using System;
using System.Collections.Generic;
using UnityEngine;

namespace VisualNovel
{
    /// <summary>
    /// Справочник персонажей: какие спрайты кому принадлежат и как его зовут.
    /// Директор смотрит, какая картинка стоит в CharacterOne, и подставляет имя,
    /// а заодно выбирает спрайт панели текста.
    /// Создать: ПКМ в Project → Create → Visual Novel → Character Catalog.
    /// </summary>
    [CreateAssetMenu(menuName = "Visual Novel/Character Catalog", fileName = "CharacterCatalog")]
    public class CharacterCatalog : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("Имя, которое покажется в поле имени.")]
            public string displayName;

            [Tooltip("Все картинки этого персонажа: позы, эмоции и т.п.")]
            public List<Sprite> sprites = new List<Sprite>();

            public bool Has(Sprite sprite)
            {
                if (sprite == null) return false;
                foreach (var candidate in sprites)
                    if (candidate == sprite) return true;
                return false;
            }
        }

        [Header("Персонажи")]
        [SerializeField] List<Entry> characters = new List<Entry>();

        [Header("Панель текста")]
        [Tooltip("Спрайт панели, когда персонаж на кадре виден полностью (прозрачность максимальная).")]
        [SerializeField] Sprite panelWithCharacter;

        [Tooltip("Спрайт панели, когда персонажа нет или он не полностью видим.")]
        [SerializeField] Sprite panelWithoutCharacter;

        [Tooltip("Во сколько раз панель выше, когда персонаж виден. 1 — как записано в кадре.")]
        [SerializeField] float heightWithCharacter = 1.3f;

        [Tooltip("Во сколько раз панель выше, когда персонажа нет.")]
        [SerializeField] float heightWithoutCharacter = 1f;

        [Header("Поле имени")]
        [Tooltip("Поднимать имя вместе с ростом панели.")]
        [SerializeField] bool nameFollowsPanel = true;

        [Tooltip("Дополнительный сдвиг имени по высоте, когда персонаж виден, px.")]
        [SerializeField] float nameOffsetWithCharacter;

        [Tooltip("Сдвиг имени по высоте, когда персонажа нет, px.")]
        [SerializeField] float nameOffsetWithoutCharacter;

        public IReadOnlyList<Entry> Characters => characters;

        /// <summary>Имя персонажа по его картинке. Не нашли — пустая строка.</summary>
        public string ResolveName(Sprite sprite)
        {
            if (sprite == null) return string.Empty;

            foreach (var entry in characters)
                if (entry != null && entry.Has(sprite))
                    return entry.displayName;

            return string.Empty;
        }

        /// <summary>Спрайт панели текста: с персонажем или без.</summary>
        public Sprite ResolvePanel(bool characterVisible) =>
            characterVisible ? panelWithCharacter : panelWithoutCharacter;

        /// <summary>
        /// Насколько поднять поле имени от его исходного места:
        /// прирост панели (если включено) плюс свой сдвиг.
        /// </summary>
        public float ResolveNameOffset(bool characterVisible, float panelGrowth)
        {
            float offset = characterVisible ? nameOffsetWithCharacter : nameOffsetWithoutCharacter;
            return (nameFollowsPanel ? panelGrowth : 0f) + offset;
        }

        /// <summary>Множитель высоты панели текста: с персонажем или без.</summary>
        public float ResolvePanelHeight(bool characterVisible)
        {
            float scale = characterVisible ? heightWithCharacter : heightWithoutCharacter;
            return scale > 0f ? scale : 1f;
        }
    }
}
