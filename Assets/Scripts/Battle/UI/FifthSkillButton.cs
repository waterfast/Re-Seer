using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

using ReSeer.Pets;

/// <summary>继承普通技能按钮的数据及技能点击事件发送，加上第五技能的名称美术和中心动效。</summary>
public class FifthSkillButton : SkillButton
{
    [Serializable]
    public sealed class NameArtwork
    {
        [Tooltip("优先用稳定技能 ID 匹配；留空时用数据库中的原始中文名称匹配。")]
        public string skillId;
        public string originalName;
        public Sprite sprite;
    }

    [Header("名称显示")]
    [Tooltip("留空使用 SkillData 的名称；可输入任意名称，支持换行。不会修改技能数据库。")]
    [SerializeField, TextArea(1, 3)] private string customName = "";
    [Tooltip("只在没有自定义名称时使用原版名称图片。默认使用可编辑文字。")]
    [SerializeField] private bool useOriginalNameArtwork;

    [Header("可选的原版名称图片")]
    [SerializeField] private SpriteRenderer nameArtworkRenderer;
    [SerializeField] private List<NameArtwork> nameArtworks = new List<NameArtwork>();

    [Header("橙色中心光圈")]
    [SerializeField] private SpriteRenderer centerEffectRenderer;
    [SerializeField] private Sprite[] centerEffectFrames = Array.Empty<Sprite>();
    [SerializeField, Min(1f)] private float framesPerSecond = 24f;
    [SerializeField] private bool animateCenter = true;

    [Header("橙色中心按钮三态")]
    [SerializeField] private Sprite centerNormal;
    [SerializeField] private Sprite centerHover;
    [SerializeField] private Sprite centerPressed;

    private int effectFrame;
    private float frameTime;
    private bool pointerInside;
    private bool pointerPressed;

    public Sprite CurrentNameArtwork => nameArtworkRenderer != null ? nameArtworkRenderer.sprite : null;
    public string DisplayName => string.IsNullOrEmpty(customName) ? SkillName : customName;

    public void SetCustomName(string value)
    {
        customName = value ?? "";
        RefreshView();
    }

    protected override string FormatPP(int current, int maximum) => $"{current}/{maximum}";
    protected override string FormatPower(int value) => value > 0 ? value.ToString() : "--";

    protected override void OnEnable()
    {
        base.OnEnable();
        effectFrame = 0;
        frameTime = 0f;
        RefreshCenterEffect();
    }

    protected override void OnValidate()
    {
        framesPerSecond = Mathf.Max(1f, framesPerSecond);
        base.OnValidate();
        RefreshCenterEffect();
    }

    public override void RefreshView()
    {
        base.RefreshView();
        Sprite artwork = useOriginalNameArtwork && string.IsNullOrEmpty(customName) ? FindNameArtwork() : null;
        if (nameArtworkRenderer != null)
        {
            nameArtworkRenderer.sprite = artwork;
            nameArtworkRenderer.enabled = artwork != null;
            nameArtworkRenderer.color = Available ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
        }
        // Unknown skills must fall back to their current name rather than retain the previous artwork.
        if (NameText != null)
        {
            NameText.text = DisplayName;
            NameText.gameObject.SetActive(artwork == null);
        }
        RefreshCenterSurface();
    }

    private Sprite FindNameArtwork()
    {
        if (nameArtworks == null) return null;
        foreach (var entry in nameArtworks)
            if (BoundSkill != null && entry != null && entry.sprite != null && !string.IsNullOrWhiteSpace(entry.skillId) &&
                entry.skillId == BoundSkill.id) return entry.sprite;
        foreach (var entry in nameArtworks)
            if (entry != null && entry.sprite != null && string.IsNullOrWhiteSpace(entry.skillId) &&
                !string.IsNullOrWhiteSpace(entry.originalName) && entry.originalName == (BoundSkill?.fallbackName ?? SkillName))
                return entry.sprite;
        return null;
    }

    protected override void OnDisable()
    {
        pointerInside = false;
        pointerPressed = false;
        base.OnDisable();
        RefreshCenterSurface();
    }

    public override void OnPointerEnter(PointerEventData e)
    {
        pointerInside = true;
        base.OnPointerEnter(e);
    }

    public override void OnPointerExit(PointerEventData e)
    {
        pointerInside = false;
        pointerPressed = false;
        base.OnPointerExit(e);
    }

    public override void OnPointerDown(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left || !CanUse) return;
        pointerPressed = true;
        base.OnPointerDown(e);
        RefreshCenterSurface();
    }

    public override void OnPointerUp(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;
        pointerPressed = false;
        base.OnPointerUp(e);
    }

    private void RefreshCenterSurface()
    {
        if (BackgroundRenderer == null || centerNormal == null) return;
        BackgroundRenderer.sprite = Available && pointerPressed && centerPressed != null ? centerPressed :
            Available && pointerInside && centerHover != null ? centerHover : centerNormal;
    }

    private void Update()
    {
        if (!Application.isPlaying || !animateCenter || centerEffectFrames == null ||
            centerEffectFrames.Length < 2 || centerEffectRenderer == null) return;
        frameTime += Time.unscaledDeltaTime * framesPerSecond;
        int advance = Mathf.FloorToInt(frameTime);
        if (advance == 0) return;
        frameTime -= advance;
        effectFrame = (effectFrame + advance) % centerEffectFrames.Length;
        RefreshCenterEffect();
    }

    private void RefreshCenterEffect()
    {
        if (centerEffectRenderer == null) return;
        bool hasFrames = centerEffectFrames != null && centerEffectFrames.Length > 0;
        centerEffectRenderer.enabled = hasFrames;
        if (hasFrames)
            centerEffectRenderer.sprite = centerEffectFrames[Mathf.Clamp(effectFrame, 0, centerEffectFrames.Length - 1)];
        else centerEffectRenderer.sprite = null;
    }
}
