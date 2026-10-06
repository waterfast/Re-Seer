using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace ReSeer.Battle.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer), typeof(PolygonCollider2D))]
    public sealed class ActionMenuButton : MonoBehaviour, IPointerEnterHandler,
        IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        [Header("按钮三态")]
        [SerializeField] private string actionId;
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite hoverSprite;
        [SerializeField] private Sprite pressedSprite;
        [SerializeField] private bool interactable = true;

        [Header("发光反馈")]
        [SerializeField] private Material glowMaterial;
        [SerializeField] private Color glowColor = new Color(0.25f, 0.85f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float hoverGlow = 0.12f;
        [SerializeField, Range(0f, 1f)] private float clickGlow = 0.55f;
        [SerializeField, Min(0.01f)] private float flashDuration = 0.24f;

        [Header("点击行为（由外部接入战斗操作）")]
        [SerializeField] private BattleMenuAction menuAction;
        [SerializeField] private BattleMenuActionEventSO menuClickEvent;
        [SerializeField] private UnityEvent onClick = new UnityEvent();

        private SpriteRenderer visual;
        private SpriteRenderer glow;
        private Color normalColor;
        private bool hovered;
        private bool pressed;
        private Coroutine flash;

        public string ActionId => actionId;
        public bool CanClick => interactable && isActiveAndEnabled;
        public UnityEvent OnClick => onClick;
        public event Action<string> Clicked;

        private void Awake()
        {
            visual = GetComponent<SpriteRenderer>();
            normalColor = visual.color;
            if (normalSprite == null) normalSprite = visual.sprite;
            // 发光层不带碰撞器，始终由按钮本体接收点击。
            var glowObject = new GameObject("Click Glow");
            glowObject.layer = gameObject.layer;
            glowObject.transform.SetParent(transform, false);
            glow = glowObject.AddComponent<SpriteRenderer>();
            glow.sharedMaterial = glowMaterial != null ? glowMaterial : visual.sharedMaterial;
            RefreshView();
        }

        private void OnEnable()
        {
            if (visual != null) RefreshView();
        }

        private void OnDisable()
        {
            StopFlash();
            hovered = false;
            pressed = false;
            if (visual != null) RefreshView();
            SetGlow(0f, 1f);
        }

        public void SetInteractable(bool value)
        {
            interactable = value;
            if (!value)
            {
                pressed = false;
                StopFlash();
            }
            if (visual != null) RefreshView();
        }

        private void RefreshView()
        {
            visual.sprite = CanClick && hovered
                ? (pressed ? pressedSprite : hoverSprite) : normalSprite;
            if (visual.sprite == null) visual.sprite = normalSprite;
            visual.color = interactable ? normalColor
                : normalColor * new Color(0.5f, 0.5f, 0.5f, 1f);
            glow.sprite = visual.sprite;
            glow.sortingLayerID = visual.sortingLayerID;
            glow.sortingOrder = visual.sortingOrder + 1;
            glow.flipX = visual.flipX;
            glow.flipY = visual.flipY;
            if (flash == null) SetGlow(CanClick && hovered && !pressed ? hoverGlow : 0f, 1.025f);
        }

        private void SetGlow(float alpha, float scale)
        {
            if (glow == null) return;
            Color color = glowColor;
            color.a = alpha * normalColor.a;
            glow.color = color;
            glow.enabled = alpha > 0f;
            glow.transform.localScale = Vector3.one * scale;
        }

        public void PlayClickEffect()
        {
            if (!Application.isPlaying || !CanClick) return;
            StopFlash();
            flash = StartCoroutine(Flash());
        }

        private IEnumerator Flash()
        {
            float elapsed = 0f;
            // 使用非缩放时间，战斗暂停时点击反馈也能正常结束。
            while (elapsed < flashDuration)
            {
                float t = Mathf.Clamp01(elapsed / flashDuration);
                float restingGlow = CanClick && hovered ? hoverGlow : 0f;
                SetGlow(Mathf.Lerp(clickGlow, restingGlow, t), Mathf.Lerp(1.035f, 1.10f, t));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            flash = null;
            RefreshView();
        }

        private void StopFlash()
        {
            if (flash != null) StopCoroutine(flash);
            flash = null;
        }

        public void OnPointerEnter(PointerEventData e) { hovered = true; RefreshView(); }
        public void OnPointerExit(PointerEventData e) { hovered = false; RefreshView(); }

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || !CanClick) return;
            hovered = true;
            pressed = true;
            StopFlash();
            RefreshView();
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            pressed = false;
            RefreshView();
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || !CanClick) return;
            PlayClickEffect();
            menuClickEvent?.RaiseEvent(menuAction, this);
            onClick.Invoke();
            Clicked?.Invoke(actionId);
        }
    }
}
