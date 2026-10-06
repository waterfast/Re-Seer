using UnityEngine;

/// <summary>SpriteRenderer 技能槽的网格布局，使用世界单位而非 Canvas 像素。</summary>
[DisallowMultipleComponent]
public sealed class SkillSlotLayout : MonoBehaviour
{
    [SerializeField, Min(1)] private int columns = 4;
    [SerializeField] private Vector2 cellStep = new Vector2(4.8f, 3f);

    public void SetHorizontalSpacing(float value)
    {
        cellStep.x = Mathf.Max(0f, value);
        Arrange();
    }

    public void Arrange()
    {
        int index = 0;
        int columnCount = Mathf.Max(1, columns);
        foreach (Transform slot in transform)
        {
            // 清理旧槽时先禁用，Destroy 要到帧末执行，不能把旧槽计入新布局。
            if (!slot.gameObject.activeSelf) continue;
            slot.localPosition = new Vector3(index % columnCount * cellStep.x,
                -(index / columnCount) * cellStep.y, 0f);
            index++;
        }
    }
}
