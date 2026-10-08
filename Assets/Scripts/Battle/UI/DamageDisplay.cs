using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ReSeer.Battle.UI
{
    public enum DamageNumberStyle { Normal, Critical, FixedDamage, TrueDamage, Healing }
    public enum NumberSign { Auto, None, Minus, Plus }

    /// <summary>共用的数字显示器，只拼字与动画，不计算伤害和治疗。</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class DamageDisplay : MonoBehaviour
    {
        [Serializable]
        private sealed class GlyphSet
        {
            public Sprite[] digits;
            public Sprite minus;
            public Sprite plus;
            public Sprite Get(char c) => c == '-' ? minus : c == '+' ? plus : digits[c - '0'];
        }

        [Header("数字素材（每组按 0–9 排列）")]
        [SerializeField] private GlyphSet normal;
        [SerializeField] private GlyphSet critical;
        [SerializeField] private GlyphSet fixedDamage;
        [SerializeField] private GlyphSet trueDamage;
        [SerializeField] private GlyphSet healing;
        [SerializeField] private Transform content;
        [SerializeField] private Sprite criticalFlame;
        [Header("显示")]
        [SerializeField, Min(0f)] private float spacing = 0.01f;
        [SerializeField, Min(1f)] private float criticalScale = 1.25f;
        [SerializeField] private string sortingLayer = "L3";
        [SerializeField] private int sortingOrder = 50;
        [Header("上浮动画")]
        [SerializeField, Min(0.01f)] private float duration = 0.85f;
        [SerializeField, Min(0f)] private float riseDistance = 0.65f;
        [SerializeField, Range(0f, 1f)] private float fadeStart = 0.65f;
        [SerializeField, Min(0.01f)] private float popDuration = 0.12f;
        [Header("编辑器预览")]
        [SerializeField] private int previewDamage = 12345;
        [SerializeField] private DamageNumberStyle previewStyle;
        [SerializeField] private NumberSign previewSign;

        // 按实际位数创建数字槽并复用，不把预览子对象保存进 prefab 或场景。
        private readonly List<SpriteRenderer> slots = new List<SpriteRenderer>();
        private SpriteRenderer flame;
        private bool animating;
        private float elapsed;
        private float displayScale = 1f;

        private void OnEnable()
        {
            // 域重载或关闭域重载的播放切换都可能保留原生预览对象，但丢失列表。
            if (content != null && slots.Count == 0)
            {
                foreach (SpriteRenderer orphan in content.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if ((orphan.gameObject.hideFlags & HideFlags.DontSave) == 0) continue;
                    orphan.gameObject.SetActive(false);
                    DestroyGenerated(orphan);
                }
                flame = null;
            }
            if (Application.IsPlaying(gameObject)) Hide();
            else if (content != null && normal != null && normal.digits != null && normal.digits.Length == 10)
                Preview();
        }
        private void OnDisable()
        {
            Hide();
            if (Application.IsPlaying(gameObject)) return;
            // 域重载不会序列化 slots；先释放预览对象，避免重载后重复生成。
            foreach (SpriteRenderer slot in slots) DestroyGenerated(slot);
            slots.Clear();
            DestroyGenerated(flame);
            flame = null;
        }
        private void OnDestroy()
        {
            foreach (SpriteRenderer slot in slots) DestroyGenerated(slot);
            DestroyGenerated(flame);
        }

        // 兼容原来的普通/暴击调用方式。
        public void Show(int amount, bool isCritical = false) =>
            Show((long)amount, isCritical ? DamageNumberStyle.Critical : DamageNumberStyle.Normal);
        public void SetValue(int amount, bool isCritical = false) =>
            SetValue((long)amount, isCritical ? DamageNumberStyle.Critical : DamageNumberStyle.Normal);

        /// <summary>Auto 为伤害减号、回血加号；可显式指定 +、- 或无符号。</summary>
        public void Show(long amount, DamageNumberStyle style, NumberSign sign = NumberSign.Auto)
        {
            SetValue(amount, style, sign);
            elapsed = 0f;
            animating = Application.IsPlaying(gameObject);
            if (animating) ApplyAnimation(0f);
        }

        public void SetValue(long amount, DamageNumberStyle style, NumberSign sign = NumberSign.Auto)
        {
            GlyphSet glyphs = GetGlyphs(style);
            if (content == null || glyphs == null || glyphs.digits == null || glyphs.digits.Length != 10)
                throw new InvalidOperationException("DamageDisplay 缺少 Content 或对应类型的数字素材。");
            // 从字符串取绝对值，避免 long.MinValue 取负数溢出。
            string text = amount.ToString(CultureInfo.InvariantCulture).TrimStart('-');
            if (sign == NumberSign.Auto)
                sign = amount < 0 || style != DamageNumberStyle.Healing ? NumberSign.Minus : NumberSign.Plus;
            if (sign == NumberSign.Minus) text = "-" + text;
            else if (sign == NumberSign.Plus) text = "+" + text;
            else if (sign != NumberSign.None) throw new ArgumentOutOfRangeException(nameof(sign));

            float width = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                Sprite sprite = glyphs.Get(text[i]);
                if (sprite == null) throw new InvalidOperationException($"DamageDisplay 缺少 {style} 的 {text[i]} 素材。");
                if (i == slots.Count) slots.Add(CreateRenderer($"Glyph{i:00}"));
                slots[i].sprite = sprite;
                slots[i].sortingLayerName = sortingLayer;
                slots[i].sortingOrder = sortingOrder;
                width += sprite.bounds.size.x;
            }
            float gap = Mathf.Max(0f, spacing);
            width += gap * (text.Length - 1);
            float cursor = -width * 0.5f;
            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].gameObject.SetActive(i < text.Length);
                if (i >= text.Length) continue;
                float digitWidth = slots[i].sprite.bounds.size.x;
                slots[i].transform.localPosition = new Vector3(cursor + digitWidth * 0.5f, 0f, 0f);
                cursor += digitWidth + gap;
            }
            bool isCritical = style == DamageNumberStyle.Critical;
            if (isCritical && criticalFlame != null)
            {
                if (flame == null) flame = CreateRenderer("CriticalFlame");
                flame.sprite = criticalFlame;
                flame.sortingLayerName = sortingLayer;
                flame.sortingOrder = sortingOrder - 1;
                flame.transform.localPosition = new Vector3(0f, 0.32f, 0f);
                flame.transform.localScale = Vector3.one * 0.65f;
            }
            if (flame != null) flame.gameObject.SetActive(isCritical && criticalFlame != null);
            animating = false;
            displayScale = isCritical ? Mathf.Max(1f, criticalScale) : 1f;
            content.localPosition = Vector3.zero;
            content.localScale = Vector3.one * displayScale;
            SetAlpha(1f);
            content.gameObject.SetActive(true);
        }

        private GlyphSet GetGlyphs(DamageNumberStyle style)
        {
            switch (style)
            {
                case DamageNumberStyle.Normal: return normal;
                case DamageNumberStyle.Critical: return critical;
                case DamageNumberStyle.FixedDamage: return fixedDamage;
                case DamageNumberStyle.TrueDamage: return trueDamage;
                case DamageNumberStyle.Healing: return healing;
                default: throw new ArgumentOutOfRangeException(nameof(style));
            }
        }
        private SpriteRenderer CreateRenderer(string name)
        {
            var child = new GameObject(name);
            child.hideFlags = HideFlags.DontSave;
            child.layer = gameObject.layer;
            child.transform.SetParent(content, false);
            return child.AddComponent<SpriteRenderer>();
        }
        private static void DestroyGenerated(SpriteRenderer renderer)
        {
            if (renderer == null) return;
            if (Application.IsPlaying(renderer.gameObject)) Destroy(renderer.gameObject);
            else DestroyImmediate(renderer.gameObject);
        }
        public void Hide()
        {
            animating = false;
            if (content == null) return;
            content.localPosition = Vector3.zero;
            content.localScale = Vector3.one;
            content.gameObject.SetActive(false);
        }
        [ContextMenu("预览数字")]
        public void Preview() => SetValue((long)previewDamage, previewStyle, previewSign);
        [ContextMenu("播放预览数字（运行模式）")]
        public void PlayPreview() => Show((long)previewDamage, previewStyle, previewSign);
        private void Update()
        {
            if (!animating) return;
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            if (progress >= 1f) Hide();
            else ApplyAnimation(progress);
        }
        private void ApplyAnimation(float progress)
        {
            content.localPosition = Vector3.up * (riseDistance * progress);
            float pop = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, popDuration));
            content.localScale = Vector3.one * (displayScale * Mathf.Lerp(0.8f, 1f, Mathf.Sin(pop * Mathf.PI * 0.5f)));
            float fade = Mathf.InverseLerp(Mathf.Min(fadeStart, 0.99f), 1f, progress);
            SetAlpha(1f - fade);
        }
        private void SetAlpha(float alpha)
        {
            foreach (SpriteRenderer slot in slots) slot.color = new Color(1f, 1f, 1f, alpha);
            if (flame != null) flame.color = new Color(1f, 1f, 1f, alpha);
        }
    }
}
