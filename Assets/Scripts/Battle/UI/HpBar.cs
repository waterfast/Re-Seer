using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using ReSeer.Battle.Art;

namespace ReSeer.Battle.UI
{
    /// <summary>显示头像槽数据，处理体力动画和左右布局；不参与战斗结算。</summary>
    [DisallowMultipleComponent]
    public sealed class HpBar : MonoBehaviour
    {
        [Header("当前组件")]
        [SerializeField] private SpriteRenderer mainbar;
        [FormerlySerializedAs("avatar")]
        [SerializeField] private SpriteRenderer avatar;
        [SerializeField] private TMP_Text petLevel;
        [SerializeField] private TMP_Text petName;
        [SerializeField] private SpriteRenderer hpFill;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private SpriteRenderer element;
        [SerializeField, HideInInspector] private string[] elementIds;

        [Header("体力")]
        [SerializeField, Min(0)] private int currentHp = 200;
        [SerializeField, Min(1)] private int maxHp = 300;
        [Tooltip("对方血条可勾选，让右端固定。不会镜像名字和等级。")]
        [SerializeField] private bool fillFromRight;
        [SerializeField, Min(0f)] private float changeDuration = 0.25f;

        [Header("镜像")]
        [Tooltip("围绕框体中心切换左右布局；Lv、等级、名字和体力数字保持可读。")]
        [SerializeField] private bool mirrored;
        // 与已保存的场景布局一起序列化，重复刷新不能再次把位置反转。
        [SerializeField, HideInInspector] private bool appliedMirror;
        [SerializeField, HideInInspector] private float mirrorAxis;

        // 保存满血布局，避免重复绑定或编辑器重载后，把缩短后的宽度当成满血宽度。
        [SerializeField, HideInInspector] private SpriteRenderer layoutFill;
        [SerializeField, HideInInspector] private Vector3 fullFillScale;
        [SerializeField, HideInInspector] private Vector3 fullFillPosition;
        private float displayedRatio = 1f;
        private Coroutine healthAnimation;
        [SerializeField, HideInInspector] private string petId;

        public bool HasBoundPet { get; private set; }
        public int CurrentHp => currentHp;
        public int MaxHp => maxHp;
        public bool Mirrored => mirrored;
        public string PetId => petId;

        private void Reset() => ResolveComponents();
        private void Awake()
        {
            ResolveComponents();
            SetHealth(currentHp, Mathf.Max(1, maxHp), false);
            HasBoundPet = !string.IsNullOrEmpty(petId);
        }

        private void OnDisable()
        {
            PetArtResources.ResourcesChanged -= RefreshAvatar;
            StopHealthAnimation();
            ApplyFillRatio(maxHp > 0 ? (float)currentHp / maxHp : 0f);
        }

        private void OnEnable()
        {
            PetArtResources.ResourcesChanged += RefreshAvatar;
            RefreshAvatar();
        }

        /// <summary>常用入口：只传精灵资料，头像由资源模块按精灵编号查找。</summary>
        public void BindPet(BattlePetState pet)
        {
            if (pet == null) throw new ArgumentNullException(nameof(pet));
            BindPet(pet.Id, pet.Name, pet.Level, pet.CurrentHp, pet.MaxHp);
            // 正式战斗资料中的属性优先，支持属性改变和双属性。
            SetElements(pet.Elements);
        }

        public void BindPet(string id, string displayName, int level, int health, int maximumHealth)
        {
            Sprite portrait = PetArtResources.GetAvatar(id);
            Bind(displayName, level, portrait, health, maximumHealth);
            petId = id;
            string typeId = TypeIconResources.GetPetTypeId(id);
            SetElements(typeId == null ? Array.Empty<string>() : new[] { typeId });
            if (portrait == null) Debug.LogWarning($"没有找到精灵头像 avatar/{id}.png。", this);
        }

        /// <summary>没有出战精灵时清空内容，保留框体和布局。</summary>
        public void ClearPet()
        {
            petId = null;
            ResolveComponents();
            if (avatar != null) avatar.sprite = null;
            SetElements(Array.Empty<string>());
            if (petName != null) petName.text = string.Empty;
            if (petLevel != null) petLevel.text = string.Empty;
            SetHealth(0, 1, false);
            if (hpText != null) hpText.text = string.Empty;
            HasBoundPet = false;
        }

