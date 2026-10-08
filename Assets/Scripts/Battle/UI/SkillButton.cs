using System;
using System.Collections;
using ReSeer.Skills;
using ReSeer.Battle.Art;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace ReSeer.Pets
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public class SkillButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        [Header("组件（拖入对应的文字和图片）")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text ppText;
        [SerializeField] private TMP_Text powerText;
        [SerializeField] private SpriteRenderer elementIcon;
        [SerializeField] private SpriteRenderer background;
        [SerializeField] private SpriteRenderer glow;

        [Header("技能显示")]
        [SerializeField] private string skillName = "";
        [SerializeField, Min(0)] private int power;
        [SerializeField, Min(0)] private int maxPP;
        [SerializeField, Min(0)] private int currentPP;
        [SerializeField] private bool interactable = true;

        [Header("技能数据库预览（当前 PP 不写入数据库）")]
        [SerializeField] private SkillDatabaseSO skillDatabase;
        [SerializeField] private string previewSkillId;
        [SerializeField, Min(0)] private int previewCurrentPP;
        [Tooltip("独立按钮预览时使用；若上层已经 Bind，则不覆盖上层数据。")]
        [SerializeField] private bool loadDatabasePreviewOnStart;

        private SkillData boundSkill;
        public SkillData BoundSkill => boundSkill;
        public string SkillId => boundSkill?.id;
        public event Action<SkillData> SkillSelected;
        protected TMP_Text NameText => nameText;
        protected SpriteRenderer BackgroundRenderer => background;
        protected bool Available => interactable && currentPP >= ppCost;

        [Header("点击行为")]
        [Tooltip("普通技能和第五技能共用此通道，发送 Bind 绑定的实际 SkillData。")]
        [SerializeField] private SkillClickEventSO skillClickEvent;
        [SerializeField, Min(1)] private int ppCost = 1;
        [Tooltip("点击可用按钮时提交操作意图；可用状态由外部同步。")]
        [SerializeField] private UnityEvent onClick = new UnityEvent();

        [Header("发光反馈")]
        [SerializeField] private Color glowColor = new Color(0.5f, 1f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float hoverGlow = 0.18f;
        [SerializeField, Range(0f, 1f)] private float clickGlow = 0.65f;
        [SerializeField, Min(0.01f)] private float flashDuration = 0.22f;

        private bool hovered;
        private Coroutine flash;
        [SerializeField, HideInInspector] private Color normalBackground;
        [SerializeField, HideInInspector] private Color normalIcon;
        [SerializeField, HideInInspector] private bool colorsCached;
#if UNITY_EDITOR
        private bool validationRefreshQueued;
#endif

        public string SkillName => skillName;
        public int Power => power;
        public int CurrentPP => currentPP;
        public int MaxPP => maxPP;
        public bool CanUse => interactable && currentPP >= ppCost && isActiveAndEnabled;
        public UnityEvent OnClick => onClick;

        protected virtual void Awake() => CacheColors();
        protected virtual void OnEnable() => RefreshView();
        protected virtual void Start()
        {
            if (loadDatabasePreviewOnStart && boundSkill == null) LoadDatabasePreview();
        }

        protected virtual void OnValidate()
        {
            maxPP = Mathf.Max(0, maxPP);
            currentPP = Mathf.Clamp(currentPP, 0, maxPP);
            power = Mathf.Max(0, power);
            ppCost = Mathf.Max(1, ppCost);
            flashDuration = Mathf.Max(0.01f, flashDuration);
#if UNITY_EDITOR
            // TMP activation can rebuild meshes, which Unity forbids inside OnValidate.
            if (!validationRefreshQueued)
            {
                validationRefreshQueued = true;
                UnityEditor.EditorApplication.delayCall += RefreshAfterValidation;
            }
#else
            RefreshView();
#endif
        }

#if UNITY_EDITOR
        private void RefreshAfterValidation()
        {
            validationRefreshQueued = false;
            if (this != null) RefreshView();
        }
#endif

        protected virtual void OnDisable()
        {
            if (flash != null) StopCoroutine(flash);
            flash = null;
            hovered = false;
            SetGlow(0f);
        }

        // Public setters always refresh the labels; callers never need to assemble "x/y".
        public void SetSkillName(string value) { skillName = value ?? ""; RefreshView(); }
        public void SetPower(int value) { power = Mathf.Max(0, value); RefreshView(); }
        public void SetPP(int current, int maximum)
        {
            maxPP = Mathf.Max(0, maximum);
            currentPP = Mathf.Clamp(current, 0, maxPP);
            RefreshView();
        }
        public void SetCurrentPP(int value) => SetPP(value, maxPP);
        public void RestorePP() => SetCurrentPP(maxPP);
        public void SetInteractable(bool value) { interactable = value; RefreshView(); }
        public void SetElementIcon(Sprite value)
        {
            if (elementIcon != null) elementIcon.sprite = value;
        }

        /// <summary>技能资料只读使用；剩余 PP 由调用者传入，不改动数据库。</summary>
        public void Bind(SkillData skill, int remainingPP, Func<string, string> translate = null)
        {
            if (skill == null) throw new ArgumentNullException(nameof(skill));
            boundSkill = skill;
            skillName = skill.DisplayName(translate);
            power = Mathf.Max(0, skill.power);
            maxPP = Mathf.Max(0, skill.maxPp);
            currentPP = Mathf.Clamp(remainingPP, 0, maxPP);
            ppCost = Mathf.Max(1, skill.ppCost);
            // 和血条共用属性清单；未收录的自定义属性仍可使用技能指定的图片。
            SetElementIcon(TypeIconResources.Get(new[] { skill.elementId }) ?? skill.icon);
            RefreshView();
        }

        [ContextMenu("从技能数据库载入预览")]
        public void LoadDatabasePreview()
        {
            if (skillDatabase == null || !skillDatabase.TryGet(previewSkillId, out var skill))
            {
                Debug.LogWarning("请指定技能数据库和有效的 Preview Skill Id。", this);
                return;
            }
            Bind(skill, previewCurrentPP);
        }
        public void SetSkill(string displayName, int skillPower, int current, int maximum, Sprite icon)
        {
            // 此接口仅配置显示，不能继续报告上一次 Bind 的技能。
            boundSkill = null;
            skillName = displayName ?? "";
            power = Mathf.Max(0, skillPower);
            SetElementIcon(icon);
            SetPP(current, maximum);
        }

        public virtual void RefreshView()
        {
            if (nameText != null) nameText.text = skillName;
            if (ppText != null)
            {
                ppText.text = FormatPP(currentPP, maxPP);
                ppText.color = currentPP == 0 ? new Color(1f, 0.5f, 0.45f) : Color.white;
            }
            if (powerText != null) powerText.text = FormatPower(power);
            CacheColors();
            bool available = interactable && currentPP >= ppCost;
            if (background != null) background.color = available ? normalBackground : normalBackground * new Color(0.5f, 0.5f, 0.5f, 1f);
            if (elementIcon != null) elementIcon.color = available ? normalIcon : normalIcon * new Color(0.5f, 0.5f, 0.5f, 1f);
            if (flash == null) SetGlow(available && hovered ? hoverGlow : 0f);
        }

        protected virtual string FormatPP(int current, int maximum) => $"PP：{current}/{maximum}";
        protected virtual string FormatPower(int value) => value > 0 ? $"威力：{value}" : "威力：--";

        public void PlayClickEffect()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || glow == null) return;
            if (flash != null) StopCoroutine(flash);
            flash = StartCoroutine(Flash());
        }

        private IEnumerator Flash()
        {
            float elapsed = 0f;
            while (elapsed < flashDuration)
            {
                float end = CanUse && hovered ? hoverGlow : 0f;
                SetGlow(Mathf.Lerp(clickGlow, end, elapsed / flashDuration));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            flash = null;
            SetGlow(CanUse && hovered ? hoverGlow : 0f);
        }

        private void CacheColors()
        {
            if (colorsCached || background == null) return;
            normalBackground = background.color;
            normalIcon = elementIcon != null ? elementIcon.color : Color.white;
            colorsCached = true;
        }

        private void SetGlow(float alpha)
        {
            if (glow == null) return;
            var color = glowColor;
            color.a = alpha;
            glow.color = color;
        }

        // Sprite buttons use EventSystem + Physics2DRaycaster + BoxCollider2D.
        public virtual void OnPointerEnter(PointerEventData e) { hovered = true; RefreshView(); }
        public virtual void OnPointerExit(PointerEventData e) { hovered = false; RefreshView(); }
        public virtual void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || !CanUse) return;
            SetGlow(clickGlow);
        }
        public virtual void OnPointerUp(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            RefreshView();
        }
        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || !CanUse) return;
            // 先记录点击时的技能，避免响应回调切换面板或重新 Bind 后传错资料。
            SkillData clickedSkill = boundSkill;
            PlayClickEffect();
            if (clickedSkill != null) skillClickEvent?.RaiseEvent(clickedSkill, this);
            else if (skillClickEvent != null)
                Debug.LogWarning("技能按钮尚未 Bind 技能资料，本次不广播技能点击事件。", this);
            onClick.Invoke();
            if (clickedSkill != null) SkillSelected?.Invoke(clickedSkill);
        }
    }
}