        private void RefreshAvatar()
        {
            if (string.IsNullOrEmpty(petId)) return;
            ResolveComponents();
            if (avatar != null) avatar.sprite = PetArtResources.GetAvatar(petId);
            if (elementIds == null)
            {
                string typeId = TypeIconResources.GetPetTypeId(petId);
                elementIds = typeId == null ? Array.Empty<string>() : new[] { typeId };
            }
            if (element != null) element.sprite = TypeIconResources.Get(elementIds);
        }

        /// <summary>仅更新属性图片，沿用你在 Prefab 中调整的位置、缩放与朝向。</summary>
        public void SetElements(IReadOnlyList<string> values)
        {
            ResolveComponents();
            elementIds = new string[values == null ? 0 : values.Count];
            for (int i = 0; i < elementIds.Length; i++) elementIds[i] = values[i];
            if (element != null) element.sprite = TypeIconResources.Get(elementIds);
        }

        private void OnValidate()
        {
            maxHp = Mathf.Max(1, maxHp);
            currentHp = Mathf.Clamp(currentHp, 0, maxHp);
            changeDuration = Mathf.Max(0f, changeDuration);
#if UNITY_EDITOR
            // Inspector 回调可能发生在导入线程，变换更新延后到编辑器主线程。
            UnityEditor.EditorApplication.delayCall -= RefreshEditorLayout;
            UnityEditor.EditorApplication.delayCall += RefreshEditorLayout;
#endif
        }

        /// <summary>补齐未指定的引用；保留 Inspector 中已经绑定的组件。</summary>
        [ContextMenu("按当前子对象名称绑定组件")]
        public void ResolveComponents()
        {
            if (mainbar == null) mainbar = FindComponent<SpriteRenderer>("mainbar");
            if (avatar == null) avatar = FindComponent<SpriteRenderer>("avatar")
                ?? FindComponent<SpriteRenderer>("avator");
            if (petLevel == null) petLevel = FindComponent<TMP_Text>("pet_level");
            if (petName == null) petName = FindComponent<TMP_Text>("pet_name");
            if (hpFill == null) hpFill = FindComponent<SpriteRenderer>("hp")
                ?? FindComponent<SpriteRenderer>("hp_fill")
                ?? FindComponent<SpriteRenderer>("HpFillRoot");
            if (hpText == null) hpText = FindComponent<TMP_Text>("hp_text");
            if (element == null) element = FindComponent<SpriteRenderer>("element");
            CacheFillLayout();
            ApplyMirrorLayout();
        }

        /// <summary>由上层传入图片和显示数据；不改变布局、字体或战斗数值。</summary>
        public void Bind(string displayName, int level, Sprite portrait, Sprite frame = null)
            => Bind(displayName, level, portrait, currentHp, maxHp, frame);

        public void Bind(string displayName, int level, Sprite portrait,
            int health, int maximumHealth, Sprite frame = null)
        {
            // 在修改显示之前验证数据，避免只更新了一部分组件就中断。
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("精灵名字不能为空。", nameof(displayName));
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
            if (maximumHealth <= 0) throw new ArgumentOutOfRangeException(nameof(maximumHealth));

            ResolveComponents();
            // 绑定数据
            petId = null;
            if (avatar != null) avatar.sprite = portrait;
            SetElements(Array.Empty<string>());
            if (petLevel != null) petLevel.text = level.ToString();
            if (petName != null) petName.text = displayName;
            // 不传框体时沿用编辑器中已装配的样式。
            if (mainbar != null && frame != null) mainbar.sprite = frame;
            // 切换精灵直接显示新精灵的体力，不从上一只的血条缓慢过渡。
            SetHealth(health, maximumHealth, false);
            HasBoundPet = true;
        }

        /// <summary>接收上层已经计算好的 HP；动画不参与扣血规则。</summary>
        public void SetHealth(int value, int maximum, bool animate = true)
        {
            if (maximum <= 0) throw new ArgumentOutOfRangeException(nameof(maximum));
            ResolveComponents();
            currentHp = Mathf.Clamp(value, 0, maximum);
            maxHp = maximum;
            if (hpText != null) hpText.text = $"{currentHp}/{maxHp}";
            StopHealthAnimation();
            float ratio = (float)currentHp / maxHp;
            if (animate && Application.isPlaying && isActiveAndEnabled && changeDuration > 0f && hpFill != null)
                healthAnimation = StartCoroutine(AnimateHealth(ratio));
            else
                ApplyFillRatio(ratio);
        }

        private void CacheFillLayout()
        {
            if (hpFill == null) return;
            var fill = hpFill.transform;
            if (layoutFill != hpFill)
            {
                layoutFill = hpFill;
                fullFillScale = fill.localScale;
                fullFillPosition = fill.localPosition;
            }
            // X 使用满血基准；Y/Z 始终沿用你当前摆好的高度、厚度和层级位置。
            // 不能从半血时的 X 缩放重新捕获满血宽度。
            fullFillScale.y = fill.localScale.y;
            fullFillScale.z = fill.localScale.z;
            fullFillPosition.y = fill.localPosition.y;
            fullFillPosition.z = fill.localPosition.z;
        }

        private void ApplyFillRatio(float ratio)
        {
            displayedRatio = Mathf.Clamp01(ratio);
            if (hpFill == null || hpFill.sprite == null) return;
            CacheFillLayout();
            Transform fill = hpFill.transform;
            fill.localScale = new Vector3(fullFillScale.x * displayedRatio, fullFillScale.y, fullFillScale.z);

            // Sprite 的默认轴心在中心。同步移动轴心，才能让指定的一端固定。
            // 使用 sprite.bounds 也能兼容非中心轴心和负缩放。
            bool fromRight = fillFromRight ^ mirrored;
            float edge = fromRight ? hpFill.sprite.bounds.max.x : hpFill.sprite.bounds.min.x;
            if (hpFill.flipX) edge = fromRight ? -hpFill.sprite.bounds.min.x : -hpFill.sprite.bounds.max.x;
            Vector3 offset = fill.localRotation * new Vector3(edge * fullFillScale.x * (1f - displayedRatio), 0f, 0f);
            fill.localPosition = new Vector3(fullFillPosition.x + offset.x,
                fullFillPosition.y, fullFillPosition.z);
        }

        private IEnumerator AnimateHealth(float targetRatio)
        {
            float startRatio = displayedRatio;
            float elapsed = 0f;
            while (elapsed < changeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                ApplyFillRatio(Mathf.Lerp(startRatio, targetRatio, Mathf.Clamp01(elapsed / changeDuration)));
                yield return null;
            }
            ApplyFillRatio(targetRatio);
            healthAnimation = null;
        }

        private void StopHealthAnimation()
        {
            if (healthAnimation == null) return;
            StopCoroutine(healthAnimation);
            healthAnimation = null;
        }

        /// <summary>切换布局方向，不改变精灵资料或根对象的缩放。</summary>
        public void SetMirrored(bool value)
        {
            mirrored = value;
            ResolveComponents();
            ApplyFillRatio(displayedRatio);
        }

        private void ApplyMirrorLayout()
        {
            if (mirrored == appliedMirror) return;
            // 框体位置不变，其余组围绕框体中心换边。取消镜像使用同一轴线还原。
            if (!appliedMirror)
                mirrorAxis = mainbar != null ? mainbar.transform.localPosition.x : 0f;
            foreach (Transform child in transform)
            {
                var position = child.localPosition;
                position.x = 2f * mirrorAxis - position.x;
                child.localPosition = position;
                // 只翻转各组自身的图片，不翻转子层：pet_level/lv-icon 和文字保持正常。
                var image = child.GetComponent<SpriteRenderer>();
                if (image != null) image.flipX = !image.flipX;
            }
            if (layoutFill != null)
                fullFillPosition.x = 2f * mirrorAxis - fullFillPosition.x;
            appliedMirror = mirrored;
        }

#if UNITY_EDITOR
        private void RefreshEditorLayout()
        {
            if (this == null) return;
            ResolveComponents();
            SetHealth(currentHp, maxHp, false);
        }
#endif

        private T FindComponent<T>(string childName) where T : Component
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (!string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase)) continue;
                // 支持 avatar 直接挂图片，以及 pet_level/level、pet_name/information 的结构。
                // TMP_Text 只匹配文字组件，不会绑定自动生成的 TMP SubMesh。
                return child.GetComponentInChildren<T>(true);
            }
            return null;
        }
    }
}
